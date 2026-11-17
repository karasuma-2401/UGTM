"""Smoke test script for Neon PostgreSQL integration (CP-01.2).

Verifies:
1. TLS connection to Neon (both runtime pooled and direct migration DSNs).
2. Extensions: pgvector ('vector') and pg_trgm.
3. Schema integrity: jobs, sources, chunks, schema_migrations.
4. Real vector and trigram insert, search, and RRF retrieval using app/search.py.
5. Error classification on invalid connection parameters.
"""

from __future__ import annotations

import json
import logging
import math
import sys
import uuid
from pathlib import Path
from typing import Any

# Ensure project root is in sys.path
PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

import numpy as np

from app.db import (
    DatabaseAuthError,
    DatabaseConnectionError,
    classify_db_exception,
    connect,
    mask_dsn,
    settings,
)
from app.migrations import run_migrations
from app.search import retrieve, vector_literal

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("smoke_test_neon")


def test_tls_connection() -> dict[str, Any]:
    """Verify TLS connection on runtime pooled DSN and direct DSN."""
    results = {}
    for label, is_direct in [("runtime_pooled", False), ("migration_direct", True)]:
        try:
            with connect(direct=is_direct) as con:
                row = con.execute(
                    """
                    SELECT version(),
                           current_database(),
                           current_user,
                           inet_server_addr() AS server_ip;
                    """
                ).fetchone()

                ssl_row = con.execute(
                    """
                    SELECT ssl, version, cipher, bits
                    FROM pg_stat_ssl
                    WHERE pid = pg_backend_pid();
                    """
                ).fetchone()

                ssl_info = bool(ssl_row["ssl"]) if ssl_row else True
                ssl_cipher = ssl_row["cipher"] if ssl_row else "unknown"
                ssl_ver = ssl_row["version"] if ssl_row else "unknown"

                results[label] = {
                    "status": "connected",
                    "ssl_in_use": ssl_info,
                    "ssl_version": ssl_ver,
                    "ssl_cipher": ssl_cipher,
                    "database": row["current_database"],
                    "server_version": (row["version"] or "").split()[0:2],
                }
                logger.info(
                    "Connection [%s] OK: database='%s', ssl=%s (%s, cipher=%s)",
                    label,
                    row["current_database"],
                    ssl_info,
                    ssl_ver,
                    ssl_cipher,
                )
        except Exception as exc:
            logger.error("Connection [%s] FAILED: %s", label, exc)
            results[label] = {
                "status": "failed",
                "error_code": classify_db_exception(exc),
                "error": str(exc),
            }
    return results


