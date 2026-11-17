import uuid
from unittest.mock import MagicMock

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def test_retry_scan_enqueues_only_after_commit(monkeypatch):
    job_id = uuid.uuid4()
    conn = MagicMock()
    conn.execute.return_value.fetchone.return_value = {"id": job_id}
    manager = MagicMock()
    manager.__enter__.return_value = conn
    enqueue = MagicMock()
    manager.__exit__.side_effect = lambda *args: setattr(manager, "committed", True)
    enqueue.side_effect = lambda *_: assert_committed(manager)
    monkeypatch.setattr("app.main.connect", lambda: manager)
    monkeypatch.setattr("app.main.safe_enqueue_job", enqueue)

    response = TestClient(app).post(f"/api/v1/jobs/{job_id}/retry", headers={"X-API-Key": settings.api_key})

    assert response.status_code == 202
    assert response.json() == {"job_id": str(job_id), "status": "queued"}
    enqueue.assert_called_once_with(str(job_id), "scan")


def assert_committed(manager):
    assert manager.committed


def test_retry_active_job_is_idempotent(monkeypatch):
    job_id = uuid.uuid4()
    conn = MagicMock()
    conn.execute.return_value.fetchone.side_effect = [None, {"job_type": "scan", "status": "processing"}]
    manager = MagicMock()
    manager.__enter__.return_value = conn
    enqueue = MagicMock()
    monkeypatch.setattr("app.main.connect", lambda: manager)
    monkeypatch.setattr("app.main.safe_enqueue_job", enqueue)
    response = TestClient(app).post(f"/api/v1/jobs/{job_id}/retry", headers={"X-API-Key": settings.api_key})
    assert response.status_code == 202
    assert response.json()["status"] == "processing"
    enqueue.assert_not_called()


def test_retry_rejects_index_job_and_unknown_job(monkeypatch):
    conn = MagicMock()
    conn.execute.return_value.fetchone.side_effect = [None, {"job_type": "index", "status": "completed"}, None, None]
    manager = MagicMock()
    manager.__enter__.return_value = conn
    monkeypatch.setattr("app.main.connect", lambda: manager)
    client = TestClient(app)
    headers = {"X-API-Key": settings.api_key}
    assert client.post(f"/api/v1/jobs/{uuid.uuid4()}/retry", headers=headers).status_code == 409
    assert client.post(f"/api/v1/jobs/{uuid.uuid4()}/retry", headers=headers).status_code == 404
