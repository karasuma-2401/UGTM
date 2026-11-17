"""CLI Benchmark Evaluation Engine for Multi-Tier Plagiarism AI (CP-03.4).

Executes reproducible evaluation on benchmark datasets (default: data/benchmark_v1/val.csv).
Outputs structured metrics reports adhering to Metrics Report Schema v1.
Strictly blocks unauthorized evaluation on the locked test split.

Usage:
    python scripts/evaluate_benchmark.py --split val
    python scripts/evaluate_benchmark.py --predictions data/benchmark_v1/sample_predictions.json --output reports/val_metrics.json
"""

from __future__ import annotations

import argparse
import csv
import json
import logging
import sys
from pathlib import Path
from typing import Any

# Configure project path and UTF-8 output on Windows
PROJECT_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT_ROOT))
sys.stdout.reconfigure(encoding="utf-8")

from app.evaluator import (
    PairPrediction,
    RetrievalSample,
    SpanSample,
    SplitLockError,
    check_split_access_permission,
    compute_cluster_bootstrap_ci,
    compute_false_alarms_per_document,
    diagnose_stage_failure,
    evaluate_multi_slice_predictions,
    evaluate_pair_classification,
    evaluate_retrieval,
    evaluate_spans,
)

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

BENCHMARK_DIR = PROJECT_ROOT / "data" / "benchmark_v1"
CONFIG_PATH = PROJECT_ROOT / "configs" / "evaluation_config.json"


