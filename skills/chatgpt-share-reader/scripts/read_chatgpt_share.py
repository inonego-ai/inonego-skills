#!/usr/bin/env python3
from __future__ import annotations

import argparse
import html
import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from collections import deque
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from typing import Any, Iterator

ENQUEUE_RE = re.compile(
    r'window\.__reactRouterContext\.streamController\.enqueue\(("(?:\\.|[^"\\])*")\)'
)
TITLE_RE = re.compile(r"<title>(.*?)</title>", re.IGNORECASE | re.DOTALL)
REDACTED_PLACEHOLDER = "The output of this plugin was redacted."
DEFAULT_USER_AGENT = "Mozilla/5.0 (compatible; inonego-chatgpt-share-reader/1.0)"


class ShareReadError(RuntimeError):
    pass


@dataclass(frozen=True)
class Selection:
    mode: str
    count: int | None


class FlatPayload:
    def __init__(self, values: list[Any]) -> None:
        self.values = values
        self._tokens_by_name: dict[str, list[str]] = {}
        for index, value in enumerate(values):
            if isinstance(value, str):
                self._tokens_by_name.setdefault(value, []).append(f"_{index}")

    def value(self, ref: Any) -> Any:
        if isinstance(ref, int) and not isinstance(ref, bool) and 0 <= ref < len(self.values):
            return self.values[ref]
        return ref

    def field_ref(self, object_ref: Any, name: str) -> Any:
        if not isinstance(object_ref, int) or isinstance(object_ref, bool):
            return None
        if not 0 <= object_ref < len(self.values):
            return None
        obj = self.values[object_ref]
        if not isinstance(obj, dict):
            return None
        for token in self._tokens_by_name.get(name, ()):
            if token in obj:
                return obj[token]
        return None

    def field_value(self, object_ref: Any, name: str) -> Any:
        return self.value(self.field_ref(object_ref, name))

    def find_object_ref(self, required_field: str) -> int | None:
        tokens = tuple(self._tokens_by_name.get(required_field, ()))
        if not tokens:
            return None
        for index, value in enumerate(self.values):
            if isinstance(value, dict) and any(token in value for token in tokens):
                return index
        return None


def parse_share_url(url: str) -> tuple[str, str]:
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != "https" or parsed.hostname not in {"chatgpt.com", "www.chatgpt.com"}:
        raise ShareReadError("`https://chatgpt.com/share/...` 형식의 URL이 필요합니다.")
    match = re.fullmatch(r"/share/([^/]+)", parsed.path.rstrip("/"))
    if not match:
        raise ShareReadError("`https://chatgpt.com/share/<id>` 형식의 URL이 필요합니다.")
    return url, match.group(1)


def fetch_html(url: str, timeout: float, user_agent: str) -> str:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": user_agent,
            "Accept": "text/html,application/xhtml+xml",
            "Accept-Language": "ko-KR,ko;q=0.9,en;q=0.7",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            status = getattr(response, "status", 200)
            if status != 200:
                raise ShareReadError(f"HTTP {status}")
            return response.read().decode("utf-8")
    except (urllib.error.URLError, TimeoutError) as exc:
        raise ShareReadError(f"요청에 실패했습니다: {exc}") from exc


def extract_payload(page_html: str) -> FlatPayload:
    chunks: list[str] = []
    for match in ENQUEUE_RE.finditer(page_html):
        try:
            chunks.append(json.loads(match.group(1)))
        except json.JSONDecodeError:
            continue
    if not chunks:
        raise ShareReadError("React Router 스트림 데이터를 찾지 못했습니다.")
    stream = "".join(chunks)
    start = stream.find("[")
    if start < 0:
        raise ShareReadError("대화 데이터 배열을 찾지 못했습니다.")
    try:
        values, _ = json.JSONDecoder().raw_decode(stream, start)
    except json.JSONDecodeError as exc:
        raise ShareReadError(f"대화 데이터 JSON을 해석하지 못했습니다: {exc}") from exc
    if not isinstance(values, list):
        raise ShareReadError("대화 데이터의 최상위 값이 목록이 아닙니다.")
    return FlatPayload(values)


def fallback_html_title(page_html: str) -> str | None:
    match = TITLE_RE.search(page_html)
    if not match:
        return None
    title = html.unescape(match.group(1)).strip()
    prefix = "ChatGPT - "
    return title[len(prefix):] if title.startswith(prefix) else title


