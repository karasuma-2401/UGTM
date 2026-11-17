from __future__ import annotations

import uuid
from datetime import datetime, timedelta, timezone
from unittest.mock import MagicMock, patch

import pytest
from fastapi.testclient import TestClient
from psycopg import Connection
from psycopg.types.json import Jsonb

from app.config import settings
from app.db import (
    VALID_JOB_STATUSES,
    can_transition,
    cancel_job,
    connect,
    get_job,
    reclaim_stale_jobs,
    record_job_checkpoint,
    record_job_heartbeat,
    schedule_waiting_for_compute,
    transition_job_status,
)
from app.documents import Chunk
from app.inference import InferenceError
from app.main import app
from app.tasks import index_source, scan_document


@pytest.fixture(scope="module")
def auth_headers():
    return {"X-API-Key": settings.api_key}


@pytest.fixture(scope="module")
def client():
    return TestClient(app)


@pytest.fixture
def db_conn():
    with connect(autocommit=True) as con:
        yield con


def _create_raw_job(
    status: str = "queued",
    attempt: int = 1,
    max_attempts: int = 5,
    last_heartbeat: datetime | None = None,
    checkpoint: dict | None = None,
    job_type: str = "scan",
    corpus_id: str = "uit_thesis",
    con: Connection | None = None,
) -> str:
    jid = str(uuid.uuid4())
    doc_key = f"resilience_{jid[:8]}"

    def _do_insert(c):
        c.execute(
            """
            INSERT INTO integrity.jobs
              (id, job_type, corpus_id, document_key, version,
               filename, file_path, file_sha256, status, attempt,
               max_attempts, last_heartbeat, checkpoint)
            VALUES (%s::uuid, %s, %s, %s, 1,
                    'test.pdf', '/tmp/test.pdf', %s, %s, %s, %s, %s, %s)
            """,
            (
                jid,
                job_type,
                corpus_id,
                doc_key,
                "0" * 64,
                status,
                attempt,
                max_attempts,
                last_heartbeat,
                Jsonb(checkpoint or {}),
            ),
        )

    if con is not None:
        _do_insert(con)
    else:
        with connect(autocommit=True) as c:
            _do_insert(c)
    return jid


def _delete_job(jid: str, con: Connection | None = None):
    if con is not None:
        con.execute("DELETE FROM integrity.jobs WHERE id = %s::uuid", (jid,))
    else:
        with connect(autocommit=True) as c:
            c.execute("DELETE FROM integrity.jobs WHERE id = %s::uuid", (jid,))


# ==============================================================================
# 1. Finite State Machine (FSM) Transition Rules
# ==============================================================================


def test_fsm_valid_and_invalid_transitions():
    """Verify strictly defined valid transitions and rejection of invalid/terminal transitions."""
    assert len(VALID_JOB_STATUSES) == 6
    assert set(VALID_JOB_STATUSES) == {
        "queued",
        "processing",
        "waiting_for_compute",
        "completed",
        "failed",
        "cancelled",
    }

    # Valid transitions
    assert can_transition("queued", "processing") is True
    assert can_transition("queued", "cancelled") is True
    assert can_transition("queued", "failed") is True
    assert can_transition("processing", "waiting_for_compute") is True
    assert can_transition("processing", "completed") is True
    assert can_transition("processing", "failed") is True
    assert can_transition("processing", "cancelled") is True
    assert can_transition("waiting_for_compute", "processing") is True
    assert can_transition("waiting_for_compute", "queued") is True
    assert can_transition("waiting_for_compute", "failed") is True
    assert can_transition("waiting_for_compute", "cancelled") is True

    # Invalid transitions
    assert can_transition("queued", "completed") is False
    assert can_transition("queued", "waiting_for_compute") is False
    assert can_transition("waiting_for_compute", "completed") is False

    # Terminal Invariants (zero outward transitions to other states)
    for terminal in ("completed", "failed", "cancelled"):
        for target in VALID_JOB_STATUSES:
            if target != terminal:
                assert can_transition(terminal, target) is False, f"Illegal transition from terminal {terminal} -> {target}"


# ==============================================================================
# 2. Database Job Transition Execution
# ==============================================================================


