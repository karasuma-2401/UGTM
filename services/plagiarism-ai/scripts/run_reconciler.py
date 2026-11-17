"""Periodically recover committed jobs whose broker publication was interrupted."""
from __future__ import annotations

import logging
import signal
from threading import Event

from app.reconciler import reconcile_pending_jobs

logger = logging.getLogger(__name__)


def run(stop: Event, interval: float = 30) -> None:
    while not stop.is_set():
        try:
            result = reconcile_pending_jobs()
            logger.info("Reconciled=%d failed=%d", result["reconciled_count"], result["failed_count"])
        except Exception:
            # Keep retrying after transient outages without logging document data or DSNs.
            logger.error("Reconciliation failed; will retry on the next interval")
        stop.wait(interval)


def main() -> None:
    logging.basicConfig(level=logging.INFO)
    stop = Event()
    for signum in (signal.SIGINT, signal.SIGTERM):
        signal.signal(signum, lambda *_: stop.set())
    run(stop)


if __name__ == "__main__":
    main()
