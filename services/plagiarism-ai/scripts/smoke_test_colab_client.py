"""CLI tool to verify connectivity and inference contract with Colab Remote GPU Worker (CP-01.5).

Usage:
    python scripts/smoke_test_colab_client.py
    python scripts/smoke_test_colab_client.py --url https://example.trycloudflare.com --key secret-token
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
import time
from pathlib import Path
from typing import Any

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from app.config import settings
from app.inference.remote import RemoteInferenceClient

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("smoke_test_colab")


def test_colab_worker(url: str, key: str | None = None, health_timeout: float = 8.0) -> dict[str, Any]:
    client = RemoteInferenceClient(
        base_url=url,
        api_key=key,
        embedding_model=settings.embedding_model,
        embedding_revision=settings.embedding_revision,
        embedding_dim=settings.embedding_dim,
        reranker_model=settings.reranker_model,
        reranker_revision=settings.reranker_revision,
        timeout_seconds=30.0,
        health_timeout_seconds=health_timeout,
    )

    logger.info("1. Probing /health endpoint...")
    t0 = time.perf_counter()
    health_data = client.health()
    health_latency_ms = round((time.perf_counter() - t0) * 1000, 2)
    logger.info("Health OK in %.2f ms: device='%s'", health_latency_ms, health_data.get("device"))

    logger.info("2. Testing /encode with sample Vietnamese sentences...")
    sample_texts = [
        "Hệ thống phát hiện đạo văn học thuật dựa trên mô hình nhúng BGE-M3.",
        "Trường Đại học Công nghệ Thông tin - Đại học Quốc gia TP. Hồ Chí Minh.",
    ]
    t1 = time.perf_counter()
    vecs = client.encode(sample_texts, batch_size=2)
    encode_latency_ms = round((time.perf_counter() - t1) * 1000, 2)
    assert vecs.shape == (2, 1024), f"Expected shape (2, 1024), got {vecs.shape}"
    logger.info("Encode OK in %.2f ms: vector shape=%s", encode_latency_ms, vecs.shape)

    logger.info("3. Testing /rerank with sample candidate pair...")
    sample_pairs = [
        [
            "Thuật toán đối chiếu tài liệu và trích xuất bằng chứng.",
            "Phương pháp so khớp văn bản và tìm kiếm bằng chứng tương đồng.",
        ]
    ]
    t2 = time.perf_counter()
    scores = client.rerank(sample_pairs, batch_size=1)
    rerank_latency_ms = round((time.perf_counter() - t2) * 1000, 2)
    assert len(scores) == 1, f"Expected 1 score, got {len(scores)}"
    logger.info("Rerank OK in %.2f ms: score=%.4f", rerank_latency_ms, scores[0])

    return {
        "status": "PASSED",
        "remote_gpu_url": url,
        "device": health_data.get("device"),
        "cuda": health_data.get("cuda"),
        "vram_allocated_mb": health_data.get("vram_allocated_mb"),
        "embedding_model": health_data.get("embedding_model"),
        "reranker_model": health_data.get("reranker_model"),
        "health_latency_ms": health_latency_ms,
        "encode_latency_ms": encode_latency_ms,
        "rerank_latency_ms": rerank_latency_ms,
        "vector_shape": list(vecs.shape),
        "sample_rerank_score": round(scores[0], 4),
    }


def main():
    parser = argparse.ArgumentParser(description="Test Colab Remote GPU Inference Worker")
    parser.add_argument("--url", default=None, help="Colab Cloudflare Tunnel endpoint URL")
    parser.add_argument("--key", default=None, help="Remote GPU API key")
    parser.add_argument("--health-timeout", type=float, default=8.0, help="Health timeout in seconds (allow more for a cold start)")
    args = parser.parse_args()

    target_url = (args.url or settings.remote_gpu_url or "").strip().rstrip("/")
    target_key = (args.key or settings.remote_gpu_api_key or "").strip() or None

    print("==================================================")
    print("UGTM AI — Remote GPU Worker Smoke Test (CP-01.5)")
    print(f"Target URL: {target_url or '<NOT CONFIGURED>'}")
    print("==================================================")

    if not target_url:
        print("\n[NOTE] REMOTE_GPU_URL is currently not set in .env.")
        print("To start the GPU worker:")
        print("  1. Open notebooks/colab_gpu_worker.ipynb in Google Colab (T4 GPU).")
        print("  2. Run all cells and copy the generated public endpoint URL.")
        print("  3. Set REMOTE_GPU_URL=https://... in .env or run with --url https://...")
        print("==================================================\n")
        return

    try:
        report = test_colab_worker(target_url, target_key, args.health_timeout)
        print("\n=== GPU WORKER TEST RESULT ===")
        print(json.dumps(report, indent=2))
        print("==============================\n")
    except Exception as exc:
        logger.error("GPU worker verification failed: %s", exc)
        sys.exit(1)


if __name__ == "__main__":
    main()

