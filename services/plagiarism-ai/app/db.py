from __future__ import annotations

import logging
import re
import uuid
from contextlib import contextmanager
from datetime import datetime, timezone, timedelta
from typing import Any, Generator
from urllib.parse import urlparse

import psycopg
from psycopg import Connection
from psycopg.errors import (
    AdminShutdown,
    CannotConnectNow,
    ConnectionDoesNotExist,
    ConnectionException,
    ConnectionFailure,
    InvalidPassword,
    OperationalError,
    UndefinedFunction,
    UndefinedObject,
    UndefinedTable,
)
from psycopg.rows import dict_row
from psycopg.types.json import Jsonb

from app.config import settings

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Job Lifecycle State Machine Definition (CP-05.1)
# ---------------------------------------------------------------------------

VALID_JOB_STATUSES = {
    "queued",
    "processing",
    "waiting_for_compute",
    "completed",
    "failed",
    "cancelled",
}

VALID_TRANSITIONS: dict[str, set[str]] = {
    "queued": {"processing", "cancelled", "failed"},
    "processing": {"waiting_for_compute", "completed", "failed", "cancelled"},
    "waiting_for_compute": {"queued", "processing", "failed", "cancelled"},
    "completed": set(),  # Terminal state
    "failed": set(),     # Terminal state
    "cancelled": set(),  # Terminal state
}


def can_transition(current_status: str, target_status: str) -> bool:
    """Check whether a status transition is permitted by the job lifecycle state machine."""
    if target_status not in VALID_JOB_STATUSES:
        return False
    if current_status == target_status:
        return True
    return target_status in VALID_TRANSITIONS.get(current_status, set())


class DatabaseError(Exception):
    """Base error for database connection or execution issues."""

    default_code = "DB_ERROR"

    def __init__(self, message: str, code: str | None = None):
        super().__init__(message)
        self.code = code or self.default_code


class DatabaseConnectionError(DatabaseError):
    default_code = "DB_CONNECTION_ERROR"


class DatabaseAuthError(DatabaseError):
    default_code = "DB_AUTH_ERROR"


class DatabaseSchemaError(DatabaseError):
    default_code = "DB_SCHEMA_NOT_FOUND"


def mask_dsn(dsn: str) -> str:
    """Safely mask password and user in DSN for safe logging and error reporting."""
    if not dsn:
        return "<empty>"
    try:
        # Handles postgresql://...
        parsed = urlparse(dsn)
        if parsed.scheme:
            masked_netloc = parsed.hostname or "unknown-host"
            if parsed.port:
                masked_netloc += f":{parsed.port}"
            return f"{parsed.scheme}://***:***@{masked_netloc}{parsed.path}"
    except Exception:
        pass
    # Regex fallback
    return re.sub(r"://([^:@]+):([^@]+)@", r"://***:***@", dsn)


def classify_db_exception(exc: Exception) -> str:
    """Classify database exception into structured error codes for jobs and observability."""
    if isinstance(exc, InvalidPassword):
        return "DB_AUTH_ERROR"

    if isinstance(exc, (UndefinedTable, UndefinedFunction, UndefinedObject)):
        return "DB_SCHEMA_NOT_FOUND"

    if isinstance(exc, (ConnectionException, ConnectionFailure, ConnectionDoesNotExist, AdminShutdown, CannotConnectNow)):
        return "DB_CONNECTION_ERROR"

    if isinstance(exc, OperationalError):
        msg = str(exc).lower()
        if "password" in msg or "authentication" in msg:
            return "DB_AUTH_ERROR"
        if "ssl" in msg or "tls" in msg:
            return "DB_TLS_ERROR"
        if "could not translate host name" in msg or "name or service not known" in msg:
            return "DB_DNS_ERROR"
        if "timeout" in msg or "connection refused" in msg or "endpoint is disabled" in msg:
            return "DB_UNAVAILABLE"
        return "DB_CONNECTION_ERROR"

    return "DB_ERROR"


def get_target_dsn(direct: bool = False) -> str:
    """Retrieve connection string based on target connection mode (direct vs pooled)."""
    if direct and settings.postgres_direct_dsn:
        return settings.postgres_direct_dsn.strip()
    return settings.postgres_dsn.strip()


def connect(direct: bool = False, autocommit: bool = False) -> Connection:
    """Create a new PostgreSQL connection with dict_row factory and safe error classification.

    Parameters:
        direct: If True, uses POSTGRES_DIRECT_DSN (for migrations/backups); otherwise POSTGRES_DSN.
        autocommit: If True, sets autocommit mode on the connection.
    """
    dsn = get_target_dsn(direct=direct)
    if not dsn:
        raise DatabaseConnectionError(
            "Database DSN is not configured.", code="DB_NOT_CONFIGURED"
        )

    try:
        con = psycopg.connect(dsn, row_factory=dict_row, autocommit=autocommit)
        return con
    except Exception as exc:
        code = classify_db_exception(exc)
        masked = mask_dsn(dsn)
        logger.error(
            "Failed to connect to database [%s] (%s): %s",
            masked,
            code,
            exc,
        )
        if code in ("DB_AUTH_ERROR",):
            raise DatabaseAuthError(
                f"Authentication failed for database at {masked}", code=code
            ) from exc
        raise DatabaseConnectionError(
            f"Failed to connect to database at {masked}: {exc}", code=code
        ) from exc


