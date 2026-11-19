#!/usr/bin/env python
"""CLI Utility to run the UGTM Job Reconciler (CP-05.2).

Rescues orphaned queued jobs and overdue waiting_for_compute jobs.
Usage:
    python scripts/reconcile_jobs.py [--queued-timeout 60] [--limit 50]
"""

from __future__ import annotations

import argparse
import json
import logging
import sys

from app.reconciler import reconcile_pending_jobs

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("reconcile_jobs")


def main() -> int:
    parser = argparse.ArgumentParser(description="Reconcile pending jobs stuck in the commit-enqueue gap.")
    parser.add_argument(
        "--queued-timeout",
        type=int,
        default=60,
        help="Seconds a job may remain in queued status before being considered orphaned (default: 60)",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=50,
        help="Maximum number of jobs to reconcile in one pass (default: 50)",
    )
    args = parser.parse_args()

    logger.info(
        "Starting job reconciliation with queued_timeout=%ds, limit=%d...",
        args.queued_timeout,
        args.limit,
    )

    result = reconcile_pending_jobs(
        queued_timeout_seconds=args.queued_timeout,
        limit=args.limit,
    )

    print(json.dumps(result, indent=2, default=str))

    logger.info(
        "Reconciliation complete: %d jobs re-enqueued, %d failed.",
        result["reconciled_count"],
        result["failed_count"],
    )
    return 0 if result["failed_count"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())

