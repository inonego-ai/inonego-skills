from __future__ import annotations

import hashlib
import json
import os
import re
import sys
import tempfile
import time
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator


PROGRAMMER_DOCUMENT = "skills/programmer/SKILL.md"
C_SHARP_CONVENTION_DOCUMENT = "skills/programmer/conventions/csharp.md"
EDIT_TOOL_NAMES = {"apply_patch", "Edit", "Write"}
STATE_LOCK_TIMEOUT_SECONDS = 2.0
STATE_LOCK_RETRY_SECONDS = 0.02
C_SHARP_PATH_PATTERN = re.compile(r"(?i)\.cs(?![a-z0-9_.])")
PATCH_PATH_PATTERN = re.compile(
    r"(?m)^\*\*\* (?:Add|Update|Delete) File: (?P<path>.+?)\s*$"
)
MOVE_PATH_PATTERN = re.compile(r"(?m)^\*\*\* Move to: (?P<path>.+?)\s*$")
SHELL_WRITE_PATTERN = re.compile(
    r"(?i)(?:\b(?:set-content|add-content|out-file|remove-item|move-item|copy-item|"
    r"new-item|rename-item)\b|(?:^|[;&|]\s*)(?:rm|mv|cp|mkdir|touch)\b|"
    r"(?:^|\s)(?:>|>>)\s*[^&|])"
)
REQUEST_CONTEXT = (
    "현재 사용자 메시지와 앞선 후속 정정·제외를 함께 읽고 목적, 쓰기 권한, 범위, 제외 대상과 "
    "검증 권한을 AGENTS 기준으로 판단하십시오. 질문·가능성·이름 후보는 변경 승인으로 간주하지 "
    "말고, 테스트 생성과 실행 권한은 서로 별도로 판단하십시오. 일부 단어만으로 권한을 넓히거나 "
    "줄이지 마십시오."
)
COMPACT_CONTEXT = (
    "대화가 압축되었습니다. 현재 사용자 요청과 이후 추가·정정·제외를 다시 확인하고, 현재 작업에 "
    "필요한 모든 스킬을 다시 읽어 기준을 복구하십시오. 코드 편집 전에는 programmer와 해당 언어 "
    "컨벤션을 다시 읽고, 종료 전에는 전체 요구를 실제 결과와 근거에 대조하십시오."
)
DEFER_CONTEXT = (
    "이번 턴의 실제 파일 변경이 완료 대조 대상으로 기록되었습니다. 작업 중 경과와 다음 단계는 "
    "commentary로 계속 알릴 수 있지만, 결과 요약과 완료 주장은 Stop 훅의 완료 대조 뒤로 미루십시오. "
    "아직 완료 대조 프롬프트로 재개되기 전이라면 `변경 작업을 마쳤으며 완료 대조를 진행합니다.`만 "
    "최종 메시지로 출력하십시오."
)
REVIEW_CONTEXT = (
    "현재 턴은 Stop 훅의 완료 대조로 재개된 단계입니다. 추가 수정이 생겨도 결과를 다시 보류하지 "
    "말고, 최초 요청과 후속 정정·제외, 실제 변경, 검증을 대조한 뒤 완전한 최종 답변을 한 번 "
    "작성하십시오."
)
STOP_REASON = (
    "실제 파일 변경이 있는 턴의 완료 대조 단계입니다. 최초 사용자 요청과 이후 추가·정정·제외를 "
    "항목별로 다시 확인하고, 각 필수 항목의 실제 결과와 허용된 수준의 근거를 대조하십시오. 계획 "
    "단계나 파일 존재만으로 완료 처리하지 마십시오. 누락이 있으면 허용 범위에서 작업을 계속하고, "
    "완료 후에는 감사 결과만 출력하지 말고 변경 결과·근거·미확인 범위를 포함한 완전한 최종 답변을 "
    "한 번 작성하십시오. 실제 장애가 있을 때만 수행 항목·남은 항목·근거·장애를 구분해 부분 완료로 "
    "보고하십시오."
)


def read_input() -> dict[str, Any]:
    try:
        value = json.load(sys.stdin)
    except (json.JSONDecodeError, OSError):
        return {}
    return value if isinstance(value, dict) else {}


def write_output(value: dict[str, Any]) -> None:
    json.dump(value, sys.stdout, ensure_ascii=True, separators=(",", ":"))


def state_path(payload: dict[str, Any]) -> Path:
    data_root = os.environ.get("PLUGIN_DATA") or os.environ.get("CLAUDE_PLUGIN_DATA")
    if not data_root:
        data_root = str(Path(tempfile.gettempdir()) / "inonego-skills")

    session_id = str(payload.get("session_id") or "unknown-session")
    session_key = hashlib.sha256(session_id.encode("utf-8")).hexdigest()[:24]
    return Path(data_root) / "guard" / f"{session_key}.json"


