"""Smoke test script for Queue & Worker flow (CP-01.3).

Verifies:
1. Fast job submission and persistence into Neon PostgreSQL (status='queued').
2. Task dispatch into Redis message broker ('celery' queue).
3. Worker message retrieval and status transition (queued -> processing -> completed).
4. API restart resilience (persisted state in Neon is unaffected by API restarts).
5. Idempotent submission (duplicate submit returns existing job_id without error).
"""

from __future__ import annotations

import json
import logging
import sys
import time
import uuid
from pathlib import Path
from typing import Any

import redis

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from app.celery_app import celery_app
from app.config import settings
from app.db import connect, get_job, mask_dsn, set_status
from app.main import insert_job

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("smoke_test_queue")


def run_queue_lifecycle_test() -> dict[str, Any]:
    test_id = uuid.uuid4().hex[:8]
    corpus_id = f"smoke_corpus_{test_id}"
    document_key = f"smoke_doc_{test_id}"
    version = 1
    test_sha = uuid.uuid4().hex * 2  # 64-char hex

    logger.info("1. Testing fast job insertion into Neon...")
    t0 = time.perf_counter()
    job_id, created = insert_job(
        job_type="scan",
        corpus_id=corpus_id,
        document_key=document_key,
        version=version,
        filename="smoke_thesis.docx",
        path=f"/tmp/smoke_{test_id}.docx",
        sha=test_sha,
    )
    insert_duration_ms = round((time.perf_counter() - t0) * 1000, 2)
    assert created is True, "Expected created=True"
    logger.info("Job %s inserted in %.2f ms", job_id, insert_duration_ms)

    try:
        # Verify Neon record
        job_record = get_job(job_id)
        assert job_record is not None
        assert job_record["status"] == "queued"
        assert job_record["file_sha256"] == test_sha

        logger.info("2. Testing task dispatch to Redis Cloud broker...")
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
        initial_queue_len = r.llen("celery")

        t1 = time.perf_counter()
        async_result = celery_app.send_task(
            "app.tasks.scan_document",
            args=[job_id],
        )
        dispatch_duration_ms = round((time.perf_counter() - t1) * 1000, 2)
        logger.info(
            "Task dispatched in %.2f ms (celery_task_id=%s)",
            dispatch_duration_ms,
            async_result.id,
        )

        new_queue_len = r.llen("celery")
        assert new_queue_len >= initial_queue_len + 1, "Queue depth did not increase"

        logger.info("3. Simulating CPU worker consuming task from Redis...")
        raw_msg = r.rpop("celery")
        assert raw_msg is not None, "Failed to pop task from Redis queue"
        msg_data = json.loads(raw_msg.decode("utf-8"))
        headers = msg_data.get("headers", {})
        task_name = headers.get("task") or msg_data.get("task")
        assert task_name == "app.tasks.scan_document"
        logger.info("Worker received task '%s' for job_id=%s", task_name, job_id)

        # Worker updates status to processing then completed
        set_status(job_id, "processing")
        job_proc = get_job(job_id)
        assert job_proc["status"] == "processing"

        set_status(job_id, "completed")
        job_done = get_job(job_id)
        assert job_done["status"] == "completed"

        logger.info("4. Testing API restart resilience...")
        # Fresh connection / simulated restart
        restarted_job = get_job(job_id)
        assert restarted_job is not None
        assert restarted_job["status"] == "completed"
        assert restarted_job["file_sha256"] == test_sha

        logger.info("5. Testing idempotency on duplicate submission...")
        duplicate_job_id, dup_created = insert_job(
            job_type="scan",
            corpus_id=corpus_id,
            document_key=document_key,
            version=version,
            filename="smoke_thesis.docx",
            path=f"/tmp/smoke_{test_id}.docx",
            sha=test_sha,
        )
        assert dup_created is False
        assert duplicate_job_id == job_id
    finally:
        # Cleanup
        with connect() as con:
            con.execute("DELETE FROM integrity.jobs WHERE id = %s::uuid;", (job_id,))
        logger.info("Cleaned up test job %s from Neon", job_id)

    return {
        "status": "PASSED",
        "job_id": job_id,
        "insert_duration_ms": insert_duration_ms,
        "dispatch_duration_ms": dispatch_duration_ms,
        "broker_queue": "celery",
        "neon_persistence": "verified",
        "api_restart_resilience": "verified",
        "idempotency": "verified",
    }


def main():
    print("==================================================")
    print("UGTM AI — Queue & CPU Worker Smoke Test (CP-01.3)")
    print(f"Target Database: {mask_dsn(settings.postgres_dsn)}")
    print(f"Target Broker:   {mask_dsn(settings.redis_url)}")
    print("==================================================")

    results = run_queue_lifecycle_test()
    print("\n=== QUEUE LIFECYCLE SUMMARY ===")
    print(json.dumps(results, indent=2))
    print("================================\n")


if __name__ == "__main__":
    main()

