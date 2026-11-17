"""CLI tool to inspect and run database migrations for UGTM Plagiarism AI."""

from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

# Ensure project root is in sys.path
PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from app.db import mask_dsn, settings
from app.migrations import get_migration_status, run_migrations

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("migrate")


def main():
    parser = argparse.ArgumentParser(description="UGTM AI Database Migration Tool")
    parser.add_argument(
        "action",
        choices=["apply", "status"],
        default="apply",
        nargs="?",
        help="Action to perform: 'apply' pending migrations, or view 'status'",
    )
    parser.add_argument(
        "--pooled",
        action="store_true",
        help="Use pooled connection (POSTGRES_DSN) instead of direct connection",
    )
    args = parser.parse_args()

    use_direct = not args.pooled
    target_dsn = (
        settings.postgres_direct_dsn
        if use_direct and settings.postgres_direct_dsn
        else settings.postgres_dsn
    )
    logger.info("Target Database: %s (direct=%s)", mask_dsn(target_dsn), use_direct)

    if args.action == "status":
        statuses = get_migration_status(direct=use_direct)
        print("\n=== MIGRATION STATUS ===")
        for s in statuses:
            state = "APPLIED" if s.applied else "PENDING"
            match = "MATCH" if s.checksum_match else "MISMATCH!"
            print(
                f"- {s.version} ({s.filename}): [{state}] "
                f"Checksum: {s.checksum_sha256[:12]}... (applied_at: {s.applied_at}, {match})"
            )
        print("========================\n")
        return

    if args.action == "apply":
        summary = run_migrations(direct=use_direct)
        logger.info(
            "Migration finished. Applied: %d, Skipped: %d, Total: %d",
            len(summary["applied"]),
            len(summary["skipped"]),
            summary["total"],
        )
        if summary["applied"]:
            print(f"Applied migrations: {', '.join(summary['applied'])}")
        else:
            print("Database schema is already up to date.")


if __name__ == "__main__":
    main()
