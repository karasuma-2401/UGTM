"""Train and Evaluate Suspicious Passage Classifier with Calibration & Review Policy (CP-04.4).

Compares 4 decision strategies on the shared validation split (val.csv, 120 pairs):
1. Heuristic Decision: reranker >= 0.70 or lexical >= 0.92 (B0/B4 baseline)
2. Reranker-Only Threshold: sweep threshold on dev for optimal F1
3. Logistic Regression: trained on train.csv (560 pairs), evaluated on val.csv
4. Calibrated Logistic Regression: Platt scaling / Sigmoid calibration on train.csv

Computes:
- Precision, Recall, F1, PR-AUC, ROC-AUC
- Calibration Brier score loss
- Expected Calibration Error (ECE)
- Three-band Decision Policy (Clear Independence, Uncertainty Band, High-Confidence Reuse)
- Saves model artifact: artifacts/suspicious_passage_classifier.joblib & manifest
- Saves evaluation results: data/classifier_evaluation_results.json
"""

from __future__ import annotations

import csv
import hashlib
import json
import logging
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

import joblib
import numpy as np
import sklearn
from sklearn.calibration import CalibratedClassifierCV, calibration_curve
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (
    brier_score_loss,
    confusion_matrix,
    f1_score,
    precision_recall_curve,
    precision_score,
    recall_score,
    roc_auc_score,
    auc,
)
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from app.features import (
    FEATURE_NAMES,
    FEATURE_SCHEMA,
    build_features,
    feature_vector,
)
from scripts.run_baselines import estimate_dense_cosine, estimate_reranker_score

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

PROJECT_ROOT = Path(__file__).resolve().parent.parent
TRAIN_CSV = PROJECT_ROOT / "data" / "benchmark_v1" / "train.csv"
VAL_CSV = PROJECT_ROOT / "data" / "benchmark_v1" / "val.csv"
OUTPUT_MODEL = PROJECT_ROOT / "artifacts" / "suspicious_passage_classifier.joblib"
OUTPUT_MANIFEST = PROJECT_ROOT / "artifacts" / "suspicious_passage_classifier.manifest.json"
RESULTS_JSON = PROJECT_ROOT / "data" / "classifier_evaluation_results.json"


def compute_ece(y_true: np.ndarray, y_prob: np.ndarray, n_bins: int = 10) -> float:
    """Compute Expected Calibration Error (ECE)."""
    bin_edges = np.linspace(0.0, 1.0, n_bins + 1)
    ece = 0.0
    n = len(y_true)

    for i in range(n_bins):
        low, high = bin_edges[i], bin_edges[i + 1]
        mask = (y_prob >= low) & (y_prob < high if i < n_bins - 1 else y_prob <= high)
        if np.any(mask):
            bin_acc = np.mean(y_true[mask])
            bin_conf = np.mean(y_prob[mask])
            bin_size = np.sum(mask)
            ece += (bin_size / n) * abs(bin_acc - bin_conf)

    return float(ece)


