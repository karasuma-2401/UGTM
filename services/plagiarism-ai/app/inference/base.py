from __future__ import annotations

import math
from typing import Any, Protocol, Sequence

import numpy as np


class InferenceError(RuntimeError):
    """Base error for embedding and reranking inference failures."""

    default_error_code = "INFERENCE_ERROR"

    def __init__(self, message: str, *, error_code: str | None = None):
        super().__init__(message)
        self.error_code = error_code or self.default_error_code


class InferenceConfigurationError(InferenceError):
    default_error_code = "GPU_NOT_CONFIGURED"


class InferenceUnavailableError(InferenceError):
    default_error_code = "GPU_UNAVAILABLE"


class InferenceAuthError(InferenceError):
    default_error_code = "GPU_AUTH_ERROR"


class InferenceContractError(InferenceError):
    default_error_code = "GPU_CONTRACT_ERROR"


class InferenceDependencyError(InferenceError):
    default_error_code = "LOCAL_INFERENCE_DEPENDENCY_MISSING"


class InferenceBackend(Protocol):
    mode: str

    def health(self) -> dict[str, Any]: ...

    def encode(
        self, texts: list[str], batch_size: int | None = None
    ) -> np.ndarray: ...

    def rerank(
        self, pairs: list[list[str]], batch_size: int | None = None
    ) -> list[float]: ...


def validate_texts_input(texts: Sequence[str]) -> None:
    if not isinstance(texts, (list, tuple)):
        raise ValueError("texts must be a list or tuple of strings")
    for idx, item in enumerate(texts):
        if not isinstance(item, str):
            raise ValueError(f"texts[{idx}] must be a str, got {type(item).__name__}")


def validate_pairs_input(pairs: Sequence[Sequence[str]]) -> None:
    if not isinstance(pairs, (list, tuple)):
        raise ValueError("pairs must be a list or tuple of [query, source] pairs")
    for idx, pair in enumerate(pairs):
        if (
            not isinstance(pair, (list, tuple))
            or len(pair) != 2
            or not isinstance(pair[0], str)
            or not isinstance(pair[1], str)
        ):
            raise ValueError(
                f"pairs[{idx}] must be a 2-element [query_text, source_text] string pair"
            )


def validate_model_identity(
    payload: dict[str, Any],
    *,
    expected_model: str,
    expected_revision: str | None = None,
    model_fields: tuple[str, ...] = ("model",),
    revision_fields: tuple[str, ...] = ("revision",),
) -> None:
    reported_model = None
    for field in model_fields:
        value = payload.get(field)
        if value is not None:
            reported_model = str(value).strip()
            break

    if reported_model and reported_model != expected_model:
        raise InferenceContractError(
            f"Remote model mismatch: expected '{expected_model}', got '{reported_model}'"
        )

    expected_rev = (expected_revision or "").strip()
    if expected_rev:
        reported_rev = None
        for field in revision_fields:
            value = payload.get(field)
            if value is not None and str(value).strip():
                reported_rev = str(value).strip()
                break
        if reported_rev != expected_rev:
            raise InferenceContractError(
                f"Remote model revision mismatch for '{expected_model}': "
                f"expected '{expected_rev}', got '{reported_rev}'"
            )


def validate_embeddings(
    raw_vectors: Any,
    *,
    expected_count: int,
    expected_dim: int = 1024,
) -> np.ndarray:
    if expected_dim <= 0:
        raise ValueError("expected_dim must be positive")
    if expected_count == 0:
        return np.empty((0, expected_dim), dtype=np.float32)

    if raw_vectors is None or not isinstance(raw_vectors, (list, tuple, np.ndarray)):
        raise InferenceContractError(
            "Embedding response is missing a valid 'dense_vecs' array"
        )

    try:
        arr = np.asarray(raw_vectors, dtype=np.float32)
    except (TypeError, ValueError) as exc:
        raise InferenceContractError(
            f"Failed to parse dense embeddings as float32 matrix: {exc}"
        ) from exc

    if arr.ndim != 2 or arr.shape != (expected_count, expected_dim):
        raise InferenceContractError(
            f"Invalid embedding matrix shape: expected ({expected_count}, {expected_dim}), "
            f"got {arr.shape}"
        )

    if not np.isfinite(arr).all():
        raise InferenceContractError("Embedding matrix contains NaN or Inf values")

    return arr


def validate_rerank_scores(
    raw_scores: Any,
    *,
    expected_count: int,
) -> list[float]:
    if expected_count == 0:
        return []

    if raw_scores is None or not isinstance(raw_scores, (list, tuple, np.ndarray)):
        raise InferenceContractError(
            "Rerank response is missing a valid 'scores' sequence"
        )

    if len(raw_scores) != expected_count:
        raise InferenceContractError(
            f"Invalid rerank score count: expected {expected_count}, got {len(raw_scores)}"
        )

    validated: list[float] = []
    for idx, item in enumerate(raw_scores):
        if isinstance(item, bool) or not isinstance(
            item, (int, float, np.number)
        ):
            raise InferenceContractError(
                f"Rerank score at index {idx} is not numeric: {item!r}"
            )
        score = float(item)
        if not math.isfinite(score):
            raise InferenceContractError(
                f"Rerank score at index {idx} is non-finite ({score})"
            )
        validated.append(score)

    return validated

