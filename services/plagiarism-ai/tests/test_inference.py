from __future__ import annotations

import math
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import requests

import app.models as models_module
from app.config import settings
from app.inference import (
    InferenceAuthError,
    InferenceConfigurationError,
    InferenceContractError,
    InferenceDependencyError,
    InferenceUnavailableError,
    LocalInferenceBackend,
    RemoteInferenceClient,
)
from app.inference.local import _parse_major_minor


class FakeResponse:
    def __init__(
        self,
        status_code: int = 200,
        json_data: Any = None,
        invalid_json: bool = False,
    ):
        self.status_code = status_code
        self._json_data = json_data
        self._invalid_json = invalid_json

    @property
    def ok(self) -> bool:
        return 200 <= self.status_code < 300

    def json(self) -> Any:
        if self._invalid_json:
            raise ValueError("Invalid JSON")
        return self._json_data


class FakeSession:
    def __init__(self, handler):
        self.handler = handler
        self.calls: list[dict[str, Any]] = []

    def request(
        self,
        *,
        method: str,
        url: str,
        headers: dict[str, str],
        json: dict[str, Any] | None,
        timeout: float,
    ) -> FakeResponse:
        record = {
            "method": method,
            "url": url,
            "headers": headers,
            "json": json,
            "timeout": timeout,
        }
        self.calls.append(record)
        return self.handler(record)


def test_remote_encode_and_rerank_with_auth_and_batch_chunking():
    def handler(req: dict[str, Any]) -> FakeResponse:
        if req["url"].endswith("/encode"):
            n = len(req["json"]["texts"])
            vecs = [[0.125] * 1024 for _ in range(n)]
            return FakeResponse(
                200,
                {
                    "model": "BAAI/bge-m3",
                    "revision": "rev-bge-m3-01",
                    "dense_vecs": vecs,
                },
            )
        if req["url"].endswith("/rerank"):
            n = len(req["json"]["pairs"])
            return FakeResponse(
                200,
                {
                    "model": "BAAI/bge-reranker-v2-m3",
                    "revision": "rev-rerank-01",
                    "scores": [0.75 for _ in range(n)],
                },
            )
        raise AssertionError(f"Unexpected URL {req['url']}")

    session = FakeSession(handler)
    client = RemoteInferenceClient(
        base_url="https://gpu-worker.example.trycloudflare.com/",
        api_key="secret-gpu-token",
        embedding_model="BAAI/bge-m3",
        embedding_revision="rev-bge-m3-01",
        embedding_dim=1024,
        reranker_model="BAAI/bge-reranker-v2-m3",
        reranker_revision="rev-rerank-01",
        default_batch_size=32,
        max_items_per_request=128,
        session=session,
    )

    # 135 items -> automatically split into 128 + 7 so remote cap (128) is respected
    texts = [f"đoạn văn thứ {i}" for i in range(135)]
    vecs = client.encode(texts)
    assert vecs.shape == (135, 1024)
    assert vecs.dtype == np.float32
    assert len(session.calls) == 2
    assert session.calls[0]["headers"]["Authorization"] == "Bearer secret-gpu-token"
    assert session.calls[0]["headers"]["X-API-Key"] == "secret-gpu-token"
    assert len(session.calls[0]["json"]["texts"]) == 128
    assert len(session.calls[1]["json"]["texts"]) == 7

    pairs = [[f"q{i}", f"s{i}"] for i in range(130)]
    scores = client.rerank(pairs)
    assert len(scores) == 130
    assert all(s == 0.75 for s in scores)
    assert len(session.calls) == 4


def test_empty_inputs_short_circuit_without_network():
    session = FakeSession(lambda _: pytest.fail("Should not make HTTP request"))
    client = RemoteInferenceClient(
        base_url="https://gpu-worker.example.com",
        session=session,
    )
    empty_vecs = client.encode([])
    assert empty_vecs.shape == (0, 1024)
    assert empty_vecs.dtype == np.float32
    assert client.rerank([]) == []
    assert session.calls == []


def test_remote_mode_never_falls_back_to_local_when_url_missing(monkeypatch):
    def forbidden_local(*args, **kwargs):
        raise AssertionError("Silent fallback to LocalInferenceBackend is forbidden!")

    monkeypatch.setattr(models_module, "LocalInferenceBackend", forbidden_local)
    monkeypatch.setattr(settings, "inference_mode", "remote")
    monkeypatch.setattr(settings, "remote_gpu_url", None)

    with pytest.raises(InferenceConfigurationError) as exc_info:
        models_module.encode_texts(["kiểm tra không fallback"])
    assert exc_info.value.error_code == "GPU_NOT_CONFIGURED"

    with pytest.raises(InferenceConfigurationError):
        models_module.rerank_pairs([["q", "s"]])


