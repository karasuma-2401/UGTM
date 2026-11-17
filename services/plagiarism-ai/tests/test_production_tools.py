from hashlib import sha256
from threading import Event
from unittest.mock import MagicMock

from scripts import check_schema, check_storage, run_reconciler


def test_schema_preflight_rejects_pending_modified_and_future_migrations(tmp_path):
    migration = tmp_path / "001_init.sql"
    migration.write_bytes(b"SELECT 1;")
    checksum = sha256(migration.read_bytes()).hexdigest()
    assert check_schema.schema_errors({"001_init": checksum}, tmp_path) == []
    assert "Pending migration" in check_schema.schema_errors({}, tmp_path)[0]
    assert "checksum mismatch" in check_schema.schema_errors({"001_init": "wrong"}, tmp_path)[0]
    assert "outside this release" in check_schema.schema_errors(
        {"001_init": checksum, "002_future": "hash"}, tmp_path
    )[0]


def test_schema_preflight_requires_packaged_sql(tmp_path):
    assert check_schema.schema_errors({}, tmp_path)


def test_reconciler_retries_an_outage_and_stops(monkeypatch, caplog):
    stop = Event()
    attempts = []

    def reconcile():
        attempts.append(1)
        if len(attempts) == 1:
            raise RuntimeError("private-database-credential")
        stop.set()
        return {"reconciled_count": 1, "failed_count": 0}

    monkeypatch.setattr(run_reconciler, "reconcile_pending_jobs", reconcile)
    run_reconciler.run(stop, interval=0)
    assert len(attempts) == 2
    assert "private-database-credential" not in caplog.text


def test_storage_preflight_waits_for_services_without_writing_objects(monkeypatch):
    broker = MagicMock()
    broker.ping.side_effect = [ConnectionError("starting"), True]
    storage = MagicMock()
    monkeypatch.setattr(check_storage.redis.Redis, "from_url", lambda *args, **kwargs: broker)
    monkeypatch.setattr(check_storage, "get_storage", lambda: storage)
    assert check_storage.main(attempts=2, delay=0) == 0
    storage.client.head_bucket.assert_called_once_with(Bucket=storage.bucket)
    storage.client.put_object.assert_not_called()


def test_storage_preflight_fails_without_logging_credentials(monkeypatch, caplog):
    def unavailable(*args, **kwargs):
        raise RuntimeError("private-storage-password")

    monkeypatch.setattr(check_storage.redis.Redis, "from_url", unavailable)
    assert check_storage.main(attempts=1) == 1
    assert "private-storage-password" not in caplog.text