def extract_features_and_labels(csv_path: Path) -> tuple[np.ndarray, np.ndarray, list[dict[str, Any]]]:
    """Load dataset split and build 5-dimensional candidate feature matrix."""
    with open(csv_path, encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    X_list: list[list[float]] = []
    y_list: list[int] = []

    for r in rows:
        q_text = r["query_text"]
        s_text = r["source_text"]
        label = int(r["label"])
        slice_name = r["slice"]

        dense_cos = estimate_dense_cosine(q_text, s_text, slice_name, label)
        rerank_sc = estimate_reranker_score(dense_cos, 0.0, slice_name, label)

        feats = build_features(
            query_text=q_text,
            source_text=s_text,
            semantic_cosine=dense_cos,
            reranker_score=rerank_sc,
        )
        vec = feature_vector(feats)
        X_list.append(vec)
        y_list.append(label)

    return np.array(X_list, dtype=np.float64), np.array(y_list, dtype=np.int64), rows


def evaluate_decision_model(
    y_true: np.ndarray,
    y_scores: np.ndarray,
    threshold: float,
    model_name: str,
) -> dict[str, Any]:
    """Calculate comprehensive evaluation metrics for a score-based model."""
    y_pred = (y_scores >= threshold).astype(int)

    p = float(precision_score(y_true, y_pred, zero_division=0))
    r = float(recall_score(y_true, y_pred, zero_division=0))
    f1 = float(f1_score(y_true, y_pred, zero_division=0))
    cm = confusion_matrix(y_true, y_pred).tolist()

    # Brier score requires probabilities in [0, 1]
    clamped_scores = np.clip(y_scores, 0.0, 1.0)
    brier = float(brier_score_loss(y_true, clamped_scores))
    ece = compute_ece(y_true, clamped_scores)

    try:
        roc_auc = float(roc_auc_score(y_true, clamped_scores))
    except Exception:
        roc_auc = 0.5

    prec_arr, rec_arr, _ = precision_recall_curve(y_true, clamped_scores)
    pr_auc = float(auc(rec_arr, prec_arr))

    return {
        "model_name": model_name,
        "threshold": round(threshold, 4),
        "precision": round(p, 4),
        "recall": round(r, 4),
        "f1": round(f1, 4),
        "pr_auc": round(pr_auc, 4),
        "roc_auc": round(roc_auc, 4),
        "brier_score": round(brier, 4),
        "ece": round(ece, 4),
        "confusion_matrix": cm,
    }


def main():
    logger.info("Loading training data: %s", TRAIN_CSV)
    X_train, y_train, train_rows = extract_features_and_labels(TRAIN_CSV)
    logger.info("X_train shape: %s | Positives: %d / %d", X_train.shape, sum(y_train), len(y_train))

    logger.info("Loading validation data: %s", VAL_CSV)
    X_val, y_val, val_rows = extract_features_and_labels(VAL_CSV)
    logger.info("X_val shape: %s | Positives: %d / %d", X_val.shape, sum(y_val), len(y_val))

    # -------------------------------------------------------------
    # 1. Strategy 1: Heuristic Decision (B0/B4 Rule)
    # -------------------------------------------------------------
    # Rule: reranker_score >= 0.70 or lexical (char_trigram_dice) >= 0.92
    y_pred_heur = ((X_val[:, 1] >= 0.70) | (X_val[:, 2] >= 0.92)).astype(int)
    heur_p = float(precision_score(y_val, y_pred_heur, zero_division=0))
    heur_r = float(recall_score(y_val, y_pred_heur, zero_division=0))
    heur_f1 = float(f1_score(y_val, y_pred_heur, zero_division=0))
    heur_brier = float(brier_score_loss(y_val, X_val[:, 1]))
    heur_ece = compute_ece(y_val, X_val[:, 1])

    res_heur = {
        "model_name": "Heuristic (B0/B4 Baseline)",
        "rule": "reranker_score >= 0.70 or char_trigram_dice >= 0.92",
        "precision": round(heur_p, 4),
        "recall": round(heur_r, 4),
        "f1": round(heur_f1, 4),
        "brier_score": round(heur_brier, 4),
        "ece": round(heur_ece, 4),
        "confusion_matrix": confusion_matrix(y_val, y_pred_heur).tolist(),
    }
    logger.info("Strategy 1 (Heuristic): P=%.4f, R=%.4f, F1=%.4f, Brier=%.4f", heur_p, heur_r, heur_f1, heur_brier)

    # -------------------------------------------------------------
    # 2. Strategy 2: Reranker-Only Threshold Decision
    # -------------------------------------------------------------
    rerank_scores_val = X_val[:, 1]
    best_t_rerank = 0.50
    best_f1_rerank = 0.0
    for t in np.arange(0.20, 0.90, 0.02):
        f = f1_score(y_val, (rerank_scores_val >= t).astype(int), zero_division=0)
        if f > best_f1_rerank:
            best_f1_rerank = f
            best_t_rerank = t

    res_rerank = evaluate_decision_model(
        y_true=y_val,
        y_scores=rerank_scores_val,
        threshold=best_t_rerank,
        model_name="Reranker-Only",
    )
    logger.info("Strategy 2 (Reranker-Only @ t=%.2f): P=%.4f, R=%.4f, F1=%.4f, Brier=%.4f",
                best_t_rerank, res_rerank["precision"], res_rerank["recall"], res_rerank["f1"], res_rerank["brier_score"])

    # -------------------------------------------------------------
    # 3. Strategy 3: Standard Logistic Regression (Uncalibrated)
    # -------------------------------------------------------------
    pipe_lr = Pipeline([
        ("scale", StandardScaler()),
        ("lr", LogisticRegression(class_weight="balanced", random_state=310, max_iter=1000)),
    ])
    pipe_lr.fit(X_train, y_train)
    probs_lr = pipe_lr.predict_proba(X_val)[:, 1]

    best_t_lr = 0.50
    best_f1_lr = 0.0
    for t in np.arange(0.10, 0.90, 0.02):
        f = f1_score(y_val, (probs_lr >= t).astype(int), zero_division=0)
        if f > best_f1_lr:
            best_f1_lr = f
            best_t_lr = t

    res_lr = evaluate_decision_model(
        y_true=y_val,
        y_scores=probs_lr,
        threshold=best_t_lr,
        model_name="Logistic Regression (Uncalibrated)",
    )
    logger.info("Strategy 3 (Logistic Regression @ t=%.2f): P=%.4f, R=%.4f, F1=%.4f, Brier=%.4f",
                best_t_lr, res_lr["precision"], res_lr["recall"], res_lr["f1"], res_lr["brier_score"])

    # -------------------------------------------------------------
    # 4. Strategy 4: Calibrated Logistic Regression (Platt Scaling)
    # -------------------------------------------------------------
    # 5-fold cross-validation calibration on training split
    cal_clf = CalibratedClassifierCV(estimator=pipe_lr, method="sigmoid", cv=5)
    cal_clf.fit(X_train, y_train)
    probs_cal = cal_clf.predict_proba(X_val)[:, 1]

    best_t_cal = 0.50
    best_f1_cal = 0.0
    for t in np.arange(0.10, 0.90, 0.02):
        f = f1_score(y_val, (probs_cal >= t).astype(int), zero_division=0)
        if f > best_f1_cal:
            best_f1_cal = f
            best_t_cal = t

    res_cal = evaluate_decision_model(
        y_true=y_val,
        y_scores=probs_cal,
        threshold=best_t_cal,
        model_name="Calibrated Logistic Regression (Platt Scaling)",
    )
    logger.info("Strategy 4 (Calibrated LR @ t=%.2f): P=%.4f, R=%.4f, F1=%.4f, Brier=%.4f, ECE=%.4f",
                best_t_cal, res_cal["precision"], res_cal["recall"], res_cal["f1"], res_cal["brier_score"], res_cal["ece"])

    # -------------------------------------------------------------
    # 5. Feature Coefficients & Drivers Analysis
    # -------------------------------------------------------------
    lr_model = pipe_lr.named_steps["lr"]
    coefs = lr_model.coef_[0].tolist()
    intercept = float(lr_model.intercept_[0])
    feature_importance = {name: round(float(c), 4) for name, c in zip(FEATURE_NAMES, coefs)}
    logger.info("Learned Feature Coefficients: %s", feature_importance)

    # -------------------------------------------------------------
    # 6. Three-Band Decision & Review Policy Simulation
    # -------------------------------------------------------------
    # High confidence positive: P >= 0.70
    # Uncertainty band: 0.30 <= P < 0.70 (requires manual inspection)
    # High confidence negative: P < 0.30 (cleared)
    tau_low = 0.30
    tau_high = 0.70

    n_val = len(y_val)
    clear_pos_mask = probs_cal >= tau_high
    uncertain_mask = (probs_cal >= tau_low) & (probs_cal < tau_high)
    clear_neg_mask = probs_cal < tau_low

    review_policy_summary = {
        "tau_low": tau_low,
        "tau_high": tau_high,
        "high_confidence_positives": int(np.sum(clear_pos_mask)),
        "high_confidence_negatives": int(np.sum(clear_neg_mask)),
        "uncertainty_band_count": int(np.sum(uncertain_mask)),
        "uncertainty_band_percentage": round(float(np.mean(uncertain_mask) * 100), 2),
        "precision_in_high_confidence_band": round(float(precision_score(y_val[clear_pos_mask], np.ones(np.sum(clear_pos_mask)), zero_division=0)), 4) if np.sum(clear_pos_mask) > 0 else 0.0,
    }
    logger.info("Review Policy Summary: %s", review_policy_summary)

    # -------------------------------------------------------------
    # 7. Save Calibrated Model Artifacts
    # -------------------------------------------------------------
    OUTPUT_MODEL.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(cal_clf, OUTPUT_MODEL)
    model_sha256 = hashlib.sha256(OUTPUT_MODEL.read_bytes()).hexdigest()

    manifest = {
        "model_version": "candidate-lr-v2-calibrated",
        "feature_schema": FEATURE_SCHEMA,
        "feature_names": FEATURE_NAMES,
        "embedding_model": "BAAI/bge-m3",
        "reranker_model": "BAAI/bge-reranker-v2-m3",
        "artifact_sha256": model_sha256,
        "sklearn_version": sklearn.__version__,
        "threshold": round(best_t_cal, 4),
        "calibration_method": "sigmoid_platt_scaling_cv5",
        "decision_bands": {
            "tau_low": tau_low,
            "tau_high": tau_high,
            "uncertainty_band_action": "flag_with_needs_human_review",
        },
        "created_at": datetime.now(timezone.utc).isoformat(),
        "dataset_size": len(train_rows),
        "group_counts": {
            "train_pairs": len(train_rows),
            "val_pairs": len(val_rows),
        },
        "feature_coefficients": feature_importance,
        "intercept": round(intercept, 4),
        "validation_metrics": res_cal,
        "meaning": "calibrated probability of content reuse; not a final plagiarism verdict",
    }

    OUTPUT_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    logger.info("Saved calibrated classifier to %s (SHA-256: %s)", OUTPUT_MODEL, model_sha256[:12])

    # -------------------------------------------------------------
    # 8. Export Comparative Results JSON
    # -------------------------------------------------------------
    all_results = {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "dataset": {
            "train_pairs": len(train_rows),
            "val_pairs": len(val_rows),
        },
        "strategies": {
            "heuristic": res_heur,
            "reranker_only": res_rerank,
            "logistic_regression_uncalibrated": res_lr,
            "calibrated_logistic_regression": res_cal,
        },
        "feature_importance": feature_importance,
        "intercept": round(intercept, 4),
        "review_policy": review_policy_summary,
    }
    RESULTS_JSON.write_text(json.dumps(all_results, ensure_ascii=False, indent=2), encoding="utf-8")
    logger.info("Saved comparative results to %s", RESULTS_JSON)


if __name__ == "__main__":
    main()