def metadata_value(payload: FlatPayload, message_ref: int, name: str) -> Any:
    metadata_ref = payload.field_ref(message_ref, "metadata")
    return payload.field_value(metadata_ref, name)


def extract_text(payload: FlatPayload, message_ref: int) -> str:
    content_ref = payload.field_ref(message_ref, "content")
    parts_ref = payload.field_ref(content_ref, "parts")
    parts = payload.value(parts_ref)
    if not isinstance(parts, list):
        return ""
    texts: list[str] = []
    for part_ref in parts:
        part = payload.value(part_ref)
        if isinstance(part, str):
            texts.append(part)
            continue
        if isinstance(part_ref, int) and isinstance(part, dict):
            content_type = payload.field_value(part_ref, "content_type")
            if content_type == "image_asset_pointer":
                texts.append("[이미지]")
            elif isinstance(content_type, str):
                texts.append(f"[{content_type}]")
            else:
                texts.append("[첨부]")
    return "\n".join(texts).strip()


def iter_visible_turns(payload: FlatPayload, conversation_ref: int) -> Iterator[dict[str, Any]]:
    linear_ref = payload.field_ref(conversation_ref, "linear_conversation")
    node_refs = payload.value(linear_ref)
    if not isinstance(node_refs, list):
        raise ShareReadError("`linear_conversation`이 없거나 형식이 올바르지 않습니다.")

    visible_index = 0
    last_progress: dict[str, Any] | None = None
    assistant_final_emitted = False
    for node_ref in node_refs:
        message_ref = payload.field_ref(node_ref, "message")
        if not isinstance(message_ref, int):
            continue
        author_ref = payload.field_ref(message_ref, "author")
        role = payload.field_value(author_ref, "role")
        if role not in {"user", "assistant"}:
            continue

        hidden = metadata_value(payload, message_ref, "is_visually_hidden_from_conversation")
        redacted = metadata_value(payload, message_ref, "is_redacted")
        if hidden is True or redacted is True:
            continue
        recipient = payload.field_value(message_ref, "recipient")
        content_ref = payload.field_ref(message_ref, "content")
        content_type = payload.field_value(content_ref, "content_type")
        text = extract_text(payload, message_ref)
        if not text or text == REDACTED_PLACEHOLDER:
            continue

        if role == "user":
            if metadata_value(payload, message_ref, "is_user_system_message") is True:
                continue
            if recipient not in {None, "all"}:
                continue
            if last_progress is not None and not assistant_final_emitted:
                last_progress["index"] = visible_index
                yield last_progress
                visible_index += 1
            last_progress = None
            assistant_final_emitted = False
            yield {
                "index": visible_index,
                "role": "user",
                "kind": "user",
                "text": text,
                "message_id": payload.field_value(message_ref, "id"),
                "turn_id": metadata_value(payload, message_ref, "turn_id"),
            }
            visible_index += 1
            continue

        if recipient != "all" or content_type != "text":
            continue
        is_progress = metadata_value(payload, message_ref, "is_thinking_preamble_message") is True
        turn = {
            "role": "assistant",
            "kind": "progress" if is_progress else "final",
            "text": text,
            "message_id": payload.field_value(message_ref, "id"),
            "turn_id": metadata_value(payload, message_ref, "turn_id"),
        }
        if is_progress:
            last_progress = turn
            continue
        turn["index"] = visible_index
        yield turn
        visible_index += 1
        assistant_final_emitted = True
        last_progress = None

    if last_progress is not None and not assistant_final_emitted:
        last_progress["index"] = visible_index
        yield last_progress


def select_turns(
    payload: FlatPayload,
    conversation_ref: int,
    selection: Selection,
) -> tuple[list[dict[str, Any]], bool]:
    iterator = iter_visible_turns(payload, conversation_ref)
    if selection.mode == "all":
        return list(iterator), True
    count = selection.count or 0
    if selection.mode == "tail":
        return list(deque(iterator, maxlen=count)), True

    turns: list[dict[str, Any]] = []
    for turn in iterator:
        turns.append(turn)
        if len(turns) >= count:
            return turns, False
    return turns, True


