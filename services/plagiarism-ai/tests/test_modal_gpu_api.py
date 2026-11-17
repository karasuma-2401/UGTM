import numpy as np
import pytest
from fastapi.testclient import TestClient

from scripts.modal_gpu_api import create_app


class FakeEmbedder:
    def encode(self, texts, **kwargs):
        return {"dense_vecs": np.zeros((len(texts), 1024))}


class FakeReranker:
    def compute_score(self, pairs, **kwargs):
        return 0.75 if len(pairs) == 1 else [0.75] * len(pairs)


@pytest.fixture
def client():
    return TestClient(create_app(FakeEmbedder(), FakeReranker(), "test-secret", lambda: {"device": "fake", "cuda": True}))


def test_requires_secret_and_rejects_unauthorized_requests(client):
    with pytest.raises(ValueError):
        create_app(None, None, "", lambda: {})
    assert client.get("/health").status_code == 401
    assert client.get("/health", headers={"X-API-Key": "wrong"}).status_code == 401


def test_inference_contract(client):
    headers = {"X-API-Key": "test-secret"}
    health = client.get("/health", headers=headers).json()
    assert health["embedding_model"] == "BAAI/bge-m3"
    encoded = client.post("/encode", headers=headers, json={"texts": ["một", "hai"]}).json()
    assert encoded["count"] == 2
    assert len(encoded["dense_vecs"][0]) == 1024
    reranked = client.post("/rerank", headers=headers, json={"pairs": [["một", "hai"]]}).json()
    assert reranked["scores"] == [0.75]
    assert client.post("/encode", headers=headers, json={"texts": ["x"] * 129}).status_code == 422