def test_transition_job_status_db(db_conn):
    """Verify transition_job_status enforces state machine rules in PostgreSQL."""
    jid = _create_raw_job(status="queued", con=db_conn)
    try:
        # Valid transition: queued -> processing
        ok = transition_job_status(jid, "processing", from_status=["queued"], worker_id="test-w1", con=db_conn)
        assert ok is True
        j = get_job(jid, con=db_conn)
        assert j["status"] == "processing"
        assert j["worker_id"] == "test-w1"

        # Invalid transition: processing -> queued returns False
        ok_invalid = transition_job_status(jid, "queued", con=db_conn)
        assert ok_invalid is False
        j = get_job(jid, con=db_conn)
        assert j["status"] == "processing"

        # Wrong from_status precondition
        ok2 = transition_job_status(jid, "completed", from_status=["queued"], con=db_conn)
        assert ok2 is False
        j = get_job(jid, con=db_conn)
        assert j["status"] == "processing"
    finally:
        _delete_job(jid, con=db_conn)


# ==============================================================================
# 3. Exponential Backoff and Attempt Exhaustion
# ==============================================================================


def test_schedule_waiting_for_compute(db_conn):
    """Verify exponential backoff calculation and transition to failed upon attempt exhaustion."""
    jid = _create_raw_job(status="processing", attempt=1, max_attempts=3, con=db_conn)
    try:
        # Attempt 1 -> 2 (base 10s backoff)
        res1 = schedule_waiting_for_compute(
            jid,
            error_code="GPU_UNAVAILABLE",
            checkpoint={"phase": "parsed", "chunks_count": 5},
            base_backoff_seconds=10.0,
            con=db_conn,
        )
        assert res1["action"] == "retry"
        assert res1["attempt"] == 2
        assert res1["backoff_seconds"] == 10.0

        # Attempt 2 -> 3 (20s backoff)
        res2 = schedule_waiting_for_compute(
            jid,
            error_code="COLAB_DISCONNECTED",
            base_backoff_seconds=10.0,
            con=db_conn,
        )
        assert res2["action"] == "retry"
        assert res2["attempt"] == 3
        assert res2["backoff_seconds"] == 20.0

        # Attempt 3 -> Exhausted (since current attempt 3 >= max_attempts 3)
        res3 = schedule_waiting_for_compute(
            jid,
            error_code="GPU_TIMEOUT",
            base_backoff_seconds=10.0,
            con=db_conn,
        )
        assert res3["action"] == "failed"
        assert res3["error_code"] == "GPU_UNAVAILABLE_EXHAUSTED"

        # Final DB verification
        j_final = get_job(jid, con=db_conn)
        assert j_final["status"] == "failed"
        assert j_final["error_code"] == "GPU_UNAVAILABLE_EXHAUSTED"
        assert j_final["attempt"] == 3
        assert j_final["checkpoint"]["phase"] == "parsed"
    finally:
        _delete_job(jid, con=db_conn)


# ==============================================================================
# 4. Heartbeat and Stale Job Reclamation
# ==============================================================================


def test_reclaim_stale_jobs(db_conn):
    """Verify stale processing jobs with expired heartbeats are automatically reclaimed or failed."""
    old_time = datetime.now(timezone.utc) - timedelta(seconds=400)
    fresh_time = datetime.now(timezone.utc) - timedelta(seconds=10)

    # Job A: stale, attempt 1 -> should become waiting_for_compute (attempt 2)
    j_a = _create_raw_job(status="processing", attempt=1, max_attempts=3, last_heartbeat=old_time, con=db_conn)
    # Job B: stale, attempt 3 >= max_attempts -> should become failed
    j_b = _create_raw_job(status="processing", attempt=3, max_attempts=3, last_heartbeat=old_time, con=db_conn)
    # Job C: fresh heartbeat -> should remain processing
    j_c = _create_raw_job(status="processing", attempt=1, max_attempts=3, last_heartbeat=fresh_time, con=db_conn)

    try:
        reclaimed = reclaim_stale_jobs(stale_seconds=120, con=db_conn)
        reclaimed_ids = {r["job_id"] for r in reclaimed}

        assert j_a in reclaimed_ids
        assert j_b in reclaimed_ids
        assert j_c not in reclaimed_ids

        job_a_db = get_job(j_a, con=db_conn)
        assert job_a_db["status"] == "waiting_for_compute"
        assert job_a_db["error_code"] == "WORKER_HEARTBEAT_TIMEOUT"
        assert job_a_db["attempt"] == 2

        job_b_db = get_job(j_b, con=db_conn)
        assert job_b_db["status"] == "failed"
        assert job_b_db["error_code"] == "WORKER_HEARTBEAT_TIMEOUT_EXHAUSTED"

        job_c_db = get_job(j_c, con=db_conn)
        assert job_c_db["status"] == "processing"
    finally:
        _delete_job(j_a, con=db_conn)
        _delete_job(j_b, con=db_conn)
        _delete_job(j_c, con=db_conn)