def read_share(
    url: str,
    selection: Selection,
    timeout: float,
    user_agent: str,
) -> dict[str, Any]:
    normalized_url, share_id = parse_share_url(url)
    page_html = fetch_html(normalized_url, timeout, user_agent)
    payload = extract_payload(page_html)
    conversation_ref = payload.find_object_ref("linear_conversation")
    if conversation_ref is None:
        raise ShareReadError("대화 객체를 찾지 못했습니다.")

    title = payload.field_value(conversation_ref, "title") or fallback_html_title(page_html)
    backing_id = payload.field_value(conversation_ref, "backing_conversation_id")
    turns, scan_complete = select_turns(payload, conversation_ref, selection)
    if not turns:
        raise ShareReadError("화면에 표시되는 대화 메시지를 찾지 못했습니다.")

    return {
        "source": {
            "url": normalized_url,
            "share_id": share_id,
            "backing_conversation_id": backing_id,
            "title": title,
        },
        "selection": {
            "mode": selection.mode,
            "requested": selection.count,
            "returned": len(turns),
            "scan_complete": scan_complete,
        },
        "turns": turns,
    }


def render_text(result: dict[str, Any]) -> str:
    source = result["source"]
    lines = [
        f"# {source.get('title') or '(제목 없음)'}",
        f"공유 ID: {source.get('share_id')}",
        f"원본 대화 ID: {source.get('backing_conversation_id') or '(확인되지 않음)'}",
        "",
    ]
    role_labels = {"user": "사용자", "assistant": "ChatGPT"}
    for turn in result["turns"]:
        suffix = " (진행 중)" if turn.get("kind") == "progress" else ""
        role_label = role_labels.get(turn["role"], turn["role"])
        lines.append(f"[{turn['index']}] {role_label}{suffix}")
        lines.append(turn["text"])
        lines.append("")
    return "\n".join(lines).rstrip()


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="ChatGPT 공유 링크에서 화면에 표시되는 대화 메시지를 읽습니다."
    )
    parser.add_argument("urls", nargs="+", help="https://chatgpt.com/share/<id>")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--head", type=int, metavar="N", help="앞부분의 화면 표시 메시지 N개를 읽습니다.")
    group.add_argument("--tail", type=int, metavar="N", help="마지막 화면 표시 메시지 N개를 읽습니다.")
    group.add_argument("--all", action="store_true", help="화면에 표시되는 대화 메시지를 모두 읽습니다.")
    parser.add_argument("--jobs", type=int, default=4, help="동시에 가져올 URL 수입니다. 최대 8개까지 사용합니다.")
    parser.add_argument("--timeout", type=float, default=20.0, help="HTTP 요청 제한 시간(초)입니다.")
    parser.add_argument("--format", choices=("json", "text"), default="json", help="출력 형식입니다.")
    return parser.parse_args(argv)


def selection_from_args(args: argparse.Namespace) -> Selection:
    if args.all:
        return Selection("all", None)
    if args.tail is not None:
        if args.tail < 1:
            raise ShareReadError("--tail 값은 1 이상이어야 합니다.")
        return Selection("tail", args.tail)
    count = args.head if args.head is not None else 8
    if count < 1:
        raise ShareReadError("--head 값은 1 이상이어야 합니다.")
    return Selection("head", count)


def read_many(
    urls: list[str],
    selection: Selection,
    jobs: int,
    timeout: float,
    user_agent: str,
) -> list[dict[str, Any]]:
    if len(urls) == 1:
        return [read_share(urls[0], selection, timeout, user_agent)]
    workers = min(max(1, jobs), 8, len(urls))
    results: list[dict[str, Any] | None] = [None] * len(urls)
    with ThreadPoolExecutor(max_workers=workers) as executor:
        futures = {
            executor.submit(read_share, url, selection, timeout, user_agent): index
            for index, url in enumerate(urls)
        }
        for future in as_completed(futures):
            index = futures[future]
            try:
                results[index] = future.result()
            except Exception as exc:
                results[index] = {"source": {"url": urls[index]}, "error": str(exc)}
    return [result for result in results if result is not None]


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    try:
        selection = selection_from_args(args)
        results = read_many(
            args.urls,
            selection,
            args.jobs,
            args.timeout,
            DEFAULT_USER_AGENT,
        )
    except ShareReadError as exc:
        print(f"오류: {exc}", file=sys.stderr)
        return 2

    failed = any("error" in result for result in results)
    if args.format == "json":
        output: Any = results[0] if len(results) == 1 else results
        print(json.dumps(output, ensure_ascii=False, indent=2))
    else:
        for index, result in enumerate(results):
            if index:
                print("\n" + "=" * 72 + "\n")
            if "error" in result:
                print(f"오류 {result['source']['url']}: {result['error']}")
            else:
                print(render_text(result))
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
