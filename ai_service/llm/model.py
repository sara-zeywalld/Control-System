"""ספק AI מבוסס LLM.

הזרימה זהה למודל אמיתי:
    בקשה -> build_chat_request (System Prompt + קטלוג) -> client.complete -> parse_response -> הצעת בקרה

נקודת ה-Mock היא הלקוח בלבד (MockLlmClient), כך שכל שכבת התקשורת רצה בפועל.
מעבר למודל אמיתי = החלפת הלקוח ב-HttpLlmClient, ללא שינוי בשאר הקוד וללא שינוי בצד ה-.NET.
"""
import os

from .clients import LlmClient, MockLlmClient
from .errors import LlmError
from .messages import build_chat_request
from .parsing import parse_response, require_proposal

MAX_PROACTIVE_SUGGESTIONS = 5


class LlmModel:
    """הלקוח מוזרק - Mock להדגמה, HTTP לספק אמיתי."""

    def __init__(self, client: LlmClient | None = None, model: str | None = None):
        self.client = client or MockLlmClient()
        self.model = model or os.getenv("LLM_MODEL", "mock-llm")

    def suggest_control(self, request: dict) -> dict:
        payload = build_chat_request(request, "control", self.model)
        return require_proposal(parse_response(self.client.complete(payload)))

    def suggest_proactive(self, request: dict) -> dict:
        payload = build_chat_request(request, "proactive", self.model)
        data = parse_response(self.client.complete(payload))
        if not isinstance(data.get("suggestions"), list):
            raise LlmError("ה-LLM לא החזיר רשימת suggestions")
        suggestions = [require_proposal(s) for s in data["suggestions"]]
        # ההנחיה בפרומפט אינה מבטיחה את המגבלה, ולכן נאכפת גם כאן
        suggestions.sort(key=lambda s: -float(s.get("confidence") or 0))
        return {"suggestions": suggestions[:MAX_PROACTIVE_SUGGESTIONS]}
