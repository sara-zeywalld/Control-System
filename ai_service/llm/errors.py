class LlmError(RuntimeError):
    """שגיאה בתקשורת עם ה-LLM או בתשובה שהחזיר."""


class LlmTimeoutError(LlmError):
    """הספק לא הגיב בזמן."""
