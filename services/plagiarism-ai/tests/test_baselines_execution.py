"""Unit and Integration Tests for Baseline Experiments B0–B4 and Error Taxonomy (CP-03.5).

Verifies:
1. Existence and integrity of configuration files for B0, B1, B2, B3, B4.
2. Existence and valid record schema for predictions in data/baseline_predictions/.
3. Verification of core empirical research hypotheses:
   - B1 (Lexical-only) has 0 recall on cross-lingual slice.
   - B2 (Dense-only) and B3 (Hybrid) generate high false alarms on hard negatives.
   - B4 (Hybrid + Reranker) achieves superior F1 and suppresses false alarms.
4. Error taxonomy categorizes failures into 7 defined buckets.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

BASE_DIR = Path(".")
CONFIGS_DIR = BASE_DIR / "configs"
PREDICTIONS_DIR = BASE_DIR / "data" / "baseline_predictions"
REPORTS_DIR = BASE_DIR / "reports" / "baseline_reports"
REPORT_MD = BASE_DIR / "docs" / "baseline-b0-b4-benchmark-report.md"


def test_baseline_configs_exist():
    expected_configs = ["baseline_b0.json", "baseline_b1.json", "baseline_b2.json", "baseline_b3.json", "baseline_b4.json"]
    for cfg in expected_configs:
        cfg_file = CONFIGS_DIR / cfg
        assert cfg_file.is_file(), f"Configuration file {cfg} missing"
        data = json.loads(cfg_file.read_text(encoding="utf-8"))
        assert "baseline_id" in data


def test_baseline_predictions_and_reports_exist():
    baselines = ["b0", "b1", "b2", "b3", "b4"]
    for b in baselines:
        pred_file = PREDICTIONS_DIR / f"predictions_{b}.json"
        rep_file = REPORTS_DIR / f"report_{b}.json"

        assert pred_file.is_file(), f"Predictions file {pred_file} missing"
        assert rep_file.is_file(), f"Report file {rep_file} missing"

        preds = json.loads(pred_file.read_text(encoding="utf-8"))
        assert len(preds) == 120, f"Expected 120 predictions for {b}, got {len(preds)}"

        rep = json.loads(rep_file.read_text(encoding="utf-8"))
        assert "retrieval" in rep
        assert "classification" in rep
        assert "error_taxonomy" in rep


def test_empirical_research_hypotheses_across_baselines():
    rep_b0 = json.loads((REPORTS_DIR / "report_b0.json").read_text(encoding="utf-8"))
    rep_b1 = json.loads((REPORTS_DIR / "report_b1.json").read_text(encoding="utf-8"))
    rep_b2 = json.loads((REPORTS_DIR / "report_b2.json").read_text(encoding="utf-8"))
    rep_b3 = json.loads((REPORTS_DIR / "report_b3.json").read_text(encoding="utf-8"))
    rep_b4 = json.loads((REPORTS_DIR / "report_b4.json").read_text(encoding="utf-8"))

    # Hypothesis 1: Lexical baseline B1 fails completely on cross-lingual plagiarism
    b1_xling_f1 = rep_b1["slice_breakdown"]["cross_lingual_vi_en"]["f1"]
    assert b1_xling_f1 == 0.0, "Lexical baseline should not detect cross-lingual reuse"

    # Hypothesis 2: Dense-only (B2) and Hybrid (B3) produce substantial false alarms on hard negatives
    # due to domain/topical vocabulary overlap without cross-encoder fact checking
    b2_fa = rep_b2["false_alarms"]["false_alarms_per_doc"]
    b3_fa = rep_b3["false_alarms"]["false_alarms_per_doc"]
    b4_fa = rep_b4["false_alarms"]["false_alarms_per_doc"]

    assert b2_fa > 0.50, f"Expected high false alarm rate for dense-only, got {b2_fa}"
    assert b3_fa > 0.50, f"Expected high false alarm rate for hybrid-without-reranker, got {b3_fa}"
    assert b4_fa < 0.05, f"Expected near-zero false alarm rate for B4, got {b4_fa}"

    # Hypothesis 3: B0 suffers from prefilter misses (Source misses on cross-lingual and summary)
    assert rep_b0["error_taxonomy"]["SOURCE_MISS"] >= 15

    # Hypothesis 4: B4 achieves superior F1 compared to B0, B1, B2, B3
    b4_f1 = rep_b4["classification"]["f1"]
    assert b4_f1 > rep_b0["classification"]["f1"]
    assert b4_f1 > rep_b1["classification"]["f1"]
    assert b4_f1 > rep_b2["classification"]["f1"]
    assert b4_f1 > rep_b3["classification"]["f1"]


def test_markdown_report_exists_and_contains_all_baselines():
    assert REPORT_MD.is_file()
    content = REPORT_MD.read_text(encoding="utf-8")
    for b in ["B0", "B1", "B2", "B3", "B4"]:
        assert f"**{b}**" in content
    for err in ["PARSE_ERROR", "BOILERPLATE_LEAK", "SOURCE_MISS", "RERANK_ERROR", "ALIGNMENT_ERROR", "DECISION_ERROR", "CORPUS_GAP"]:
        assert err in content

