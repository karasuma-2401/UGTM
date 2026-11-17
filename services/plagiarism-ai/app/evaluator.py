"""Multi-Tier Plagiarism Detection Evaluator (CP-03.4).

Provides independent, reproducible evaluation across all system stages:
1. Stage 1 (Retrieval): Source Recall@10, Recall@50, Recall@100, nDCG@10.
2. Stage 2 (Alignment): Span IoU, Precision, Recall, F1.
3. Stage 3 (Classification): Precision, Recall, F1, PR-AUC, Brier score, Confusion matrix.
4. Stage 4 (System & Reliability): False alarms per document, Latency breakdown (p50/p95).
5. Statistical Rigor: 7 evaluation slices breakdown, Cluster Bootstrap 95% Confidence Intervals.
6. Test Access Protocol: Rejects unsealed evaluation on locked test split prior to CP-06.1.
"""

from __future__ import annotations

import math
import random
from dataclasses import dataclass, field
from typing import Any, Callable

import numpy as np
from sklearn.metrics import precision_recall_curve, auc


@dataclass
class RetrievalSample:
    query_id: str
    relevant_sources: list[str]
    retrieved_sources: list[str]
    slice: str | None = None
    document_family: str | None = None


@dataclass
class SpanSample:
    sample_id: str
    ground_truth_span: tuple[int, int]
    predicted_span: tuple[int, int]
    document_family: str | None = None
    slice: str | None = None


@dataclass
class PairPrediction:
    pair_id: str
    query_doc_id: str
    query_family_id: str
    source_doc_id: str
    source_family_id: str
    y_true: int
    y_score: float
    slice: str


@dataclass
class LatencyRecord:
    query_id: str
    parse_ms: float = 0.0
    embed_ms: float = 0.0
    retrieval_ms: float = 0.0
    rerank_ms: float = 0.0
    align_ms: float = 0.0
    total_ms: float = 0.0
    gpu_time_ms: float = 0.0


# ---------------------------------------------------------------------------
# Test Access Protection Protocol (Rule 2 & CP-03.3 Lock)
# ---------------------------------------------------------------------------

class SplitLockError(PermissionError):
    """Raised when an unauthorized attempt is made to evaluate on the locked test split."""
    pass


TestSplitLockedError = SplitLockError
TestSplitLockedError.__test__ = False  # Prevent pytest from collecting as test class


def check_split_access_permission(split_name: str, unseal_authorized: bool = False) -> None:
    """Enforce test set access protocol: test split cannot be accessed without unseal authorization."""
    if split_name.strip().lower() == "test" and not unseal_authorized:
        raise TestSplitLockedError(
            "Evaluation on 'test' split is strictly LOCKED by FREEZE-BENCHMARK-SPLIT "
            "until checkpoint CP-06.1. Use split='val' or pass unseal_authorized=True "
            "during authorized final benchmark run."
        )


# ---------------------------------------------------------------------------
# Stage 1: Source Retrieval Evaluation
# ---------------------------------------------------------------------------

def compute_recall_at_k(relevant_sources: set[str], retrieved_ranked: list[str], k: int) -> float:
    """Compute Recall@K for a single query against a set of relevant source documents."""
    if not relevant_sources:
        return 0.0
    top_k_retrieved = set(retrieved_ranked[:k])
    hits = len(relevant_sources.intersection(top_k_retrieved))
    return float(hits / len(relevant_sources))


def compute_dcg_at_k(relevant_sources: set[str], retrieved_ranked: list[str], k: int) -> float:
    """Compute Discounted Cumulative Gain (DCG@K) using binary relevance."""
    dcg = 0.0
    for idx, doc in enumerate(retrieved_ranked[:k]):
        if doc in relevant_sources:
            # Rank position is 1-based: idx + 1. Discount = log2(rank + 1)
            rank = idx + 1
            dcg += 1.0 / math.log2(rank + 1)
    return float(dcg)


