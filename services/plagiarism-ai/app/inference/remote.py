from __future__ import annotations

import logging
from typing import Any

import numpy as np
import requests

from app.inference.base import (
    InferenceAuthError,
    InferenceConfigurationError,
    InferenceContractError,
    InferenceUnavailableError,
    validate_embeddings,
    validate_model_identity,
    validate_pairs_input,
    validate_rerank_scores,
    validate_texts_input,
)

logger = logging.getLogger(__name__)


class RemoteInferenceClient:
    """HTTP client for the Remote GPU Inference Worker (Google Colab / dedicated GPU).

    Never imports PyTorch, Transformers, or FlagEmbedding, and never falls back
    silently to local CPU model execution.
    """

    mode = "remote"

    def __init__(
        self,
        *,
        base_url: str | None,
        api_key: str | None = None,
        embedding_model: str = "BAAI/bge-m3",
        embedding_revision: str | None = None,
        embedding_dim: int = 1024,
        reranker_model: str = "BAAI/bge-reranker-v2-m3",
        reranker_revision: str | None = None,
        default_batch_size: int = 32,
        max_items_per_request: int = 128,
        timeout_seconds: float = 180.0,
        health_timeout_seconds: float = 5.0,
        session: requests.Session | None = None,
    ):
        self.base_url = (base_url or "").strip().rstrip("/")
        self.api_key = (api_key or "").strip() or None
        self.embedding_model = embedding_model
        self.embedding_revision = (embedding_revision or "").strip() or None
        self.embedding_dim = embedding_dim
        self.reranker_model = reranker_model
        self.reranker_revision = (reranker_revision or "").strip() or None
        self.max_items_per_request = max(1, int(max_items_per_request))
        self.default_batch_size = max(
            1, min(int(default_batch_size), self.max_items_per_request)
        )
        self.timeout_seconds = float(timeout_seconds)
        self.health_timeout_seconds = float(health_timeout_seconds)
        self._session = session

    def _require_url(self) -> str:
        if not self.base_url:
            raise InferenceConfigurationError(
                "REMOTE_GPU_URL is not configured while INFERENCE_MODE='remote'. "
                "Set REMOTE_GPU_URL to an active GPU worker endpoint or explicitly "
                "set INFERENCE_MODE='local'."
            )
        return self.base_url

    def _headers(self) -> dict[str, str]:
        headers = {"Accept": "application/json"}
        if self.api_key:
            headers["Authorization"] = f"Bearer {self.api_key}"
            headers["X-API-Key"] = self.api_key
        return headers

    def _request_json(
        self,
        method: str,
        path: str,
        *,
        timeout: float,
        json_body: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        url = f"{self._require_url()}{path}"
        http = self._session or requests
        try:
            response = http.request(
                method=method,
                url=url,
                headers=self._headers(),
                json=json_body,
                timeout=timeout,
            )
        except (requests.Timeout, requests.ConnectionError) as exc:
            raise InferenceUnavailableError(
                f"Remote GPU endpoint {url} is unreachable or timed out: {exc}"
            ) from exc
        except requests.RequestException as exc:
            raise InferenceUnavailableError(
                f"Remote GPU transport error calling {url}: {exc}"
            ) from exc

        status_code = response.status_code
        if status_code in (401, 403):
            raise InferenceAuthError(
                f"Remote GPU endpoint {url} rejected credentials (HTTP {status_code})"
            )
        if status_code >= 500:
            raise InferenceUnavailableError(
                f"Remote GPU endpoint {url} failed with HTTP {status_code}"
            )
        if not (200 <= status_code < 300):
            raise InferenceContractError(
                f"Remote GPU endpoint {url} returned unexpected HTTP {status_code}"
            )

        try:
            payload = response.json()
        except ValueError as exc:
            raise InferenceContractError(
                f"Remote GPU endpoint {url} returned invalid JSON"
            ) from exc

        if not isinstance(payload, dict):
            raise InferenceContractError(
                f"Remote GPU endpoint {url} returned non-object JSON payload"
            )
        return payload

    def health(self) -> dict[str, Any]:
        payload = self._request_json(
            "GET",
            "/health",
            timeout=self.health_timeout_seconds,
        )
        validate_model_identity(
            payload,
            expected_model=self.embedding_model,
            expected_revision=self.embedding_revision,
            model_fields=("embedding_model",),
            revision_fields=("embedding_revision",),
        )
        validate_model_identity(
            payload,
            expected_model=self.reranker_model,
            expected_revision=self.reranker_revision,
            model_fields=("reranker_model",),
            revision_fields=("reranker_revision",),
        )
        return payload

    def encode(
        self, texts: list[str], batch_size: int | None = None
    ) -> np.ndarray:
        validate_texts_input(texts)
        if not texts:
            return np.empty((0, self.embedding_dim), dtype=np.float32)

        self._require_url()
        effective_batch = max(
            1,
            min(
                int(batch_size or self.default_batch_size),
                self.max_items_per_request,
            ),
        )
        logger.info(
            "Remote GPU encoding %d texts via %s (batch_size=%d)...",
            len(texts),
            self.base_url,
            effective_batch,
        )

        order = sorted(range(len(texts)), key=lambda i: len(texts[i]))
        sorted_texts = [texts[i] for i in order]

        batches: list[np.ndarray] = []
        for start in range(0, len(sorted_texts), self.max_items_per_request):
            chunk = list(sorted_texts[start : start + self.max_items_per_request])
            payload = self._request_json(
                "POST",
                "/encode",
                timeout=self.timeout_seconds,
                json_body={
                    "texts": chunk,
                    "batch_size": min(effective_batch, len(chunk)),
                },
            )
            validate_model_identity(
                payload,
                expected_model=self.embedding_model,
                expected_revision=self.embedding_revision,
                model_fields=("model", "embedding_model"),
                revision_fields=("revision", "embedding_revision"),
            )
            batches.append(
                validate_embeddings(
                    payload.get("dense_vecs"),
                    expected_count=len(chunk),
                    expected_dim=self.embedding_dim,
                )
            )

        stacked = (
            batches[0]
            if len(batches) == 1
            else np.vstack(batches).astype(np.float32, copy=False)
        )
        result = np.empty_like(stacked)
        result[order] = stacked
        return result

    def rerank(
        self, pairs: list[list[str]], batch_size: int | None = None
    ) -> list[float]:
        validate_pairs_input(pairs)
        if not pairs:
            return []

        self._require_url()
        effective_batch = max(
            1,
            min(
                int(batch_size or self.default_batch_size),
                self.max_items_per_request,
            ),
        )
        logger.info(
            "Remote GPU reranking %d pairs via %s (batch_size=%d)...",
            len(pairs),
            self.base_url,
            effective_batch,
        )

        order = sorted(
            range(len(pairs)),
            key=lambda i: len(pairs[i][0]) + len(pairs[i][1]),
        )
        sorted_pairs = [pairs[i] for i in order]

        sorted_scores: list[float] = []
        for start in range(0, len(sorted_pairs), self.max_items_per_request):
            chunk = [
                [pair[0], pair[1]]
                for pair in sorted_pairs[start : start + self.max_items_per_request]
            ]
            payload = self._request_json(
                "POST",
                "/rerank",
                timeout=self.timeout_seconds,
                json_body={
                    "pairs": chunk,
                    "batch_size": min(effective_batch, len(chunk)),
                },
            )
            validate_model_identity(
                payload,
                expected_model=self.reranker_model,
                expected_revision=self.reranker_revision,
                model_fields=("model", "reranker_model"),
                revision_fields=("revision", "reranker_revision"),
            )
            sorted_scores.extend(
                validate_rerank_scores(
                    payload.get("scores"),
                    expected_count=len(chunk),
                )
            )

        restored_scores = [0.0] * len(pairs)
        for sorted_idx, orig_idx in enumerate(order):
            restored_scores[orig_idx] = sorted_scores[sorted_idx]
        return restored_scores

