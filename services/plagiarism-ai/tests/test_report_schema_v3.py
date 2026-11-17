from __future__ import annotations

import json
from pathlib import Path
import pytest
from fastapi.testclient import TestClient

from app.config import settings
from app.documents import Chunk
from app.main import app
from app.reports import (
    DEFAULT_DISCLAIMER,
    PARSER_VERSION,
    ReportV3Model,
    build_report_v3,
    compute_similarity_coverage,
    convert_report_to_schema_v2,
    extract_passage_groups,
    paginate_report_findings,
)

FIXTURES_DIR = Path(__file__).resolve().parent.parent / "docs" / "contracts"


def test_report_v3_fixture_validates_against_pydantic():
    """Verify that docs/contracts/report_v3.sample.json strictly satisfies ReportV3Model."""
    sample_file = FIXTURES_DIR / "report_v3.sample.json"
    assert sample_file.exists(), f"Sample fixture missing at {sample_file}"

    payload = json.loads(sample_file.read_text(encoding="utf-8"))
    # Exclude internal comment
    clean_payload = {k: v for k, v in payload.items() if not k.startswith("_")}

    validated = ReportV3Model.model_validate(clean_payload)
    assert validated.schema_version == 3
    assert validated.provenance.parser_version == "parser-v2.1-block"
    assert validated.summary.similarity_coverage == 0.0236
    assert len(validated.passage_groups) == 1
    assert len(validated.findings) == 2


def test_compute_similarity_coverage_interval_merging():
    """Verify interval union calculation correctly merges overlapping chunks."""
    # Empty case
    matched, cov = compute_similarity_coverage([], total_characters=1000)
    assert matched == 0
    assert cov == 0.0

    # Disjoint intervals: [100, 200] (len 100) and [300, 450] (len 150)
    findings = [
        {"submission_start": 100, "submission_end": 200},
        {"submission_start": 300, "submission_end": 450},
    ]
    matched, cov = compute_similarity_coverage(findings, total_characters=1000)
    assert matched == 250
    assert cov == 0.25

    # Overlapping chunks (due to sliding window): [100, 250] and [200, 350] -> union is [100, 350] (len 250)
    overlapping_findings = [
        {"submission_start": 100, "submission_end": 250},
        {"submission_start": 200, "submission_end": 350},
    ]
    matched, cov = compute_similarity_coverage(overlapping_findings, total_characters=1000)
    assert matched == 250
    assert cov == 0.25

    # Nested intervals: [100, 500] and [200, 300] -> union is [100, 500] (len 400)
    nested = [
        {"submission_start": 100, "submission_end": 500},
        {"submission_start": 200, "submission_end": 300},
    ]
    matched, cov = compute_similarity_coverage(nested, total_characters=1000)
    assert matched == 400
    assert cov == 0.40


