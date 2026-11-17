"""Job Outbox Reconciler Engine (CP-05.2).

Ensures durability and zero-job-loss between Neon PostgreSQL database commits
and Celery message broker enqueues. Reconciles orphaned queued jobs and
overdue waiting_for_compute jobs.
"""

from __future__ import annotations

import logging
import uuid
from datetime import datetime, timezone
from typing import Any

from psycopg import Connection

from app.db import _get_connection, _is_valid_uuid, connect

logger = logging.getLogger(__name__)


def mark_job_enqueued(job_id: str, con: Connection | None = None) -> bool:
    """Record that a job has been dispatched to the message broker."""
    if not _is_valid_uuid(job_id):
        return False
    with _get_connection(con) as c:
        cur = c.execute(
            """
            UPDATE integrity.jobs
            SET enqueued_at = now(), updated_at = now()
            WHERE id = %s::uuid
            """,
            (job_id,),
        )
        return cur.rowcount > 0


def safe_enqueue_job(
    job_id: str,
    job_type: str,
    con: Connection | None = None,
) -> bool:
    """Attempt immediate Celery enqueue, logging and falling back to reconciler on failure.

    Guarantees that a broker connection error never fails the API request (returns False),
    leaving the committed database record in 'queued' status for reconciliation.
    """
    if not _is_valid_uuid(job_id):
        return False

    try:
        if job_type == "scan":
            from app.tasks import scan_document

            scan_document.delay(job_id)
        elif job_type == "index":
            from app.tasks import index_source

            index_source.delay(job_id)
        else:
            logger.error("Unknown job_type '%s' for safe_enqueue_job", job_type)
            return False

        mark_job_enqueued(job_id, con=con)
        logger.info("Successfully enqueued %s job %s to broker", job_type, job_id)
        return True
    except Exception as exc:
        logger.warning(
            "Broker enqueue failed for job %s (type=%s): %s. "
            "Job remains safely committed in DB (status=queued) and will be rescued by reconciler.",
            job_id,
            job_type,
            exc,
        )
        return False


def reconcile_pending_jobs(
    queued_timeout_seconds: int = 60,
    limit: int = 50,
    con: Connection | None = None,
) -> dict[str, Any]:
    """Scan and rescue jobs stuck in the commit-enqueue gap or overdue waiting_for_compute.

    Criteria for reconciliation:
    1. status = 'queued' AND (
           enqueued_at IS NULL
           OR (
               created_at <= now() - interval 'X seconds'
               AND (reconciled_at IS NULL OR reconciled_at <= now() - interval 'X seconds')
           )
       )
    2. status = 'waiting_for_compute' AND retry_after <= now()
       AND (reconciled_at IS NULL OR reconciled_at <= now() - interval '30 seconds')

    Returns:
        Telemetry summary dict with reconciled and failed jobs.
    """
    reconciled: list[dict[str, Any]] = []
    failed: list[dict[str, Any]] = []

    with _get_connection(con) as c:
        # 1. Fetch orphaned or un-enqueued 'queued' jobs
        queued_candidates = c.execute(
            """
            SELECT id, job_type, corpus_id, document_key, version, attempt,
                   created_at, enqueued_at, reconciled_at
            FROM integrity.jobs
            WHERE status = 'queued'
              AND (
                  enqueued_at IS NULL
                  OR (
                      created_at <= now() - (%s || ' seconds')::interval
                      AND (reconciled_at IS NULL OR reconciled_at <= now() - (%s || ' seconds')::interval)
                  )
              )
            ORDER BY created_at ASC
            LIMIT %s
            """,
            (queued_timeout_seconds, queued_timeout_seconds, limit),
        ).fetchall()

        # 2. Fetch overdue 'waiting_for_compute' jobs
        remaining_limit = max(0, limit - len(queued_candidates))
        waiting_candidates = []
        if remaining_limit > 0:
            waiting_candidates = c.execute(
                """
                SELECT id, job_type, corpus_id, document_key, version, attempt,
                       retry_after, created_at, enqueued_at, reconciled_at
                FROM integrity.jobs
                WHERE status = 'waiting_for_compute'
                  AND retry_after <= now()
                  AND (reconciled_at IS NULL OR reconciled_at <= now() - interval '30 seconds')
                ORDER BY retry_after ASC
                LIMIT %s
                """,
                (remaining_limit,),
            ).fetchall()

        all_candidates = [
            (row, "queued_gap") for row in queued_candidates
        ] + [
            (row, "overdue_waiting") for row in waiting_candidates
        ]

        if not all_candidates:
            return {
                "reconciled_count": 0,
                "failed_count": 0,
                "reconciled_jobs": [],
                "failed_jobs": [],
            }

        logger.info("Reconciler found %d candidate jobs for recovery", len(all_candidates))

        for row, reason in all_candidates:
            jid = str(row["id"])
            jtype = row["job_type"]

            try:
                # Dispatch to Celery task
                if jtype == "scan":
                    from app.tasks import scan_document

                    scan_document.apply_async(args=[jid])
                elif jtype == "index":
                    from app.tasks import index_source

                    index_source.apply_async(args=[jid])
                else:
                    logger.error("Unknown job_type %s during reconciliation for job %s", jtype, jid)
                    failed.append({"job_id": jid, "reason": reason, "error": f"Unknown job_type {jtype}"})
                    continue

                # Mark enqueued and reconciled in DB
                c.execute(
                    """
                    UPDATE integrity.jobs
                    SET enqueued_at = now(),
                        reconciled_at = now(),
                        updated_at = now()
                    WHERE id = %s::uuid
                    """,
                    (jid,),
                )
                reconciled.append({
                    "job_id": jid,
                    "job_type": jtype,
                    "reason": reason,
                    "attempt": row.get("attempt") or 1,
                })
                logger.info("Reconciled job %s (type=%s, reason=%s)", jid, jtype, reason)

            except Exception as exc:
                logger.warning("Failed to reconcile job %s: %s", jid, exc)
                c.execute(
                    """
                    UPDATE integrity.jobs
                    SET reconciled_at = now(),
                        updated_at = now()
                    WHERE id = %s::uuid
                    """,
                    (jid,),
                )
                failed.append({
                    "job_id": jid,
                    "job_type": jtype,
                    "reason": reason,
                    "error": str(exc),
                })

    return {
        "reconciled_count": len(reconciled),
        "failed_count": len(failed),
        "reconciled_jobs": reconciled,
        "failed_jobs": failed,
    }

