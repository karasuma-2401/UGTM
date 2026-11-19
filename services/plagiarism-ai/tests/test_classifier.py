import hashlib
import json
import joblib
import numpy as np
import pytest

from app.classifier import load_classifier, score_candidate
from app.config import settings
from app.features import FEATURE_NAMES, FEATURE_SCHEMA, build_features


def test_score_candidate_when_disabled():
    original_enabled = settings.classifier_enabled
    try:
        settings.classifier_enabled = False
        features = build_features("test query", "test source", 0.8, 0.9)
        assert score_candidate(features) is None
    finally:
        settings.classifier_enabled = original_enabled


def test_load_classifier_missing_artifact(tmp_path):
    original_enabled = settings.classifier_enabled
    original_path = settings.classifier_path
    try:
        settings.classifier_enabled = True
        settings.classifier_path = tmp_path / "non_existent.joblib"
        load_classifier.cache_clear()
        with pytest.raises(RuntimeError, match="CLASSIFIER_ARTIFACT_MISSING"):
            load_classifier()
    finally:
        settings.classifier_enabled = original_enabled
        settings.classifier_path = original_path
        load_classifier.cache_clear()


def test_load_classifier_schema_mismatch(tmp_path):
    original_enabled = settings.classifier_enabled
    original_path = settings.classifier_path
    try:
        model_file = tmp_path / "test.joblib"
        manifest_file = tmp_path / "test.manifest.json"
        model_file.write_bytes(b"dummy")
        manifest_file.write_text(
            json.dumps(
                {
                    "feature_schema": "wrong-schema",
                    "feature_names": ["a"],
                    "embedding_model": settings.embedding_model,
                    "reranker_model": settings.reranker_model,
                }
            )
        )
        settings.classifier_enabled = True
        settings.classifier_path = model_file
        load_classifier.cache_clear()
        with pytest.raises(RuntimeError, match="CLASSIFIER_FEATURE_SCHEMA_MISMATCH"):
            load_classifier()
    finally:
        settings.classifier_enabled = original_enabled
        settings.classifier_path = original_path
        load_classifier.cache_clear()


def test_load_classifier_base_model_mismatch(tmp_path):
    original_enabled = settings.classifier_enabled
    original_path = settings.classifier_path
    try:
        model_file = tmp_path / "test.joblib"
        manifest_file = tmp_path / "test.manifest.json"
        model_file.write_bytes(b"dummy")
        manifest_file.write_text(
            json.dumps(
                {
                    "feature_schema": FEATURE_SCHEMA,
                    "feature_names": FEATURE_NAMES,
                    "embedding_model": "different-embedding-model",
                    "reranker_model": settings.reranker_model,
                }
            )
        )
        settings.classifier_enabled = True
        settings.classifier_path = model_file
        load_classifier.cache_clear()
        with pytest.raises(RuntimeError, match="CLASSIFIER_BASE_MODEL_MISMATCH"):
            load_classifier()
    finally:
        settings.classifier_enabled = original_enabled
        settings.classifier_path = original_path
        load_classifier.cache_clear()


class DummyClassifier:
    def predict_proba(self, X):
        return np.array([[0.25, 0.75]])


def test_score_candidate_when_enabled(tmp_path):
    original_enabled = settings.classifier_enabled
    original_path = settings.classifier_path
    try:
        model_file = tmp_path / "dummy.joblib"
        manifest_file = tmp_path / "dummy.manifest.json"
        joblib.dump(DummyClassifier(), model_file)
        manifest_file.write_text(
            json.dumps(
                {
                    "model_version": "candidate-lr-v1",
                    "feature_schema": FEATURE_SCHEMA,
                    "feature_names": FEATURE_NAMES,
                    "embedding_model": settings.embedding_model,
                    "reranker_model": settings.reranker_model,
                    "threshold": 0.65,
                }
            )
        )
        settings.classifier_enabled = True
        settings.classifier_path = model_file
        load_classifier.cache_clear()

        features = build_features("test query", "test source", 0.8, 0.9)
        result = score_candidate(features)

        assert result is not None
        assert result["score"] == 0.75
        assert result["threshold"] == 0.65
        assert result["version"] == "candidate-lr-v1"

        load_classifier.cache_clear()
        reloaded_result = score_candidate(features)
        assert reloaded_result == result
    finally:
        settings.classifier_enabled = original_enabled
        settings.classifier_path = original_path
        load_classifier.cache_clear()


def test_load_classifier_verifies_artifact_sha256(tmp_path):
    original_enabled = settings.classifier_enabled
    original_path = settings.classifier_path
    try:
        model_file = tmp_path / "trusted.joblib"
        manifest_file = tmp_path / "trusted.manifest.json"
        joblib.dump(DummyClassifier(), model_file)
        valid_sha = hashlib.sha256(model_file.read_bytes()).hexdigest()

        manifest_file.write_text(
            json.dumps(
                {
                    "model_version": "candidate-lr-v1",
                    "feature_schema": FEATURE_SCHEMA,
                    "feature_names": FEATURE_NAMES,
                    "embedding_model": settings.embedding_model,
                    "reranker_model": settings.reranker_model,
                    "artifact_sha256": "0" * 64,
                    "threshold": 0.65,
                }
            ),
            encoding="utf-8",
        )
        settings.classifier_enabled = True
        settings.classifier_path = model_file
        load_classifier.cache_clear()
        with pytest.raises(RuntimeError, match="CLASSIFIER_ARTIFACT_HASH_MISMATCH"):
            load_classifier()

        manifest_file.write_text(
            json.dumps(
                {
                    "model_version": "candidate-lr-v1",
                    "feature_schema": FEATURE_SCHEMA,
                    "feature_names": FEATURE_NAMES,
                    "embedding_model": settings.embedding_model,
                    "reranker_model": settings.reranker_model,
                    "artifact_sha256": valid_sha,
                    "threshold": 0.65,
                }
            ),
            encoding="utf-8",
        )
        load_classifier.cache_clear()
        _, loaded_manifest = load_classifier()
        assert loaded_manifest["artifact_sha256"] == valid_sha
    finally:
        settings.classifier_enabled = original_enabled
        settings.classifier_path = original_path
        load_classifier.cache_clear()
