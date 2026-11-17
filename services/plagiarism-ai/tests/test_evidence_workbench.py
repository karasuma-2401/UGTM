"""Tests for CP-05.3: Evidence Workbench & Report Schema v3 Contract.

Covers:
1. Schema v3 report fixture integrity and presence of all required fields.
2. No-match presentation contract (avoids claiming absolute originality).
3. Citation context and potential misattribution fields in evidence report.
4. Aligned spans character offsets and match types.
5. FastAPI root endpoint serving frontend workbench.
6. Report endpoint Schema v3 pagination and Schema v2 fallback.
"""

from __future__ import annotations

import json
import uuid
from pathlib import Path
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from app.config import settings
from app.main import app

FIXTURES_DIR = Path(__file__).resolve().parent / "fixtures"


@pytest.fixture
def client():
    return TestClient(app)


@pytest.fixture
def auth_headers():
    return {"X-API-Key": settings.api_key}


def test_schema_v3_violations_fixture_integrity():
    """Verify sample_report_v3_violations.json conforms strictly to Schema v3 specification."""
    fixture_path = FIXTURES_DIR / "sample_report_v3_violations.json"
    assert fixture_path.is_file(), "Fixture sample_report_v3_violations.json missing"

    data = json.loads(fixture_path.read_text(encoding="utf-8"))

    # Core structure
    assert data["schema_version"] == 3
    assert "job_id" in data
    assert "submission_id" in data
    assert "provenance" in data
    assert "summary" in data
    assert "passage_groups" in data
    assert "disclaimer" in data

    # Summary fields must not be None or wrong types
    summary = data["summary"]
    assert isinstance(summary["scanned_chunks"], int) and summary["scanned_chunks"] > 0
    assert isinstance(summary["scanned_characters"], int) and summary["scanned_characters"] > 0
    assert isinstance(summary["matched_characters"], int) and summary["matched_characters"] > 0
    assert 0.0 <= summary["similarity_coverage"] <= 1.0
    assert 0.0 <= summary["max_reranker_score"] <= 1.0
    assert summary["flagged_pairs"] > 0
    assert summary["matched_sources"] > 0
    assert summary["passage_groups"] > 0
    assert "cited_findings" in summary
    assert "uncited_findings" in summary
    assert "uncertain_findings" in summary

    # Provenance
    prov = data["provenance"]
    assert "embedding_model" in prov
    assert "reranker_model" in prov
    assert "detector_mode" in prov


def test_no_match_presentation_contract():
    """Verify clean no-match report fixture conforms to academic honesty rules."""
    fixture_path = FIXTURES_DIR / "sample_report_v3_nomatch.json"
    assert fixture_path.is_file(), "Fixture sample_report_v3_nomatch.json missing"

    data = json.loads(fixture_path.read_text(encoding="utf-8"))

    assert data["schema_version"] == 3
    summary = data["summary"]
    assert summary["similarity_coverage"] == 0.0
    assert summary["matched_characters"] == 0
    assert summary["flagged_pairs"] == 0
    assert len(data["passage_groups"]) == 0

    # Ensure no-match does not claim absolute originality
    interp = summary.get("interpretation", "")
    assert "nguyên bản tuyệt đối" not in interp or "không đồng nghĩa" in interp


def test_citation_context_and_misattribution_fields():
    """Verify evidence findings carry citation status, context, and secondary misattribution flags."""
    fixture_path = FIXTURES_DIR / "sample_report_v3_violations.json"
    data = json.loads(fixture_path.read_text(encoding="utf-8"))

    findings = []
    for g in data["passage_groups"]:
        findings.extend(g.get("findings", []))

    assert len(findings) == 3

    # Finding 1: absent citation
    f_uncited = next(f for f in findings if f["citation_status"] == "absent")
    assert f_uncited["citation_context"]["status"] == "absent"
    assert len(f_uncited["citation_context"]["markers"]) == 0
    assert f_uncited["citation_context"]["potential_misattribution"] is False

    # Finding 2: cited
    f_cited = next(f for f in findings if f["citation_status"] == "present")
    assert f_cited["citation_context"]["status"] == "present"
    assert len(f_cited["citation_context"]["markers"]) > 0

    # Finding 3: uncertain with potential secondary misattribution
    f_uncertain = next(f for f in findings if f["citation_status"] == "uncertain")
    assert f_uncertain["citation_context"]["potential_misattribution"] is True
    assert "Cảnh báo dẫn nguồn thứ cấp" in f_uncertain["citation_context"]["explanation"]


def test_aligned_spans_in_findings():
    """Verify aligned_spans contain accurate character boundaries and match types."""
    fixture_path = FIXTURES_DIR / "sample_report_v3_violations.json"
    data = json.loads(fixture_path.read_text(encoding="utf-8"))

    finding = data["passage_groups"][0]["findings"][0]
    spans = finding.get("aligned_spans", [])

    assert len(spans) == 2
    for s in spans:
        assert s["query_end"] > s["query_start"]
        assert s["source_end"] > s["source_start"]
        assert s["length"] > 0
        assert s["match_type"] in ("near_verbatim", "paraphrase")


def test_api_serves_frontend_workbench(client):
    """Verify root endpoint / serves the compiled React Workbench SPA."""
    resp = client.get("/")
    assert resp.status_code == 200
    assert "text/html" in resp.headers["content-type"]
    assert "UGTM" in resp.text or "<div id=\"root\">" in resp.text


def test_api_serves_report_schema_v3_and_v2_fallback(client, auth_headers):
    """Verify /api/v1/reports/{job_id} supports Schema v3 pagination and ?schema=2 fallback."""
    fake_id = str(uuid.uuid4())
    fixture_path = FIXTURES_DIR / "sample_report_v3_violations.json"
    fake_report = json.loads(fixture_path.read_text(encoding="utf-8"))

    fake_job_row = {
        "id": fake_id,
        "job_type": "scan",
        "status": "completed",
        "report": fake_report,
    }

    with patch("app.main.get_job", return_value=fake_job_row):
        # 1. Default schema (Schema v3)
        resp_v3 = client.get(f"/api/v1/reports/{fake_id}", headers=auth_headers)
        assert resp_v3.status_code == 200
        data_v3 = resp_v3.json()
        assert data_v3["schema_version"] == 3
        assert data_v3["summary"]["similarity_coverage"] == 0.25
        assert "pagination" in data_v3

        # 2. Schema v2 fallback (?schema=2)
        resp_v2 = client.get(f"/api/v1/reports/{fake_id}?schema=2", headers=auth_headers)
        assert resp_v2.status_code == 200
        data_v2 = resp_v2.json()
        assert data_v2["schema_version"] == 2
        assert "model" in data_v2
        assert "disclaimer" in data_v2

