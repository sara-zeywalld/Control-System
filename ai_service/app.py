"""שירות AI (Mock) - FastAPI.

הרצה:  python app.py [--port 8000]      (תיעוד אינטראקטיבי של החוזה: http://localhost:8000/docs)
נקודות קצה:
  POST /v1/control-suggestions    {prompt, catalog, ...}  ->  הצעת בקרה
  POST /v1/proactive-suggestions  {catalog, ...}          ->  {suggestions: [...]}
  GET  /health

המודלים כאן הם חוזה ה-AI (ראו docs/ai-contract.md). מודל אמיתי יוכל להחליף את mock_model
בלי לשנות את החוזה, ולכן גם לא את צד ה-.NET.
"""
import argparse
from typing import Any

import uvicorn
from fastapi import FastAPI
from pydantic import BaseModel, ConfigDict, Field

import mock_model

MAX_PROMPT_LENGTH = 1000


class AiRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    prompt: str = Field("", max_length=MAX_PROMPT_LENGTH)
    language: str = "he"
    schema_version: int = Field(1, alias="schemaVersion")
    allowed_operators: dict[str, list[str]] = Field(default_factory=dict, alias="allowedOperators")
    catalog: list[dict[str, Any]] = Field(min_length=1)


class AiProposal(BaseModel):
    name: str
    description: str | None = None
    explanation: str | None = None
    confidence: float = Field(ge=0, le=1)
    warnings: list[str] = []
    definition: dict[str, Any]


class ProactiveResponse(BaseModel):
    suggestions: list[AiProposal]


app = FastAPI(title="Controls AI mock", version="1")


def _to_dict(request: AiRequest) -> dict:
    return request.model_dump(by_alias=True)


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "model": "mock-rules-v1"}


@app.post("/v1/control-suggestions", response_model=AiProposal)
def control_suggestions(request: AiRequest) -> dict:
    return mock_model.suggest_control(_to_dict(request))


@app.post("/v1/proactive-suggestions", response_model=ProactiveResponse)
def proactive_suggestions(request: AiRequest) -> dict:
    return mock_model.suggest_proactive(_to_dict(request))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()
    uvicorn.run(app, host="127.0.0.1", port=args.port)
