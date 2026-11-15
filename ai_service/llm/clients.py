"""לקוחות LLM: כל לקוח מקבל payload של Chat Completions ומחזיר את התשובה הגולמית של הספק."""
import json
import os
from typing import Protocol

from rules import mock_model

MAX_RESPONSE_BYTES = 256 * 1024


class LlmError(RuntimeError):
    """שגיאה בתקשורת עם ה-LLM או בתשובה שהחזיר."""


class LlmTimeoutError(LlmError):
    """הספק לא הגיב בזמן."""


class LlmClient(Protocol):
    def complete(self, payload: dict) -> dict:
        """שולח payload של Chat Completions ומחזיר את התשובה הגולמית של הספק."""


class MockLlmClient:
    """מחליף את הספק ברמת התקשורת: קורא רק את מה שנשלח בהודעות ומחזיר מעטפת תשובה של ספק.

    התוכן מופק ע"י המודל מבוסס הכללים, כך שגם ה-Mock "רואה" רק את ההקשר שבנינו - בדיוק כמו LLM."""

    def complete(self, payload: dict) -> dict:
        user = json.loads(next(m["content"] for m in payload["messages"] if m["role"] == "user"))
        if user["task"] == "proactive":
            result = mock_model.suggest_proactive(user)
        else:
            result = mock_model.suggest_control(user)
        return {
            "model": payload["model"],
            "choices": [{"index": 0, "finish_reason": "stop",
                         "message": {"role": "assistant", "content": json.dumps(result, ensure_ascii=False)}}],
        }


class HttpLlmClient:
    """לקוח HTTP לספק LLM תואם Chat Completions. לא מופעל כברירת מחדל (ראו AI_PROVIDER).

    ספקים נבדלים בכתובת ובאימות, ולכן שניהם ניתנים להגדרה:
      OpenAI:        LLM_API_URL=https://api.openai.com/v1/chat/completions   (ברירת המחדל: Authorization: Bearer <key>)
      Azure OpenAI:  LLM_API_URL=https://<resource>.openai.azure.com/openai/deployments/<deployment>/chat/completions?api-version=<ver>
                     LLM_AUTH_HEADER=api-key  LLM_AUTH_SCHEME=   (ריק - המפתח נשלח ללא קידומת)
    ספקים שאינם תואמים Chat Completions (למשל Anthropic) ידרשו לקוח נפרד שמממש את LlmClient."""

    def __init__(self, url: str, api_key: str, timeout: float = 30,
                 auth_header: str = "Authorization", auth_scheme: str = "Bearer"):
        self.url, self.timeout = url, timeout
        self.headers = {auth_header: f"{auth_scheme} {api_key}".strip()}

    def complete(self, payload: dict) -> dict:
        import httpx

        try:
            with httpx.stream("POST", self.url, json=payload, headers=self.headers, timeout=self.timeout) as r:
                r.raise_for_status()
                body = bytearray()
                for chunk in r.iter_bytes():
                    body += chunk
                    if len(body) > MAX_RESPONSE_BYTES:
                        raise LlmError("תשובת ספק ה-LLM חורגת מהגודל המותר")
            return json.loads(body)
        except httpx.TimeoutException as ex:
            raise LlmTimeoutError("ספק ה-LLM לא הגיב בזמן") from ex
        except httpx.HTTPStatusError as ex:
            raise LlmError(f"ספק ה-LLM החזיר שגיאה ({ex.response.status_code})") from ex
        except (httpx.HTTPError, ValueError) as ex:
            raise LlmError("הקריאה לספק ה-LLM נכשלה") from ex

    @classmethod
    def from_env(cls) -> "HttpLlmClient":
        url, key = os.getenv("LLM_API_URL"), os.getenv("LLM_API_KEY")
        if not url or not key:
            raise LlmError("יש להגדיר LLM_API_URL ו-LLM_API_KEY")
        return cls(url, key, float(os.getenv("LLM_TIMEOUT_SECONDS", "30")),
                   os.getenv("LLM_AUTH_HEADER", "Authorization"), os.getenv("LLM_AUTH_SCHEME", "Bearer"))