def compute_idcg_at_k(num_relevant: int, k: int) -> float:
    """Compute Ideal Discounted Cumulative Gain (IDCG@K)."""
    idcg = 0.0
    max_hits = min(num_relevant, k)
    for idx in range(max_hits):
        rank = idx + 1
        idcg += 1.0 / math.log2(rank + 1)
    return float(idcg)


def compute_ndcg_at_k(relevant_sources: set[str], retrieved_ranked: list[str], k: int) -> float:
    """Compute Normalized Discounted Cumulative Gain (nDCG@K)."""
    if not relevant_sources or k <= 0:
        return 0.0
    idcg = compute_idcg_at_k(len(relevant_sources), k)
    if idcg <= 0.0:
        return 0.0
    dcg = compute_dcg_at_k(relevant_sources, retrieved_ranked, k)
    return float(dcg / idcg)


def evaluate_retrieval(
    samples: list[RetrievalSample],
    k_values: list[int] = (10, 50, 100),
    ndcg_k: int = 10,
) -> dict[str, float]:
    """Calculate macro-averaged Recall@K and nDCG across all valid queries with >= 1 relevant source."""
    valid_samples = [s for s in samples if len(s.relevant_sources) > 0]
    if not valid_samples:
        metrics = {f"recall@{k}": 0.0 for k in k_values}
        metrics[f"ndcg@{ndcg_k}"] = 0.0
        metrics["evaluated_queries"] = 0
        return metrics

    recalls: dict[int, list[float]] = {k: [] for k in k_values}
    ndcgs: list[float] = []

    for s in valid_samples:
        rel_set = set(s.relevant_sources)
        for k in k_values:
            recalls[k].append(compute_recall_at_k(rel_set, s.retrieved_sources, k))
        ndcgs.append(compute_ndcg_at_k(rel_set, s.retrieved_sources, ndcg_k))

    result: dict[str, float] = {
        f"recall@{k}": float(np.mean(recalls[k])) for k in k_values
    }
    result[f"ndcg@{ndcg_k}"] = float(np.mean(ndcgs))
    result["evaluated_queries"] = float(len(valid_samples))
    return result


# ---------------------------------------------------------------------------
# Stage 2: Span Alignment Evaluation
# ---------------------------------------------------------------------------

def compute_span_iou(gt_span: tuple[int, int], pred_span: tuple[int, int]) -> float:
    """Compute Intersection over Union (IoU) for character offsets [start, end]."""
    g_start, g_end = gt_span
    p_start, p_end = pred_span

    if g_start >= g_end and p_start >= p_end:
        return 1.0  # Both empty
    if g_start >= g_end or p_start >= p_end:
        return 0.0

    inter_start = max(g_start, p_start)
    inter_end = min(g_end, p_end)
    intersection = max(0, inter_end - inter_start)

    g_len = g_end - g_start
    p_len = p_end - p_start
    union = g_len + p_len - intersection

    return float(intersection / union) if union > 0 else 0.0


def compute_span_precision_recall_f1(
    gt_span: tuple[int, int],
    pred_span: tuple[int, int],
) -> tuple[float, float, float]:
    """Compute character-level precision, recall, and F1 between ground-truth and predicted spans."""
    g_start, g_end = gt_span
    p_start, p_end = pred_span

    g_len = max(0, g_end - g_start)
    p_len = max(0, p_end - p_start)

    inter_start = max(g_start, p_start)
    inter_end = min(g_end, p_end)
    intersection = max(0, inter_end - inter_start)

    precision = float(intersection / p_len) if p_len > 0 else 0.0
    recall = float(intersection / g_len) if g_len > 0 else 0.0

    if precision + recall > 0:
        f1 = float(2 * precision * recall / (precision + recall))
    else:
        f1 = 0.0

    return precision, recall, f1


