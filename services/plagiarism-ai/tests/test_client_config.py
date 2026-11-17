from __future__ import annotations

from fastapi.testclient import TestClient

from app.main import app


def test_client_config_does_not_leak_secrets():
    client = TestClient(app)
    resp = client.get("/api/v1/client-config")
    assert resp.status_code == 200
    data = resp.json()

    # CRITICAL: zero secrets in public client config
    assert "api_key" not in data
    assert "token" not in data
    assert "secret" not in data
    assert "postgres_dsn" not in data
    assert "database_url" not in data
    assert "redis_url" not in data
    assert "s3_secret_access_key" not in data

    # Public metadata flags
    assert data["default_corpus"] == "uit_thesis"
    assert "embedding_model" in data
    assert "reranker_model" in data
    assert "storage_backend" in data
    assert "inference_mode" in data
    assert "remote_gpu_enabled" in data