def test_remote_mode_never_falls_back_to_local_on_transport_or_5xx_failure(monkeypatch):
    def forbidden_local(*args, **kwargs):
        raise AssertionError("Silent fallback to LocalInferenceBackend is forbidden!")

    monkeypatch.setattr(models_module, "LocalInferenceBackend", forbidden_local)

    conn_err_session = FakeSession(
        lambda _: (_ for _ in ()).throw(requests.ConnectionError("tunnel offline"))
    )
    client_offline = RemoteInferenceClient(
        base_url="https://offline.trycloudflare.com",
        session=conn_err_session,
    )
    monkeypatch.setattr(models_module, "build_remote_client", lambda: client_offline)
    monkeypatch.setattr(settings, "inference_mode", "remote")
    monkeypatch.setattr(settings, "remote_gpu_url", "https://offline.trycloudflare.com")

    with pytest.raises(InferenceUnavailableError) as exc_info:
        models_module.encode_texts(["bài nộp thử nghiệm"])
    assert exc_info.value.error_code == "GPU_UNAVAILABLE"

    # check_remote_gpu remains a safe non-throwing probe returning None
    assert models_module.check_remote_gpu() is None

    http_503_client = RemoteInferenceClient(
        base_url="https://overloaded.trycloudflare.com",
        session=FakeSession(lambda _: FakeResponse(503, {"detail": "busy"})),
    )
    with pytest.raises(InferenceUnavailableError):
        http_503_client.rerank([["q", "s"]])


def test_remote_client_rejects_bad_auth():
    client = RemoteInferenceClient(
        base_url="https://gpu.example.com",
        api_key="wrong-token",
        session=FakeSession(lambda _: FakeResponse(401, {"detail": "unauthorized"})),
    )
    with pytest.raises(InferenceAuthError) as exc_info:
        client.health()
    assert exc_info.value.error_code == "GPU_AUTH_ERROR"


@pytest.mark.parametrize(
    ("encode_payload", "match_msg"),
    [
        ({"dense_vecs": [[0.1] * 512]}, "Invalid embedding matrix shape"),
        ({"dense_vecs": [[0.1] * 1024, [0.2] * 1024]}, "Invalid embedding matrix shape"),
        ({"dense_vecs": [[math.nan] + [0.1] * 1023]}, "NaN or Inf"),
        ({"dense_vecs": [[math.inf] + [0.1] * 1023]}, "NaN or Inf"),
        (
            {"model": "wrong/model", "dense_vecs": [[0.1] * 1024]},
            "Remote model mismatch",
        ),
        (
            {"model": "BAAI/bge-m3", "revision": "wrong-rev", "dense_vecs": [[0.1] * 1024]},
            "revision mismatch",
        ),
    ],
)
def test_remote_encode_contract_validation(encode_payload, match_msg):
    client = RemoteInferenceClient(
        base_url="https://gpu.example.com",
        embedding_model="BAAI/bge-m3",
        embedding_revision="expected-rev" if "revision" in match_msg else None,
        embedding_dim=1024,
        session=FakeSession(lambda _: FakeResponse(200, encode_payload)),
    )
    with pytest.raises(InferenceContractError, match=match_msg) as exc_info:
        client.encode(["một đoạn văn"])
    assert exc_info.value.error_code == "GPU_CONTRACT_ERROR"


@pytest.mark.parametrize(
    ("rerank_payload", "match_msg"),
    [
        ({"scores": [0.5, 0.6]}, "Invalid rerank score count"),
        ({"scores": [math.nan]}, "non-finite"),
        ({"scores": [True]}, "not numeric"),
        ({"model": "wrong/reranker", "scores": [0.8]}, "Remote model mismatch"),
    ],
)
def test_remote_rerank_contract_validation(rerank_payload, match_msg):
    client = RemoteInferenceClient(
        base_url="https://gpu.example.com",
        reranker_model="BAAI/bge-reranker-v2-m3",
        session=FakeSession(lambda _: FakeResponse(200, rerank_payload)),
    )
    with pytest.raises(InferenceContractError, match=match_msg):
        client.rerank([["query", "source"]])


def test_local_mode_fails_cleanly_when_gpu_packages_not_installed(monkeypatch):
    monkeypatch.setattr(settings, "inference_mode", "local")
    models_module.reset_inference_backend_cache()
    backend = models_module.get_inference_backend()
    assert isinstance(backend, LocalInferenceBackend)

    with pytest.raises(InferenceDependencyError) as exc_info:
        models_module.encode_texts(["kiểm tra local thiếu torch"])
    assert exc_info.value.error_code == "LOCAL_INFERENCE_DEPENDENCY_MISSING"


def test_no_torch_load_safety_bypass_exists_in_codebase():
    assert _parse_major_minor("2.5.1") < (2, 6)
    assert _parse_major_minor("2.6.0+cu124") >= (2, 6)

    project_root = Path(__file__).resolve().parent.parent
    checked_files = [
        project_root / "app" / "models.py",
        project_root / "app" / "inference" / "base.py",
        project_root / "app" / "inference" / "remote.py",
        project_root / "app" / "inference" / "local.py",
    ]
    for file_path in checked_files:
        source = file_path.read_text(encoding="utf-8")
        assert "check_torch_load_is_safe = lambda" not in source

