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


def test_packaged_migrations_preserve_existing_database_checksums():
    # Historical migrations are immutable, including their trailing newlines.
    applied = {
        "001_init": "a60f5c48a0f9bf66b70cc26f86fb28c9afc6b09f6093ba81e148d2d164c902e1",
        "002_corpus_family_and_permissions": "13fa6e00200419244708c5b1cf5ce757bf9832f9a24291234830e456b54d0ab4",
        "003_job_lifecycle_resilience": "5df48dcf6961c881b3fa47cf73351b0f4ebf34d4d0badcbc010066bb0d60840f",
        "004_outbox_reconciler_and_storage_durability": "11da45f31553ae02589d373d2fe8be25992223e15eb8fb30b4e922a7a3d6bdd4",
        "005_fts_lexical_index": "d33f22eb04e7aaa074173aaa859ad276aaddb5524e33a397c2e703e32637e538",
    }
    for path in check_schema.SQL_DIR.glob("*.sql"):
        if path.stem in applied:
            assert sha256(path.read_bytes()).hexdigest() == applied[path.stem], path.name


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
