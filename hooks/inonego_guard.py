from __future__ import annotations

import hashlib
import json
import os
import re
import sys
import tempfile
from pathlib import Path
from typing import Any


PROGRAMMER_PATHS = (
    "/skills/programmer/skill.md",
    "\\skills\\programmer\\skill.md",
)
C_SHARP_CONVENTION_PATHS = (
    "/skills/programmer/conventions/csharp.md",
    "\\skills\\programmer\\conventions\\csharp.md",
)
FULL_READ_MARKERS = ("get-content", "cat ", "cat\t")
TEST_RUN_PATTERN = re.compile(
    r"(?i)(?:\bdotnet\s+test\b|\bpytest\b|\bnpm\s+(?:run\s+)?test\b|"
    r"\bpnpm\s+(?:run\s+)?test\b|\byarn\s+test\b|\b-runTests\b)"
)
TEST_PATCH_PATTERN = re.compile(
    r"(?im)(?:^\*\*\*\s+(?:Add|Update)\s+File:\s+.*(?:^|[\\/])(?:Tests?|TEST)(?:[\\/]|[^\r\n]*\.(?:cs|py|js|ts))\s*$|"
    r"^\+\s*\[(?:Test|TestCase|UnityTest)\b|^\+\s*(?:def|async\s+def)\s+test_|"
    r"^\+.*\b(?:NUnit|pytest)\b)"
)
C_SHARP_PATCH_PATTERN = re.compile(
    r"(?im)^\*\*\*\s+(?:Add|Update|Delete)\s+File:\s+.*\.cs\s*$"
)


def read_input() -> dict[str, Any]:
    try:
        value = json.load(sys.stdin)
    except (json.JSONDecodeError, OSError):
        return {}
    return value if isinstance(value, dict) else {}


def write_output(value: dict[str, Any]) -> None:
    json.dump(value, sys.stdout, ensure_ascii=False, separators=(",", ":"))


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

    value.setdefault("epoch", 0)
    value.setdefault("programmer_epoch", -1)
    value.setdefault("csharp_epoch", -1)
    value.setdefault("substantive_turn_id", None)
    value.setdefault("audited_turn_id", None)
    return value


