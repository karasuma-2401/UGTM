from __future__ import annotations

import hashlib
import logging
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any

from psycopg import Connection

from app.db import connect

logger = logging.getLogger(__name__)

DEFAULT_SQL_DIR = Path(__file__).resolve().parent.parent / "sql"


@dataclass
class MigrationInfo:
    version: str
    filename: str
    file_path: Path
    checksum_sha256: str
    applied: bool
    applied_at: datetime | None = None
    stored_checksum: str | None = None
    checksum_match: bool = True


def compute_file_sha256(path: Path) -> str:
    """Compute SHA256 hex digest of a migration file."""
    content = path.read_bytes()
    return hashlib.sha256(content).hexdigest()


def ensure_migration_table(con: Connection) -> None:
    """Ensure the schema_migrations tracking table exists."""
    with con.cursor() as cur:
        cur.execute("CREATE SCHEMA IF NOT EXISTS integrity;")
        cur.execute(
            """
            CREATE TABLE IF NOT EXISTS integrity.schema_migrations (
                version VARCHAR(255) PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                checksum_sha256 VARCHAR(64) NOT NULL
            );
            """
        )
    con.commit()


def get_applied_migrations(con: Connection) -> dict[str, dict[str, Any]]:
    """Retrieve all previously applied migrations."""
    ensure_migration_table(con)
    with con.cursor() as cur:
        cur.execute(
            """
            SELECT version, applied_at, checksum_sha256
            FROM integrity.schema_migrations
            ORDER BY applied_at ASC;
            """
        )
        rows = cur.fetchall()
        return {
            row["version"]: {
                "applied_at": row["applied_at"],
                "checksum_sha256": row["checksum_sha256"],
            }
            for row in rows
        }


def get_migration_status(
    sql_dir: Path | None = None, direct: bool = True
) -> list[MigrationInfo]:
    """Inspect migration files on disk against the database."""
    target_dir = sql_dir or DEFAULT_SQL_DIR
    sql_files = sorted(target_dir.glob("*.sql"))

    with connect(direct=direct) as con:
        applied = get_applied_migrations(con)

    statuses: list[MigrationInfo] = []
    for file_path in sql_files:
        version = file_path.stem
        file_hash = compute_file_sha256(file_path)
        is_applied = version in applied
        applied_at = applied[version]["applied_at"] if is_applied else None
        stored_hash = applied[version]["checksum_sha256"] if is_applied else None
        match = (stored_hash == file_hash) if is_applied else True

        statuses.append(
            MigrationInfo(
                version=version,
                filename=file_path.name,
                file_path=file_path,
                checksum_sha256=file_hash,
                applied=is_applied,
                applied_at=applied_at,
                stored_checksum=stored_hash,
                checksum_match=match,
            )
        )
    return statuses


def run_migrations(
    sql_dir: Path | None = None, direct: bool = True
) -> dict[str, Any]:
    """Execute all pending migrations in alphabetical order using direct connection.

    Returns:
        Summary dict containing 'applied', 'skipped', and 'total'.
    """
    target_dir = sql_dir or DEFAULT_SQL_DIR
    sql_files = sorted(target_dir.glob("*.sql"))
    if not sql_files:
        logger.warning("No migration SQL files found in %s", target_dir)
        return {"applied": [], "skipped": [], "total": 0}

    with connect(direct=direct, autocommit=True) as con:
        ensure_migration_table(con)
        applied_records = get_applied_migrations(con)

        applied_list: list[str] = []
        skipped_list: list[str] = []

        for file_path in sql_files:
            version = file_path.stem
            file_hash = compute_file_sha256(file_path)

            if version in applied_records:
                stored_hash = applied_records[version]["checksum_sha256"]
                if stored_hash != file_hash:
                    logger.warning(
                        "Migration %s checksum mismatch! Stored: %s, Disk: %s",
                        version,
                        stored_hash,
                        file_hash,
                    )
                skipped_list.append(version)
                continue

            logger.info("Applying migration: %s (%s)...", version, file_path.name)
            sql_content = file_path.read_text(encoding="utf-8")

            # Execute migration SQL
            with con.cursor() as cur:
                cur.execute(sql_content)
                cur.execute(
                    """
                    INSERT INTO integrity.schema_migrations
                      (version, applied_at, checksum_sha256)
                    VALUES (%s, now(), %s)
                    ON CONFLICT (version) DO UPDATE
                    SET checksum_sha256 = EXCLUDED.checksum_sha256,
                        applied_at = EXCLUDED.applied_at;
                    """,
                    (version, file_hash),
                )
            logger.info("Successfully applied migration: %s", version)
            applied_list.append(version)

    return {
        "applied": applied_list,
        "skipped": skipped_list,
        "total": len(sql_files),
    }