def test_build_report_v3_does_not_cap_at_200():
    """Verify that build_report_v3 preserves all findings without arbitrary 200 truncation."""
    # Synthesize 250 findings
    synthetic_findings = []
    for i in range(250):
        synthetic_findings.append({
            "submission_chunk_no": i,
            "submission_page": (i // 5) + 1,
            "submission_start": i * 100,
            "submission_end": (i + 1) * 100,
            "submission_text": f"Đoạn văn thứ {i}",
            "chunk_id": 1000 + i,
            "source_key": f"source-{(i % 3) + 1}",
            "source_version": 1,
            "source_filename": f"source-{(i % 3) + 1}.pdf",
            "document_family": f"fam-{(i % 3) + 1}",
            "source_chunk_no": i,
            "source_page": (i // 5) + 1,
            "source_start": i * 100,
            "source_end": (i + 1) * 100,
            "source_text": f"Đoạn gốc thứ {i}",
            "rrf_score": 0.03,
            "dense_score": 0.85,
            "lexical_score": 0.50,
            "reranker_score": 0.70 + (i % 30) * 0.01,
            "decision_source": "heuristic",
            "citation_status": "unexamined",
            "review_required": True,
        })

    mock_query_chunks = [
        Chunk(chunk_no=i, page_number=1, start_offset=i*100, end_offset=(i+1)*100, text="A" * 100)
        for i in range(250)
    ]

    report = build_report_v3(
        job_id="test-job-unlimited",
        job={"corpus_id": "uit-v1", "document_key": "sub-1", "version": 1},
        query_chunks=mock_query_chunks,
        all_findings=synthetic_findings,
    )

    assert report["schema_version"] == 3
    # Check that ALL 250 findings are preserved, NOT cut at 200
    assert len(report["findings"]) == 250
    assert report["summary"]["total_findings"] == 250
    assert report["summary"]["flagged_pairs"] == 250
    assert report["summary"]["scanned_characters"] == 25000
    assert report["summary"]["matched_characters"] == 25000
    assert report["summary"]["similarity_coverage"] == 1.0


def test_compatibility_adapter_schema_v2():
    """Verify that convert_report_to_schema_v2 seamlessly downgrades v3 to v2."""
    sample_file = FIXTURES_DIR / "report_v3.sample.json"
    payload = json.loads(sample_file.read_text(encoding="utf-8"))
    clean_v3 = {k: v for k, v in payload.items() if not k.startswith("_")}

    v2 = convert_report_to_schema_v2(clean_v3)
    assert v2["schema_version"] == 2
    assert "provenance" not in v2
    assert "model" in v2
    assert v2["model"]["embedding"] == "BAAI/bge-m3"
    assert v2["model"]["reranker"] == "BAAI/bge-reranker-v2-m3"
    assert v2["summary"]["scanned_chunks"] == 84
    assert v2["summary"]["flagged_pairs"] == 2
    assert len(v2["findings"]) == 2


def test_pagination_of_findings():
    """Verify findings pagination metadata and slicing."""
    synthetic_findings = [
        {"passage_group_id": f"PG-{i}", "submission_start": i*10, "submission_end": (i+1)*10, "reranker_score": 0.8}
        for i in range(25)
    ]
    report = {
        "schema_version": 3,
        "summary": {"total_findings": 25, "findings_returned": 25},
        "findings": synthetic_findings,
    }

    # Page 1, size 10
    p1 = paginate_report_findings(report, page=1, page_size=10)
    assert len(p1["findings"]) == 10
    assert p1["summary"]["findings_returned"] == 10
    assert p1["pagination"]["page"] == 1
    assert p1["pagination"]["total_pages"] == 3
    assert p1["pagination"]["has_next"] is True
    assert p1["pagination"]["has_prev"] is False

    # Page 3, size 10 (last page with 5 items)
    p3 = paginate_report_findings(report, page=3, page_size=10)
    assert len(p3["findings"]) == 5
    assert p3["summary"]["findings_returned"] == 5
    assert p3["pagination"]["page"] == 3
    assert p3["pagination"]["has_next"] is False
    assert p3["pagination"]["has_prev"] is True


def test_api_report_endpoint_v3_and_v2_adaptation(monkeypatch):
    """Verify FastAPI /api/v1/reports/{job_id} supports Schema v3 and v2 fallback."""
    import uuid
    from app.db import get_job

    mock_job_id = str(uuid.uuid4())
    sample_file = FIXTURES_DIR / "report_v3.sample.json"
    payload = json.loads(sample_file.read_text(encoding="utf-8"))
    clean_v3 = {k: v for k, v in payload.items() if not k.startswith("_")}
    clean_v3["job_id"] = mock_job_id

    fake_job_row = {
        "id": mock_job_id,
        "type": "scan",
        "status": "completed",
        "error_code": None,
        "report": clean_v3,
        "updated_at": "2026-10-30T11:45:00Z",
    }

    monkeypatch.setattr("app.main.get_job", lambda j_id: fake_job_row if str(j_id) == mock_job_id else None)

    client = TestClient(app)
    headers = {"X-API-Key": settings.api_key}

    # 1. Default request -> Schema v3
    res_default = client.get(f"/api/v1/reports/{mock_job_id}", headers=headers)
    assert res_default.status_code == 200
    data_v3 = res_default.json()
    assert data_v3["schema_version"] == 3
    assert "provenance" in data_v3
    assert "pagination" in data_v3

    # 2. Explicit request ?schema=2 -> Downgrades to Schema v2
    res_v2 = client.get(f"/api/v1/reports/{mock_job_id}?schema=2", headers=headers)
    assert res_v2.status_code == 200
    data_v2 = res_v2.json()
    assert data_v2["schema_version"] == 2
    assert "provenance" not in data_v2
    assert "model" in data_v2

    # 3. Request with pagination ?page=1&page_size=1
    res_page = client.get(f"/api/v1/reports/{mock_job_id}?page=1&page_size=1", headers=headers)
    assert res_page.status_code == 200
    data_page = res_page.json()
    assert len(data_page["findings"]) == 1
    assert data_page["pagination"]["total_pages"] == 2
    assert data_page["pagination"]["has_next"] is True

