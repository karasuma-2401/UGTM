"""Comprehensive End-to-End Smoke Test for UGTM-Plagiarism-AI (CP-01.6).

Verifies the full pipeline with all live cloud infrastructure:
1. Infrastructure versions probe (Neon PostgreSQL + pgvector, Redis Cloud, Cloudflare R2, Google Colab GPU).
2. Public client-config secret audit (assert zero secrets/API keys exposed).
3. End-to-end indexing of source_doc.docx via Cloudflare R2, Colab GPU /encode, and Neon pgvector.
4. End-to-end scan of suspect_doc.docx via R2, Colab GPU /encode, Neon RRF dense+trigram search, Colab GPU /rerank, and report generation.
5. Offline GPU resilience verification (verifies worker captures GPU_UNAVAILABLE error without hanging).
"""

from __future__ import annotations

import hashlib
import json
import logging
import sys
import time
import uuid
from pathlib import Path
from typing import Any

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from fastapi.testclient import TestClient
import redis
from redis.backoff import ExponentialBackoff
from redis.retry import Retry

from app.config import settings
from app.db import connect, get_job, mask_dsn
from app.inference.remote import RemoteInferenceClient
from app.main import app
from app.storage import get_storage
from app.tasks import index_source, scan_document

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("smoke_test_e2e")


def probe_infrastructure_versions() -> dict[str, Any]:
    logger.info("=== 1. Probing Infrastructure Component Versions ===")
    results: dict[str, Any] = {}

    # 1. Neon PostgreSQL & Extensions
    with connect() as con:
        pg_version = con.execute("SELECT version();").fetchone()["version"]
        extensions = con.execute(
            """
            SELECT extname, extversion
            FROM pg_extension
            WHERE extname IN ('vector', 'pg_trgm');
            """
        ).fetchall()
        ext_map = {r["extname"]: r["extversion"] for r in extensions}
        results["neon_postgres"] = {
            "version_string": pg_version,
            "pgvector_version": ext_map.get("vector", "missing"),
            "pg_trgm_version": ext_map.get("pg_trgm", "missing"),
            "masked_dsn": mask_dsn(settings.postgres_dsn),
        }
    logger.info("Neon: %s | pgvector: %s | pg_trgm: %s",
                results["neon_postgres"]["version_string"][:40],
                results["neon_postgres"]["pgvector_version"],
                results["neon_postgres"]["pg_trgm_version"])

    # 2. Redis Cloud
    retry_policy = Retry(ExponentialBackoff(cap=5, base=1), 3)
    r = redis.from_url(
        settings.redis_url,
        retry=retry_policy,
        retry_on_timeout=True,
        socket_connect_timeout=15,
        socket_timeout=15,
        socket_keepalive=True,
    )
    info = r.info("server")
    results["redis_cloud"] = {
        "redis_version": info.get("redis_version"),
        "os": info.get("os"),
        "mode": info.get("redis_mode"),
        "used_memory_human": r.info("memory").get("used_memory_human"),
    }
    logger.info("Redis Cloud: v%s on %s (used mem: %s)",
                results["redis_cloud"]["redis_version"],
                results["redis_cloud"]["os"],
                results["redis_cloud"]["used_memory_human"])

    # 3. Cloudflare R2 Object Storage
    storage = get_storage()
    results["storage"] = {
        "backend": storage.name,
        "bucket": settings.effective_s3_bucket,
        "endpoint": settings.s3_endpoint_url,
    }
    logger.info("Storage: %s (bucket: %s, endpoint: %s)",
                storage.name,
                settings.effective_s3_bucket,
                settings.s3_endpoint_url)

    # 4. Remote Colab GPU Worker
    gpu_client = RemoteInferenceClient(
        base_url=settings.remote_gpu_url,
        api_key=settings.remote_gpu_api_key,
        embedding_model=settings.embedding_model,
        embedding_revision=settings.embedding_revision,
        embedding_dim=settings.embedding_dim,
        reranker_model=settings.reranker_model,
        reranker_revision=settings.reranker_revision,
        default_batch_size=settings.remote_gpu_batch_size,
        timeout_seconds=settings.remote_gpu_timeout_seconds,
        health_timeout_seconds=settings.remote_gpu_health_timeout_seconds,
    )
    health = gpu_client.health()
    results["colab_gpu"] = {
        "device": health.get("device"),
        "cuda": health.get("cuda"),
        "vram_allocated_mb": health.get("vram_allocated_mb"),
        "embedding_model": health.get("embedding_model"),
        "embedding_revision": health.get("embedding_revision"),
        "reranker_model": health.get("reranker_model"),
        "reranker_revision": health.get("reranker_revision"),
        "remote_gpu_url": settings.remote_gpu_url,
    }
    logger.info("Colab GPU: %s (VRAM: %s MB) | Models: %s + %s",
                health.get("device"),
                health.get("vram_allocated_mb"),
                health.get("embedding_model"),
                health.get("reranker_model"))

    return results


