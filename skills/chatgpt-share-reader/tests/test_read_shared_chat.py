import importlib.util
import pathlib
import sys
import unittest


SCRIPT_PATH = pathlib.Path(__file__).parents[1] / "scripts" / "read_shared_chat.py"
SPEC = importlib.util.spec_from_file_location("read_shared_chat", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
reader = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = reader
SPEC.loader.exec_module(reader)


class SharedChatReaderTests(unittest.TestCase):
    def test_parse_share_url_supports_web_and_codex(self) -> None:
        web = reader.parse_share_url("https://chatgpt.com/share/example")
        codex = reader.parse_share_url("https://chatgpt.com/s/cx_example")

        self.assertEqual(("chatgpt", "example"), (web.kind, web.share_id))
        self.assertEqual(("codex", "cx_example"), (codex.kind, codex.share_id))

    def test_parse_share_url_rejects_unknown_shape(self) -> None:
        with self.assertRaises(reader.ShareReadError):
            reader.parse_share_url("https://chatgpt.com/s/example")

    def test_codex_filters_hidden_items_and_keeps_final(self) -> None:
        snapshot = {
            "version": 1,
            "turns": [
                {
                    "items": [
                        {
                            "type": "userMessage",
                            "content": [{"type": "text", "text": "검토"}],
                        },
                        {
                            "type": "agentMessage",
                            "phase": "commentary",
                            "text": "진행 안내",
                        },
                        {"type": "reasoning", "summary": "내부 추론"},
                        {
                            "type": "fileChange",
                            "status": "completed",
                            "changes": [
                                {"path": "A.cs", "diff": "secret diff"},
                                {"path": "B.cs", "diff": "more diff"},
                            ],
                        },
                        {"type": "agentMessage", "text": "최종 답변"},
                    ]
                }
            ],
        }

        messages = list(reader.iter_codex_messages(snapshot))
        self.assertEqual(["검토", "최종 답변"], [message["text"] for message in messages])
        self.assertNotIn("진행 안내", str(messages))
        self.assertNotIn("내부 추론", str(messages))
        self.assertNotIn("secret diff", str(messages))
        self.assertEqual(
            ["A.cs", "B.cs"],
            messages[-1]["activities"][0]["paths"],
        )

    def test_codex_incomplete_turn_keeps_activity_on_user_message(self) -> None:
        snapshot = {
            "version": 1,
            "turns": [
                {
                    "items": [
                        {
                            "type": "userMessage",
                            "content": [{"type": "text", "text": "수정"}],
                        },
                        {
                            "type": "agentMessage",
                            "phase": "commentary",
                            "text": "작업 중",
                        },
                        {
                            "type": "fileChange",
                            "status": "completed",
                            "changes": [{"path": "GameWorld.cs", "diff": "@@ ..."}],
                        },
                    ]
                }
            ],
        }

        messages = list(reader.iter_codex_messages(snapshot))

        self.assertEqual(1, len(messages))
        self.assertEqual("user", messages[0]["role"])
        self.assertEqual("수정", messages[0]["text"])
        self.assertEqual(["GameWorld.cs"], messages[0]["activities"][0]["paths"])

    def test_codex_rejects_unknown_snapshot_version(self) -> None:
        with self.assertRaisesRegex(reader.ShareReadError, "지원하지 않는"):
            list(reader.iter_codex_messages({"version": 2, "turns": []}))

    def test_selection_counts_visible_messages_only(self) -> None:
        messages = iter(
            [
                {"index": 0, "role": "user", "kind": "user", "text": "a"},
                {"index": 1, "role": "assistant", "kind": "final", "text": "b"},
                {"index": 2, "role": "user", "kind": "user", "text": "c"},
            ]
        )

        selected, complete = reader.select_messages(
            messages,
            reader.Selection("head", 2),
        )

        self.assertEqual(["a", "b"], [message["text"] for message in selected])
        self.assertFalse(complete)

    def test_web_share_visible_message_contract_is_preserved(self) -> None:
        values: list[object] = []
        field_tokens: dict[str, int] = {}

        def token(name: str) -> int:
            if name not in field_tokens:
                field_tokens[name] = len(values)
                values.append(name)
            return field_tokens[name]

        def add(value: object) -> int:
            ref = len(values)
            values.append(value)
            return ref

        def obj(**fields: object) -> int:
            encoded: dict[str, object] = {}
            ref = add(encoded)
            for name, value in fields.items():
                encoded[f"_{token(name)}"] = value
            return ref

        user_author = obj(role="user")
        user_content = obj(content_type="text", parts=add(["질문"]))
        user_message = obj(
            author=user_author,
            metadata=obj(),
            recipient="all",
            content=user_content,
            id="u1",
        )
        assistant_author = obj(role="assistant")
        assistant_content = obj(content_type="text", parts=add(["답변"]))
        assistant_message = obj(
            author=assistant_author,
            metadata=obj(),
            recipient="all",
            content=assistant_content,
            id="a1",
        )
        nodes = add([obj(message=user_message), obj(message=assistant_message)])
        conversation = obj(
            linear_conversation=nodes,
            title="공유",
            backing_conversation_id="backing",
        )

        payload = reader.FlatPayload(values)
        messages = list(reader.iter_web_messages(payload, conversation))

        self.assertEqual(["질문", "답변"], [message["text"] for message in messages])
        self.assertEqual(["user", "assistant"], [message["role"] for message in messages])


if __name__ == "__main__":
    unittest.main()
