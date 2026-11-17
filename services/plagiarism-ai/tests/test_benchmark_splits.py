"""Unit and Integration Tests for Benchmark Splits and Leakage Verification (CP-03.3).

Verifies:
1. Split sizes: train=560, val=120, test=120, total=800 pairs.
2. Exact slice representation across splits (7 slices preserved proportionally).
3. Zero cross-split family leakage: train, val, and test document families are strictly disjoint.
4. Zero cross-split query leakage: no query document appears in more than one split.
5. Test query doc diversity: >= 20-30 distinct query documents in test split.
6. Split manifest integrity: SHA-256 hashes in split_manifest.json match actual files.
7. Test access locking policy metadata compliance.
"""

from __future__ import annotations

import csv
import json
from pathlib import Path

import pytest

from app.corpus import compute_file_sha256

BENCHMARK_DIR = Path("data/benchmark_v1")
TRAIN_CSV = BENCHMARK_DIR / "train.csv"
VAL_CSV = BENCHMARK_DIR / "val.csv"
TEST_CSV = BENCHMARK_DIR / "test.csv"
PAIRS_CSV = BENCHMARK_DIR / "benchmark_pairs.csv"
MANIFEST_JSON = BENCHMARK_DIR / "split_manifest.json"

REQUIRED_COLUMNS = [
    "pair_id",
    "split",
    "query_doc_id",
    "query_family_id",
    "source_doc_id",
    "source_family_id",
    "query_text",
    "source_text",
    "label",
    "slice",
    "reuse_type",
    "citation_status",
    "query_span",
    "source_span",
    "provenance_type",
    "transformation_details",
    "annotator_id",
    "status",
]


def load_pairs(path: Path) -> list[dict[str, str]]:
    assert path.is_file(), f"File {path} does not exist"
    with path.open("r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        return list(reader)


@pytest.fixture(scope="module")
def benchmark_data():
    train = load_pairs(TRAIN_CSV)
    val = load_pairs(VAL_CSV)
    test = load_pairs(TEST_CSV)
    all_pairs = load_pairs(PAIRS_CSV)
    manifest = json.loads(MANIFEST_JSON.read_text(encoding="utf-8"))
    return {
        "train": train,
        "val": val,
        "test": test,
        "all": all_pairs,
        "manifest": manifest,
    }


def test_split_files_exist_and_counts(benchmark_data):
    train = benchmark_data["train"]
    val = benchmark_data["val"]
    test = benchmark_data["test"]
    all_pairs = benchmark_data["all"]

    assert len(train) == 560, f"Expected 560 train pairs, got {len(train)}"
    assert len(val) == 120, f"Expected 120 val pairs, got {len(val)}"
    assert len(test) == 120, f"Expected 120 test pairs, got {len(test)}"
    assert len(all_pairs) == 800, f"Expected 800 total pairs, got {len(all_pairs)}"


def test_split_column_headers(benchmark_data):
    for split_name in ["train", "val", "test", "all"]:
        rows = benchmark_data[split_name]
        for col in REQUIRED_COLUMNS:
            assert col in rows[0], f"Column '{col}' missing from {split_name}"


def test_label_and_slice_stratification(benchmark_data):
    expected_slices = {
        "train": {
            "verbatim_copy": 84,
            "paraphrase": 98,
            "summarization": 70,
            "cross_lingual_vi_en": 70,
            "multi_source": 56,
            "hard_negatives": 126,
            "out_of_corpus": 56,
        },
        "val": {
            "verbatim_copy": 18,
            "paraphrase": 21,
            "summarization": 15,
            "cross_lingual_vi_en": 15,
            "multi_source": 12,
            "hard_negatives": 27,
            "out_of_corpus": 12,
        },
        "test": {
            "verbatim_copy": 18,
            "paraphrase": 21,
            "summarization": 15,
            "cross_lingual_vi_en": 15,
            "multi_source": 12,
            "hard_negatives": 27,
            "out_of_corpus": 12,
        },
    }

    for split_name, expected_counts in expected_slices.items():
        rows = benchmark_data[split_name]
        actual_counts: dict[str, int] = {}
        positives = 0
        negatives = 0

        for r in rows:
            slice_name = r["slice"]
            actual_counts[slice_name] = actual_counts.get(slice_name, 0) + 1
            if r["label"] == "1":
                positives += 1
            elif r["label"] == "0":
                negatives += 1

        assert actual_counts == expected_counts, f"Slice count mismatch in {split_name}"
        total = len(rows)
        pos_ratio = positives / total
        neg_ratio = negatives / total
        assert abs(pos_ratio - 0.675) < 1e-4, f"Positive ratio {pos_ratio} != 0.675 in {split_name}"
        assert abs(neg_ratio - 0.325) < 1e-4, f"Negative ratio {neg_ratio} != 0.325 in {split_name}"


def test_zero_family_leakage(benchmark_data):
    def extract_internal_families(rows: list[dict[str, str]]) -> set[str]:
        fams = set()
        for r in rows:
            for f in (r["query_family_id"], r["source_family_id"]):
                if not f.startswith("fam-external") and not f.startswith("fam-xling"):
                    fams.add(f)
        return fams

    train_fams = extract_internal_families(benchmark_data["train"])
    val_fams = extract_internal_families(benchmark_data["val"])
    test_fams = extract_internal_families(benchmark_data["test"])

    assert len(train_fams) > 0
    assert len(val_fams) > 0
    assert len(test_fams) > 0

    assert train_fams.isdisjoint(val_fams), f"Train and Val share families: {train_fams & val_fams}"
    assert train_fams.isdisjoint(test_fams), f"Train and Test share families: {train_fams & test_fams}"
    assert val_fams.isdisjoint(test_fams), f"Val and Test share families: {val_fams & test_fams}"


def test_zero_query_doc_leakage(benchmark_data):
    def extract_queries(rows: list[dict[str, str]]) -> set[str]:
        return {r["query_doc_id"] for r in rows}

    train_queries = extract_queries(benchmark_data["train"])
    val_queries = extract_queries(benchmark_data["val"])
    test_queries = extract_queries(benchmark_data["test"])

    assert train_queries.isdisjoint(val_queries), "Train and Val share query docs"
    assert train_queries.isdisjoint(test_queries), "Train and Test share query docs"
    assert val_queries.isdisjoint(test_queries), "Val and Test share query docs"


def test_test_split_diversity_and_size(benchmark_data):
    test_rows = benchmark_data["test"]
    unique_queries = {r["query_doc_id"] for r in test_rows}
    # Check query document count in test split >= 20-30 query docs
    assert len(unique_queries) >= 30, f"Expected >= 30 test query docs, got {len(unique_queries)}"


def test_split_manifest_sha256_integrity(benchmark_data):
    manifest = benchmark_data["manifest"]
    assert manifest["freeze_id"] == "FREEZE-BENCHMARK-SPLIT"
    assert manifest["checkpoint"] == "CP-03.3"
    assert manifest["test_access_policy"]["status"] == "LOCKED"
    assert manifest["test_access_policy"]["authorized_unlock_checkpoint"] == "CP-06.1"

    files_meta = manifest["files"]
    assert files_meta["train_csv"]["sha256"] == compute_file_sha256(TRAIN_CSV)
    assert files_meta["val_csv"]["sha256"] == compute_file_sha256(VAL_CSV)
    assert files_meta["test_csv"]["sha256"] == compute_file_sha256(TEST_CSV)
    assert files_meta["benchmark_pairs_csv"]["sha256"] == compute_file_sha256(PAIRS_CSV)

