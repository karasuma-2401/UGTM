"""Automated tests for CP-06.2: Reproducibility Packaging & Release Manifest."""

import hashlib
import json
from pathlib import Path
import subprocess
import sys
import pytest

ROOT_DIR = Path(__file__).resolve().parent.parent
MANIFEST_PATH = ROOT_DIR / "artifacts" / "release_manifest_v1.0.0.json"
MODEL_CARD_PATH = ROOT_DIR / "docs" / "model-card-rc1.md"
REPRO_GUIDE_PATH = ROOT_DIR / "docs" / "reproducibility-guide.md"
REPRODUCED_TABLES_PATH = ROOT_DIR / "reports" / "reproduced_tables.md"
SAMPLE_CORPUS_DIR = ROOT_DIR / "data" / "sample_shareable_corpus"
LOCKFILE_PATH = ROOT_DIR / "requirements" / "cpu-dev-win32.lock.txt"


def test_reproduce_script_runs_and_generates_tables():
    """Verify that reproduce_benchmark_tables.py runs cleanly and asserts 100% parity."""
    script_path = ROOT_DIR / "scripts" / "reproduce_benchmark_tables.py"
    assert script_path.exists(), "reproduce_benchmark_tables.py missing"

    result = subprocess.run(
        [sys.executable, str(script_path)],
        capture_output=True,
        text=True,
        cwd=str(ROOT_DIR),
    )
    assert result.returncode == 0, f"Script failed with output:\n{result.stderr}\n{result.stdout}"
    assert "Parity check PASSED" in result.stdout or "Parity check PASSED" in result.stderr or "parity check PASSED" in result.stdout or "parity check PASSED" in result.stderr
    assert REPRODUCED_TABLES_PATH.exists(), "reproduced_tables.md was not generated"

    content = REPRODUCED_TABLES_PATH.read_text(encoding="utf-8")
    assert "B0" in content and "RC1" in content
    assert "cross_lingual_vi_en" in content
    assert "100% Bit-for-bit Parity" in content


def test_release_manifest_integrity_and_sha256_match():
    """Verify release_manifest_v1.0.0.json schema and ensure all listed files match disk SHA-256."""
    assert MANIFEST_PATH.exists(), "release_manifest_v1.0.0.json missing"
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))

    assert manifest["release_id"] == "RELEASE-V1.0.0-RC1"
    assert manifest["version"] == "1.0.0-rc1"
    assert "primary_metrics" in manifest
    assert "files" in manifest
    assert len(manifest["files"]) >= 25, "Manifest should index at least 25 core files"

    # Verify each indexed file actually exists and matches its SHA-256
    for rel_path, meta in manifest["files"].items():
        disk_file = ROOT_DIR / rel_path
        assert disk_file.exists(), f"File listed in manifest is missing from disk: {rel_path}"
        actual_sha = hashlib.sha256(disk_file.read_bytes()).hexdigest()
        assert actual_sha == meta["sha256"], f"SHA-256 mismatch for {rel_path}: {actual_sha} vs {meta['sha256']}"


def test_model_card_and_guide_completeness():
    """Verify Model Card and Reproducibility Guide conform to documentation standards."""
    assert MODEL_CARD_PATH.exists(), "model-card-rc1.md missing"
    mc_text = MODEL_CARD_PATH.read_text(encoding="utf-8")
    assert "Model Details" in mc_text
    assert "Intended Use" in mc_text
    assert "BAAI/bge-m3" in mc_text
    assert "BAAI/bge-reranker-v2-m3" in mc_text
    assert "Ethical Considerations" in mc_text
    assert "Limitations & Caveats" in mc_text

    assert REPRO_GUIDE_PATH.exists(), "reproducibility-guide.md missing"
    rg_text = REPRO_GUIDE_PATH.read_text(encoding="utf-8")
    assert "System Prerequisites" in rg_text
    assert "reproduce_benchmark_tables.py" in rg_text
    assert "cpu-dev-win32.lock.txt" in rg_text


def test_sample_shareable_corpus_structure():
    """Verify sample shareable corpus has valid text files and manifest without private student info."""
    assert SAMPLE_CORPUS_DIR.exists(), "sample_shareable_corpus directory missing"
    readme = SAMPLE_CORPUS_DIR / "README.md"
    assert readme.exists(), "sample corpus README missing"

    manifest_csv = SAMPLE_CORPUS_DIR / "corpus_manifest.sample.csv"
    assert manifest_csv.exists(), "corpus_manifest.sample.csv missing"
    lines = manifest_csv.read_text(encoding="utf-8").strip().splitlines()
    assert len(lines) >= 6, "Expected at least 5 sample records plus header"
    assert "document_key,filename,document_family" in lines[0]

    # Verify presence of sample txt files
    for fname in [
        "source_01_nlp_thesis_intro.txt",
        "source_02_blockchain_security.txt",
        "suspect_01_verbatim_reuse.txt",
        "suspect_02_paraphrase_reuse.txt",
        "suspect_03_independent_work.txt",
    ]:
        fpath = SAMPLE_CORPUS_DIR / fname
        assert fpath.exists(), f"Sample corpus file missing: {fname}"
        text = fpath.read_text(encoding="utf-8")
        assert len(text.strip()) > 50, f"Sample file {fname} is too short"


def test_lockfile_stability():
    """Verify lockfile exists, is pinned, and does not pull torch/cuda in local dev."""
    assert LOCKFILE_PATH.exists(), "cpu-dev-win32.lock.txt missing"
    lock_text = LOCKFILE_PATH.read_text(encoding="utf-8").lower()
    assert "fastapi" in lock_text
    assert "psycopg" in lock_text
    assert "torch" not in lock_text, "Local dev lockfile should not include PyTorch"