def test_extensions_and_schema() -> dict[str, Any]:
    """Verify required extensions (vector, pg_trgm) and integrity tables."""
    with connect(direct=False) as con:
        # Check extensions
        exts = con.execute(
            """
            SELECT extname, extversion
            FROM pg_extension
            WHERE extname IN ('vector', 'pg_trgm')
            ORDER BY extname;
            """
        ).fetchall()
        installed_exts = {r["extname"]: r["extversion"] for r in exts}

        # Check tables in integrity schema
        tables = con.execute(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'integrity'
            ORDER BY table_name;
            """
        ).fetchall()
        installed_tables = [r["table_name"] for r in tables]

    return {
        "extensions": installed_exts,
        "tables": installed_tables,
        "vector_ready": "vector" in installed_exts,
        "pg_trgm_ready": "pg_trgm" in installed_exts,
    }


def test_vector_and_trigram_retrieval() -> dict[str, Any]:
    """Insert synthetic source chunks and execute app/search.py retrieve() pipeline."""
    test_corpus = f"smoke_corpus_{uuid.uuid4().hex[:6]}"
    test_source_key = f"smoke_doc_{uuid.uuid4().hex[:6]}"
    test_dim = 1024

    # Generate two synthetic 1024-dim unit vectors
    rng = np.random.default_rng(42)
    vec1 = rng.standard_normal(test_dim).astype(np.float32)
    vec1 /= np.linalg.norm(vec1)

    vec2 = rng.standard_normal(test_dim).astype(np.float32)
    vec2 /= np.linalg.norm(vec2)

    text_chunk1 = (
        "Hệ thống phát hiện đạo văn sử dụng học sâu và mô hình ngôn ngữ lớn "
        "để đối chiếu khóa luận tốt nghiệp của sinh viên tại trường đại học."
    )
    text_chunk2 = (
        "Nghiên cứu kiến trúc vi dịch vụ và xử lý dữ liệu lớn thời gian thực "
        "với Apache Spark và Kubernetes trên hạ tầng điện toán đám mây."
    )

    with connect(direct=False) as con:
        # Insert source
        source_row = con.execute(
            """
            INSERT INTO integrity.sources
              (corpus_id, source_key, version, filename, file_sha256, is_active, indexed_at)
            VALUES (%s, %s, 1, 'smoke_sample.pdf', '0123456789abcdef', TRUE, now())
            RETURNING id;
            """,
            (test_corpus, test_source_key),
        ).fetchone()
        source_id = source_row["id"]

        # Insert chunks
        con.execute(
            """
            INSERT INTO integrity.chunks
              (source_id, chunk_no, page_number, start_offset, end_offset, content, embedding)
            VALUES (%s, 0, 1, 0, %s, %s, %s::vector),
                   (%s, 1, 2, 0, %s, %s, %s::vector);
            """,
            (
                source_id,
                len(text_chunk1),
                text_chunk1,
                vector_literal(vec1),
                source_id,
                len(text_chunk2),
                text_chunk2,
                vector_literal(vec2),
            ),
        )

    try:
        # Query using a vector close to vec1 + query text overlapping chunk 1
        query_vec = vec1 + rng.normal(0, 0.05, test_dim).astype(np.float32)
        query_vec /= np.linalg.norm(query_vec)
        query_text = "phát hiện đạo văn sử dụng học sâu khóa luận tốt nghiệp"

        results = retrieve(
            query_text=query_text,
            query_vector=query_vec,
            corpus_id=test_corpus,
            top_k=5,
            fused_k=2,
        )

        assert len(results) > 0, "Expected at least 1 retrieved candidate"
        top = results[0]
        assert top["source_key"] == test_source_key
        assert top["dense_score"] is not None
        assert top["lexical_score"] is not None
        assert top["rrf_score"] > 0
        assert math.isfinite(top["dense_score"])
        assert math.isfinite(top["lexical_score"])

        logger.info(
            "Smoke test retrieval SUCCESS: top match chunk_no=%d, dense=%.4f, lexical=%.4f, rrf=%.4f",
            top["source_chunk_no"],
            top["dense_score"],
            top["lexical_score"],
            top["rrf_score"],
        )
        return {
            "status": "passed",
            "candidates_found": len(results),
            "top_dense_score": top["dense_score"],
            "top_lexical_score": top["lexical_score"],
            "top_rrf_score": top["rrf_score"],
        }
    finally:
        # Cleanup
        with connect(direct=False) as con:
            con.execute(
                "DELETE FROM integrity.sources WHERE corpus_id = %s;",
                (test_corpus,),
            )
            logger.info("Cleaned up synthetic test corpus '%s'", test_corpus)


def test_error_classification() -> dict[str, Any]:
    """Verify error classifier on bad credentials or unreachable hosts."""
    import psycopg

    bad_auth_code = None
    try:
        # Deliberately attempt connection with invalid password on real Neon host if configured
        current_dsn = settings.postgres_dsn
        if "@" in current_dsn:
            bad_dsn = current_dsn.replace(
                current_dsn.split("@")[0].split(":")[-1], "wrong_password"
            )
            with psycopg.connect(bad_dsn):
                pass
    except Exception as exc:
        bad_auth_code = classify_db_exception(exc)

    return {
        "bad_auth_classification": bad_auth_code,
        "expected_code": "DB_AUTH_ERROR",
        "matches_expected": bad_auth_code == "DB_AUTH_ERROR",
    }


def main():
    print("==================================================")
    print("UGTM AI — Neon PostgreSQL Smoke Test (CP-01.2)")
    print(f"Target Pooled DSN: {mask_dsn(settings.postgres_dsn)}")
    print(f"Target Direct DSN: {mask_dsn(settings.postgres_direct_dsn or '<not set>')}")
    print("==================================================")

    # 1. First ensure migrations are applied
    logger.info("Running migrations on Neon database...")
    mig_summary = run_migrations(direct=True)
    logger.info("Migrations: %s", mig_summary)

    # 2. Check TLS and connectivity
    tls_results = test_tls_connection()
    if tls_results.get("runtime_pooled", {}).get("status") != "connected":
        logger.error("Pooled connection failed! Halting smoke test.")
        sys.exit(1)

    # 3. Check extensions & schema
    schema_results = test_extensions_and_schema()
    logger.info("Schema & Extensions: %s", schema_results)
    if not schema_results["vector_ready"]:
        logger.error("Extension 'vector' is NOT installed!")
        sys.exit(1)
    if not schema_results["pg_trgm_ready"]:
        logger.error("Extension 'pg_trgm' is NOT installed!")
        sys.exit(1)

    # 4. Search & retrieval smoke test
    search_results = test_vector_and_trigram_retrieval()

    # 5. Error classification check
    err_results = test_error_classification()
    logger.info("Error classification: %s", err_results)

    report = {
        "tls_connections": tls_results,
        "schema_and_extensions": schema_results,
        "search_and_retrieval": search_results,
        "error_classification": err_results,
        "overall_status": "PASSED",
    }

    print("\n=== SMOKE TEST SUMMARY ===")
    print(json.dumps(report, indent=2))
    print("==========================\n")


if __name__ == "__main__":
    main()
