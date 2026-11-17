from __future__ import annotations

import importlib.util
import logging
import re
from typing import Any

import numpy as np

from app.inference.base import (
    InferenceDependencyError,
    validate_embeddings,
    validate_pairs_input,
    validate_rerank_scores,
    validate_texts_input,
)

logger = logging.getLogger(__name__)

MIN_SAFE_TORCH_VERSION = (2, 6)


def _parse_major_minor(version_str: str) -> tuple[int, int]:
    match = re.match(r"^\s*(\d+)\.(\d+)", version_str or "")
    if not match:
        return (0, 0)
    return int(match.group(1)), int(match.group(2))


def _load_local_ml_dependencies() -> tuple[Any, Any, Any]:
    """Lazy-load PyTorch and FlagEmbedding only when INFERENCE_MODE='local'.

    Enforces torch>=2.6.0 + safetensors so transformers loads trusted weights
    safely without bypassing check_torch_load_is_safe (CVE-2025-32434).
    """
    missing = [
        pkg
        for pkg in ("torch", "safetensors", "FlagEmbedding")
        if importlib.util.find_spec(pkg) is None
    ]
    if missing:
        raise InferenceDependencyError(
            f"INFERENCE_MODE='local' requires GPU/ML packages ({', '.join(missing)}). "
            "Install `requirements/gpu-colab.txt` for local inference, or use "
            "INFERENCE_MODE='remote' with REMOTE_GPU_URL."
        )

    import torch

    torch_ver = getattr(torch, "__version__", "0.0")
    if _parse_major_minor(torch_ver) < MIN_SAFE_TORCH_VERSION:
        raise InferenceDependencyError(
            f"INFERENCE_MODE='local' requires torch>=2.6.0 (detected {torch_ver}) "
            "for safe checkpoint loading without CVE-2025-32434 bypasses."
        )

    from FlagEmbedding import BGEM3FlagModel, FlagReranker

    return torch, BGEM3FlagModel, FlagReranker


class LocalInferenceBackend:
    """Explicit local inference loader for GPU workstations or offline evaluation.

    Never imported or instantiated when INFERENCE_MODE='remote'.
    """

    mode = "local"

    def __init__(
        self,
        *,
        embedding_model: str = "BAAI/bge-m3",
        embedding_revision: str | None = None,
        embedding_dim: int = 1024,
        reranker_model: str = "BAAI/bge-reranker-v2-m3",
        reranker_revision: str | None = None,
        default_batch_size: int = 4,
    ):
        self.embedding_model = embedding_model
        self.embedding_revision = (embedding_revision or "").strip() or None
        self.embedding_dim = embedding_dim
        self.reranker_model = reranker_model
        self.reranker_revision = (reranker_revision or "").strip() or None
        self.default_batch_size = max(1, int(default_batch_size))
        self._embedder: Any = None
        self._reranker: Any = None

    def _device_config(self, torch_module: Any) -> tuple[str, bool, list[str] | None]:
        device = "cuda:0" if torch_module.cuda.is_available() else "cpu"
        use_fp16 = device.startswith("cuda")
        devices = [device] if use_fp16 else None
        return device, use_fp16, devices

    def _get_embedder(self) -> Any:
        if self._embedder is None:
            torch_mod, bge_cls, _ = _load_local_ml_dependencies()
            device, use_fp16, devices = self._device_config(torch_mod)
            logger.info(
                "Loading local embedding model '%s' on %s (fp16=%s)...",
                self.embedding_model,
                device,
                use_fp16,
            )
            kwargs: dict[str, Any] = {
                "use_fp16": use_fp16,
                "devices": devices,
            }
            if self.embedding_revision:
                kwargs["model_kwargs"] = {"revision": self.embedding_revision}
            self._embedder = bge_cls(self.embedding_model, **kwargs)
        return self._embedder

    def _get_reranker(self) -> Any:
        if self._reranker is None:
            torch_mod, _, reranker_cls = _load_local_ml_dependencies()
            device, use_fp16, devices = self._device_config(torch_mod)
            logger.info(
                "Loading local reranker model '%s' on %s (fp16=%s)...",
                self.reranker_model,
                device,
                use_fp16,
            )
            kwargs: dict[str, Any] = {
                "use_fp16": use_fp16,
                "devices": devices,
            }
            if self.reranker_revision:
                kwargs["model_kwargs"] = {"revision": self.reranker_revision}
            self._reranker = reranker_cls(self.reranker_model, **kwargs)
        return self._reranker

    def health(self) -> dict[str, Any]:
        torch_mod, _, _ = _load_local_ml_dependencies()
        cuda_available = bool(torch_mod.cuda.is_available())
        device = (
            torch_mod.cuda.get_device_name(0)
            if cuda_available
            else "cpu"
        )
        return {
            "status": "healthy",
            "mode": "local",
            "cuda": cuda_available,
            "device": device,
            "torch_version": getattr(torch_mod, "__version__", None),
            "embedding_model": self.embedding_model,
            "embedding_revision": self.embedding_revision,
            "reranker_model": self.reranker_model,
            "reranker_revision": self.reranker_revision,
        }

    def encode(
        self, texts: list[str], batch_size: int | None = None
    ) -> np.ndarray:
        validate_texts_input(texts)
        if not texts:
            return np.empty((0, self.embedding_dim), dtype=np.float32)

        effective_batch = max(1, int(batch_size or self.default_batch_size))
        data = self._get_embedder().encode(
            list(texts),
            batch_size=effective_batch,
            max_length=512,
            return_dense=True,
            return_sparse=False,
            return_colbert_vecs=False,
        )
        return validate_embeddings(
            data.get("dense_vecs"),
            expected_count=len(texts),
            expected_dim=self.embedding_dim,
        )

    def rerank(
        self, pairs: list[list[str]], batch_size: int | None = None
    ) -> list[float]:
        validate_pairs_input(pairs)
        if not pairs:
            return []

        effective_batch = max(1, int(batch_size or self.default_batch_size))
        scores = self._get_reranker().compute_score(
            [[pair[0], pair[1]] for pair in pairs],
            batch_size=effective_batch,
            normalize=True,
            max_length=512,
        )
        if isinstance(scores, (float, int, np.floating, np.integer)):
            raw_list = [float(scores)]
        else:
            raw_list = list(scores)
        return validate_rerank_scores(raw_list, expected_count=len(pairs))

