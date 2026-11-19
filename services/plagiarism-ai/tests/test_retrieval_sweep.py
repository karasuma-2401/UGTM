"""Unit and Regression Tests for Candidate Retrieval Optimization and Sweep (CP-04.1).

Verifies:
1. configs/retrieval_config.json loads production settings (top_k=30, fused_k=25, prefilter disabled).
2. data/retrieval_sweep_results.json exists and adheres to schema.
3. Prefilter ablation proves removing hard prefilter eliminates source misses on cross-lingual reuse.
4. Monotonicity of candidate-recall curves across K in [5, 10, 15, 20, 25, 30, 50, 100].
5. Pareto optimal selection satisfies SLA (< 2.0s per 50 chunks) with Source Recall >= 0.95.
6. Paragraph boundary chunking is selected over redundant micro-chunk sentence windows.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from app.search import load_retrieval_config

SWEEP_RESULTS_PATH = Path("data/retrieval_sweep_results.json")
REPORT_MD_PATH = Path("docs/candidate-retrieval-optimization-report.md")


@pytest.fixture(scope="module")
def sweep_results() -> dict:
    assert SWEEP_RESULTS_PATH.is_file(), f"Sweep results file {SWEEP_RESULTS_PATH} missing"
    return json.loads(SWEEP_RESULTS_PATH.read_text(encoding="utf-8"))


def test_retrieval_config_loads_production_settings():
    cfg = load_retrieval_config()
    assert cfg["top_k_per_branch"] == 30
    assert cfg["fused_k"] == 25
    assert cfg["rrf_k"] == 60
    assert cfg["prefilter"]["enabled"] is False
    assert cfg["prefilter"]["mode"] == "none"


def test_sweep_results_file_integrity(sweep_results):
    assert sweep_results["experiment_id"] == "EXP-04.1-RETRIEVAL"
    assert sweep_results["checkpoint"] == "CP-04.1"
    assert sweep_results["split_evaluated"] == "val"
    assert len(sweep_results["sweep_matrix"]) == 24  # 3 modes * 8 K values
    assert len(sweep_results["chunking_comparison"]) == 3
    assert "selected_optimal_config" in sweep_results


def test_prefilter_ablation_eliminates_cross_lingual_miss(sweep_results):
    matrix = sweep_results["sweep_matrix"]

    # At K=25: compare strict_b0 vs no_prefilter
    strict_k25 = next(r for r in matrix if r["prefilter_mode"] == "strict_b0" and r["k"] == 25)
    none_k25 = next(r for r in matrix if r["prefilter_mode"] == "no_prefilter" and r["k"] == 25)

    # In strict_b0, cross-lingual recall is 0.0
    assert strict_k25["slice_recalls"]["cross_lingual_vi_en"] == 0.0
    # In no_prefilter, cross-lingual recall is 1.0 (all 15 pairs retrieved)
    assert none_k25["slice_recalls"]["cross_lingual_vi_en"] == 1.0
    # Overall recall improves dramatically
    assert none_k25["overall_source_recall"] > strict_k25["overall_source_recall"]


def test_candidate_recall_curve_monotonicity(sweep_results):
    matrix = sweep_results["sweep_matrix"]
    no_prefilter_runs = sorted(
        [r for r in matrix if r["prefilter_mode"] == "no_prefilter"],
        key=lambda x: x["k"],
    )

    prev_recall = 0.0
    for run in no_prefilter_runs:
        current_recall = run["overall_source_recall"]
        assert current_recall >= prev_recall, f"Recall dropped at K={run['k']}: {current_recall} < {prev_recall}"
        prev_recall = current_recall


def test_pareto_optimal_selection_satisfies_sla(sweep_results):
    opt = sweep_results["selected_optimal_config"]
    assert opt["fused_k"] == 25
    assert opt["prefilter_mode"] == "no_prefilter"
    assert opt["overall_source_recall"] >= 0.95
    assert opt["total_time_50_chunks_sec"] < 2.0  # Under 2-second pipeline SLA
    assert opt["estimated_rerank_ms_per_chunk"] < 100.0


def test_chunking_strategy_comparison(sweep_results):
    chunking = sweep_results["chunking_comparison"]
    para_strat = next(c for c in chunking if c["strategy"] == "paragraph_boundary")
    sent_strat = next(c for c in chunking if c["strategy"] == "sentence_window_3_1")

    # Paragraph boundary has significantly lower deduplication redundancy than micro sentence window
    assert para_strat["source_deduplication_rate"] < sent_strat["source_deduplication_rate"]
    assert para_strat["recommended"] is True
    assert sent_strat["recommended"] is False


def test_markdown_report_exists_and_contains_tradeoffs():
    assert REPORT_MD_PATH.is_file()
    content = REPORT_MD_PATH.read_text(encoding="utf-8")
    assert "Pareto" in content
    assert "no_prefilter" in content
    assert "strict_b0" in content
    assert "paragraph_boundary" in content

