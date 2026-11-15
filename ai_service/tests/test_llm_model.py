import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from llm import LlmError, LlmModel, MockLlmClient  # noqa: E402
from llm.parsing import MAX_RESPONSE_BYTES  # noqa: E402
from llm.prompts import SYSTEM_PROMPTS  # noqa: E402

CATALOG = [{
    "key": "Employee", "displayName": "עובד", "pluralName": "עובדים", "synonyms": ["עובדים"],
    "fields": [
        {"key": "Age", "displayName": "גיל", "type": "number", "synonyms": ["גילם"], "isRequired": True},
        {"key": "IsActive", "displayName": "פעיל", "type": "boolean", "synonyms": ["פעילים"]},
    ],
    "relations": [],
}]
REQUEST = {"prompt": "עובדים פעילים שגילם נמוך מ-18", "catalog": CATALOG, "allowedOperators": {"number": ["lt"]}}


class RecordingClient:
    def __init__(self, content=None, raw=None):
        self.content, self.raw, self.payload = content, raw, None

    def complete(self, payload):
        self.payload = payload
        if self.content is None and self.raw is None:
            return MockLlmClient().complete(payload)
        return self.raw if self.raw is not None else {"choices": [{"message": {"content": self.content}}]}


class LlmModelTests(unittest.TestCase):
    def test_sends_system_prompt_and_business_catalog(self):
        client = RecordingClient()
        LlmModel(client).suggest_control(REQUEST)

        system, user = client.payload["messages"]
        self.assertEqual((system["role"], system["content"]), ("system", SYSTEM_PROMPTS["control"]))
        sent = json.loads(user["content"])
        self.assertEqual((sent["task"], sent["prompt"], sent["catalog"]), ("control", REQUEST["prompt"], CATALOG))
        self.assertEqual(sent["allowedOperators"], REQUEST["allowedOperators"])
        self.assertEqual(client.payload["response_format"], {"type": "json_object"})

    def test_mock_llm_end_to_end_returns_contract(self):
        r = LlmModel().suggest_control(REQUEST)
        ops = {(c["field"]["field"], c["operator"], c["value"]) for c in r["definition"]["root"]["items"]}
        self.assertEqual(ops, {("IsActive", "eq", True), ("Age", "lt", 18)})
        self.assertTrue(LlmModel().suggest_proactive({"catalog": CATALOG})["suggestions"])

    def test_fenced_json_is_parsed(self):
        content = '```json\n{"name": "x", "definition": {"rootEntity": "Employee", ' \
                  '"root": {"operator": "and", "items": [], "type": "group"}}}\n```'
        r = LlmModel(RecordingClient(content)).suggest_control(REQUEST)
        self.assertEqual(r["definition"]["root"]["type"], "group")

    def test_invalid_responses_raise_llm_error(self):
        oversized = '{"name": "' + "x" * MAX_RESPONSE_BYTES + '"}'
        for client in (RecordingClient("not json"), RecordingClient('{"name": "x"}'), RecordingClient(raw={}),
                       RecordingClient('{"definition": {"rootEntity": "E", "root": []}}'), RecordingClient(oversized)):
            with self.assertRaises(LlmError):
                LlmModel(client).suggest_control(REQUEST)

    def test_proactive_is_capped_and_ordered_by_confidence(self):
        def proposal(i):
            return {"name": str(i), "confidence": i / 10,
                    "definition": {"rootEntity": "Employee", "root": {"type": "group", "operator": "and", "items": []}}}
        content = json.dumps({"suggestions": [proposal(i) for i in range(8)]})
        names = [s["name"] for s in LlmModel(RecordingClient(content)).suggest_proactive({"catalog": CATALOG})["suggestions"]]
        self.assertEqual(names, ["7", "6", "5", "4", "3"])

    def test_each_task_gets_its_own_system_prompt(self):
        control, proactive = RecordingClient(), RecordingClient()
        LlmModel(control).suggest_control(REQUEST)
        LlmModel(proactive).suggest_proactive({"catalog": CATALOG})
        control_prompt = control.payload["messages"][0]["content"]
        proactive_prompt = proactive.payload["messages"][0]["content"]

        self.assertIn("AiControlProposal", control_prompt)
        self.assertIn("עד 5 הצעות", proactive_prompt)
        self.assertNotIn("עד 5 הצעות", control_prompt)
        self.assertNotIn("תרגם את תיאור הבקרה", proactive_prompt)


if __name__ == "__main__":
    unittest.main()