def test_client_config_secrets(client: TestClient) -> dict[str, Any]:
    logger.info("=== 2. Verifying /api/v1/client-config (Zero Secrets Leak) ===")
    resp = client.get("/api/v1/client-config")
    assert resp.status_code == 200, f"Expected 200, got {resp.status_code}"
    data = resp.json()

    assert "api_key" not in data, "CRITICAL: api_key must NOT be exposed in client-config!"
    assert "token" not in data
    assert "secret" not in data
    assert "database_url" not in data
    assert "redis_url" not in data

    assert data.get("default_corpus") == "uit_thesis"
    assert data.get("storage_backend") == "s3"
    assert data.get("inference_mode") == "remote"
    assert data.get("remote_gpu_enabled") is True
    logger.info("Client config verified secure: %s", data)
    return data


def run_e2e_indexing_and_scanning(client: TestClient) -> dict[str, Any]:
    logger.info("=== 3. Executing End-to-End Pipeline on Sample Documents ===")
    test_run_id = uuid.uuid4().hex[:6]
    corpus_id = "uit_thesis"
    source_id = f"kltn_nguon_{test_run_id}"
    submission_id = f"kltn_nop_{test_run_id}"

    source_path = PROJECT_ROOT / "app" / "static" / "samples" / "source_doc.docx"
    suspect_path = PROJECT_ROOT / "app" / "static" / "samples" / "suspect_doc.docx"

    assert source_path.exists(), f"Source sample not found at {source_path}"
    assert suspect_path.exists(), f"Suspect sample not found at {suspect_path}"

    headers = {"X-API-Key": settings.api_key}

    # --- Step A: Index Source Document ---
    logger.info("3A. Uploading and indexing source document: %s", source_path.name)
    t_index_start = time.perf_counter()
    with source_path.open("rb") as f:
        resp = client.post(
            "/api/v1/corpus/documents",
            headers=headers,
            data={
                "corpus_id": corpus_id,
                "source_id": source_id,
                "version": 1,
            },
            files={"file": (source_path.name, f, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")},
        )
    assert resp.status_code == 202, f"Expected 202, got {resp.status_code}: {resp.text}"
    index_job_id = resp.json()["job_id"]
    logger.info("Source document accepted -> index_job_id=%s", index_job_id)

    # Verify job record in Neon
    job_record = get_job(index_job_id)
    assert job_record["status"] == "queued"
    assert job_record["file_path"].startswith("uploads/")  # Stored in R2

    # Execute worker task for indexing
    logger.info("Worker executing index_source(%s)...", index_job_id)
    index_source(index_job_id)
    t_index_end = time.perf_counter()
    index_duration_s = round(t_index_end - t_index_start, 2)

    # Verify Neon indexing state
    job_record = get_job(index_job_id)
    assert job_record["status"] == "completed", f"Index job failed: {job_record}"
    logger.info("Indexing completed in %.2fs. Report: %s", index_duration_s, job_record.get("report"))

    with connect() as con:
        source_row = con.execute(
            "SELECT id, is_active FROM integrity.sources WHERE corpus_id=%s AND source_key=%s",
            (corpus_id, source_id),
        ).fetchone()
        assert source_row is not None
        assert source_row["is_active"] is True

        chunks = con.execute(
            """
            SELECT chunk_no, page_number, start_offset, end_offset,
                   vector_dims(embedding) as dim
            FROM integrity.chunks
            WHERE source_id = %s
            ORDER BY chunk_no
            """,
            (source_row["id"],),
        ).fetchall()
        assert len(chunks) == 4, f"Expected 4 chunks for source_doc.docx, got {len(chunks)}"
        assert all(c["dim"] == 1024 for c in chunks), "All embeddings must be 1024 dimensions"
        logger.info("Indexed %d chunks with 1024-d pgvector embeddings in Neon.", len(chunks))

    # --- Step B: Scan Suspect Document ---
    logger.info("3B. Uploading and scanning suspect document: %s", suspect_path.name)
    t_scan_start = time.perf_counter()
    with suspect_path.open("rb") as f:
        resp = client.post(
            "/api/v1/scans",
            headers=headers,
            data={
                "corpus_id": corpus_id,
                "submission_id": submission_id,
                "version": 1,
            },
            files={"file": (suspect_path.name, f, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")},
        )
    assert resp.status_code == 202, f"Expected 202, got {resp.status_code}: {resp.text}"
    scan_job_id = resp.json()["job_id"]
    logger.info("Suspect document accepted -> scan_job_id=%s", scan_job_id)

    # Verify job record in Neon
    scan_job = get_job(scan_job_id)
    assert scan_job["status"] == "queued"
    assert scan_job["file_path"].startswith("uploads/")  # Stored in R2

    # Execute worker task for scanning
    logger.info("Worker executing scan_document(%s)...", scan_job_id)
    scan_document(scan_job_id)
    t_scan_end = time.perf_counter()
    scan_duration_s = round(t_scan_end - t_scan_start, 2)

    # Verify Neon job status
    poll_resp = client.get(f"/api/v1/jobs/{scan_job_id}", headers=headers)
    assert poll_resp.status_code == 200
    assert poll_resp.json()["status"] == "completed"

    # Fetch final report
    report_resp = client.get(f"/api/v1/reports/{scan_job_id}", headers=headers)
    assert report_resp.status_code == 200
    report = report_resp.json()

    assert report["schema_version"] == 2
    assert report["summary"]["scanned_chunks"] == 3
    assert report["summary"]["flagged_pairs"] > 0
    assert len(report["findings"]) > 0

    top_finding = report["findings"][0]
    logger.info("Scan completed in %.2fs. Flagged pairs: %d | Top match: source=%s (reranker=%.4f, dense=%.4f, lexical=%.4f)",
                scan_duration_s,
                report["summary"]["flagged_pairs"],
                top_finding["source_key"],
                top_finding["reranker_score"],
                top_finding["dense_score"],
                top_finding["lexical_score"])

    return {
        "index_job_id": index_job_id,
        "scan_job_id": scan_job_id,
        "index_duration_s": index_duration_s,
        "scan_duration_s": scan_duration_s,
        "indexed_chunks": len(chunks),
        "scanned_chunks": report["summary"]["scanned_chunks"],
        "flagged_pairs": report["summary"]["flagged_pairs"],
        "passage_groups": report["summary"]["passage_groups"],
        "top_reranker_score": top_finding["reranker_score"],
        "top_dense_score": top_finding["dense_score"],
        "top_lexical_score": top_finding["lexical_score"],
    }


def test_offline_gpu_resilience(client: TestClient) -> dict[str, Any]:
    logger.info("=== 4. Testing Offline Remote GPU Resilience ===")
    original_gpu_url = settings.remote_gpu_url
    original_timeout = settings.remote_gpu_timeout_seconds

    test_run_id = uuid.uuid4().hex[:6]
    test_sub_id = f"fail_test_{test_run_id}"
    suspect_path = PROJECT_ROOT / "app" / "static" / "samples" / "suspect_doc.docx"

    try:
        # Point to unreachable URL with 2s timeout
        settings.remote_gpu_url = "http://127.0.0.1:9999/unreachable_gpu"
        settings.remote_gpu_timeout_seconds = 2.0
        settings.remote_gpu_health_timeout_seconds = 2.0

        headers = {"X-API-Key": settings.api_key}
        with suspect_path.open("rb") as f:
            resp = client.post(
                "/api/v1/scans",
                headers=headers,
                data={
                    "corpus_id": "uit_thesis",
                    "submission_id": test_sub_id,
                    "version": 1,
                },
                files={"file": (suspect_path.name, f, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")},
            )
        fail_job_id = resp.json()["job_id"]

        t0 = time.perf_counter()
        logger.info("Worker executing scan_document(%s) against dead GPU...", fail_job_id)
        scan_document(fail_job_id)
        duration_s = round(time.perf_counter() - t0, 2)

        job_record = get_job(fail_job_id)
        assert job_record["status"] == "failed", f"Expected failed status, got {job_record['status']}"
        assert job_record["error_code"] in {"GPU_UNAVAILABLE", "GPU_CONNECTION_ERROR"}, (
            f"Expected GPU error code, got {job_record['error_code']}"
        )
        logger.info("Resilience verified! Job cleanly transitioned to failed (error_code=%s) in %.2fs without hanging.",
                    job_record["error_code"], duration_s)

        return {
            "status": "PASSED",
            "error_code_recorded": job_record["error_code"],
            "detection_duration_s": duration_s,
        }
    finally:
        settings.remote_gpu_url = original_gpu_url
        settings.remote_gpu_timeout_seconds = original_timeout


def main():
    logger.info("Starting End-to-End Smoke Test Suite (CP-01.6)...")
    client = TestClient(app)

    # 1. Probing versions
    versions = probe_infrastructure_versions()

    # 2. Secret audit
    config = test_client_config_secrets(client)

    # 3. Full pipeline run
    pipeline_res = run_e2e_indexing_and_scanning(client)

    # 4. Resilience test
    resilience_res = test_offline_gpu_resilience(client)

    summary = {
        "status": "ALL_TESTS_PASSED",
        "infrastructure": versions,
        "client_config_audit": config,
        "pipeline_execution": pipeline_res,
        "offline_resilience": resilience_res,
    }

    print("\n" + "=" * 60)
    print("UGTM AI — END-TO-END SMOKE TEST REPORT (CP-01.6)")
    print("=" * 60)
    print(json.dumps(summary, indent=2))
    print("=" * 60)


if __name__ == "__main__":
    main()
