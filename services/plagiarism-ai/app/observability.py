"""Observability, Telemetry, and Stage Timing Tracking (CP-05.5).

Key Principles:
1. Structured execution timing for each stage: parse, encode, retrieve, rerank, align, report.
2. Privacy First: NO full raw text logging (only character lengths, chunk counts, sha256).
3. Rich Context: job_id, run_id, stage, attempt, worker_id, gpu_revision, model_revision, error_code.
4. Telemetry aggregation into Job Provenance / Report Summary.
"""

from __future__ import annotations

import json
import logging
import time
import uuid
from contextlib import contextmanager
from typing import Any, Generator

logger = logging.getLogger("ugtm.observability")


class StageTimer:
    """Tracks latency for multiple pipeline stages in a job execution."""

    def __init__(self, job_id: str, run_id: str | None = None):
        self.job_id = job_id
        self.run_id = run_id or uuid.uuid4().hex[:12]
        self.stage_timings_ms: dict[str, float] = {}
        self.stage_metadata: dict[str, dict[str, Any]] = {}
        self.start_time = time.perf_counter()

    @contextmanager
    def time_stage(
        self,
        stage_name: str,
        extra_meta: dict[str, Any] | None = None,
    ) -> Generator[dict[str, Any], None, None]:
        meta = extra_meta.copy() if extra_meta else {}
        t0 = time.perf_counter()
        try:
            yield meta
        finally:
            elapsed_ms = round((time.perf_counter() - t0) * 1000.0, 2)
            self.stage_timings_ms[stage_name] = elapsed_ms
            self.stage_metadata[stage_name] = meta
            # Safe structured logging without raw text
            safe_meta = {
                k: v for k, v in meta.items()
                if not any(f in k.lower() for f in ("text", "content", "raw", "passage"))
            }
            logger.info(
                "StageCompleted: job_id=%s run_id=%s stage=%s duration_ms=%.2f %s",
                self.job_id,
                self.run_id,
                stage_name,
                elapsed_ms,
                json.dumps(safe_meta) if safe_meta else "",
            )

    def total_elapsed_ms(self) -> float:
        return round((time.perf_counter() - self.start_time) * 1000.0, 2)

    def to_telemetry_dict(self) -> dict[str, Any]:
        return {
            "job_id": self.job_id,
            "run_id": self.run_id,
            "total_duration_ms": self.total_elapsed_ms(),
            "stages_ms": self.stage_timings_ms,
            "metadata": self.stage_metadata,
        }


def sanitize_log_payload(payload: dict[str, Any]) -> dict[str, Any]:
    """Sanitizes data dictionaries to eliminate raw text leaks before logging or reporting."""
    sanitized: dict[str, Any] = {}
    for k, v in payload.items():
        k_lower = k.lower()
        if any(sensitive in k_lower for sensitive in ("text", "content", "raw_passage", "source_text", "submission_text")):
            if isinstance(v, str):
                sanitized[f"{k}_length"] = len(v)
            elif isinstance(v, list):
                sanitized[f"{k}_count"] = len(v)
            else:
                sanitized[f"{k}_type"] = type(v).__name__
        elif isinstance(v, dict):
            sanitized[k] = sanitize_log_payload(v)
        elif isinstance(v, list):
            sanitized[k] = [
                sanitize_log_payload(item) if isinstance(item, dict) else item
                for item in v[:20]  # Cap list logging to first 20 items
            ]
        else:
            sanitized[k] = v
    return sanitized