def save_state(path: Path, state: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(
        json.dumps(state, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    temporary.replace(path)


def tool_command(payload: dict[str, Any]) -> str:
    tool_input = payload.get("tool_input")
    if isinstance(tool_input, dict):
        for key in ("command", "cmd", "patch"):
            value = tool_input.get(key)
            if isinstance(value, str):
                return value
    return tool_input if isinstance(tool_input, str) else ""


def response_succeeded(payload: dict[str, Any]) -> bool:
    response = payload.get("tool_response")
    if isinstance(response, dict):
        if response.get("isError") is True:
            return False
        exit_code = response.get("exit_code")
        if isinstance(exit_code, int) and exit_code != 0:
            return False
    return True


def contains_path(command: str, candidates: tuple[str, ...]) -> bool:
    lowered = command.lower()
    return any(candidate in lowered for candidate in candidates)


def is_full_read(command: str) -> bool:
    lowered = command.lower()
    return any(marker in lowered for marker in FULL_READ_MARKERS)


def session_start(payload: dict[str, Any], path: Path, state: dict[str, Any]) -> None:
    if payload.get("source") != "compact":
        return

    state["epoch"] = int(state.get("epoch", 0)) + 1
    state["programmer_epoch"] = -1
    state["csharp_epoch"] = -1
    save_state(path, state)

    write_output(
        {
            "hookSpecificOutput": {
                "hookEventName": "SessionStart",
                "additionalContext": (
                    "대화가 압축되었습니다. 현재 사용자 요청과 이후 추가·정정·제외를 다시 확인하고, "
                    "현재 작업에 필요한 모든 스킬을 다시 읽어 기준을 복구하십시오. 코드 편집 전에는 "
                    "programmer와 해당 언어 컨벤션을 다시 읽고, 종료 전에는 전체 요구를 실제 결과와 "
                    "근거에 대조하십시오."
                ),
            }
        }
    )


def pre_tool_use(payload: dict[str, Any], state: dict[str, Any]) -> None:
    command = tool_command(payload)
    tool_name = str(payload.get("tool_name") or "")

    if tool_name in {"apply_patch", "Edit", "Write"} and C_SHARP_PATCH_PATTERN.search(command):
        epoch = int(state.get("epoch", 0))
        missing: list[str] = []
        if state.get("programmer_epoch") != epoch:
            missing.append("programmer/SKILL.md")
        if state.get("csharp_epoch") != epoch:
            missing.append("programmer/conventions/csharp.md")

        if missing:
            write_output(
                {
                    "hookSpecificOutput": {
                        "hookEventName": "PreToolUse",
                        "permissionDecision": "deny",
                        "permissionDecisionReason": (
                            "현재 작업 기준에서 C# 편집 전 다시 읽어야 할 문서가 있습니다: "
                            + ", ".join(missing)
                        ),
                    }
                }
            )
            return

    test_input = TEST_RUN_PATTERN.search(command) or (
        tool_name in {"apply_patch", "Edit", "Write"}
        and TEST_PATCH_PATTERN.search(command)
    )
    if test_input:
        write_output(
            {
                "hookSpecificOutput": {
                    "hookEventName": "PreToolUse",
                    "additionalContext": (
                        "이 입력은 테스트 생성 또는 실행으로 보입니다. 사용자가 해당 작업을 명시했는지, "
                        "생성과 실행 권한이 각각 있는지 programmer와 test-design 기준으로 다시 확인하십시오."
                    ),
                }
            }
        )


def post_tool_use(payload: dict[str, Any], path: Path, state: dict[str, Any]) -> None:
    if not response_succeeded(payload):
        return

    command = tool_command(payload)
    epoch = int(state.get("epoch", 0))
    if is_full_read(command) and contains_path(command, PROGRAMMER_PATHS):
        state["programmer_epoch"] = epoch
    if is_full_read(command) and contains_path(command, C_SHARP_CONVENTION_PATHS):
        state["csharp_epoch"] = epoch

    turn_id = payload.get("turn_id")
    if isinstance(turn_id, str) and turn_id:
        state["substantive_turn_id"] = turn_id
        if state.get("audited_turn_id") != turn_id:
            state["audited_turn_id"] = None

    save_state(path, state)


def stop(payload: dict[str, Any], path: Path, state: dict[str, Any]) -> None:
    turn_id = payload.get("turn_id")
    if not isinstance(turn_id, str) or not turn_id:
        return
    if state.get("substantive_turn_id") != turn_id:
        return
    if payload.get("stop_hook_active") is True:
        return
    if state.get("audited_turn_id") == turn_id:
        return

    state["audited_turn_id"] = turn_id
    save_state(path, state)
    write_output(
        {
            "decision": "block",
            "reason": (
                "종료 전 완료 대조를 한 번 수행하십시오. 최초 사용자 요청과 이후 추가·정정·제외를 "
                "항목별로 다시 확인하고, 각 필수 항목의 실제 결과와 허용된 수준의 근거를 대조하십시오. "
                "계획 단계나 파일 존재만으로 완료 처리하지 마십시오. 누락이 있으면 허용 범위에서 작업을 "
                "계속하고, 실제 장애가 있을 때만 수행 항목·남은 항목·근거·장애를 구분해 부분 완료로 보고하십시오."
            ),
        }
    )


def main() -> None:
    payload = read_input()
    event = str(payload.get("hook_event_name") or "")
    path = state_path(payload)
    state = load_state(path)

    if event == "SessionStart":
        session_start(payload, path, state)
    elif event == "PreToolUse":
        pre_tool_use(payload, state)
    elif event == "PostToolUse":
        post_tool_use(payload, path, state)
    elif event == "Stop":
        stop(payload, path, state)


if __name__ == "__main__":
    main()
