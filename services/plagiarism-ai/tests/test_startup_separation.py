from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

from fastapi.testclient import TestClient

from app.main import app


def test_api_and_worker_import_without_gpu_modules():
    """Verify API and Celery CPU worker start in a fresh Python process without loading Torch/BGE."""
    project_root = Path(__file__).resolve().parent.parent
    env = os.environ.copy()
    env.setdefault("POSTGRES_DSN", "postgresql://ugtm_ai:test@localhost:5432/ugtm_ai")
    env.setdefault("REDIS_URL", "redis://localhost:6379/0")
    env.setdefault("API_KEY", "test-api-key")
    env["INFERENCE_MODE"] = "remote"

    probe_code = """
import json
import sys

import app.main
import app.celery_app
import app.tasks
import app.models

forbidden = ("torch", "transformers", "FlagEmbedding", "sentence_transformers")
loaded_forbidden = [
    name for name in sys.modules
    if any(name == pkg or name.startswith(pkg + ".") for pkg in forbidden)
]
registered_tasks = sorted(
    name for name in app.celery_app.celery_app.tasks.keys()
    if name.startswith("app.tasks.")
)
print(json.dumps({
    "loaded_forbidden": loaded_forbidden,
    "registered_tasks": registered_tasks,
    "backend_mode": app.models.get_inference_backend().mode,
}))
"""
    proc = subprocess.run(
        [sys.executable, "-c", probe_code],
        cwd=str(project_root),
        env=env,
        capture_output=True,
        text=True,
        check=True,
    )
    result = json.loads(proc.stdout.strip())
    assert result["loaded_forbidden"] == []
    assert result["registered_tasks"] == [
        "app.tasks.index_source",
        "app.tasks.scan_document",
    ]
    assert result["backend_mode"] == "remote"


def test_fastapi_healthz_and_client_config_do_not_expose_gpu_token():
    client = TestClient(app)

    health = client.get("/healthz")
    assert health.status_code == 200
    assert health.json() == {"status": "ok"}

    cfg = client.get("/api/v1/client-config")
    assert cfg.status_code == 200
    body = cfg.json()
    assert body["inference_mode"] in ("remote", "local")
    assert "remote_gpu_api_key" not in body

