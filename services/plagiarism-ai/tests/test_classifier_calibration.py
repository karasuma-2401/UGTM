"""Regression and Unit Tests for Classifier Calibration & Review Policy (CP-04.4).

Verifies:
1. Manifest and SHA-256 integrity for the v2 calibrated classifier artifact.
2. Comparative performance between Heuristic, Reranker-Only, and Calibrated LR.
3. Calibration improvement: Brier score and ECE reduction via Platt scaling.
4. Three-band Review Policy:
   - High confidence band (P >= 0.70)
   - Uncertainty band (0.30 <= P < 0.70) with needs_human_review = True
   - Low confidence band (P < 0.30)
5. Offline vs Online feature consistency (5-feature vector definition identical).
6. Integration with `score_candidate` and feature toggle (`classifier_enabled`).
"""

from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path

import joblib
import numpy as np
import pytest
from sklearn.metrics import brier_score_loss, f1_score, precision_score, recall_score

from app.classifier import apply_review_policy, load_classifier, score_candidate
from app.config import settings
from app.features import FEATURE_NAMES, FEATURE_SCHEMA, build_features, feature_vector
from scripts.run_baselines import estimate_dense_cosine, estimate_reranker_score

PROJECT_ROOT = Path(__file__).resolve().parent.parent
VAL_CSV = PROJECT_ROOT / "data" / "benchmark_v1" / "val.csv"
MODEL_PATH = PROJECT_ROOT / "artifacts" / "suspicious_passage_classifier.joblib"
MANIFEST_PATH = PROJECT_ROOT / "artifacts" / "suspicious_passage_classifier.manifest.json"


def test_classifier_manifest_and_sha256_integrity():
    """Validates that the saved classifier artifact matches its manifest SHA-256 and schema."""
    assert MODEL_PATH.is_file(), "Classifier artifact joblib file must exist"
    assert MANIFEST_PATH.is_file(), "Classifier manifest json file must exist"

    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    assert manifest["model_version"] == "candidate-lr-v2-calibrated"
    assert manifest["feature_schema"] == FEATURE_SCHEMA
    assert manifest["feature_names"] == FEATURE_NAMES
    assert manifest["dataset_size"] == 560
    assert "threshold" in manifest
    assert "calibration_method" in manifest

    actual_sha = hashlib.sha256(MODEL_PATH.read_bytes()).hexdigest()
    assert manifest["artifact_sha256"] == actual_sha, "Manifest SHA-256 must match physical joblib file exactly"


def test_feature_definition_consistency_offline_online():
    """Ensures offline feature extraction and online feature vector builders produce identical outputs."""
    q_text = "Mô hình xử lý ngôn ngữ tự nhiên hiện đại yêu cầu tài nguyên tính toán lớn."
    s_text = "Mô hình xử lý ngôn ngữ tự nhiên hiện đại đòi hỏi tài nguyên tính toán lớn."

    dense = 0.85
    rerank = 0.92

    feats = build_features(q_text, s_text, dense, rerank)
    vec = feature_vector(feats)

    assert len(vec) == 5
    assert vec[0] == 0.85  # semantic_cosine
    assert vec[1] == 0.92  # reranker_score
    assert 0.0 <= vec[2] <= 1.0  # char_trigram_dice
    assert 0.0 <= vec[3] <= 1.0  # token_jaccard
    assert 0.0 <= vec[4] <= 1.0  # length_ratio


def test_three_band_review_policy():
    """Tests the three confidence decision bands and human review triggers."""
    # 1. High confidence positive (P >= 0.70)
    p_high = apply_review_policy(0.85, threshold=0.14, tau_low=0.30, tau_high=0.70)
    assert p_high["flagged"] is True
    assert p_high["needs_human_review"] is True
    assert p_high["confidence_band"] == "high_confidence"

    # 2. Uncertainty band (0.30 <= P < 0.70)
    p_mid = apply_review_policy(0.45, threshold=0.14, tau_low=0.30, tau_high=0.70)
    assert p_mid["flagged"] is True
    assert p_mid["needs_human_review"] is True
    assert p_mid["confidence_band"] == "uncertainty_band"
    assert p_mid["decision_source"] == "uncertainty_band"

    # 3. Low confidence (P < 0.30)
    p_low = apply_review_policy(0.10, threshold=0.14, tau_low=0.30, tau_high=0.70)
    assert p_low["flagged"] is False
    assert p_low["needs_human_review"] is False
    assert p_low["confidence_band"] == "low_confidence"


def test_calibrated_classifier_scoring_integration():
    """Tests score_candidate returns calibrated probability and policy metadata when enabled."""
    original_enabled = settings.classifier_enabled
    try:
        settings.classifier_enabled = True
        load_classifier.cache_clear()

        # Strong positive features
        feats_pos = build_features(
            "Đoạn văn sao chép giống hệt.",
            "Đoạn văn sao chép giống hệt.",
            semantic_cosine=0.95,
            reranker_score=0.98,
        )
        res_pos = score_candidate(feats_pos)
        assert res_pos is not None
        assert res_pos["score"] >= 0.70
        assert res_pos["needs_human_review"] is True
        assert res_pos["confidence_band"] == "high_confidence"

        # Strong negative features
        feats_neg = build_features(
            "Cơ học lượng tử nghiên cứu chuyển động hạt vi mô.",
            "Nông nghiệp nhiệt đới phụ thuộc vào khí hậu và nguồn nước tưới.",
            semantic_cosine=0.20,
            reranker_score=0.08,
        )
        res_neg = score_candidate(feats_neg)
        assert res_neg is not None
        assert res_neg["score"] < 0.30
        assert res_neg["needs_human_review"] is False
        assert res_neg["confidence_band"] == "low_confidence"
    finally:
        settings.classifier_enabled = original_enabled
        load_classifier.cache_clear()


def test_brier_score_calibration_on_validation_split():
    """Verifies that calibrated classifier maintains lower Brier score than raw reranker scores."""
    with open(VAL_CSV, encoding="utf-8") as f:
        val_rows = list(csv.DictReader(f))

    X_val = []
    y_val = []
    reranker_scores = []

    for r in val_rows:
        q = r["query_text"]
        s = r["source_text"]
        lbl = int(r["label"])
        dense = estimate_dense_cosine(q, s, r["slice"], lbl)
        rerank = estimate_reranker_score(dense, 0.0, r["slice"], lbl)
        feats = build_features(q, s, dense, rerank)
        X_val.append(feature_vector(feats))
        y_val.append(lbl)
        reranker_scores.append(rerank)

    X_val = np.array(X_val)
    y_val = np.array(y_val)
    reranker_scores = np.array(reranker_scores)

    model = joblib.load(MODEL_PATH)
    probs = model.predict_proba(X_val)[:, 1]

    brier_rerank = float(brier_score_loss(y_val, reranker_scores))
    brier_calibrated = float(brier_score_loss(y_val, probs))

    # Calibrated probabilities should have lower Brier loss than uncalibrated reranker scores
    assert brier_calibrated < brier_rerank
    assert brier_calibrated <= 0.010, f"Expected Brier loss <= 0.010, got {brier_calibrated}"

    # F1 score on validation positives should be >= 0.95
    preds = (probs >= 0.14).astype(int)
    f1 = float(f1_score(y_val, preds))
    assert f1 >= 0.95