# ==============================================================================
# 5. Job Cancellation DB Logic & API Endpoints
# ==============================================================================


def test_cancel_job_db_logic(db_conn):
    """Verify cancel_job function on DB for non-terminal and terminal states."""
    j_active = _create_raw_job(status="waiting_for_compute", con=db_conn)
    try:
        # Cancel active job -> True
        assert cancel_job(j_active, reason="OPERATOR_STOP", con=db_conn) is True
        assert get_job(j_active, con=db_conn)["status"] == "cancelled"

        # Cancel already cancelled job -> False
        assert cancel_job(j_active, reason="AGAIN", con=db_conn) is False
    finally:
        _delete_job(j_active, con=db_conn)


def test_cancel_job_api_endpoints(client, auth_headers, monkeypatch):
    """Verify POST /api/v1/jobs/{job_id}/cancel endpoint responses (200, 409, 404)."""
    fake_id = str(uuid.uuid4())

    # 1. Success cancel -> 200 OK
    monkeypatch.setattr("app.main.cancel_job", lambda j_id, reason="USER_CANCELLED": True)
    resp = client.post(f"/api/v1/jobs/{fake_id}/cancel?reason=OPERATOR_STOP", headers=auth_headers)
    assert resp.status_code == 200
    assert resp.json()["status"] == "cancelled"
    assert resp.json()["reason"] == "OPERATOR_STOP"

    # 2. Terminal state conflict -> 409 Conflict
    monkeypatch.setattr("app.main.cancel_job", lambda j_id, reason="USER_CANCELLED": False)
    monkeypatch.setattr("app.main.get_job", lambda j_id: {"id": fake_id, "status": "completed"})
    resp_term = client.post(f"/api/v1/jobs/{fake_id}/cancel", headers=auth_headers)
    assert resp_term.status_code == 409
    assert "Cannot cancel job in terminal state" in resp_term.json()["detail"]

    # 3. Job not found -> 404 Not Found
    monkeypatch.setattr("app.main.get_job", lambda j_id: None)
    resp_404 = client.post(f"/api/v1/jobs/{fake_id}/cancel", headers=auth_headers)
    assert resp_404.status_code == 404


# ==============================================================================
# 6. Fault Injection & Task Recovery (Scan Document)
# ==============================================================================


