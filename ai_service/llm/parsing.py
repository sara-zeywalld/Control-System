"""קליטת תשובת הספק. תשובת המודל אינה נחשבת אמינה."""
import json
import re

from .errors import LlmError

MAX_RESPONSE_BYTES = 256 * 1024


def parse_response(raw: dict) -> dict:
    """מחלץ את תוכן התשובה מהמעטפת של הספק ומפענח אותו ל-JSON."""
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
