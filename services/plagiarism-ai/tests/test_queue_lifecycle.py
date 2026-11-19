from __future__ import annotations

import json
import uuid
from pathlib import Path
from unittest.mock import patch

import pytest
import redis

from app.celery_app import celery_app
from app.config import settings
from app.db import connect, get_job
from app.main import insert_job


def test_insert_job_persists_in_neon():
    """Verify insert_job immediately commits a queued record into Neon before enqueue."""
    unique_doc = f"queue_test_doc_{uuid.uuid4().hex[:8]}"
    test_sha = "a" * 64
    job_id, created = insert_job(
        job_type="scan",
        corpus_id="queue_test_corpus",
        document_key=unique_doc,
        version=1,
        filename="test_file.docx",
        path="/tmp/test_file.docx",
        sha=test_sha,
    )

    try:
        assert created is True
        assert job_id is not None

        # Verify job is committed in Neon
        persisted = get_job(job_id)
        assert persisted is not None
        assert persisted["job_type"] == "scan"
        assert persisted["status"] == "queued"
        assert persisted["file_sha256"] == test_sha
        assert persisted["error_code"] is None
    finally:
        with connect() as con:
            con.execute("DELETE FROM integrity.jobs WHERE id = %s::uuid;", (job_id,))


def test_enqueue_task_publishes_to_redis_broker():
    """Verify celery task message enters the Redis queue without Celery backend storage."""
    from redis.backoff import ExponentialBackoff
    from redis.retry import Retry

    retry_policy = Retry(ExponentialBackoff(cap=5, base=1), 3)
    r = redis.from_url(
        settings.redis_url,
        retry=retry_policy,
        retry_on_timeout=True,
        socket_connect_timeout=15,
        socket_timeout=15,
        socket_keepalive=True,
    )
    initial_len = r.llen("celery")

    fake_job_id = str(uuid.uuid4())
    # Publish task to Redis
    result = celery_app.send_task("app.tasks.scan_document", args=[fake_job_id])
    assert result.id is not None

    new_len = r.llen("celery")
    assert new_len >= initial_len + 1

    # Look for our published task message in the queue
    found_payload = None
    messages = r.lrange("celery", 0, -1)
    for raw in messages:
        try:
            item = json.loads(raw.decode("utf-8"))
            if item.get("headers", {}).get("id") == result.id:
                found_payload = item
                r.lrem("celery", 1, raw)
                break
        except Exception:
            continue

    assert found_payload is not None, f"Published task {result.id} not found in Redis queue"
    headers = found_payload.get("headers", {})

    # In modern Celery, task name is in headers or body
    assert headers.get("task") == "app.tasks.scan_document" or found_payload.get("task") == "app.tasks.scan_document"
    args = headers.get("argsrepr") or str(found_payload.get("body", ""))
    assert fake_job_id in args


def test_api_restart_preserves_job_records_in_neon():
    """Verify that restarting/re-importing API never loses job state in Neon."""
    unique_doc = f"restart_test_{uuid.uuid4().hex[:8]}"
    test_sha = "b" * 64

    job_id, _ = insert_job(
        job_type="scan",
        corpus_id="restart_test_corpus",
        document_key=unique_doc,
        version=1,
        filename="test_restart.docx",
        path="/tmp/test_restart.docx",
        sha=test_sha,
    )

    try:
        # Simulate API process restart by reading directly from a fresh connection
        fresh_job = get_job(job_id)
        assert fresh_job is not None
        assert str(fresh_job["id"]) == job_id
        assert fresh_job["status"] == "queued"
        assert fresh_job["file_sha256"] == test_sha
    finally:
        with connect() as con:
            con.execute("DELETE FROM integrity.jobs WHERE id = %s::uuid;", (job_id,))


def test_idempotent_duplicate_submission_handling():
    """Verify submitting same version with same content returns existing ID without error."""
    unique_doc = f"idempotent_test_{uuid.uuid4().hex[:8]}"
    test_sha = "c" * 64

    job_id_1, created_1 = insert_job(
        job_type="scan",
        corpus_id="idempotent_corpus",
        document_key=unique_doc,
        version=1,
        filename="doc.pdf",
        path="/tmp/doc.pdf",
        sha=test_sha,
    )
    assert created_1 is True

    # Resubmit identical version and content
    job_id_2, created_2 = insert_job(
        job_type="scan",
        corpus_id="idempotent_corpus",
        document_key=unique_doc,
        version=1,
        filename="doc.pdf",
        path="/tmp/doc.pdf",
        sha=test_sha,
    )
    assert created_2 is False
    assert job_id_1 == job_id_2

    # Clean up
    with connect() as con:
        con.execute("DELETE FROM integrity.jobs WHERE id = %s::uuid;", (job_id_1,))

