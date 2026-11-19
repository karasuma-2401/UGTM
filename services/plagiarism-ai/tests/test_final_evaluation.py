"""Automated verification tests for CP-06.1 Final Blind Test Benchmark."""

import json
from pathlib import Path
import hashlib
import pytest

ROOT_DIR = Path(__file__).resolve().parent.parent
TEST_CSV_PATH = ROOT_DIR / "data" / "benchmark_v1" / "test.csv"
REPORT_MD_PATH = ROOT_DIR / "docs" / "final-test-benchmark-report.md"
PREDICTIONS_DIR = ROOT_DIR / "data" / "final_test_predictions"
REPORTS_DIR = ROOT_DIR / "reports" / "final_test_reports"

EXPECTED_TEST_SHA = "7b8432a8e7f480ad3589c26e9220acac5d9afd7e89b22c914a9ac2cf277a340a"
SYSTEM_IDS = ["b0", "b1", "b2", "b3", "b4", "rc1"]


def test_test_dataset_sha256_integrity():
    """Verify that test.csv was not altered or tampered during the blind test."""
    assert TEST_CSV_PATH.exists(), "test.csv missing"
    content = TEST_CSV_PATH.read_bytes()
    calc_sha = hashlib.sha256(content).hexdigest()
    assert calc_sha == EXPECTED_TEST_SHA, f"test.csv SHA mismatch: {calc_sha} vs {EXPECTED_TEST_SHA}"


def test_markdown_report_exists_and_valid():
    """Verify final benchmark markdown report contents."""
    assert REPORT_MD_PATH.exists(), "Markdown benchmark report missing"
    text = REPORT_MD_PATH.read_text(encoding="utf-8")
    assert "FREEZE-BENCHMARK-SPLIT" in text
    assert EXPECTED_TEST_SHA in text
    assert "Tesla T4" in text
    assert "B0" in text and "RC1" in text
    assert "cross_lingual_vi_en" in text
    assert "hard_negatives" in text
    assert "verbatim_copy" in text


def test_all_system_predictions_exist():
    """Verify all 6 systems produced exactly 120 predictions matching test split."""
    for sys_id in SYSTEM_IDS:
        pred_path = PREDICTIONS_DIR / f"predictions_{sys_id}.json"
        assert pred_path.exists(), f"Missing prediction file for {sys_id}"
        preds = json.loads(pred_path.read_text(encoding="utf-8"))
        assert len(preds) == 120, f"Expected 120 predictions for {sys_id}, got {len(preds)}"
        assert all("pair_id" in p and "y_true" in p and "y_score" in p for p in preds)


def test_all_system_reports_exist_and_conform_schema():
    """Verify report JSON files exist with all 4 evaluation tiers and bootstrap CI."""
    for sys_id in SYSTEM_IDS:
        rep_path = REPORTS_DIR / f"report_{sys_id}.json"
        assert rep_path.exists(), f"Missing report file for {sys_id}"
        rep = json.loads(rep_path.read_text(encoding="utf-8"))
        assert rep["split"] == "test"
        assert rep["device"] == "Tesla T4"
        assert "retrieval" in rep
        assert "alignment" in rep
        assert "classification" in rep
        assert "false_alarms_per_doc" in rep
        assert "slice_breakdown" in rep
        assert "f1_bootstrap_95_ci" in rep["classification"]


def test_rc1_superiority_over_b0():
    """Verify RC1 significantly suppresses false alarms compared to B0 baseline."""
    b0_rep = json.loads((REPORTS_DIR / "report_b0.json").read_text(encoding="utf-8"))
    rc1_rep = json.loads((REPORTS_DIR / "report_rc1.json").read_text(encoding="utf-8"))

    # B0 false alarm rate vs RC1 false alarm rate
    b0_fa = b0_rep["false_alarms_per_doc"]["false_alarms_per_doc"]
    rc1_fa = rc1_rep["false_alarms_per_doc"]["false_alarms_per_doc"]
    assert b0_fa > rc1_fa, f"Expected RC1 false alarms ({rc1_fa}) to be lower than B0 ({b0_fa})"
    assert rc1_fa == 0.0, "RC1 false alarm rate should be 0.0000 on test set"

    # F1 score comparison
    rc1_f1 = rc1_rep["classification"]["f1"]
    assert rc1_f1 > 0.95, f"Expected RC1 F1 > 0.95, got {rc1_f1}"