def load_state(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError, OSError):
        value = {}

    if not isinstance(value, dict):
        value = {}

    review = value.get("review")
    if isinstance(review, dict):
        turn_id = review.get("turn_id")
        phase = review.get("phase")
        changed_paths = review.get("changed_paths")
        if not isinstance(turn_id, str) or phase not in {"pending", "reviewing"}:
            review = None
        else:
            if not isinstance(changed_paths, list):
                changed_paths = []
            review = {
                "turn_id": turn_id,
                "phase": phase,
                "changed_paths": [
                    item for item in changed_paths if isinstance(item, str)
                ],
            }
    else:
        review = None

    return {
        "epoch": value.get("epoch", 0),
        "programmer_epoch": value.get("programmer_epoch", -1),
        "csharp_epoch": value.get("csharp_epoch", -1),
        "review": review,
    }


def save_state(path: Path, state: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f"{path.name}.{os.getpid()}.tmp")
    temporary.write_text(
        json.dumps(state, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    temporary.replace(path)


@contextmanager
def state_lock(path: Path) -> Iterator[None]:
    path.parent.mkdir(parents=True, exist_ok=True)
    lock_path = path.with_name(f"{path.name}.lock")
    handle = lock_path.open("a+b")
    handle.seek(0, os.SEEK_END)
    if handle.tell() == 0:
        handle.write(b"\0")
        handle.flush()

    if os.name == "nt":
        import msvcrt

        def acquire() -> None:
            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)

        def release() -> None:
            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
    else:
        import fcntl

        def acquire() -> None:
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)

        def release() -> None:
            fcntl.flock(handle.fileno(), fcntl.LOCK_UN)

    deadline = time.monotonic() + STATE_LOCK_TIMEOUT_SECONDS
    acquired = False
    try:
        while not acquired:
            try:
                acquire()
                acquired = True
            except (BlockingIOError, OSError):
                if time.monotonic() >= deadline:
                    raise TimeoutError(f"Timed out waiting for hook state lock: {path}")
                time.sleep(STATE_LOCK_RETRY_SECONDS)
        yield
    finally:
        if acquired:
            release()
        handle.close()


def tool_input_text(payload: dict[str, Any]) -> str:
    tool_input = payload.get("tool_input")
    if isinstance(tool_input, str):
        return tool_input
    if not isinstance(tool_input, dict):
        return ""

    for key in ("command", "patch", "input"):
        value = tool_input.get(key)
        if isinstance(value, str):
            return value
    return json.dumps(tool_input, ensure_ascii=False, sort_keys=True)


def changed_paths(payload: dict[str, Any]) -> list[str]:
    tool_input = payload.get("tool_input")
    paths: list[str] = []
    if isinstance(tool_input, dict):
        for key in ("file_path", "path"):
            value = tool_input.get(key)
            if isinstance(value, str) and value.strip():
                paths.append(value.strip())

    text = tool_input_text(payload)
    for pattern in (PATCH_PATH_PATTERN, MOVE_PATH_PATTERN):
        paths.extend(match.group("path").strip() for match in pattern.finditer(text))

    return list(dict.fromkeys(paths))


def tool_response(payload: dict[str, Any]) -> Any:
    response = payload.get("tool_response")
    return payload.get("tool_result") if response is None else response


def response_succeeded(payload: dict[str, Any]) -> bool:
    response = tool_response(payload)
    if isinstance(response, dict):
        if response.get("isError") is True:
            return False
        exit_code = response.get("exit_code")
        if isinstance(exit_code, int) and exit_code != 0:
            return False
    return True


def response_text(payload: dict[str, Any]) -> str:
    parts: list[str] = []

    def collect(value: Any) -> None:
        if isinstance(value, str):
            parts.append(value)
        elif isinstance(value, dict):
            for child in value.values():
                collect(child)
        elif isinstance(value, list):
            for child in value:
                collect(child)

    collect(tool_response(payload))
    return "\n".join(parts).replace("\r\n", "\n")


def document_was_read(payload: dict[str, Any], relative_path: str) -> bool:
    plugin_root = os.environ.get("PLUGIN_ROOT") or os.environ.get("CLAUDE_PLUGIN_ROOT")
    root = Path(plugin_root) if plugin_root else Path(__file__).resolve().parent.parent
    try:
        expected = (root / relative_path).read_text(encoding="utf-8")
    except OSError:
        return False

    expected = expected.replace("\r\n", "\n").strip()
    return bool(expected) and expected in response_text(payload)


def is_csharp_edit(payload: dict[str, Any]) -> bool:
    tool_name = str(payload.get("tool_name") or "")
    if tool_name in EDIT_TOOL_NAMES:
        return any(path.casefold().endswith(".cs") for path in changed_paths(payload))

    text = tool_input_text(payload)
    return (
        tool_name == "Bash"
        and C_SHARP_PATH_PATTERN.search(text) is not None
        and SHELL_WRITE_PATTERN.search(text) is not None
    )


def additional_context(event: str, context: str) -> None:
    write_output(
        {
            "hookSpecificOutput": {
                "hookEventName": event,
                "additionalContext": context,
            }
        }
    )


