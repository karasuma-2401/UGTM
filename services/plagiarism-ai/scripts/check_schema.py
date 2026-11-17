"""Read-only release preflight; migrations are applied separately after backup."""
from __future__ import annotations

import hashlib
import logging
from pathlib import Path

from app.db import connect

logger = logging.getLogger(__name__)
SQL_DIR = Path(__file__).resolve().parent.parent / "sql"


def schema_errors(applied: dict[str, str], sql_dir: Path = SQL_DIR) -> list[str]:
    files = sorted(sql_dir.glob("*.sql"))
    if not files:
        return ["No migration files packaged in this image"]
    errors = []
    expected = {path.stem for path in files}
    for path in files:
        checksum = hashlib.sha256(path.read_bytes()).hexdigest()
        if path.stem not in applied:
            errors.append(f"Pending migration: {path.stem}")
        elif applied[path.stem] != checksum:
            errors.append(f"Migration checksum mismatch: {path.stem}")
    for version in sorted(applied.keys() - expected):
        errors.append(f"Database has a migration outside this release: {version}")
    return errors


def main() -> int:
    try:
        with connect(direct=True) as connection:
            rows = connection.execute(
                "SELECT version, checksum_sha256 FROM integrity.schema_migrations"
            ).fetchall()
        errors = schema_errors({row["version"]: row["checksum_sha256"] for row in rows})
    except Exception:
        # Connection errors can contain credentials; report no DSN or driver text.
        logger.error("Cannot read AI migration history; check connectivity and apply reviewed migrations")
        return 1
    for error in errors:
        logger.error("%s", error)
    return 1 if errors else 0


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    raise SystemExit(main())
