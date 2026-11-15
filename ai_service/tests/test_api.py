import sys
import unittest
from pathlib import Path
from unittest.mock import patch

from fastapi.testclient import TestClient

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import app as app_module  # noqa: E402
from app import app  # noqa: E402
from llm import LlmError, LlmTimeoutError  # noqa: E402

CATALOG = [{
    "key": "Employee", "displayName": "עובד", "pluralName": "עובדים", "synonyms": ["עובדים"],
    "fields": [
        {"key": "Age", "displayName": "גיל", "type": "number", "synonyms": ["גילם"]},
        {"key": "IsActive", "displayName": "פעיל", "type": "boolean", "synonyms": ["פעילים"]},
    ],
    "relations": [],
}]


class ApiTests(unittest.TestCase):
    client = TestClient(app)

    def test_health(self):
        self.assertEqual(self.client.get("/health").json()["status"], "ok")

    def test_control_suggestion_follows_the_contract(self):
        r = self.client.post("/v1/control-suggestions", json={
            "prompt": "מצא עובדים פעילים שגילם נמוך מ-18", "language": "he", "schemaVersion": 1,
            "allowedOperators": {"number": ["lt"]}, "catalog": CATALOG})
        self.assertEqual(r.status_code, 200)
        body = r.json()
        self.assertEqual(body["definition"]["rootEntity"], "Employee")
        self.assertEqual(body["definition"]["root"]["type"], "group")

    def test_proactive_returns_suggestions_list(self):
        r = self.client.post("/v1/proactive-suggestions", json={"catalog": CATALOG})
        self.assertEqual(r.status_code, 200)
        self.assertIsInstance(r.json()["suggestions"], list)

    def test_missing_or_empty_catalog_is_rejected(self):
        self.assertEqual(self.client.post("/v1/control-suggestions", json={"prompt": "x"}).status_code, 422)
        self.assertEqual(self.client.post("/v1/control-suggestions", json={"prompt": "x", "catalog": []}).status_code, 422)

    def test_invalid_llm_response_returns_bad_gateway(self):
        def broken(_):
            raise LlmError("bad json")
        with patch.object(app_module.provider, "suggest_control", broken):
            r = self.client.post("/v1/control-suggestions", json={"prompt": "x", "catalog": CATALOG})
        self.assertEqual(r.status_code, 502)

    def test_llm_timeout_returns_gateway_timeout(self):
        def slow(_):
            raise LlmTimeoutError("timeout")
        with patch.object(app_module.provider, "suggest_control", slow):
            r = self.client.post("/v1/control-suggestions", json={"prompt": "x", "catalog": CATALOG})
        self.assertEqual(r.status_code, 504)


if __name__ == "__main__":
    unittest.main()