def deny(reason: str) -> None:
    write_output(
        {
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": reason,
            }
        }
    )


def session_start(path: Path) -> None:
    with state_lock(path):
        state = load_state(path)
        state["epoch"] = int(state.get("epoch", 0)) + 1
        state["programmer_epoch"] = -1
        state["csharp_epoch"] = -1
        save_state(path, state)
        review = state.get("review")

    context = COMPACT_CONTEXT
    if isinstance(review, dict):
        context += " " + (
            REVIEW_CONTEXT if review.get("phase") == "reviewing" else DEFER_CONTEXT
        )
    additional_context("SessionStart", context)


def user_prompt_submit(payload: dict[str, Any], path: Path) -> None:
    prompt = payload.get("prompt")
    if not isinstance(prompt, str):
        prompt = payload.get("user_prompt")
    if not isinstance(prompt, str):
        return

    with state_lock(path):
        state = load_state(path)
        review = state.get("review")
        turn_id = payload.get("turn_id")
        if isinstance(review, dict) and (
            not isinstance(turn_id, str) or review.get("turn_id") != turn_id
        ):
            state["review"] = None
            save_state(path, state)
    additional_context("UserPromptSubmit", REQUEST_CONTEXT)


def pre_tool_use(payload: dict[str, Any], path: Path) -> None:
    if not is_csharp_edit(payload):
        return

    with state_lock(path):
        state = load_state(path)

    epoch = int(state.get("epoch", 0))
    missing: list[str] = []
    if state.get("programmer_epoch") != epoch:
        missing.append("programmer/SKILL.md")
    if state.get("csharp_epoch") != epoch:
        missing.append("programmer/conventions/csharp.md")

    if missing:
        deny(
            "현재 작업 기준에서 C# 편집 전 전체를 다시 읽어야 할 문서가 있습니다: "
            + ", ".join(missing)
        )


def post_tool_use(payload: dict[str, Any], path: Path) -> None:
    if not response_succeeded(payload):
        return

    tool_name = str(payload.get("tool_name") or "")
    turn_id = payload.get("turn_id")
    edit_succeeded = (
        tool_name in EDIT_TOOL_NAMES and isinstance(turn_id, str) and bool(turn_id)
    )
    programmer_read = tool_name == "Bash" and document_was_read(
        payload, PROGRAMMER_DOCUMENT
    )
    csharp_read = tool_name == "Bash" and document_was_read(
        payload, C_SHARP_CONVENTION_DOCUMENT
    )

    with state_lock(path):
        state = load_state(path)
        changed = False
        epoch = int(state.get("epoch", 0))
        if programmer_read and state.get("programmer_epoch") != epoch:
            state["programmer_epoch"] = epoch
            changed = True
        if csharp_read and state.get("csharp_epoch") != epoch:
            state["csharp_epoch"] = epoch
            changed = True

        if edit_succeeded:
            review = state.get("review")
            if not isinstance(review, dict) or review.get("turn_id") != turn_id:
                review = {
                    "turn_id": turn_id,
                    "phase": "pending",
                    "changed_paths": [],
                }

            recorded_paths = list(review.get("changed_paths") or [])
            recorded_paths.extend(changed_paths(payload))
            review["changed_paths"] = list(dict.fromkeys(recorded_paths))
            state["review"] = review
            changed = True
            review_phase = review.get("phase")
        else:
            review_phase = None

        if changed:
            save_state(path, state)

    if not edit_succeeded:
        return

    additional_context(
        "PostToolUse",
        REVIEW_CONTEXT if review_phase == "reviewing" else DEFER_CONTEXT,
    )


def stop(payload: dict[str, Any], path: Path) -> None:
    turn_id = payload.get("turn_id")
    if not isinstance(turn_id, str) or not turn_id:
        return
    if payload.get("stop_hook_active") is True:
        return

    with state_lock(path):
        state = load_state(path)
        review = state.get("review")
        if not isinstance(review, dict) or review.get("turn_id") != turn_id:
            return

        if review.get("phase") == "reviewing":
            state["review"] = None
            save_state(path, state)
            return

        review["phase"] = "reviewing"
        state["review"] = review
        save_state(path, state)
        reason = STOP_REASON
        recorded_paths = review.get("changed_paths")
        if isinstance(recorded_paths, list) and recorded_paths:
            reason += " 이번 턴에 기록된 변경 경로: " + ", ".join(recorded_paths)

    write_output({"decision": "block", "reason": reason})


def main() -> None:
    payload = read_input()
    event = str(payload.get("hook_event_name") or "")
    path = state_path(payload)

    if event == "SessionStart":
        session_start(path)
    elif event == "UserPromptSubmit":
        user_prompt_submit(payload, path)
    elif event == "PreToolUse":
        pre_tool_use(payload, path)
    elif event == "PostToolUse":
        post_tool_use(payload, path)
    elif event == "Stop":
        stop(payload, path)


if __name__ == "__main__":
    main()
