"""Tests for CP-05.2: DB Commit-Enqueue Gap, Outbox Reconciler & Storage Durability.

Covers:
1. Safe enqueue pattern & graceful broker failure handling.
2. Reconciler recovery of orphaned queued jobs (DB committed, broker dropped).
3. Reconciler recovery of overdue waiting_for_compute jobs (broker/worker restart).
4. Reconciler REST API endpoint (POST /api/v1/jobs/reconcile).
5. Database constraint: at most one active source version per document in corpus.
6. Active versioning: older version reindex does not supersede newer active version.
7. Storage download permanent errors (file not found, checksum mismatch).
8. Storage download transient errors (retry with exponential backoff).
"""

from __future__ import annotations

import uuid
from datetime import datetime, timedelta, timezone
from unittest.mock import MagicMock, patch

import psycopg
from psycopg.errors import UniqueViolation
from psycopg.types.json import Jsonb
import pytest
import requests
from fastapi.testclient import TestClient

from app.config import settings
from app.db import _get_connection, connect, get_job
from app.main import app
from app.reconciler import (
    mark_job_enqueued,
    reconcile_pending_jobs,
    safe_enqueue_job,
)
from app.storage import StorageError, StorageFileNotFoundError
from app.tasks import StorageTransientError, index_source, scan_document


@pytest.fixture(scope="module")
def db_conn():
    con = connect()
    con.autocommit = True
    try:
        yield con
    finally:
        con.close()


@pytest.fixture
def client():
    return TestClient(app)


@pytest.fixture
def auth_headers():
    return {"X-API-Key": settings.api_key}


def _create_raw_job(
    *,
    status: str = "queued",
    job_type: str = "scan",
    corpus_id: str = "uit_thesis",
    document_key: str = "doc_test_rec",
    version: int = 1,
    enqueued_at: datetime | None = None,
    reconciled_at: datetime | None = None,
    retry_after: datetime | None = None,
    attempt: int = 1,
    con=None,
) -> str:
    jid = str(uuid.uuid4())
    with _get_connection(con) as c:
        c.execute(
            """
            INSERT INTO integrity.jobs
              (id, job_type, corpus_id, document_key, version,
               filename, file_path, file_sha256, status,
               enqueued_at, reconciled_at, retry_after, attempt)
            VALUES (%s::uuid, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            """,
            (
                jid,
                job_type,
                corpus_id,
                f"{document_key}_{jid[:8]}",
                version,
                "test.pdf",
                "uploads/test.pdf",
                "0" * 64,
                status,
                enqueued_at,
                reconciled_at,
                retry_after,
                attempt,
            ),
        )
    return jid


def _delete_job(job_id: str, con=None):
    with _get_connection(con) as c:
        c.execute("DELETE FROM integrity.jobs WHERE id = %s::uuid", (job_id,))


# ==============================================================================
# 1. Safe Enqueue Pattern
# ==============================================================================


def test_safe_enqueue_job_success(db_conn):
    """Verify safe_enqueue_job dispatches to Celery and records enqueued_at."""
    jid = _create_raw_job(status="queued", job_type="scan", con=db_conn)
    try:
        with patch("app.tasks.scan_document.delay") as mock_delay:
            ok = safe_enqueue_job(jid, "scan", con=db_conn)
            assert ok is True
            assert mock_delay.called

        job = get_job(jid, con=db_conn)
        assert job["enqueued_at"] is not None
    finally:
        _delete_job(jid, con=db_conn)


def test_safe_enqueue_job_broker_failure_graceful(db_conn):
    """Verify broker connection failure does not crash or fail the job; leaves it for reconciler."""
    jid = _create_raw_job(status="queued", job_type="scan", con=db_conn)
    try:
        with patch(
            "app.tasks.scan_document.delay",
            side_effect=ConnectionError("Redis broker unreachable"),
        ):
            ok = safe_enqueue_job(jid, "scan", con=db_conn)
            # Must return False without re-raising exception
            assert ok is False

        # Job is intact in DB, status is still 'queued', enqueued_at is None
        job = get_job(jid, con=db_conn)
        assert job["status"] == "queued"
        assert job["enqueued_at"] is None
    finally:
        _delete_job(jid, con=db_conn)