@contextmanager
def _get_connection(con: Connection | None = None) -> Generator[Connection, None, None]:
    """Helper to reuse an existing connection or open a managed connection."""
    if con is not None:
        yield con
    else:
        with connect() as new_con:
            yield new_con


def _is_valid_uuid(val: Any) -> bool:
    try:
        uuid.UUID(str(val))
        return True
    except (ValueError, AttributeError, TypeError):
        return False


def set_status(job_id: str, status: str, error_code: str | None = None) -> None:
    """Update job status and optional error code in integrity.jobs."""
    if not _is_valid_uuid(job_id):
        return
    with connect() as con:
        con.execute(
            """
            UPDATE integrity.jobs
            SET status = %s, error_code = %s, updated_at = now()
            WHERE id = %s::uuid
            """,
            (status, error_code, job_id),
        )


def get_job(job_id: str, con: Connection | None = None):
    """Retrieve job details by job_id from integrity.jobs."""
    if not _is_valid_uuid(job_id):
        return None
    with _get_connection(con) as c:
        return c.execute(
            """
            SELECT * FROM integrity.jobs WHERE id = %s::uuid
            """,
            (job_id,),
        ).fetchone()


def transition_job_status(
    job_id: str,
    target_status: str,
    *,
    from_status: str | list[str] | set[str] | None = None,
    error_code: str | None = None,
    attempt: int | None = None,
    checkpoint: dict[str, Any] | None = None,
    retry_after: datetime | None = None,
    worker_id: str | None = None,
    con: Connection | None = None,
) -> bool:
    """Atomically transition job status enforcing state machine and concurrency constraints.

    Returns True if transition succeeded, False if current status or attempt did not match.
    """
    if not _is_valid_uuid(job_id):
        return False
    with _get_connection(con) as c:
        job = c.execute(
            "SELECT status, attempt FROM integrity.jobs WHERE id = %s::uuid",
            (job_id,),
        ).fetchone()
        if not job:
            return False

        current_status = job["status"]
        current_attempt = job["attempt"]

        if not can_transition(current_status, target_status):
            logger.warning(
                "Illegal state transition requested for job %s: %s -> %s",
                job_id,
                current_status,
                target_status,
            )
            return False

        if from_status is not None:
            if isinstance(from_status, str) and current_status != from_status:
                return False
            elif isinstance(from_status, (list, tuple, set)) and current_status not in from_status:
                return False

        if attempt is not None and current_attempt != attempt:
            logger.warning(
                "Stale attempt collision detected for job %s: current=%s, given=%s",
                job_id,
                current_attempt,
                attempt,
            )
            return False

        params: list[Any] = [target_status, error_code]
        updates = ["status = %s", "error_code = %s", "updated_at = now()", "last_heartbeat = now()"]

        if checkpoint is not None:
            updates.append("checkpoint = %s")
            params.append(Jsonb(checkpoint))

        if retry_after is not None:
            updates.append("retry_after = %s")
            params.append(retry_after)

        if worker_id is not None:
            updates.append("worker_id = %s")
            params.append(worker_id)

        params.append(job_id)
        query = f"UPDATE integrity.jobs SET {', '.join(updates)} WHERE id = %s::uuid"
        c.execute(query, tuple(params))
        return True


def record_job_heartbeat(job_id: str, worker_id: str | None = None, con: Connection | None = None) -> bool:
    """Record worker heartbeat for an active processing job."""
    if not _is_valid_uuid(job_id):
        return False
    with _get_connection(con) as c:
        cur = c.execute(
            """
            UPDATE integrity.jobs
            SET last_heartbeat = now(),
                worker_id = COALESCE(%s, worker_id)
            WHERE id = %s::uuid AND status = 'processing'
            """,
            (worker_id, job_id),
        )
        return cur.rowcount > 0


def record_job_checkpoint(
    job_id: str,
    phase: str,
    data: dict[str, Any] | None = None,
    con: Connection | None = None,
) -> None:
    """Store intermediate checkpoint progress without modifying job status."""
    if not _is_valid_uuid(job_id):
        return
    checkpoint_payload = {
        "phase": phase,
        "updated_at": datetime.now(timezone.utc).isoformat(),
        "data": data or {},
    }
    with _get_connection(con) as c:
        c.execute(
            """
            UPDATE integrity.jobs
            SET checkpoint = %s,
                last_heartbeat = now(),
                updated_at = now()
            WHERE id = %s::uuid
            """,
            (Jsonb(checkpoint_payload), job_id),
        )


