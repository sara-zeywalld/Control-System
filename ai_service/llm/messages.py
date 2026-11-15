"""בניית הבקשה לספק: הודעת system לפי המשימה + הודעת user עם הקטלוג."""
import json

from .prompts import SYSTEM_PROMPTS


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
