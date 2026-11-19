"""Unit and Regression Tests for Comprehensive Ablation Study (CP-04.5).

Verifies:
1. Integrity and schema of data/ablation_results.json.
2. Existence and completeness of docs/ablation-and-robustness-report.md.
3. Verification of core scientific findings across 8 configurations:
   - M0 (Full Pipeline) achieves superior F1, Span IoU, and calibrated Brier score.
   - M1 (w/o Section Filtering) leaks boilerplate front-matter causing false alarms.
   - M2 (w/o Paragraph Chunking) suffers boundary truncation and lower span IoU.
   - M3 (w/o Sentence Alignment) exhibits massive span precision collapse.
   - M4 (w/o Prefilter Removal) collapses cross-lingual recall to 0%.
   - M5 (w/o Reranker) causes false alarm explosion on hard negatives due to topical overlap.
   - M6 (w/o Citation Context) surges human review burden to 100% while preserving reuse recall.
   - M7 (w/o Probability Calibration) suffers over 17x Brier score loss.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

BASE_DIR = Path(__file__).resolve().parent.parent
ABLATION_JSON_PATH = BASE_DIR / "data" / "ablation_results.json"
REPORT_MD_PATH = BASE_DIR / "docs" / "ablation-and-robustness-report.md"


@pytest.fixture(scope="module")
def ablation_data() -> dict:
    assert ABLATION_JSON_PATH.is_file(), f"Missing ablation results at {ABLATION_JSON_PATH}"
    with open(ABLATION_JSON_PATH, encoding="utf-8") as f:
        return json.load(f)


def test_ablation_results_structure_and_completeness(ablation_data: dict):
    """Verify that all 8 configurations are evaluated on the 120 validation pairs."""
    assert ablation_data["benchmark_split"] == "val"
    assert ablation_data["dataset_size"] == 120

    expected_configs = [
        "M0_FULL_PIPELINE",
        "M1_NO_SECTION_FILTER",
        "M2_NO_PARAGRAPH_CHUNKING",
        "M3_NO_SENTENCE_ALIGNMENT",
        "M4_NO_PREFILTER_REMOVAL",
        "M5_NO_RERANKER",
        "M6_NO_CITATION_CONTEXT",
        "M7_UNCALIBRATED",
    ]
    for cfg in expected_configs:
        assert cfg in ablation_data["configurations"], f"Configuration {cfg} missing in results"
        m = ablation_data["configurations"][cfg]["metrics"]
        assert "pair_f1" in m
        assert "pair_precision" in m
        assert "pair_recall" in m
        assert "span_mean_iou" in m
        assert "brier_score" in m
        assert "false_alarms_per_doc" in m
        assert "review_workload_pct" in m


def test_m0_full_pipeline_superiority(ablation_data: dict):
    """M0 must achieve 100% classification metrics on val, zero false alarms, and calibrated Brier."""
    m0 = ablation_data["configurations"]["M0_FULL_PIPELINE"]["metrics"]
    assert m0["pair_f1"] == 1.0000
    assert m0["pair_precision"] == 1.0000
    assert m0["pair_recall"] == 1.0000
    assert m0["source_recall"] == 1.0000
    assert m0["false_alarms_per_doc"] == 0.0000
    assert m0["brier_score"] < 0.0050
    assert m0["span_mean_iou"] > 0.70


def test_m1_section_filter_prevents_boilerplate_leaks(ablation_data: dict):
    """M1 leaks boilerplate front-matter, creating false alarms on hard negatives."""
    m1 = ablation_data["configurations"]["M1_NO_SECTION_FILTER"]["metrics"]
    assert m1["false_positives_count"] > 0
    assert m1["pair_precision"] < 1.0000
    assert m1["false_alarms_per_doc"] > 0.20


def test_m2_paragraph_chunking_preserves_span_iou(ablation_data: dict):
    """M2 (naive sliding window) reduces span IoU compared to paragraph boundary chunking."""
    m0 = ablation_data["configurations"]["M0_FULL_PIPELINE"]["metrics"]
    m2 = ablation_data["configurations"]["M2_NO_PARAGRAPH_CHUNKING"]["metrics"]
    assert m2["span_mean_iou"] < m0["span_mean_iou"]
    assert m2["span_mean_iou"] < 0.65


def test_m3_sentence_alignment_prevents_precision_collapse(ablation_data: dict):
    """M3 (B0 naive adjacent grouping) suffers span precision and IoU collapse."""
    m0 = ablation_data["configurations"]["M0_FULL_PIPELINE"]["metrics"]
    m3 = ablation_data["configurations"]["M3_NO_SENTENCE_ALIGNMENT"]["metrics"]
    assert m3["span_mean_precision"] < 0.60
    assert m3["span_mean_iou"] < 0.55
    assert m3["span_mean_precision"] < m0["span_mean_precision"]


def test_m4_prefilter_removal_rescues_cross_lingual(ablation_data: dict):
    """M4 (strict B0 prefilter) causes 100% loss on cross-lingual candidates."""
    m4 = ablation_data["configurations"]["M4_NO_PREFILTER_REMOVAL"]
    xling = m4["slice_breakdown"]["cross_lingual_vi_en"]
    assert xling["recall"] == 0.0000
    assert xling["f1"] == 0.0000
    assert m4["metrics"]["pair_recall"] < 0.8500


def test_m5_reranker_suppresses_topical_false_alarms(ablation_data: dict):
    """M5 (w/o Cross-Encoder) explodes false alarms on hard negatives due to topical similarity."""
    m5 = ablation_data["configurations"]["M5_NO_RERANKER"]["metrics"]
    assert m5["false_alarms_per_doc"] > 0.60
    assert m5["pair_precision"] < 0.8500
    assert m5["false_positives_count"] >= 15


def test_m6_citation_context_reduces_human_workload(ablation_data: dict):
    """M6 disables citation awareness: 100% matches require human review vs 88.9% in M0.

    Crucially, reuse detection recall remains 1.0000 (no artificial label shifts).
    """
    m0 = ablation_data["configurations"]["M0_FULL_PIPELINE"]["metrics"]
    m6 = ablation_data["configurations"]["M6_NO_CITATION_CONTEXT"]["metrics"]

    assert m6["pair_recall"] == 1.0000
    assert m6["review_workload_pct"] == 100.0
    assert m0["review_workload_pct"] < 100.0
    assert m0["valid_citations_exempted"] > 0
    assert m6["valid_citations_exempted"] == 0


def test_m7_calibration_improves_brier_score(ablation_data: dict):
    """M7 lacks Platt calibration, yielding a degraded Brier score."""
    m0 = ablation_data["configurations"]["M0_FULL_PIPELINE"]["metrics"]
    m7 = ablation_data["configurations"]["M7_UNCALIBRATED"]["metrics"]
    assert m7["brier_score"] > 0.0400
    assert m0["brier_score"] < m7["brier_score"]


def test_report_markdown_file_exists_and_complete():
    """Verify markdown research report exists and has required sections."""
    assert REPORT_MD_PATH.is_file(), f"Report file missing at {REPORT_MD_PATH}"
    content = REPORT_MD_PATH.read_text(encoding="utf-8")
    assert "CP-04.5" in content
    assert "M0: Full Proposed Pipeline" in content
    assert "M1: w/o Section Filtering" in content
    assert "M2: w/o Paragraph Boundary Chunking" in content
    assert "M3: w/o Sentence/Span Alignment" in content
    assert "M4: w/o Prefilter Removal" in content
    assert "M5: w/o Cross-Encoder Reranker" in content
    assert "M6: w/o Academic Citation Context" in content
    assert "M7: w/o Probability Calibration" in content
    assert "BENCH-0661" in content
    assert "BENCH-0616" in content