def schedule_waiting_for_compute(
    job_id: str,
    *,
    error_code: str = "GPU_UNAVAILABLE",
    checkpoint: dict[str, Any] | None = None,
    base_backoff_seconds: float = 10.0,
    max_backoff_seconds: float = 300.0,
    con: Connection | None = None,
) -> dict[str, Any]:
    """Schedule a job to enter waiting_for_compute state with exponential backoff.

    If max_attempts is reached, transitions directly to failed.
    """
    with _get_connection(con) as c:
        job = c.execute(
            """
            SELECT id, status, attempt, max_attempts, checkpoint
            FROM integrity.jobs
            WHERE id = %s::uuid
            """,
            (job_id,),
        ).fetchone()

        if not job:
            raise ValueError(f"Job {job_id} not found")

        current_attempt = job["attempt"] or 1
        max_attempts = job["max_attempts"] or 5

        # Check for attempt exhaustion
        if current_attempt >= max_attempts:
            logger.warning(
                "Job %s reached max attempts (%d/%d), failing permanently.",
                job_id,
                current_attempt,
                max_attempts,
            )
            c.execute(
                """
                UPDATE integrity.jobs
                SET status = 'failed',
                    error_code = 'GPU_UNAVAILABLE_EXHAUSTED',
                    updated_at = now()
                WHERE id = %s::uuid
                """,
                (job_id,),
            )
            return {
                "action": "failed",
                "attempt": current_attempt,
                "error_code": "GPU_UNAVAILABLE_EXHAUSTED",
            }

        # Exponential backoff: min(base * 2^(attempt - 1), max_backoff)
        backoff_seconds = min(
            base_backoff_seconds * (2 ** (current_attempt - 1)), max_backoff_seconds
        )
        retry_after = datetime.now(timezone.utc) + timedelta(seconds=backoff_seconds)
        next_attempt = current_attempt + 1

        chk = checkpoint if checkpoint is not None else (job["checkpoint"] or {})

        c.execute(
            """
            UPDATE integrity.jobs
            SET status = 'waiting_for_compute',
                error_code = %s,
                attempt = %s,
                retry_after = %s,
                checkpoint = %s,
                updated_at = now()
            WHERE id = %s::uuid
            """,
            (error_code, next_attempt, retry_after, Jsonb(chk), job_id),
        )

        logger.info(
            "Job %s transitioned to waiting_for_compute (attempt %d/%d, retry in %.1fs at %s)",
            job_id,
            next_attempt,
            max_attempts,
            backoff_seconds,
            retry_after.isoformat(),
        )

        return {
            "action": "retry",
            "attempt": next_attempt,
            "max_attempts": max_attempts,
            "retry_after": retry_after.isoformat(),
            "backoff_seconds": backoff_seconds,
        }


def reclaim_stale_jobs(stale_seconds: int = 180, con: Connection | None = None) -> list[dict[str, Any]]:
    """Detect and recover jobs stuck in processing state with expired heartbeats."""
    reclaimed: list[dict[str, Any]] = []
    cutoff = datetime.now(timezone.utc) - timedelta(seconds=stale_seconds)

    with _get_connection(con) as c:
        stale_rows = c.execute(
            """
            SELECT id, job_type, corpus_id, document_key, version,
                   attempt, max_attempts, checkpoint
            FROM integrity.jobs
            WHERE status = 'processing'
              AND (last_heartbeat IS NULL OR last_heartbeat < %s)
            """,
            (cutoff,),
        ).fetchall()

        for row in stale_rows:
            jid = str(row["id"])
            att = row["attempt"] or 1
            max_att = row["max_attempts"] or 5

            if att >= max_att:
                c.execute(
                    """
                    UPDATE integrity.jobs
                    SET status = 'failed',
                        error_code = 'WORKER_HEARTBEAT_TIMEOUT_EXHAUSTED',
                        updated_at = now()
                    WHERE id = %s::uuid
                    """,
                    (jid,),
                )
                reclaimed.append({"job_id": jid, "action": "failed", "attempt": att})
            else:
                next_att = att + 1
                c.execute(
                    """
                    UPDATE integrity.jobs
                    SET status = 'waiting_for_compute',
                        error_code = 'WORKER_HEARTBEAT_TIMEOUT',
                        attempt = %s,
                        retry_after = now(),
                        updated_at = now()
                    WHERE id = %s::uuid
                    """,
                    (next_att, jid),
                )
                reclaimed.append({"job_id": jid, "action": "reclaimed", "attempt": next_att})

    return reclaimed


def cancel_job(job_id: str, reason: str = "USER_CANCELLED", con: Connection | None = None) -> bool:
    """Cancel a job if it has not reached a terminal state."""
    if not _is_valid_uuid(job_id):
        return False
    with _get_connection(con) as c:
        cur = c.execute(
            """
            UPDATE integrity.jobs
            SET status = 'cancelled',
                error_code = %s,
                updated_at = now()
            WHERE id = %s::uuid
              AND status IN ('queued', 'processing', 'waiting_for_compute')
            """,
            (reason, job_id),
        )
        return cur.rowcount > 0
