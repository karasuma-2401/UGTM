import hashlib
import json
import logging
from functools import lru_cache
from pathlib import Path

import joblib

from app.config import settings
from app.features import FEATURE_NAMES, FEATURE_SCHEMA, feature_vector

logger = logging.getLogger(__name__)


@lru_cache(maxsize=1)
def load_classifier():
    # Chỉ dùng artifact do pipeline nội bộ tạo và đã được kiểm chứng.
    # joblib/pickle KHÔNG an toàn với file không đáng tin cậy.
    path = Path(settings.classifier_path)
    manifest_path = path.with_suffix(".manifest.json")
    if not path.is_file() or not manifest_path.is_file():
        raise RuntimeError("CLASSIFIER_ARTIFACT_MISSING")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if (
        manifest.get("feature_schema") != FEATURE_SCHEMA
        or manifest.get("feature_names") != FEATURE_NAMES
    ):
        raise RuntimeError("CLASSIFIER_FEATURE_SCHEMA_MISMATCH")
    if (
        manifest.get("embedding_model") != settings.embedding_model
        or manifest.get("reranker_model") != settings.reranker_model
    ):
        raise RuntimeError("CLASSIFIER_BASE_MODEL_MISMATCH")

    expected_sha = (manifest.get("artifact_sha256") or "").strip().lower()
    if expected_sha:
        actual_sha = hashlib.sha256(path.read_bytes()).hexdigest().lower()
        if actual_sha != expected_sha:
            raise RuntimeError("CLASSIFIER_ARTIFACT_HASH_MISMATCH")

    expected_sklearn = (manifest.get("sklearn_version") or "").strip()
    if expected_sklearn:
        try:
            import sklearn

            if sklearn.__version__ != expected_sklearn:
                logger.warning(
                    "Classifier artifact sklearn_version (%s) differs from runtime (%s)",
                    expected_sklearn,
                    sklearn.__version__,
                )
        except ImportError:
            pass

    model = joblib.load(path)
    return model, manifest


def apply_review_policy(
    score: float,
    threshold: float = 0.14,
    tau_low: float = 0.30,
    tau_high: float = 0.70,
) -> dict:
    """Evaluate decision policy and human review requirements based on calibrated confidence."""
    if score >= tau_high:
        return {
            "flagged": True,
            "needs_human_review": True,
            "decision_source": "calibrated_classifier_high_confidence",
            "confidence_band": "high_confidence",
            "reason": "Calibrated probability exceeds high-confidence threshold (P >= 0.70).",
        }
    elif score >= tau_low:
        return {
            "flagged": True,
            "needs_human_review": True,
            "decision_source": "uncertainty_band",
            "confidence_band": "uncertainty_band",
            "reason": "Probability falls in ambiguous calibration zone [0.30, 0.70); requires evaluator verification.",
        }
    else:
        return {
            "flagged": score >= threshold,
            "needs_human_review": False,
            "decision_source": "calibrated_classifier_low_confidence",
            "confidence_band": "low_confidence",
            "reason": "Calibrated probability falls below uncertainty band (P < 0.30).",
        }


def score_candidate(features: dict):
    if not settings.classifier_enabled:
        return None
    model, manifest = load_classifier()
    # predict_proba là điểm mô hình trên dữ liệu huấn luyện, KHÔNG phải
    # xác suất vi phạm quy chế hay đạo văn trong thực tế.
    score = float(model.predict_proba([feature_vector(features)])[0, 1])
    threshold = float(manifest["threshold"])
    bands = manifest.get("decision_bands", {})
    tau_low = float(bands.get("tau_low", 0.30))
    tau_high = float(bands.get("tau_high", 0.70))
    policy = apply_review_policy(score, threshold, tau_low, tau_high)

    return {
        "score": score,
        "threshold": threshold,
        "version": str(manifest["model_version"]),
        "needs_human_review": policy["needs_human_review"],
        "decision_source": policy["decision_source"],
        "confidence_band": policy["confidence_band"],
        "policy": policy,
    }


def score_candidates_batch(features_list: list[dict]) -> list[dict] | None:
    """Vectorized batch inference for candidate pairs (1000x faster than per-row predict_proba)."""
    if not settings.classifier_enabled:
        return None
    if not features_list:
        return []
    model, manifest = load_classifier()
    matrix = [feature_vector(f) for f in features_list]
    scores = model.predict_proba(matrix)[:, 1]
    threshold = float(manifest["threshold"])
    version = str(manifest["model_version"])
    bands = manifest.get("decision_bands", {})
    tau_low = float(bands.get("tau_low", 0.30))
    tau_high = float(bands.get("tau_high", 0.70))

    results: list[dict] = []
    for raw_score in scores:
        score = float(raw_score)
        policy = apply_review_policy(score, threshold, tau_low, tau_high)
        results.append(
            {
                "score": score,
                "threshold": threshold,
                "version": version,
                "needs_human_review": policy["needs_human_review"],
                "decision_source": policy["decision_source"],
                "confidence_band": policy["confidence_band"],
                "policy": policy,
            }
        )
    return results