def evaluate_spans(
    samples: list[SpanSample],
    iou_match_threshold: float = 0.3,
) -> dict[str, float]:
    """Evaluate macro-averaged Span IoU, Precision, Recall, and Match Rate."""
    if not samples:
        return {
            "mean_iou": 0.0,
            "mean_precision": 0.0,
            "mean_recall": 0.0,
            "mean_f1": 0.0,
            "match_rate": 0.0,
            "evaluated_spans": 0,
        }

    ious: list[float] = []
    precisions: list[float] = []
    recalls: list[float] = []
    f1s: list[float] = []
    matches: list[float] = []

    for s in samples:
        iou = compute_span_iou(s.ground_truth_span, s.predicted_span)
        p, r, f1 = compute_span_precision_recall_f1(s.ground_truth_span, s.predicted_span)
        ious.append(iou)
        precisions.append(p)
        recalls.append(r)
        f1s.append(f1)
        matches.append(1.0 if iou >= iou_match_threshold else 0.0)

    return {
        "mean_iou": float(np.mean(ious)),
        "mean_precision": float(np.mean(precisions)),
        "mean_recall": float(np.mean(recalls)),
        "mean_f1": float(np.mean(f1s)),
        "match_rate": float(np.mean(matches)),
        "evaluated_spans": float(len(samples)),
    }


# ---------------------------------------------------------------------------
# Stage 3: Pair Classification & Calibration Evaluation
# ---------------------------------------------------------------------------

def compute_confusion_matrix(
    y_true: list[int],
    y_score: list[float],
    threshold: float = 0.5,
) -> dict[str, int]:
    """Compute exact TP, FP, FN, TN at a given decision threshold."""
    tp = fp = fn = tn = 0
    for yt, ys in zip(y_true, y_score):
        pred_label = 1 if ys >= threshold else 0
        if yt == 1 and pred_label == 1:
            tp += 1
        elif yt == 0 and pred_label == 1:
            fp += 1
        elif yt == 1 and pred_label == 0:
            fn += 1
        else:
            tn += 1
    return {"tp": tp, "fp": fp, "fn": fn, "tn": tn}


def compute_brier_score(y_true: list[int], y_score: list[float]) -> float:
    """Compute Brier Score (Mean Squared Error between probability and true label)."""
    if not y_true:
        return 0.0
    sq_errs = [(ys - yt) ** 2 for yt, ys in zip(y_true, y_score)]
    return float(np.mean(sq_errs))


def compute_pr_auc(y_true: list[int], y_score: list[float]) -> float:
    """Compute Area Under the Precision-Recall Curve (PR-AUC / Average Precision)."""
    if len(set(y_true)) < 2:
        return 0.0
    precisions, recalls, _ = precision_recall_curve(y_true, y_score)
    return float(auc(recalls, precisions))


def evaluate_pair_classification(
    y_true: list[int],
    y_score: list[float],
    threshold: float = 0.5,
) -> dict[str, float]:
    """Comprehensive evaluation of pairwise classification metrics."""
    if not y_true:
        return {
            "precision": 0.0,
            "recall": 0.0,
            "f1": 0.0,
            "accuracy": 0.0,
            "pr_auc": 0.0,
            "brier_score": 0.0,
            "tp": 0,
            "fp": 0,
            "fn": 0,
            "tn": 0,
        }

    cm = compute_confusion_matrix(y_true, y_score, threshold)
    tp, fp, fn, tn = cm["tp"], cm["fp"], cm["fn"], cm["tn"]

    precision = float(tp / (tp + fp)) if (tp + fp) > 0 else 0.0
    recall = float(tp / (tp + fn)) if (tp + fn) > 0 else 0.0
    f1 = float(2 * precision * recall / (precision + recall)) if (precision + recall) > 0 else 0.0
    total = tp + fp + fn + tn
    accuracy = float((tp + tn) / total) if total > 0 else 0.0
    brier = compute_brier_score(y_true, y_score)
    pr_auc_val = compute_pr_auc(y_true, y_score)

    return {
        "precision": precision,
        "recall": recall,
        "f1": f1,
        "accuracy": accuracy,
        "pr_auc": pr_auc_val,
        "brier_score": brier,
        "tp": float(tp),
        "fp": float(fp),
        "fn": float(fn),
        "tn": float(tn),
    }


