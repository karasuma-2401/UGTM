from __future__ import annotations

from pathlib import Path
from unittest.mock import MagicMock

import pytest

from app.db import (
    DatabaseAuthError,
    DatabaseConnectionError,
    classify_db_exception,
    mask_dsn,
)
from app.migrations import (
    MigrationInfo,
    compute_file_sha256,
    ensure_migration_table,
    get_applied_migrations,
)


def test_mask_dsn():
    dsn = "postgresql://myuser:secret_pass_123@ep-cool-pooler.ap-southeast-1.aws.neon.tech/ugtm_ai?sslmode=require"
    masked = mask_dsn(dsn)
    assert "secret_pass_123" not in masked
    assert "myuser" not in masked
    assert "ep-cool-pooler.ap-southeast-1.aws.neon.tech" in masked
    assert masked.startswith("postgresql://***:***@")

    assert mask_dsn("") == "<empty>"


def test_classify_db_exception():
    from psycopg.errors import InvalidPassword, OperationalError, UndefinedTable

    assert classify_db_exception(InvalidPassword("password failed")) == "DB_AUTH_ERROR"
    assert classify_db_exception(UndefinedTable("table not found")) == "DB_SCHEMA_NOT_FOUND"

    ssl_err = OperationalError("SSL connection error: certificate verify failed")
    assert classify_db_exception(ssl_err) == "DB_TLS_ERROR"

    dns_err = OperationalError("could not translate host name 'ep-fake.neon.tech'")
    assert classify_db_exception(dns_err) == "DB_DNS_ERROR"

    conn_refused = OperationalError("connection refused on port 5432")
    assert classify_db_exception(conn_refused) == "DB_UNAVAILABLE"


def test_compute_file_sha256(tmp_path):
    sql_file = tmp_path / "001_test.sql"
    sql_file.write_text("CREATE TABLE test (id INT);", encoding="utf-8")
    sha = compute_file_sha256(sql_file)
    assert len(sha) == 64
    assert sha == compute_file_sha256(sql_file)


def test_migration_tracking_queries():
    mock_cursor = MagicMock()
    mock_cursor.fetchall.return_value = [
        {"version": "001_init", "applied_at": "2026-10-30T12:00:00Z", "checksum_sha256": "abc"}
    ]
    mock_con = MagicMock()
    mock_con.cursor.return_value.__enter__.return_value = mock_cursor

    ensure_migration_table(mock_con)
    assert mock_cursor.execute.call_count >= 2

    applied = get_applied_migrations(mock_con)
    assert "001_init" in applied
    assert applied["001_init"]["checksum_sha256"] == "abc"
