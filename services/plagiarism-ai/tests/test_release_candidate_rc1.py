"""Unit and Integrity Tests for AI Release Candidate 1 (RC1) Freeze (CP-04.6).

Verifies:
1. Existence, valid schema, and non-empty metadata of artifacts/ai_release_candidate_rc1.manifest.json.
2. Cryptographic checksums (SHA-256) of all bound pipeline assets:
   - Classifier model artifact (artifacts/suspicious_passage_classifier.joblib)
   - Preprocessing configuration (configs/preprocessing_config.json)
   - Retrieval configuration (configs/retrieval_config.json)
   - Corpus snapshot manifest (data/corpus_manifest.csv)
   - Split benchmark manifest (data/benchmark_v1/split_manifest.json)
3. Strict enforcement of frozen decision thresholds:
   - tau_high = 0.70, tau_low = 0.30
   - top_k = 30, fused_k = 25, prefilter = "no_prefilter"
4. Freeze milestone state in configs/experiment_registry.json (FREEZE-AI-RC1 is locked).
5. Completeness of docs/experiment-decision-log.md documenting EXT-01/02/03.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

BASE_DIR = Path(__file__).resolve().parent.parent
RC1_MANIFEST_PATH = BASE_DIR / "artifacts" / "ai_release_candidate_rc1.manifest.json"
DECISION_LOG_PATH = BASE_DIR / "docs" / "experiment-decision-log.md"
REGISTRY_PATH = BASE_DIR / "configs" / "experiment_registry.json"


def compute_file_sha256(path: Path) -> str:
    """Calculate exact SHA-256 hex digest of a file on disk."""
    hasher = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(65536):
            hasher.update(chunk)
    return hasher.hexdigest().lower()


@pytest.fixture(scope="module")
def rc1_manifest() -> dict:
    assert RC1_MANIFEST_PATH.is_file(), f"RC1 manifest missing at {RC1_MANIFEST_PATH}"
    with open(RC1_MANIFEST_PATH, encoding="utf-8") as f:
        return json.load(f)


def test_rc1_manifest_core_metadata(rc1_manifest: dict):
    """Verify basic manifest identifiers and version tags."""
    assert rc1_manifest["manifest_version"] == "1.0.0"
    assert rc1_manifest["pipeline_version"] == "1.0.0-rc1"
    assert rc1_manifest["release_candidate"] == "RC1"
    assert rc1_manifest["freeze_milestone"] == "FREEZE-AI-RC1"
    assert "git_commit" in rc1_manifest and len(rc1_manifest["git_commit"]) >= 7


def test_rc1_manifest_cryptographic_checksums(rc1_manifest: dict):
    """Verify all files referenced in the manifest match their declared SHA-256 checksums."""
    comps = rc1_manifest["components"]

    # 1. Classifier Model Artifact
    clf_info = comps["classifier_and_review_policy"]
    model_path = BASE_DIR / clf_info["model_artifact"]
    assert model_path.is_file(), f"Model file missing at {model_path}"
    assert compute_file_sha256(model_path) == clf_info["model_sha256"].lower()

    # 2. Preprocessing Config
    prep_info = comps["preprocessing_and_chunking"]
    prep_path = BASE_DIR / prep_info["config_file"]
    assert prep_path.is_file(), f"Preprocessing config missing at {prep_path}"
    assert compute_file_sha256(prep_path) == prep_info["config_sha256"].lower()

    # 3. Retrieval Config
    ret_info = comps["candidate_retrieval"]
    ret_path = BASE_DIR / ret_info["config_file"]
    assert ret_path.is_file(), f"Retrieval config missing at {ret_path}"
    assert compute_file_sha256(ret_path) == ret_info["config_sha256"].lower()

    # 4. Corpus Snapshot Manifest
    corp_info = rc1_manifest["corpus_snapshot"]
    corp_path = BASE_DIR / corp_info["manifest_file"]
    assert corp_path.is_file(), f"Corpus manifest missing at {corp_path}"
    assert compute_file_sha256(corp_path) == corp_info["manifest_sha256"].lower()

    # 5. Split Benchmark Manifest
    split_info = rc1_manifest["benchmark_split_lock"]
    split_path = BASE_DIR / split_info["split_manifest"]
    assert split_path.is_file(), f"Split manifest missing at {split_path}"
    assert compute_file_sha256(split_path) == split_info["split_manifest_sha256"].lower()


def test_rc1_manifest_frozen_hyperparameters(rc1_manifest: dict):
    """Verify that retrieval, alignment, and classifier thresholds are strictly frozen."""
    comps = rc1_manifest["components"]

    # Retrieval
    ret = comps["candidate_retrieval"]
    assert ret["top_k_per_branch"] == 30
    assert ret["fused_k"] == 25
    assert ret["prefilter_policy"] == "no_prefilter"

    # Dense & Reranker models
    assert comps["dense_embedding_model"]["model_id"] == "BAAI/bge-m3"
    assert comps["dense_embedding_model"]["dimension"] == 1024
    assert comps["cross_encoder_reranker"]["model_id"] == "BAAI/bge-reranker-v2-m3"

    # Alignment
    align = comps["sentence_span_alignment"]
    assert align["min_sentence_similarity"] == 0.70
    assert align["composite_margin"] == 0.05
    assert align["deduplicate_overlapping_spans"] is True
    assert align["multi_source_separation"] is True

    # Review policy
    thresh = comps["classifier_and_review_policy"]["thresholds"]
    assert thresh["tau_high"] == 0.70
    assert thresh["tau_low"] == 0.30


def test_rc1_known_limitations_catalog(rc1_manifest: dict):
    """Verify that known limitations are explicitly declared with mitigations."""
    limitations = rc1_manifest.get("known_limitations_and_unresolved_challenges", [])
    assert len(limitations) >= 4, "Must declare at least 4 known limitations"
    for lim in limitations:
        assert "id" in lim
        assert "area" in lim
        assert "description" in lim
        assert "mitigation_in_rc1" in lim
        assert len(lim["description"].strip()) > 20


def test_experiment_registry_freeze_milestone_locked():
    """Verify that FREEZE-AI-RC1 is marked locked in configs/experiment_registry.json."""
    assert REGISTRY_PATH.is_file(), f"Registry file missing at {REGISTRY_PATH}"
    with open(REGISTRY_PATH, encoding="utf-8") as f:
        reg = json.load(f)

    milestones = {m["freeze_id"]: m for m in reg["freeze_milestones"]}
    assert "FREEZE-AI-RC1" in milestones
    rc1_m = milestones["FREEZE-AI-RC1"]
    assert rc1_m["status"] == "locked"
    assert rc1_m["checkpoint"] == "CP-04.6"
    assert rc1_m["config_path"] == "artifacts/ai_release_candidate_rc1.manifest.json"
    assert rc1_m["locked_at"] is not None


def test_experiment_decision_log_exists_and_contains_decisions():
    """Verify experiment decision log covers EXT-01, EXT-02, and EXT-03."""
    assert DECISION_LOG_PATH.is_file(), f"Decision log missing at {DECISION_LOG_PATH}"
    content = DECISION_LOG_PATH.read_text(encoding="utf-8")
    assert "FREEZE-AI-RC1" in content
    assert "EXT-01" in content
    assert "EXT-02" in content
    assert "EXT-03" in content
    assert "DEFERRED" in content
    assert "REJECTED" in content

