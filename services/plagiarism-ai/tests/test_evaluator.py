"""Unit and Contract Tests for Multi-Tier Evaluator (CP-03.4).

Verifies:
1. Exact match with hand-calculated fixtures for Retrieval (Recall@K, nDCG@K).
2. Exact match with hand-calculated fixtures for Span Alignment (IoU, Precision, Recall, F1).
3. Exact match with hand-calculated fixtures for Pair Classification (TP, FP, FN, TN, Brier).
4. Stage failure diagnosis isolates retrieval error from alignment error and classifier error.
5. Cluster bootstrap confidence intervals (95% CI) around point estimates.
6. Enforcement of test split locking policy (TestSplitLockedError).
7. False alarm rate and multi-slice aggregation.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from app.evaluator import (
    RetrievalSample,
    SpanSample,
    PairPrediction,
    TestSplitLockedError,
    check_split_access_permission,
    compute_recall_at_k,
    compute_dcg_at_k,
    compute_idcg_at_k,
    compute_ndcg_at_k,
    evaluate_retrieval,
    compute_span_iou,
    compute_span_precision_recall_f1,
    evaluate_spans,
    compute_confusion_matrix,
    compute_brier_score,
    evaluate_pair_classification,
    diagnose_stage_failure,
    compute_cluster_bootstrap_ci,
    compute_false_alarms_per_document,
    evaluate_multi_slice_predictions,
)

FIXTURES_PATH = Path("tests/fixtures/evaluator_hand_fixtures.json")


@pytest.fixture(scope="module")
def hand_fixtures() -> dict:
    assert FIXTURES_PATH.is_file(), f"Fixture file {FIXTURES_PATH} missing"
    return json.loads(FIXTURES_PATH.read_text(encoding="utf-8"))


# ---------------------------------------------------------------------------
# 1. Hand-Calculated Retrieval Tests
# ---------------------------------------------------------------------------

def test_hand_calculated_retrieval_cases(hand_fixtures):
    cases = hand_fixtures["retrieval_cases"]

    # Case 1 (Single relevant doc at rank 2)
    c1 = cases[0]
    rel1 = set(c1["relevant_sources"])
    ret1 = c1["retrieved_ranked"]
    exp1 = c1["expected"]

    assert compute_recall_at_k(rel1, ret1, 1) == pytest.approx(exp1["recall_at_1"], abs=1e-5)
    assert compute_recall_at_k(rel1, ret1, 2) == pytest.approx(exp1["recall_at_2"], abs=1e-5)
    assert compute_recall_at_k(rel1, ret1, 3) == pytest.approx(exp1["recall_at_3"], abs=1e-5)
    assert compute_dcg_at_k(rel1, ret1, 2) == pytest.approx(exp1["dcg_at_2"], abs=1e-5)
    assert compute_idcg_at_k(len(rel1), 2) == pytest.approx(exp1["idcg_at_2"], abs=1e-5)
    assert compute_ndcg_at_k(rel1, ret1, 2) == pytest.approx(exp1["ndcg_at_2"], abs=1e-5)

    # Case 2 (Two relevant docs at ranks 1 and 3)
    c2 = cases[1]
    rel2 = set(c2["relevant_sources"])
    ret2 = c2["retrieved_ranked"]
    exp2 = c2["expected"]

    assert compute_recall_at_k(rel2, ret2, 1) == pytest.approx(exp2["recall_at_1"], abs=1e-5)
    assert compute_recall_at_k(rel2, ret2, 2) == pytest.approx(exp2["recall_at_2"], abs=1e-5)
    assert compute_recall_at_k(rel2, ret2, 3) == pytest.approx(exp2["recall_at_3"], abs=1e-5)
    assert compute_dcg_at_k(rel2, ret2, 3) == pytest.approx(exp2["dcg_at_3"], abs=1e-5)
    assert compute_idcg_at_k(len(rel2), 3) == pytest.approx(exp2["idcg_at_3"], abs=1e-5)
    assert compute_ndcg_at_k(rel2, ret2, 3) == pytest.approx(exp2["ndcg_at_3"], abs=1e-5)

    # Macro-averaged
    samples = [
        RetrievalSample(query_id=c["query_id"], relevant_sources=c["relevant_sources"], retrieved_sources=c["retrieved_ranked"])
        for c in cases
    ]
    macro_res = evaluate_retrieval(samples, k_values=[1, 2, 3], ndcg_k=3)
    macro_exp = hand_fixtures["macro_retrieval_expected"]

    assert macro_res["recall@1"] == pytest.approx(macro_exp["mean_recall_at_1"], abs=1e-5)
    assert macro_res["recall@2"] == pytest.approx(macro_exp["mean_recall_at_2"], abs=1e-5)
    assert macro_res["recall@3"] == pytest.approx(macro_exp["mean_recall_at_3"], abs=1e-5)
    assert macro_res["ndcg@3"] == pytest.approx(macro_exp["mean_ndcg_at_3"], abs=1e-5)


# ---------------------------------------------------------------------------
# 2. Hand-Calculated Span Alignment Tests
# ---------------------------------------------------------------------------

def test_hand_calculated_span_alignment(hand_fixtures):
    cases = hand_fixtures["span_alignment_cases"]
    for case in cases:
        gt = tuple(case["ground_truth"])
        pred = tuple(case["predicted"])
        exp = case["expected"]

        iou = compute_span_iou(gt, pred)
        p, r, f1 = compute_span_precision_recall_f1(gt, pred)

        assert iou == pytest.approx(exp["iou"], abs=1e-5), f"IoU mismatch in {case['name']}"
        assert p == pytest.approx(exp["precision"], abs=1e-5), f"Precision mismatch in {case['name']}"
        assert r == pytest.approx(exp["recall"], abs=1e-5), f"Recall mismatch in {case['name']}"
        assert f1 == pytest.approx(exp["f1"], abs=1e-5), f"F1 mismatch in {case['name']}"


# ---------------------------------------------------------------------------
# 3. Hand-Calculated Pair Classification Tests
# ---------------------------------------------------------------------------

def test_hand_calculated_pair_classification(hand_fixtures):
    data = hand_fixtures["pair_classification_cases"]
    thresh = data["threshold"]
    pairs = data["pairs"]
    exp = data["expected"]

    y_true = [p["y_true"] for p in pairs]
    y_score = [p["y_score"] for p in pairs]

    cm = compute_confusion_matrix(y_true, y_score, threshold=thresh)
    assert cm["tp"] == exp["tp"]
    assert cm["fp"] == exp["fp"]
    assert cm["fn"] == exp["fn"]
    assert cm["tn"] == exp["tn"]

    brier = compute_brier_score(y_true, y_score)
    assert brier == pytest.approx(exp["brier_score"], abs=1e-5)

    res = evaluate_pair_classification(y_true, y_score, threshold=thresh)
    assert res["precision"] == pytest.approx(exp["precision"], abs=1e-5)
    assert res["recall"] == pytest.approx(exp["recall"], abs=1e-5)
    assert res["f1"] == pytest.approx(exp["f1"], abs=1e-5)
    assert res["accuracy"] == pytest.approx(exp["accuracy"], abs=1e-5)


# ---------------------------------------------------------------------------
# 4. Error Isolation Tests (Tách tầng lỗi)
# ---------------------------------------------------------------------------

def test_stage_failure_diagnosis(hand_fixtures):
    cases = hand_fixtures["error_isolation_cases"]
    for c in cases:
        gt_span = tuple(c["ground_truth_span"]) if c.get("ground_truth_span") else None
        pred_span = tuple(c["predicted_span"]) if c.get("predicted_span") else None

        stage = diagnose_stage_failure(
            true_source=c["true_source"],
            retrieved_candidates=c["retrieved_candidates"],
            ground_truth_span=gt_span,
            predicted_span=pred_span,
            classifier_score=c["classifier_score"],
            decision_threshold=0.5,
            iou_threshold=0.3,
        )

        assert stage == c["category"], f"Failed to isolate error for {c['sample_id']}: expected {c['category']}, got {stage}"


# ---------------------------------------------------------------------------
# 5. Cluster Bootstrap Confidence Interval Tests
# ---------------------------------------------------------------------------

def test_cluster_bootstrap_confidence_interval():
    items = [
        {"val": 1.0, "query_family_id": "f1"},
        {"val": 0.8, "query_family_id": "f1"},
        {"val": 0.9, "query_family_id": "f2"},
        {"val": 0.7, "query_family_id": "f2"},
        {"val": 0.85, "query_family_id": "f3"},
        {"val": 0.95, "query_family_id": "f3"},
    ]

    def mean_fn(batch):
        return sum(x["val"] for x in batch) / len(batch)

    point, lower, upper = compute_cluster_bootstrap_ci(
        items,
        eval_fn=mean_fn,
        cluster_key="query_family_id",
        n_bootstrap=500,
        confidence_level=0.95,
        seed=42,
    )

    assert 0.7 <= lower <= point <= upper <= 1.0
    assert abs(point - 0.8666667) < 1e-4


# ---------------------------------------------------------------------------
# 6. Test Split Access Lock Policy Enforcement
# ---------------------------------------------------------------------------

def test_test_split_access_policy_lock():
    # Attempting to access test without unseal authorization must raise TestSplitLockedError
    with pytest.raises(TestSplitLockedError):
        check_split_access_permission("test", unseal_authorized=False)

    with pytest.raises(TestSplitLockedError):
        check_split_access_permission("TEST", unseal_authorized=False)

    # Allowed splits: val, train, dev
    check_split_access_permission("val", unseal_authorized=False)
    check_split_access_permission("train", unseal_authorized=False)

    # When officially authorized at CP-06.1, unseal passes cleanly
    check_split_access_permission("test", unseal_authorized=True)


# ---------------------------------------------------------------------------
# 7. False Alarm and Multi-Slice Tests
# ---------------------------------------------------------------------------

def test_false_alarms_and_slice_breakdown():
    preds = [
        PairPrediction("p1", "q1", "f1", "s1", "f1", y_true=1, y_score=0.9, slice="verbatim_copy"),
        PairPrediction("p2", "q1", "f1", "s2", "f2", y_true=0, y_score=0.8, slice="hard_negatives"),
        PairPrediction("p3", "q2", "f2", "s3", "f3", y_true=0, y_score=0.1, slice="out_of_corpus"),
        PairPrediction("p4", "q2", "f2", "s4", "f4", y_true=0, y_score=0.2, slice="out_of_corpus"),
    ]

    fa = compute_false_alarms_per_document(preds, decision_threshold=0.5)
    # q1 has 1 false alarm on negative pair (score 0.8 >= 0.5)
    # q2 has 0 false alarms on negative pairs
    # Mean false alarms per doc = (1 + 0) / 2 = 0.5
    assert fa["false_alarms_per_doc"] == 0.5
    assert fa["total_negative_queries"] == 2.0

    multi = evaluate_multi_slice_predictions(preds, threshold=0.5)
    assert "verbatim_copy" in multi["slices"]
    assert "hard_negatives" in multi["slices"]
    assert "out_of_corpus" in multi["slices"]
    assert multi["overall"]["tp"] == 1
    assert multi["overall"]["fp"] == 1


# ---------------------------------------------------------------------------
# 8. Reproducibility & Stage Error Isolation in Benchmark Evaluation
# ---------------------------------------------------------------------------

def test_evaluator_reproducibility_and_stage_isolation(tmp_path):
    from scripts.evaluate_benchmark import run_benchmark_evaluation

    # Run evaluation twice on val split in oracle mode
    out_1 = tmp_path / "rep1.json"
    out_2 = tmp_path / "rep2.json"

    res_1 = run_benchmark_evaluation(split_name="val", output_path=out_1)
    res_2 = run_benchmark_evaluation(split_name="val", output_path=out_2)

    assert out_1.is_file()
    assert out_2.is_file()

    # Verify deterministic reproducibility: exact match across both runs
    assert res_1["stage_1_retrieval"] == res_2["stage_1_retrieval"]
    assert res_1["stage_2_span_alignment"] == res_2["stage_2_span_alignment"]
    assert res_1["stage_3_pair_classification"]["precision"] == res_2["stage_3_pair_classification"]["precision"]
    assert res_1["stage_3_pair_classification"]["recall"] == res_2["stage_3_pair_classification"]["recall"]
    assert res_1["stage_3_pair_classification"]["f1"] == res_2["stage_3_pair_classification"]["f1"]
    assert res_1["error_isolation_taxonomy"] == res_2["error_isolation_taxonomy"]


def test_evaluator_isolates_retrieval_miss_from_classifier_miss(tmp_path):
    from scripts.evaluate_benchmark import run_benchmark_evaluation

    # Construct synthetic predictions where retrieval deliberately misses all sources
    val_csv = Path("data/benchmark_v1/val.csv")
    assert val_csv.is_file()
    import csv
    with val_csv.open("r", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    # All positive queries get empty retrieval candidates
    retrieval_fail_preds = []
    for r in rows:
        retrieval_fail_preds.append({
            "pair_id": r["pair_id"],
            "y_score": 0.9 if r["label"] == "1" else 0.1,  # classifier would be confident
            "retrieved_candidates": [],  # but retrieval failed completely
            "predicted_span": None,
        })

    preds_file = tmp_path / "retrieval_fail_preds.json"
    preds_file.write_text(json.dumps(retrieval_fail_preds), encoding="utf-8")

    report = run_benchmark_evaluation(split_name="val", predictions_path=preds_file)

    # Retrieval Recall@10 must be 0.0
    assert report["stage_1_retrieval"]["recall@10"] == 0.0
    # Error taxonomy must catch the retrieval misses
    assert report["error_isolation_taxonomy"]["RETRIEVAL_MISS"] == 81  # all 81 positives in val split

