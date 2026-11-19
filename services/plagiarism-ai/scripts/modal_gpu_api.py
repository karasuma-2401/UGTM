"""HTTP inference contract used by the Modal GPU worker."""
from __future__ import annotations

import secrets
import threading
from numbers import Real
from typing import Callable

from fastapi import FastAPI, Header, HTTPException, Security
from pydantic import BaseModel, Field

EMBEDDING_MODEL = "BAAI/bge-m3"
RERANKER_MODEL = "BAAI/bge-reranker-v2-m3"


class EncodeRequest(BaseModel):
    texts: list[str] = Field(min_length=1, max_length=128)
    batch_size: int = Field(default=32, ge=1, le=128)


class RerankRequest(BaseModel):
    pairs: list[tuple[str, str]] = Field(min_length=1, max_length=128)
    batch_size: int = Field(default=32, ge=1, le=128)


def create_app(embedder, reranker, api_key: str, device_info: Callable[[], dict]) -> FastAPI:
    if not api_key.strip():
        raise ValueError("GPU_WORKER_API_KEY must be configured in the Modal Secret")
    app = FastAPI(title="UGTM Remote GPU Inference Worker")
    inference_lock = threading.Lock()

    def authorize(authorization: str | None = Header(default=None), x_api_key: str | None = Header(default=None)):
        token = authorization[7:].strip() if authorization and authorization.startswith("Bearer ") else x_api_key
        if not token or not secrets.compare_digest(token, api_key):
            raise HTTPException(status_code=401, detail="Unauthorized GPU Worker Token")

    @app.get("/health")
    def health(_auth=Security(authorize)):
        return {
            "status": "healthy", **device_info(),
            "embedding_model": EMBEDDING_MODEL, "embedding_revision": "main", "embedding_dim": 1024,
            "reranker_model": RERANKER_MODEL, "reranker_revision": "main",
        }

    @app.post("/encode")
    def encode(payload: EncodeRequest, _auth=Security(authorize)):
        with inference_lock:
            result = embedder.encode(payload.texts, batch_size=payload.batch_size, max_length=512,
                                     return_dense=True, return_sparse=False, return_colbert_vecs=False)
        vectors = result["dense_vecs"].tolist()
        return {"model": EMBEDDING_MODEL, "revision": "main", "dimension": 1024,
                "count": len(vectors), "dense_vecs": vectors}

    @app.post("/rerank")
    def rerank(payload: RerankRequest, _auth=Security(authorize)):
        with inference_lock:
            scores = reranker.compute_score([list(pair) for pair in payload.pairs], batch_size=payload.batch_size,
                                             normalize=True, max_length=512)
        values = [float(scores)] if isinstance(scores, Real) else [float(score) for score in scores]
        return {"model": RERANKER_MODEL, "revision": "main", "count": len(values), "scores": values}

    return app