def load_eval_config(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise FileNotFoundError(f"Evaluation config {path} not found")
    return json.loads(path.read_text(encoding="utf-8"))


def load_split_pairs(split_name: str) -> list[dict[str, str]]:
    csv_file = BENCHMARK_DIR / f"{split_name}.csv"
    if not csv_file.is_file():
        raise FileNotFoundError(f"Split file {csv_file} not found")
    with csv_file.open("r", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def run_benchmark_evaluation(
    split_name: str = "val",
    predictions_path: Path | None = None,
    config_path: Path = CONFIG_PATH,
    output_path: Path | None = None,
    unseal_authorized: bool = False,
) -> dict[str, Any]:
    """Execute evaluation pipeline and compute metrics across all stages."""
    # 1. Enforce Test Split Lock Policy
    check_split_access_permission(split_name, unseal_authorized=unseal_authorized)
    logger.info("Evaluating on split: %s (Unseal Authorized: %s)", split_name, unseal_authorized)

    config = load_eval_config(config_path)
    ground_truth_rows = load_split_pairs(split_name)
    logger.info("Loaded %d ground-truth pairs from %s.csv", len(ground_truth_rows), split_name)

    # 2. Load or Build Predictions
    # If a precomputed predictions file is provided, load it;
    # otherwise, generate an initial baseline pass (or synthetic identity pass for testing).
    predictions_map: dict[str, dict[str, Any]] = {}
    if predictions_path and predictions_path.is_file():
        logger.info("Loading precomputed predictions from %s", predictions_path)
        raw_preds = json.loads(predictions_path.read_text(encoding="utf-8"))
        for item in raw_preds:
            predictions_map[item["pair_id"]] = item
    else:
        logger.info("No external predictions supplied. Evaluating in oracle ground-truth mode.")
        for row in ground_truth_rows:
            pid = row["pair_id"]
            label_val = int(row["label"])
            # For demonstration / reference, ground-truth score is 1.0 for positive, 0.0 for negative
            predictions_map[pid] = {
                "pair_id": pid,
                "y_score": float(label_val),
                "retrieved_candidates": [row["source_doc_id"]] if label_val == 1 else [],
                "predicted_span": (0, len(row["query_text"])) if label_val == 1 else None,
            }

    # 3. Construct Evaluation Datasets
    pair_preds: list[PairPrediction] = []
    retrieval_samples: list[RetrievalSample] = []
    span_samples: list[SpanSample] = []
    stage_failures: dict[str, int] = {
        "RETRIEVAL_MISS": 0,
        "ALIGNMENT_BOUNDARY_ERROR": 0,
        "CLASSIFIER_FALSE_POSITIVE": 0,
        "CLASSIFIER_FALSE_NEGATIVE": 0,
        "CORRECT": 0,
    }

    decision_threshold = config["classification"]["decision_threshold"]
    iou_thresh = config["alignment"]["iou_match_threshold"]

    for row in ground_truth_rows:
        pid = row["pair_id"]
        pred_item = predictions_map.get(pid, {"y_score": 0.0, "retrieved_candidates": [], "predicted_span": None})
        y_score = float(pred_item.get("y_score", 0.0))
        y_true = int(row["label"])

        pp = PairPrediction(
            pair_id=pid,
            query_doc_id=row["query_doc_id"],
            query_family_id=row["query_family_id"],
            source_doc_id=row["source_doc_id"],
            source_family_id=row["source_family_id"],
            y_true=y_true,
            y_score=y_score,
            slice=row["slice"],
        )
        pair_preds.append(pp)

        # Retrieval sample
        relevant_srcs = [row["source_doc_id"]] if y_true == 1 else []
        retrieved_cands = pred_item.get("retrieved_candidates", [])
        retrieval_samples.append(RetrievalSample(
            query_id=row["query_doc_id"],
            relevant_sources=relevant_srcs,
            retrieved_sources=retrieved_cands,
            slice=row["slice"],
            document_family=row["query_family_id"],
        ))

        # Span sample
        gt_span = (0, len(row["query_text"])) if y_true == 1 else (0, 0)
        pred_span = pred_item.get("predicted_span") or (0, 0)
        if y_true == 1:
            span_samples.append(SpanSample(
                sample_id=pid,
                ground_truth_span=gt_span,
                predicted_span=pred_span,
                document_family=row["query_family_id"],
                slice=row["slice"],
            ))

        # Diagnose Stage Failure
        diag = diagnose_stage_failure(
            true_source=row["source_doc_id"] if y_true == 1 else None,
            retrieved_candidates=retrieved_cands,
            ground_truth_span=gt_span if y_true == 1 else None,
            predicted_span=pred_span if y_true == 1 else None,
            classifier_score=y_score,
            decision_threshold=decision_threshold,
            iou_threshold=iou_thresh,
        )
        stage_failures[diag] = stage_failures.get(diag, 0) + 1

    # 4. Compute Metrics
    retrieval_metrics = evaluate_retrieval(
        retrieval_samples,
        k_values=config["retrieval"]["k_values"],
        ndcg_k=config["retrieval"]["ndcg_k"],
    )

    span_metrics = evaluate_spans(
        span_samples,
        iou_match_threshold=iou_thresh,
    )

    multi_slice_results = evaluate_multi_slice_predictions(
        pair_preds,
        threshold=decision_threshold,
        slices=config.get("slices"),
    )

    false_alarm_stats = compute_false_alarms_per_document(
        pair_preds,
        decision_threshold=decision_threshold,
    )

    # 5. Cluster Bootstrap (95% CI) for primary metrics
    def f1_eval_fn(items):
        yt = [x.y_true for x in items]
        ys = [x.y_score for x in items]
        return evaluate_pair_classification(yt, ys, threshold=decision_threshold)["f1"]

    f1_point, f1_lower, f1_upper = compute_cluster_bootstrap_ci(
        pair_preds,
        eval_fn=f1_eval_fn,
        cluster_key="query_family_id",
        n_bootstrap=config["bootstrap"]["n_iterations"],
        confidence_level=config["bootstrap"]["confidence_level"],
        seed=config["random_seed"],
    )

    # 6. Build Final Structured Report
    report: dict[str, Any] = {
        "report_schema_version": "1.0.0",
        "evaluator_version": config["evaluator_version"],
        "split_evaluated": split_name,
        "total_pairs_evaluated": len(pair_preds),
        "stage_1_retrieval": retrieval_metrics,
        "stage_2_span_alignment": span_metrics,
        "stage_3_pair_classification": {
            **multi_slice_results["overall"],
            "bootstrap_ci_95": {
                "metric": "f1",
                "point_estimate": f1_point,
                "ci_lower": f1_lower,
                "ci_upper": f1_upper,
            },
        },
        "stage_4_system_and_false_alarms": false_alarm_stats,
        "slice_breakdown": multi_slice_results["slices"],
        "error_isolation_taxonomy": stage_failures,
    }

    if output_path:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        logger.info("Saved evaluation report to %s", output_path)

    return report


def print_report_summary_table(report: dict[str, Any]) -> None:
    """Print readable Markdown summary table of evaluation results to console."""
    print("\n" + "=" * 70)
    print(f"UGTM Plagiarism AI — Evaluation Summary [{report['split_evaluated'].upper()} SPLIT]")
    print("=" * 70)
    r1 = report["stage_1_retrieval"]
    r2 = report["stage_2_span_alignment"]
    r3 = report["stage_3_pair_classification"]
    r4 = report["stage_4_system_and_false_alarms"]
    err = report["error_isolation_taxonomy"]

    print("\n[STAGE 1: SOURCE RETRIEVAL]")
    print(f"  - Recall@10:  {r1.get('recall@10', 0.0):.4f}")
    print(f"  - Recall@50:  {r1.get('recall@50', 0.0):.4f}")
    print(f"  - Recall@100: {r1.get('recall@100', 0.0):.4f}")
    print(f"  - nDCG@10:    {r1.get('ndcg@10', 0.0):.4f}")

    print("\n[STAGE 2: SPAN ALIGNMENT]")
    print(f"  - Mean IoU:       {r2.get('mean_iou', 0.0):.4f}")
    print(f"  - Span Precision: {r2.get('mean_precision', 0.0):.4f}")
    print(f"  - Span Recall:    {r2.get('mean_recall', 0.0):.4f}")
    print(f"  - Span F1:        {r2.get('mean_f1', 0.0):.4f}")
    print(f"  - Match Rate:     {r2.get('match_rate', 0.0):.4f}")

    print("\n[STAGE 3: PAIRWISE CLASSIFICATION & CALIBRATION]")
    print(f"  - Precision: {r3.get('precision', 0.0):.4f}")
    print(f"  - Recall:    {r3.get('recall', 0.0):.4f}")
    print(f"  - F1 Score:  {r3.get('f1', 0.0):.4f} (95% CI: [{r3['bootstrap_ci_95']['ci_lower']:.4f}, {r3['bootstrap_ci_95']['ci_upper']:.4f}])")
    print(f"  - PR-AUC:    {r3.get('pr_auc', 0.0):.4f}")
    print(f"  - Accuracy:  {r3.get('accuracy', 0.0):.4f}")
    print(f"  - Brier:     {r3.get('brier_score', 0.0):.4f}")

    print("\n[STAGE 4: SYSTEM FALSE ALARMS]")
    print(f"  - False Alarms / Doc: {r4.get('false_alarms_per_doc', 0.0):.4f}")

    print("\n[STAGE ERROR TAXONOMY]")
    for cat, count in err.items():
        print(f"  - {cat:<26}: {count}")

    print("\n[PER-SLICE F1 BREAKDOWN]")
    slices = report.get("slice_breakdown", {})
    for sl_name, sl_metrics in slices.items():
        print(f"  - {sl_name:<20}: F1={sl_metrics.get('f1', 0.0):.4f} (TP={int(sl_metrics.get('tp', 0))}, FP={int(sl_metrics.get('fp', 0))}, FN={int(sl_metrics.get('fn', 0))})")
    print("=" * 70 + "\n")


def main():
    parser = argparse.ArgumentParser(description="Multi-tier Plagiarism AI Evaluator")
    parser.add_argument("--split", choices=["train", "val", "test"], default="val", help="Dataset split to evaluate (default: val)")
    parser.add_argument("--predictions", type=Path, default=None, help="Path to predictions JSON file")
    parser.add_argument("--config", type=Path, default=CONFIG_PATH, help="Path to evaluation config JSON")
    parser.add_argument("--output", type=Path, default=None, help="Path to write output report JSON")
    parser.add_argument("--unseal-test-for-final-report", action="store_true", help="Authorize single-pass evaluation on test split (CP-06.1 only)")

    args = parser.parse_args()

    try:
        report = run_benchmark_evaluation(
            split_name=args.split,
            predictions_path=args.predictions,
            config_path=args.config,
            output_path=args.output,
            unseal_authorized=args.unseal_test_for_final_report,
        )
        print_report_summary_table(report)
    except SplitLockError as e:
        logger.error("ACCESS DENIED: %s", e)
        sys.exit(2)
    except Exception as e:
        logger.exception("Evaluation failed: %s", e)
        sys.exit(1)


if __name__ == "__main__":
    main()