# ==============================================================================
# 2. Reconciler Engine: Orphaned Queued Jobs & Overdue Waiting Jobs
# ==============================================================================


def test_reconciler_rescues_orphaned_queued_jobs(db_conn):
    """Verify reconciler finds jobs stuck in commit-enqueue gap and dispatches them."""
    j1 = _create_raw_job(status="queued", job_type="scan", enqueued_at=None, con=db_conn)
    j2 = _create_raw_job(status="queued", job_type="index", enqueued_at=None, con=db_conn)
    try:
        with patch("app.tasks.scan_document.apply_async") as mock_scan, \
             patch("app.tasks.index_source.apply_async") as mock_index:

            res = reconcile_pending_jobs(queued_timeout_seconds=0, limit=50, con=db_conn)
            assert res["reconciled_count"] >= 2
            rec_ids = {r["job_id"] for r in res["reconciled_jobs"]}
            assert j1 in rec_ids
            assert j2 in rec_ids

            assert mock_scan.called
            assert mock_index.called

        job1_db = get_job(j1, con=db_conn)
        assert job1_db["enqueued_at"] is not None
        assert job1_db["reconciled_at"] is not None
    finally:
        _delete_job(j1, con=db_conn)
        _delete_job(j2, con=db_conn)


def test_reconciler_rescues_overdue_waiting_jobs(db_conn):
    """Verify reconciler re-dispatches waiting_for_compute jobs whose retry_after has elapsed."""
    past_time = datetime.now(timezone.utc) - timedelta(seconds=10)
    j_waiting = _create_raw_job(
        status="waiting_for_compute",
        job_type="scan",
        retry_after=past_time,
        attempt=2,
        con=db_conn,
    )
    try:
        with patch("app.tasks.scan_document.apply_async") as mock_scan:
            res = reconcile_pending_jobs(queued_timeout_seconds=3600, limit=50, con=db_conn)
            rec_ids = {r["job_id"] for r in res["reconciled_jobs"]}
            assert j_waiting in rec_ids
            assert mock_scan.called

        job_db = get_job(j_waiting, con=db_conn)
        assert job_db["reconciled_at"] is not None
    finally:
        _delete_job(j_waiting, con=db_conn)


def test_reconciler_handles_broker_error_gracefully(db_conn):
    """Verify reconciler records failed jobs if broker is still down, without crashing."""
    j = _create_raw_job(status="queued", job_type="scan", enqueued_at=None, con=db_conn)
    try:
        with patch(
            "app.tasks.scan_document.apply_async",
            side_effect=ConnectionError("Redis still down"),
        ):
            res = reconcile_pending_jobs(queued_timeout_seconds=0, limit=50, con=db_conn)
            assert res["failed_count"] >= 1
            fail_ids = {f["job_id"] for f in res["failed_jobs"]}
            assert j in fail_ids

        job_db = get_job(j, con=db_conn)
        assert job_db["reconciled_at"] is not None
        assert job_db["enqueued_at"] is None
    finally:
        _delete_job(j, con=db_conn)


# ==============================================================================
# 3. REST API Endpoint: POST /api/v1/jobs/reconcile
# ==============================================================================


def test_reconcile_api_endpoint(client, auth_headers, monkeypatch):
    """Verify POST /api/v1/jobs/reconcile invokes reconciler and returns schema."""
    fake_summary = {
        "reconciled_count": 1,
        "failed_count": 0,
        "reconciled_jobs": [{"job_id": "test-id", "job_type": "scan", "reason": "queued_gap"}],
        "failed_jobs": [],
    }
    monkeypatch.setattr("app.reconciler.reconcile_pending_jobs", lambda **kw: fake_summary)

    resp = client.post("/api/v1/jobs/reconcile?queued_timeout_seconds=30&limit=10", headers=auth_headers)
    assert resp.status_code == 200
    data = resp.json()
    assert data["reconciled_count"] == 1
    assert data["reconciled_jobs"][0]["job_id"] == "test-id"


