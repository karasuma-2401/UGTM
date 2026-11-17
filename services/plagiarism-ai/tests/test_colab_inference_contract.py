from __future__ import annotations

import math
from typing import Any

import numpy as np
import pytest

from app.inference.base import (
    InferenceAuthError,
    InferenceContractError,
    InferenceUnavailableError,
)
from app.inference.remote import RemoteInferenceClient


class MockHttpTransport:
    def __init__(self, responses: dict[str, Any]):
        self.responses = responses
        self.call_history: list[dict[str, Any]] = []

    def request(
        self,
        *,
        method: str,
        url: str,
        headers: dict[str, str],
        json: dict[str, Any] | None,
        timeout: float,
    ):
        self.call_history.append({"method": method, "url": url, "headers": headers, "json": json})
        for path_pattern, resp_data in self.responses.items():
            if url.endswith(path_pattern):
                status_code, body = resp_data
                return _MockHttpResponse(status_code, body)
        raise AssertionError(f"Unexpected URL: {url}")


class _MockHttpResponse:
    def __init__(self, status_code: int, data: Any):
        self.status_code = status_code
        self._data = data

    @property
    def ok(self) -> bool:
        return 200 <= self.status_code < 300

    def json(self) -> Any:
        return self._data


def test_colab_worker_contract_happy_path():
    """Verify that Colab worker response format is fully accepted by RemoteInferenceClient."""
    transport = MockHttpTransport(
        {
            "/health": (
                200,
                {
                    "status": "healthy",
                    "device": "Tesla T4",
                    "cuda": True,
                    "vram_allocated_mb": 2180.45,
                    "embedding_model": "BAAI/bge-m3",
                    "embedding_revision": "main",
                    "embedding_dim": 1024,
                    "reranker_model": "BAAI/bge-reranker-v2-m3",
                    "reranker_revision": "main",
                },
            ),
            "/encode": (
                200,
                {
                    "model": "BAAI/bge-m3",
                    "revision": "main",
                    "dimension": 1024,
                    "count": 2,
                    "dense_vecs": [[0.05] * 1024, [-0.05] * 1024],
                },
            ),
            "/rerank": (
                200,
                {
                    "model": "BAAI/bge-reranker-v2-m3",
                    "revision": "main",
                    "count": 1,
                    "scores": [0.8842],
                },
            ),
        }
    )

    client = RemoteInferenceClient(
        base_url="https://gpu-colab.trycloudflare.com",
        api_key="secret-colab-key",
        embedding_model="BAAI/bge-m3",
        embedding_revision="main",
        reranker_model="BAAI/bge-reranker-v2-m3",
        reranker_revision="main",
        session=transport,
    )

    health = client.health()
    assert health["status"] == "healthy"
    assert health["device"] == "Tesla T4"
    assert health["embedding_model"] == "BAAI/bge-m3"

    vecs = client.encode(["Đoạn một", "Đoạn hai"])
    assert vecs.shape == (2, 1024)
    assert vecs.dtype == np.float32

    scores = client.rerank([["Truy vấn", "Tài liệu nguồn"]])
    assert len(scores) == 1
    assert scores[0] == 0.8842

    # Verify auth headers were delivered to all requests
    assert len(transport.call_history) == 3
    for call in transport.call_history:
        assert call["headers"]["Authorization"] == "Bearer secret-colab-key"
        assert call["headers"]["X-API-Key"] == "secret-colab-key"


def test_colab_worker_rejects_unauthorized_token():
    transport = MockHttpTransport(
        {
            "/health": (401, {"detail": "Unauthorized GPU Worker Token"}),
        }
    )
    client = RemoteInferenceClient(
        base_url="https://gpu-colab.trycloudflare.com",
        api_key="wrong-token",
        session=transport,
    )
    with pytest.raises(InferenceAuthError) as exc_info:
        client.health()
    assert exc_info.value.error_code == "GPU_AUTH_ERROR"


def test_colab_worker_catches_model_revision_mismatch():
    transport = MockHttpTransport(
        {
            "/health": (
                200,
                {
                    "status": "healthy",
                    "embedding_model": "BAAI/bge-m3",
                    "embedding_revision": "old-rev-2024",
                    "reranker_model": "BAAI/bge-reranker-v2-m3",
                    "reranker_revision": "main",
                },
            ),
        }
    )
    client = RemoteInferenceClient(
        base_url="https://gpu-colab.trycloudflare.com",
        embedding_model="BAAI/bge-m3",
        embedding_revision="pinned-rev-2026",
        session=transport,
    )
    with pytest.raises(InferenceContractError, match="revision mismatch") as exc_info:
        client.health()
    assert exc_info.value.error_code == "GPU_CONTRACT_ERROR"