# ---------------------------------------------------------------------------
# Error Isolation & Failure Taxonomy (Tách Tầng Lỗi)
# ---------------------------------------------------------------------------

def diagnose_stage_failure(
    true_source: str | None,
    retrieved_candidates: list[str],
    ground_truth_span: tuple[int, int] | None,
    predicted_span: tuple[int, int] | None,
    classifier_score: float | None,
    decision_threshold: float = 0.5,
    iou_threshold: float = 0.3,
) -> str:
    """Isolate which stage in the pipeline caused the failure.

    Returns:
    - 'RETRIEVAL_MISS': The true source was never retrieved into candidates.
    - 'ALIGNMENT_BOUNDARY_ERROR': The true source was retrieved, but span alignment failed.
    - 'CLASSIFIER_FALSE_POSITIVE': Non-matching pair scored >= threshold (false alarm).
    - 'CLASSIFIER_FALSE_NEGATIVE': Matching pair retrieved and aligned, but classifier rejected it.
    - 'CORRECT': All stages succeeded according to ground truth.
    """
    is_positive = (true_source is not None)

    if is_positive:
        # Check Stage 1: Retrieval
        if true_source not in retrieved_candidates:
            return "RETRIEVAL_MISS"

        # Check Stage 2: Alignment
        if ground_truth_span is not None and predicted_span is not None:
            iou = compute_span_iou(ground_truth_span, predicted_span)
            if iou < iou_threshold:
                return "ALIGNMENT_BOUNDARY_ERROR"

        # Check Stage 3: Classifier Decision
        if classifier_score is not None and classifier_score < decision_threshold:
            return "CLASSIFIER_FALSE_NEGATIVE"

        return "CORRECT"

    else:
        # Ground truth is negative (no reuse)
        if classifier_score is not None and classifier_score >= decision_threshold:
            return "CLASSIFIER_FALSE_POSITIVE"
        return "CORRECT"


# ---------------------------------------------------------------------------
# Statistical Rigor: Cluster Bootstrap Confidence Intervals
# ---------------------------------------------------------------------------

def compute_cluster_bootstrap_ci(
    items: list[dict[str, Any]],
    eval_fn: Callable[[list[dict[str, Any]]], float],
    cluster_key: str = "query_family_id",
    n_bootstrap: int = 1000,
    confidence_level: float = 0.95,
    seed: int = 42,
) -> tuple[float, float, float]:
    """Compute cluster bootstrap confidence intervals by resampling document families.

    Returns: (point_estimate, ci_lower, ci_upper)
    """
    if not items:
        return 0.0, 0.0, 0.0

    # Group items by cluster
    clusters: dict[str, list[Any]] = {}
    for item in items:
        if isinstance(item, dict):
            ck = str(item.get(cluster_key, "default"))
        else:
            ck = str(getattr(item, cluster_key, "default"))
        clusters.setdefault(ck, []).append(item)

    unique_clusters = sorted(clusters.keys())
    point_est = eval_fn(items)

    if len(unique_clusters) < 2 or n_bootstrap <= 1:
        return point_est, point_est, point_est

    rng = random.Random(seed)
    n_c = len(unique_clusters)
    bootstrap_estimates: list[float] = []

    for _ in range(n_bootstrap):
        sampled_clusters = rng.choices(unique_clusters, k=n_c)
        sample_items: list[dict[str, Any]] = []
        for sc in sampled_clusters:
            sample_items.extend(clusters[sc])
        try:
            val = eval_fn(sample_items)
            bootstrap_estimates.append(val)
        except Exception:
            continue

    if not bootstrap_estimates:
        return point_est, point_est, point_est

    alpha = 1.0 - confidence_level
    lower_pct = (alpha / 2.0) * 100.0
    upper_pct = (1.0 - alpha / 2.0) * 100.0

    ci_lower = float(np.percentile(bootstrap_estimates, lower_pct))
    ci_upper = float(np.percentile(bootstrap_estimates, upper_pct))

    return point_est, ci_lower, ci_upper


