from __future__ import annotations

import csv
import json
from pathlib import Path
import pytest

from scripts.build_benchmark_dataset import BENCHMARK_FIELDS

BENCHMARK_DIR = Path(__file__).resolve().parent.parent / "data" / "benchmark_v1"
PAIRS_CSV = BENCHMARK_DIR / "benchmark_pairs.csv"
METADATA_JSON = BENCHMARK_DIR / "benchmark_metadata.json"


def test_benchmark_dataset_exists_and_matches_fields():
    """Verify that UGTM-Bench-v1 dataset files exist and adhere to the strict schema."""
    assert PAIRS_CSV.exists(), f"Benchmark pairs CSV missing at {PAIRS_CSV}"
    assert METADATA_JSON.exists(), f"Benchmark metadata JSON missing at {METADATA_JSON}"

    with PAIRS_CSV.open(newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        rows = list(reader)

    assert len(rows) == 800, f"Expected 800 benchmark pairs, got {len(rows)}"
    first_row_fields = list(rows[0].keys())
    assert first_row_fields == list(BENCHMARK_FIELDS)


def test_benchmark_all_slices_covered():
    """Verify that all 7 evaluation slices are represented with expected quotas."""
    with PAIRS_CSV.open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    slices = {r["slice"] for r in rows}
    expected_slices = {
        "verbatim_copy",
        "paraphrase",
        "summarization",
        "cross_lingual_vi_en",
        "multi_source",
        "hard_negatives",
        "out_of_corpus",
    }
    assert slices == expected_slices

    slice_counts = {}
    for r in rows:
        s = r["slice"]
        slice_counts[s] = slice_counts.get(s, 0) + 1

    assert slice_counts["verbatim_copy"] == 120
    assert slice_counts["paraphrase"] == 140
    assert slice_counts["summarization"] == 100
    assert slice_counts["cross_lingual_vi_en"] == 100
    assert slice_counts["multi_source"] == 80
    assert slice_counts["hard_negatives"] == 180
    assert slice_counts["out_of_corpus"] == 80


def test_benchmark_label_balance_and_semantics():
    """Verify label distribution and semantic consistency across slices."""
    with PAIRS_CSV.open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    positives = [r for r in rows if r["label"] == "1"]
    negatives = [r for r in rows if r["label"] == "0"]

    assert len(positives) == 540
    assert len(negatives) == 260

    # Hard negatives must have label=0 and reuse_type=independent
    hard_negs = [r for r in rows if r["slice"] == "hard_negatives"]
    assert all(r["label"] == "0" for r in hard_negs)
    assert all(r["reuse_type"] == "independent" for r in hard_negs)

    # Verbatim copy must have label=1 and reuse_type=verbatim
    verbatim_rows = [r for r in rows if r["slice"] == "verbatim_copy"]
    assert all(r["label"] == "1" for r in verbatim_rows)
    assert all(r["reuse_type"] == "verbatim" for r in verbatim_rows)


def test_benchmark_document_family_and_query_diversity():
    """Verify that the benchmark covers sufficient document families and query documents."""
    meta = json.loads(METADATA_JSON.read_text(encoding="utf-8"))
    assert meta["document_families_count"] >= 40, f"Too few families: {meta['document_families_count']}"
    assert meta["query_documents_count"] >= 60, f"Too few query docs: {meta['query_documents_count']}"
    assert meta["provenance_declaration"]["annotator_id"] == "solo_annotator_author"

