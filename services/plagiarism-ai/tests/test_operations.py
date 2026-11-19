from unittest.mock import MagicMock

import pytest

from scripts import production_ops


def test_drain_requires_exactly_one_worker_and_never_discards_tasks(monkeypatch):
    from app.celery_app import celery_app

    control = MagicMock()
    control.inspect.return_value.ping.return_value = {"worker": {"ok": "pong"}}
    control.cancel_consumer.return_value = [{"worker": {"ok": "paused"}}]
    control.inspect.return_value.active.return_value = {"worker": []}
    monkeypatch.setattr(celery_app, "control", control)
    production_ops.drain(1, "worker")
    control.inspect.assert_any_call(destination=["worker"], timeout=5)
    control.cancel_consumer.assert_called_once()
    control.purge.assert_not_called()
    control.revoke.assert_not_called()


def test_drain_resumes_consumption_when_worker_does_not_acknowledge(monkeypatch):
    from app.celery_app import celery_app

    control = MagicMock()
    control.inspect.return_value.ping.return_value = {"worker": {"ok": "pong"}}
    control.cancel_consumer.return_value = []
    monkeypatch.setattr(celery_app, "control", control)
    with pytest.raises(RuntimeError):
        production_ops.drain(1, "worker")
    control.add_consumer.assert_called_once()


def test_drain_refuses_multiple_workers(monkeypatch):
    from app.celery_app import celery_app

    control = MagicMock()
    control.inspect.return_value.ping.return_value = {"worker1": {}, "worker2": {}}
    monkeypatch.setattr(celery_app, "control", control)
    with pytest.raises(RuntimeError):
        production_ops.drain(1, "worker1")
    control.cancel_consumer.assert_not_called()


def test_a_responsive_development_worker_cannot_substitute_for_production(monkeypatch):
    from app.celery_app import celery_app

    control = MagicMock()
    control.inspect.return_value.ping.return_value = {"development-worker": {"ok": "pong"}}
    monkeypatch.setattr(celery_app, "control", control)
    with pytest.raises(RuntimeError):
        production_ops.drain(1, "production-worker")
    control.cancel_consumer.assert_not_called()


def test_classifier_fingerprint_does_not_deserialize_untrusted_model_bytes(tmp_path):
    import hashlib

    for name in (
        "suspicious_passage_classifier.joblib",
        "suspicious_passage_classifier.manifest.json",
    ):
        (tmp_path / name).write_bytes(b"opaque-test-bytes")
    fingerprints = production_ops.classifier_fingerprint(tmp_path)
    assert len(fingerprints) == 2
    assert all(
        value == hashlib.sha256(b"opaque-test-bytes").hexdigest()
        for value in fingerprints.values()
    )


def test_backup_object_keys_cannot_escape_the_backup_directory(tmp_path, monkeypatch):
    import boto3
    import io
    import json

    client = MagicMock()
    client.get_paginator.return_value.paginate.return_value = [
        {"Contents": [{"Key": "../../outside", "Size": 4, "ETag": "etag"}]}
    ]
    client.get_object.return_value = {
        "Body": MagicMock(iter_chunks=lambda size: iter([b"data"]))
    }
    monkeypatch.setattr(boto3, "client", lambda *args, **kwargs: client)
    monkeypatch.setenv("BACKUP_MINIO_USER", "test")
    monkeypatch.setenv("BACKUP_MINIO_PASSWORD", "test-only")
    monkeypatch.setenv("BACKUP_BUCKETS", "test-bucket")
    production_ops.backup_objects(tmp_path)
    record = json.loads((tmp_path / "objects.json").read_text())[0]
    assert len(record["file"]) == 64
    assert (tmp_path / record["file"]).read_bytes() == b"data"
    client.get_object.assert_called_once_with(
        Bucket="test-bucket", Key="../../outside", IfMatch="etag"
    )