# ==============================================================================
# 4. Database Constraint: Single Active Source Version
# ==============================================================================


def test_single_active_source_constraint_in_db(db_conn):
    """Verify PostgreSQL partial unique index prevents two active versions of same source."""
    corpus = "rec_test_corpus"
    skey = f"doc_{uuid.uuid4().hex[:8]}"

    with _get_connection(db_conn) as c:
        # 1. Insert version 1 as active
        r1 = c.execute(
            """
            INSERT INTO integrity.sources
              (corpus_id, source_key, version, filename, file_sha256, is_active)
            VALUES (%s, %s, 1, 'v1.pdf', '0'*64, TRUE)
            RETURNING id
            """,
            (corpus, skey),
        ).fetchone()
        id1 = r1["id"]

        try:
            # 2. Insert version 2 as inactive -> OK
            r2 = c.execute(
                """
                INSERT INTO integrity.sources
                  (corpus_id, source_key, version, filename, file_sha256, is_active)
                VALUES (%s, %s, 2, 'v2.pdf', '1'*64, FALSE)
                RETURNING id
                """,
                (corpus, skey),
            ).fetchone()
            id2 = r2["id"]

            # 3. Attempt to set version 2 active while version 1 is active -> MUST FAIL
            with pytest.raises(UniqueViolation):
                c.execute(
                    "UPDATE integrity.sources SET is_active = TRUE WHERE id = %s",
                    (id2,),
                )

        finally:
            c.execute("DELETE FROM integrity.sources WHERE corpus_id = %s", (corpus,))


# ==============================================================================
# 5. Active Versioning Protection in Task Logic
# ==============================================================================


def test_active_versioning_prevents_older_version_regression(db_conn):
    """Verify reindexing an older version (e.g. v1) does not deactivate a newer version (v2)."""
    corpus = "rec_test_corpus_v"
    skey = f"doc_{uuid.uuid4().hex[:8]}"

    with _get_connection(db_conn) as c:
        # Pre-seed version 2 as active
        c.execute(
            """
            INSERT INTO integrity.sources
              (corpus_id, source_key, version, filename, file_sha256, is_active)
            VALUES (%s, %s, 2, 'v2.pdf', '2'*64, TRUE)
            """,
            (corpus, skey),
        )

    try:
        # Simulate index_source for version 1
        fake_job = {
            "id": str(uuid.uuid4()),
            "job_type": "index",
            "corpus_id": corpus,
            "document_key": skey,
            "version": 1,
            "filename": "v1.pdf",
            "file_sha256": "1" * 64,
            "status": "queued",
            "attempt": 1,
            "max_attempts": 3,
        }

        with patch("app.tasks.get_job", return_value=fake_job), \
             patch("app.tasks.transition_job_status", return_value=True), \
             patch("app.tasks.record_job_heartbeat", return_value=True), \
             patch("app.tasks.record_job_checkpoint", return_value=True), \
             patch("app.tasks.parse_job", return_value=[MagicMock(chunk_no=1, page_number=1, start_offset=0, end_offset=10, text="test")]), \
             patch("app.models.encode_texts", return_value=[[0.01]*1024]):

            index_source(fake_job["id"])

        # Check DB: Version 2 must STILL be active! Version 1 must be inactive.
        with _get_connection(db_conn) as c:
            rows = c.execute(
                """
                SELECT version, is_active FROM integrity.sources
                WHERE corpus_id = %s AND source_key = %s
                ORDER BY version ASC
                """,
                (corpus, skey),
            ).fetchall()

            assert len(rows) == 2
            v1_row = next(r for r in rows if r["version"] == 1)
            v2_row = next(r for r in rows if r["version"] == 2)

            assert v1_row["is_active"] is False
            assert v2_row["is_active"] is True
    finally:
        with _get_connection(db_conn) as c:
            c.execute("DELETE FROM integrity.sources WHERE corpus_id = %s", (corpus,))


# ==============================================================================
# 6. Storage Durability & Download Error Classification
# ==============================================================================


