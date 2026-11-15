"""ספק AI מבוסס LLM.

הזרימה זהה למודל אמיתי:
    בקשה -> build_chat_request (System Prompt + קטלוג) -> client.complete -> parse_response -> הצעת בקרה

נקודת ה-Mock היא הלקוח בלבד (MockLlmClient), כך שכל שכבת התקשורת רצה בפועל.
מעבר למודל אמיתי = החלפת הלקוח ב-HttpLlmClient, ללא שינוי בשאר הקוד וללא שינוי בצד ה-.NET.
"""
import json
import os
import re

from .clients import MAX_RESPONSE_BYTES, LlmClient, LlmError, MockLlmClient
from .prompts import SYSTEM_PROMPTS

MAX_PROACTIVE_SUGGESTIONS = 5


def build_messages(request: dict, task: str) -> list[dict[str, str]]:
    content = {
        "task": task,
        "language": request.get("language", "he"),
        "schemaVersion": request.get("schemaVersion", 1),
        "allowedOperators": request.get("allowedOperators", {}),
        "catalog": request["catalog"],
    }
    if task == "control":
        content["prompt"] = request.get("prompt", "")
    return [
        {"role": "system", "content": SYSTEM_PROMPTS[task]},
        {"role": "user", "content": json.dumps(content, ensure_ascii=False)},
    ]


def build_chat_request(request: dict, task: str, model: str) -> dict:
    """Payload בפורמט Chat Completions (נתמך ע"י OpenAI, Azure OpenAI ושרתים תואמים)."""
    return {
        "model": model,
        "temperature": 0,
        "response_format": {"type": "json_object"},
        "messages": build_messages(request, task),
    }


def parse_response(raw: dict) -> dict:
    """מחלץ את תוכן התשובה מהמעטפת של הספק ומפענח אותו ל-JSON. תשובת המודל אינה נחשבת אמינה."""
    try:
        content = raw["choices"][0]["message"]["content"]
    except (KeyError, IndexError, TypeError) as ex:
        raise LlmError("תשובת ה-LLM אינה במבנה הצפוי") from ex
    if not isinstance(content, str) or not content.strip():
        raise LlmError("ה-LLM החזיר תשובה ריקה")
    if len(content.encode("utf-8")) > MAX_RESPONSE_BYTES:
        raise LlmError("תשובת ה-LLM חורגת מהגודל המותר")

    # מודלים מחזירים לעיתים JSON עטוף ב-```json ... ``` למרות ההנחיה
    text = re.sub(r"^```(?:json)?\s*|\s*```$", "", content.strip())
    try:
        data = json.loads(text)
    except json.JSONDecodeError as ex:
        raise LlmError("ה-LLM החזיר JSON לא תקין") from ex
    if not isinstance(data, dict):
        raise LlmError("ה-LLM החזיר JSON שאינו אובייקט")
    return data


def require_proposal(p) -> dict:
    """בדיקת מבנה ראשונית בלבד. האימות העסקי המלא מול הקטלוג מתבצע ב-.NET (ControlValidator)."""
    if not isinstance(p, dict) or not isinstance(p.get("definition"), dict):
        raise LlmError("ה-LLM החזיר הצעה ללא definition")
    d = p["definition"]
    if not isinstance(d.get("rootEntity"), str) or not isinstance(d.get("root"), dict):
        raise LlmError("ב-definition חסרים rootEntity או root")
    return p


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
