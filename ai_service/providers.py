"""בחירת ספק ה-AI לפי AI_PROVIDER: llm (ברירת מחדל) | rules | http."""
import os

from llm import HttpLlmClient, LlmModel
from rules import mock_model

PROVIDER_NAME = os.getenv("AI_PROVIDER", "llm").lower()


def create_provider(name: str = PROVIDER_NAME):
    if name == "rules":
        return mock_model
    if name == "llm":
        return LlmModel()
    if name == "http":
        return LlmModel(HttpLlmClient.from_env())
    raise ValueError(f"AI_PROVIDER לא מוכר: {name}")