# ---------------------------------------------------------------------------
# Stage 4: Latency & False Alarm Aggregators
# ---------------------------------------------------------------------------

def evaluate_latencies(records: list[LatencyRecord]) -> dict[str, float]:
    """Compute distribution statistics for stage and total latencies."""
    if not records:
        return {}

    totals = [r.total_ms for r in records]
    parses = [r.parse_ms for r in records]
    embeds = [r.embed_ms for r in records]
    retrievals = [r.retrieval_ms for r in records]
    reranks = [r.rerank_ms for r in records]
    aligns = [r.align_ms for r in records]
    gpus = [r.gpu_time_ms for r in records]

    return {
        "p50_total_ms": float(np.percentile(totals, 50)),
        "p95_total_ms": float(np.percentile(totals, 95)),
        "p99_total_ms": float(np.percentile(totals, 99)),
        "mean_total_ms": float(np.mean(totals)),
        "mean_parse_ms": float(np.mean(parses)),
        "mean_embed_ms": float(np.mean(embeds)),
        "mean_retrieval_ms": float(np.mean(retrievals)),
        "mean_rerank_ms": float(np.mean(reranks)),
        "mean_align_ms": float(np.mean(aligns)),
        "mean_gpu_ms": float(np.mean(gpus)),
    }


def compute_false_alarms_per_document(
    predictions: list[PairPrediction],
    decision_threshold: float = 0.5,
) -> dict[str, float]:
    """Compute the average false positive rate per unique query document on negative pairs."""
    negative_preds = [p for p in predictions if p.y_true == 0]
    if not negative_preds:
        return {"false_alarms_per_doc": 0.0, "total_negative_queries": 0.0}

    # Count false alarms per query document
    doc_false_alarms: dict[str, int] = {}
    for p in negative_preds:
        q_id = p.query_doc_id
        doc_false_alarms.setdefault(q_id, 0)
        if p.y_score >= decision_threshold:
            doc_false_alarms[q_id] += 1

    counts = list(doc_false_alarms.values())
    return {
        "false_alarms_per_doc": float(np.mean(counts)) if counts else 0.0,
        "max_false_alarms_doc": float(max(counts)) if counts else 0.0,
        "total_negative_queries": float(len(doc_false_alarms)),
    }


# ---------------------------------------------------------------------------
# Comprehensive Multi-Slice Evaluator
# ---------------------------------------------------------------------------

def evaluate_multi_slice_predictions(
    predictions: list[PairPrediction],
    threshold: float = 0.5,
    slices: list[str] | None = None,
) -> dict[str, Any]:
    """Evaluate pairwise classification broken down by slice and overall."""
    if slices is None:
        slices = sorted({p.slice for p in predictions})

    overall_true = [p.y_true for p in predictions]
    overall_score = [p.y_score for p in predictions]
    overall_metrics = evaluate_pair_classification(overall_true, overall_score, threshold)

    slice_metrics: dict[str, dict[str, float]] = {}
    for sl in slices:
        sl_preds = [p for p in predictions if p.slice == sl]
        if sl_preds:
            y_t = [p.y_true for p in sl_preds]
            y_s = [p.y_score for p in sl_preds]
            slice_metrics[sl] = evaluate_pair_classification(y_t, y_s, threshold)

    return {
        "overall": overall_metrics,
        "slices": slice_metrics,
        "total_samples": len(predictions),
    }
