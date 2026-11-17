from __future__ import annotations

import logging
from typing import Any

import numpy as np
import requests
from requests.adapters import HTTPAdapter

from app.config import settings
from app.inference import (
    InferenceBackend,
    InferenceConfigurationError,
    InferenceError,
    LocalInferenceBackend,
    RemoteInferenceClient,
)

logger = logging.getLogger(__name__)

_LOCAL_BACKEND_CACHE: dict[tuple[Any, ...], LocalInferenceBackend] = {}
_REMOTE_HTTP_SESSION: requests.Session | None = None


def _get_shared_http_session() -> requests.Session:
    """Persistent Keep-Alive HTTP session to eliminate repeated TLS/Cloudflare handshakes."""
    global _REMOTE_HTTP_SESSION
    if _REMOTE_HTTP_SESSION is None:
        sess = requests.Session()
        adapter = HTTPAdapter(pool_connections=4, pool_maxsize=8)
        sess.mount("https://", adapter)
        sess.mount("http://", adapter)
        _REMOTE_HTTP_SESSION = sess
    return _REMOTE_HTTP_SESSION


def reset_inference_backend_cache() -> None:
    """Clear cached local inference backend instances (useful in tests)."""
    global _REMOTE_HTTP_SESSION
    _LOCAL_BACKEND_CACHE.clear()
    if _REMOTE_HTTP_SESSION is not None:
        try:
            _REMOTE_HTTP_SESSION.close()
        except Exception:
            pass
        _REMOTE_HTTP_SESSION = None


def build_remote_client() -> RemoteInferenceClient:
    return RemoteInferenceClient(
        base_url=settings.remote_gpu_url,
        api_key=settings.remote_gpu_api_key,
        embedding_model=settings.embedding_model,
        embedding_revision=settings.embedding_revision,
        embedding_dim=settings.embedding_dim,
        reranker_model=settings.reranker_model,
        reranker_revision=settings.reranker_revision,
        default_batch_size=max(int(settings.remote_gpu_batch_size), 64),
        timeout_seconds=settings.remote_gpu_timeout_seconds,
        health_timeout_seconds=settings.remote_gpu_health_timeout_seconds,
        session=_get_shared_http_session(),
    )


def get_inference_backend() -> InferenceBackend:
    mode = (settings.inference_mode or "").strip().lower()
    if mode == "remote":
        return build_remote_client()
    if mode == "local":
        cache_key = (
            settings.embedding_model,
            settings.embedding_revision,
            settings.embedding_dim,
            settings.reranker_model,
            settings.reranker_revision,
        )
        backend = _LOCAL_BACKEND_CACHE.get(cache_key)
        if backend is None:
            backend = LocalInferenceBackend(
                embedding_model=settings.embedding_model,
                embedding_revision=settings.embedding_revision,
                embedding_dim=settings.embedding_dim,
                reranker_model=settings.reranker_model,
                reranker_revision=settings.reranker_revision,
            )
            _LOCAL_BACKEND_CACHE[cache_key] = backend
        return backend

    raise InferenceConfigurationError(
        f"Invalid INFERENCE_MODE={settings.inference_mode!r}; expected 'remote' or 'local'."
    )


def check_remote_gpu() -> dict[str, Any] | None:
    """Non-throwing probe for remote GPU health status."""
    url = (settings.remote_gpu_url or "").strip()
    if not url:
        return None
    try:
        return build_remote_client().health()
    except InferenceError as exc:
        logger.warning(
            "Remote GPU health check failed (%s): %s", exc.error_code, exc
        )
        return None


def encode_texts(
    texts: list[str], batch_size: int | None = None
) -> np.ndarray:
    """Encode texts using the explicitly configured inference backend (no silent fallback)."""
    return get_inference_backend().encode(texts, batch_size=batch_size)


def rerank_pairs(
    pairs: list[list[str]], batch_size: int | None = None
) -> list[float]:
    """Rerank candidate passage pairs using the explicitly configured inference backend."""
    return get_inference_backend().rerank(pairs, batch_size=batch_size)