def test_storage_permanent_errors(monkeypatch):
    """Verify permanent storage errors (file not found, checksum mismatch) fail immediately without retry."""
    jid = str(uuid.uuid4())
    fake_job = {
        "id": jid,
        "job_type": "scan",
        "corpus_id": "uit_thesis",
        "document_key": "doc_storage_test",
        "version": 1,
        "filename": "missing.pdf",
        "file_path": "uploads/missing.pdf",
        "file_sha256": "0" * 64,
        "status": "queued",
        "attempt": 1,
        "max_attempts": 5,
    }

    status_records = []
    monkeypatch.setattr("app.tasks.get_job", lambda j_id: fake_job)
    monkeypatch.setattr("app.tasks.transition_job_status", lambda *a, **kw: True)
    monkeypatch.setattr("app.tasks.record_job_heartbeat", lambda *a, **kw: True)
    monkeypatch.setattr("app.tasks.set_status", lambda j_id, st, code: status_records.append((st, code)))

    mock_con = MagicMock()
    mock_con.execute.return_value.fetchone.return_value = {"n": 5}
    monkeypatch.setattr("app.tasks.connect", lambda *a, **kw: MagicMock(__enter__=lambda s: mock_con, __exit__=lambda *a: None))

    # 1. StorageFileNotFoundError -> STORAGE_FILE_NOT_FOUND
    with patch("app.tasks.parse_job", side_effect=ValueError("STORAGE_FILE_NOT_FOUND")), \
         patch("app.tasks.scan_document.apply_async") as mock_retry:

        scan_document(jid)
        assert len(status_records) == 1
        assert status_records[0] == ("failed", "STORAGE_FILE_NOT_FOUND")
        assert not mock_retry.called

    # 2. Checksum mismatch -> FILE_CHECKSUM_MISMATCH
    status_records.clear()
    with patch("app.tasks.parse_job", side_effect=ValueError("FILE_CHECKSUM_MISMATCH")), \
         patch("app.tasks.scan_document.apply_async") as mock_retry:

        scan_document(jid)
        assert len(status_records) == 1
        assert status_records[0] == ("failed", "FILE_CHECKSUM_MISMATCH")
        assert not mock_retry.called


def test_storage_transient_error_backoff_retry(monkeypatch):
    """Verify transient download network errors trigger schedule_waiting_for_compute with backoff."""
    jid = str(uuid.uuid4())
    fake_job = {
        "id": jid,
        "job_type": "scan",
        "corpus_id": "uit_thesis",
        "document_key": "doc_transient_test",
        "version": 1,
        "filename": "retry.pdf",
        "file_path": "uploads/retry.pdf",
        "file_sha256": "0" * 64,
        "status": "queued",
        "attempt": 1,
        "max_attempts": 5,
    }

    scheduled = []
    monkeypatch.setattr("app.tasks.get_job", lambda j_id: fake_job)
    monkeypatch.setattr("app.tasks.transition_job_status", lambda *a, **kw: True)
    monkeypatch.setattr("app.tasks.record_job_heartbeat", lambda *a, **kw: True)
    monkeypatch.setattr(
        "app.tasks.schedule_waiting_for_compute",
        lambda j_id, **kw: scheduled.append(kw) or {"action": "retry", "attempt": 2, "backoff_seconds": 15.0},
    )

    mock_con = MagicMock()
    mock_con.execute.return_value.fetchone.return_value = {"n": 5}
    monkeypatch.setattr("app.tasks.connect", lambda *a, **kw: MagicMock(__enter__=lambda s: mock_con, __exit__=lambda *a: None))

    with patch("app.tasks.parse_job", side_effect=StorageTransientError("Timeout reaching S3 bucket")), \
         patch("app.tasks.scan_document.apply_async") as mock_retry:

        scan_document(jid)

        assert len(scheduled) == 1
        assert scheduled[0]["error_code"] == "STORAGE_TRANSIENT_ERROR"
        assert scheduled[0]["checkpoint"] == {"phase": "download_failed"}
        assert mock_retry.called
        assert mock_retry.call_args[1]["countdown"] == 15