def test_scan_task_gpu_disconnect_and_resume(monkeypatch):
    """Simulate transient GPU disconnection during encode/rerank, verifying backoff and resume."""
    jid = str(uuid.uuid4())
    fake_job = {
        "id": jid,
        "job_type": "scan",
        "corpus_id": "uit_thesis",
        "document_key": "resilience_doc_01",
        "version": 1,
        "filename": "test.pdf",
        "file_path": "/tmp/test.pdf",
        "file_sha256": "0" * 64,
        "status": "queued",
        "attempt": 1,
        "max_attempts": 5,
        "checkpoint": None,
    }

    transitions = []
    scheduled = []

    monkeypatch.setattr("app.tasks.get_job", lambda j_id: fake_job)
    monkeypatch.setattr(
        "app.tasks.transition_job_status",
        lambda j_id, target, **kw: transitions.append((target, kw)) or True,
    )
    monkeypatch.setattr("app.tasks.record_job_heartbeat", lambda *a, **kw: True)
    monkeypatch.setattr("app.tasks.record_job_checkpoint", lambda *a, **kw: True)
    monkeypatch.setattr(
        "app.tasks.schedule_waiting_for_compute",
        lambda j_id, **kw: scheduled.append(kw) or {
            "action": "retry",
            "attempt": 2,
            "backoff_seconds": 10.0,
        },
    )

    fake_chunks = [
        Chunk(
            chunk_no=1,
            page_number=1,
            start_offset=0,
            end_offset=30,
            text="Trí tuệ nhân tạo phát triển mạnh mẽ.",
        )
    ]

    mock_con = MagicMock()
    mock_con.execute.return_value.fetchone.return_value = {"n": 5}
    monkeypatch.setattr("app.tasks.connect", lambda *a, **kw: MagicMock(__enter__=lambda s: mock_con, __exit__=lambda *a: None))

    # 1. Transient GPU failure during encode
    with patch("app.tasks.parse_job", return_value=fake_chunks), \
         patch("app.models.encode_texts", side_effect=InferenceError("Colab GPU offline", error_code="GPU_UNAVAILABLE")), \
         patch("app.tasks.scan_document.apply_async") as mock_retry:

        scan_document(jid)

        assert len(scheduled) == 1
        assert scheduled[0]["error_code"] == "GPU_UNAVAILABLE"
        assert scheduled[0]["checkpoint"]["phase"] == "parsed"
        assert mock_retry.called
        assert mock_retry.call_args[1]["countdown"] == 10

    # 2. Transient GPU failure during rerank
    fake_vector = [0.05] * 1024
    fake_candidate = {
        "chunk_id": 1,
        "source_id": "src_1",
        "source_key": "ref_doc_01",
        "source_version": 1,
        "source_filename": "ref.pdf",
        "source_chunk_no": 1,
        "source_page": 1,
        "source_start": 0,
        "source_end": 35,
        "source_text": "Trí tuệ nhân tạo đang phát triển rất mạnh mẽ.",
        "dense_score": 0.88,
        "lexical_score": 0.85,
    }

    with patch("app.tasks.parse_job", return_value=fake_chunks), \
         patch("app.models.encode_texts", return_value=[fake_vector]), \
         patch("app.search.retrieve", return_value=[fake_candidate]), \
         patch("app.models.rerank_pairs", side_effect=InferenceError("Colab GPU disconnected", error_code="GPU_UNAVAILABLE")), \
         patch("app.tasks.scan_document.apply_async") as mock_retry:

        scan_document(jid)

        assert len(scheduled) == 2
        assert scheduled[1]["error_code"] == "GPU_UNAVAILABLE"
        assert scheduled[1]["checkpoint"]["phase"] == "retrieved"
        assert mock_retry.called

    # 3. GPU reconnection -> Successful completion
    with patch("app.tasks.parse_job", return_value=fake_chunks), \
         patch("app.models.encode_texts", return_value=[fake_vector]), \
         patch("app.search.retrieve", return_value=[fake_candidate]), \
         patch("app.models.rerank_pairs", return_value=[0.92]):

        scan_document(jid)

        update_calls = [
            c for c in mock_con.execute.call_args_list
            if "status = 'completed'" in str(c)
        ]
        assert len(update_calls) > 0


# ==============================================================================
# 7. Permanent Failure Does Not Retry
# ==============================================================================


def test_permanent_error_no_retry(monkeypatch):
    """Verify permanent document errors (EMPTY_TEXT, etc.) transition directly to failed without backoff."""
    jid = str(uuid.uuid4())
    fake_job = {
        "id": jid,
        "job_type": "scan",
        "corpus_id": "uit_thesis",
        "document_key": "doc_empty",
        "version": 1,
        "filename": "empty.pdf",
        "file_path": "/tmp/empty.pdf",
        "file_sha256": "0" * 64,
        "status": "queued",
        "attempt": 1,
        "max_attempts": 5,
    }

    status_updates = []
    monkeypatch.setattr("app.tasks.get_job", lambda j_id: fake_job)
    monkeypatch.setattr("app.tasks.transition_job_status", lambda *a, **kw: True)
    monkeypatch.setattr("app.tasks.record_job_heartbeat", lambda *a, **kw: True)
    monkeypatch.setattr("app.tasks.set_status", lambda j_id, st, code: status_updates.append((st, code)))

    mock_con = MagicMock()
    mock_con.execute.return_value.fetchone.return_value = {"n": 5}
    monkeypatch.setattr("app.tasks.connect", lambda *a, **kw: MagicMock(__enter__=lambda s: mock_con, __exit__=lambda *a: None))

    with patch("app.tasks.parse_job", side_effect=ValueError("EMPTY_TEXT")), \
         patch("app.tasks.scan_document.apply_async") as mock_retry:

        scan_document(jid)

        assert len(status_updates) == 1
        assert status_updates[0] == ("failed", "EMPTY_TEXT")
        assert not mock_retry.called


# ==============================================================================
# 8. Race Protection: Stale Attempt Collision
# ==============================================================================


def test_atomic_completion_attempt_protection(db_conn):
    """Verify conditional update prevents a superseded attempt from overwriting newer attempt."""
    jid = _create_raw_job(status="processing", attempt=2, con=db_conn)
    try:
        # Simulate a stale worker from attempt 1 attempting to mark job as completed
        res = db_conn.execute(
            """
            UPDATE integrity.jobs
            SET status = 'completed', report = %s, updated_at = now()
            WHERE id = %s::uuid AND status = 'processing' AND attempt = %s
            """,
            (Jsonb({"status": "stale_report"}), jid, 1),
        )
        # Rowcount must be 0
        assert res.rowcount == 0

        # State remains processing at attempt 2
        j = get_job(jid, con=db_conn)
        assert j["status"] == "processing"
        assert j["attempt"] == 2
        assert j["report"] is None
    finally:
        _delete_job(jid, con=db_conn)


# ==============================================================================
# 9. Extended API Endpoints Verification
# ==============================================================================


def test_job_status_api_extended_fields(client, auth_headers, monkeypatch):
    """Verify GET /api/v1/jobs/{job_id} surfaces all resilience and lifecycle fields."""
    fake_id = str(uuid.uuid4())
    fake_job_row = {
        "id": fake_id,
        "job_type": "scan",
        "status": "waiting_for_compute",
        "error_code": "GPU_UNAVAILABLE",
        "attempt": 2,
        "max_attempts": 5,
        "retry_after": datetime(2026, 10, 31, 12, 0, 0, tzinfo=timezone.utc),
        "last_heartbeat": datetime(2026, 10, 31, 11, 55, 0, tzinfo=timezone.utc),
        "worker_id": "worker-42",
        "checkpoint": {"phase": "retrieved", "candidates_count": 12},
        "created_at": datetime(2026, 10, 31, 11, 50, 0, tzinfo=timezone.utc),
        "updated_at": datetime(2026, 10, 31, 11, 55, 0, tzinfo=timezone.utc),
    }

    monkeypatch.setattr("app.main.get_job", lambda j_id: fake_job_row if str(j_id) == fake_id else None)
    monkeypatch.setattr(
        "app.main.reclaim_stale_jobs",
        lambda stale_seconds=180: [{"job_id": fake_id, "action": "reclaimed", "attempt": 3}],
    )

    # 1. GET /api/v1/jobs/{job_id}
    resp = client.get(f"/api/v1/jobs/{fake_id}", headers=auth_headers)
    assert resp.status_code == 200
    data = resp.json()

    assert data["job_id"] == fake_id
    assert data["status"] == "waiting_for_compute"
    assert data["attempt"] == 2
    assert data["max_attempts"] == 5
    assert data["checkpoint"] == {"phase": "retrieved", "candidates_count": 12}
    assert data["worker_id"] == "worker-42"
    assert "created_at" in data
    assert "updated_at" in data

    # 2. POST /api/v1/jobs/reclaim
    rec_resp = client.post("/api/v1/jobs/reclaim?stale_seconds=600", headers=auth_headers)
    assert rec_resp.status_code == 200
    assert rec_resp.json()["reclaimed_count"] == 1
    assert rec_resp.json()["reclaimed_jobs"][0]["job_id"] == fake_id
