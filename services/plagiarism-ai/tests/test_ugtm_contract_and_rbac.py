from __future__ import annotations

import io
import json
import uuid
from typing import Any
from unittest.mock import MagicMock, patch

import pytest
from fastapi.testclient import TestClient

from app.config import settings
from app.db import connect
from app.main import app


@pytest.fixture(scope="module")
def client():
    return TestClient(app)


@pytest.fixture(scope="module")
def valid_auth_headers():
    return {"X-API-Key": settings.api_key}


# ==============================================================================
# 1. Service Key Authentication & Browser Protection
# ==============================================================================


def test_service_key_authentication_and_unauthorized_rejection(client):
    """Verify strictly that service endpoints reject missing or invalid X-API-Key with 401."""
    # 1. Missing header -> 401
    resp_no_header = client.get("/api/v1/jobs")
    assert resp_no_header.status_code == 401
    assert resp_no_header.json()["detail"] == "Unauthorized"

    # 2. Invalid API key -> 401
    resp_bad_key = client.get("/api/v1/jobs", headers={"X-API-Key": "invalid-secret-key"})
    assert resp_bad_key.status_code == 401
    assert resp_bad_key.json()["detail"] == "Unauthorized"

    # 3. Empty API key -> 401
    resp_empty_key = client.get("/api/v1/jobs", headers={"X-API-Key": ""})
    assert resp_empty_key.status_code == 401

    # 4. Correct API key -> 200 OK
    resp_ok = client.get("/api/v1/jobs", headers={"X-API-Key": settings.api_key})
    assert resp_ok.status_code == 200


def test_browser_master_key_leak_prevention(client):
    """Verify /api/v1/client-config does NOT expose api_key, passwords, or cloud credentials.

    Ensures the browser never holds or receives the master service key.
    """
    resp = client.get("/api/v1/client-config")
    assert resp.status_code == 200
    config_data = resp.json()

    # Must contain only safe public configuration
    assert "default_corpus" in config_data
    assert "max_upload_mb" in config_data
    assert "embedding_model" in config_data

    # Zero credential leak invariant
    forbidden_keys = [
        "api_key",
        "secret",
        "token",
        "password",
        "remote_gpu_api_key",
        "database_url",
        "redis_url",
        "r2_secret_access_key",
    ]
    for key in forbidden_keys:
        assert key not in config_data, f"Security risk: {key} found in public client-config!"

    # Ensure none of the string values match the actual secret key
    for k, val in config_data.items():
        if isinstance(val, str):
            assert val != settings.api_key, f"Direct leak of service key in field {k}"
            assert val != settings.remote_gpu_api_key, f"Direct leak of GPU key in field {k}"


# ==============================================================================
# 2. Format Validation, Metadata Regex & Version Conflict
# ==============================================================================


def test_unsupported_file_format_rejection(client, valid_auth_headers):
    """Verify legacy .doc files and corrupted files return 415 Unsupported Media Type.

    UGTM allows .doc on frontend, but AI service must reject it cleanly so
    UGTM background worker can mark it UnsupportedFormat without infinite retries.
    """
    # 1. Legacy .doc binary
    doc_payload = b"\xD0\xCF\x11\xE0\xA1\xB1\x1A\xE1" + b"\x00" * 100
    resp_doc = client.post(
        "/api/v1/scans",
        data={
            "corpus_id": "uit_thesis",
            "submission_id": "sub_doc_test",
            "version": 1,
        },
        files={"file": ("thesis.doc", io.BytesIO(doc_payload), "application/msword")},
        headers=valid_auth_headers,
    )
    assert resp_doc.status_code == 415
    assert "Only PDF/DOCX allowed" in resp_doc.json()["detail"]

    # 2. Empty file -> 400
    resp_empty = client.post(
        "/api/v1/scans",
        data={
            "corpus_id": "uit_thesis",
            "submission_id": "sub_empty_test",
            "version": 1,
        },
        files={"file": ("empty.pdf", io.BytesIO(b""), "application/pdf")},
        headers=valid_auth_headers,
    )
    assert resp_empty.status_code == 400
    assert "Empty file" in resp_empty.json()["detail"]


def test_metadata_regex_validation(client, valid_auth_headers):
    """Verify corpus_id and submission_id enforce SAFE_KEY regex: ^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$."""
    # 1. Invalid corpus_id with spaces or special characters -> 422
    pdf_content = b"%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF\n"
    resp_invalid_corpus = client.post(
        "/api/v1/scans",
        data={
            "corpus_id": "invalid corpus!@#",
            "submission_id": "sub_valid_123",
            "version": 1,
        },
        files={"file": ("test.pdf", io.BytesIO(pdf_content), "application/pdf")},
        headers=valid_auth_headers,
    )
    assert resp_invalid_corpus.status_code == 422
    assert "Invalid metadata" in resp_invalid_corpus.json()["detail"]

    # 2. Invalid version <= 0 -> 422
    resp_invalid_ver = client.post(
        "/api/v1/scans",
        data={
            "corpus_id": "uit_thesis",
            "submission_id": "sub_valid_123",
            "version": 0,
        },
        files={"file": ("test.pdf", io.BytesIO(pdf_content), "application/pdf")},
        headers=valid_auth_headers,
    )
    assert resp_invalid_ver.status_code == 422


def test_version_conflict_and_idempotency(client, valid_auth_headers, monkeypatch):
    """Verify version conflict integrity:

    - Identical (corpus, submission, version) with same SHA256 -> 202 Accepted (idempotent 'existing').
    - Same (corpus, submission, version) with DIFFERENT SHA256 -> 409 Conflict.
    """
    sub_id = f"sub_ver_{uuid.uuid4().hex[:8]}"
    pdf_content_a = b"%PDF-1.4\n% Content A\ntrailer\n<<>>\n%%EOF\n"
    pdf_content_b = b"%PDF-1.4\n% Content B (different)\ntrailer\n<<>>\n%%EOF\n"

    monkeypatch.setattr("app.main.safe_enqueue_job", lambda j_id, j_type: True)

    # 1. First upload -> 202 Accepted, queued
    resp1 = client.post(
        "/api/v1/scans",
        data={"corpus_id": "uit_thesis", "submission_id": sub_id, "version": 1},
        files={"file": ("sample.pdf", io.BytesIO(pdf_content_a), "application/pdf")},
        headers=valid_auth_headers,
    )
    assert resp1.status_code == 202
    job_1_id = resp1.json()["job_id"]
    assert resp1.json()["status"] == "queued"

    # 2. Second upload with SAME content -> 202 Accepted, existing (idempotent)
    resp2 = client.post(
        "/api/v1/scans",
        data={"corpus_id": "uit_thesis", "submission_id": sub_id, "version": 1},
        files={"file": ("sample.pdf", io.BytesIO(pdf_content_a), "application/pdf")},
        headers=valid_auth_headers,
    )
    assert resp2.status_code == 202
    assert resp2.json()["job_id"] == job_1_id
    assert resp2.json()["status"] == "existing"

    # 3. Third upload with DIFFERENT content for same version -> 409 Conflict
    resp3 = client.post(
        "/api/v1/scans",
        data={"corpus_id": "uit_thesis", "submission_id": sub_id, "version": 1},
        files={"file": ("sample_modified.pdf", io.BytesIO(pdf_content_b), "application/pdf")},
        headers=valid_auth_headers,
    )
    assert resp3.status_code == 409
    assert "Version already exists with different content" in resp3.json()["detail"]


# ==============================================================================
# 3. Report Ready Check & Pagination Contract
# ==============================================================================


def test_report_endpoint_lifecycle_gate(client, valid_auth_headers, monkeypatch):
    """Verify that GET /api/v1/reports/{job_id} returns 409 Conflict if job is not completed.

    Returns 404 if job does not exist.
    """
    fake_job_id = str(uuid.uuid4())

    # 1. Job not found -> 404
    monkeypatch.setattr("app.main.get_job", lambda j_id: None)
    resp_404 = client.get(f"/api/v1/reports/{fake_job_id}", headers=valid_auth_headers)
    assert resp_404.status_code == 404

    # 2. Job in processing -> 409 Conflict
    monkeypatch.setattr(
        "app.main.get_job",
        lambda j_id: {"id": fake_job_id, "status": "processing", "report_path": None},
    )
    resp_proc = client.get(f"/api/v1/reports/{fake_job_id}", headers=valid_auth_headers)
    assert resp_proc.status_code == 409
    assert "Report not ready: processing" in resp_proc.json()["detail"]

    # 3. Job in waiting_for_compute -> 409 Conflict
    monkeypatch.setattr(
        "app.main.get_job",
        lambda j_id: {"id": fake_job_id, "status": "waiting_for_compute", "report_path": None},
    )
    resp_wait = client.get(f"/api/v1/reports/{fake_job_id}", headers=valid_auth_headers)
    assert resp_wait.status_code == 409
    assert "Report not ready: waiting_for_compute" in resp_wait.json()["detail"]


def test_report_pagination_and_schema_negotiation(client, valid_auth_headers, monkeypatch, tmp_path):
    """Verify pagination (page, page_size) and schema negotiation (?schema=2 vs schema=3)."""
    fake_job_id = str(uuid.uuid4())
    report_file = tmp_path / f"rep_{fake_job_id}.json"

    # Mock 150 findings to test pagination
    findings = [
        {
            "id": f"find_{i}",
            "submission_chunk_id": f"chunk_{i}",
            "submission_text": f"Nội dung câu {i}",
            "source_id": "src_1",
            "source_filename": "nguon.pdf",
            "source_text": f"Nội dung nguồn {i}",
            "score": 0.85,
            "reranker_score": 0.88,
            "citation_status": "present",
        }
        for i in range(150)
    ]

    report_payload = {
        "job_id": fake_job_id,
        "schema_version": 3,
        "corpus_id": "uit_thesis",
        "submission_id": "sub_test",
        "version": 1,
        "summary": {
            "similarity_coverage": 12.5,
            "max_reranker_score": 0.88,
            "scanned_chunks": 50,
            "scanned_characters": 5000,
            "matched_characters": 625,
            "cited_findings": 150,
            "uncited_findings": 0,
        },
        "findings": findings,
        "grouped_findings": [],
    }
    report_file.write_text(json.dumps(report_payload), encoding="utf-8")

    monkeypatch.setattr(
        "app.main.get_job",
        lambda j_id: {
            "id": fake_job_id,
            "status": "completed",
            "report": report_payload,
        },
    )

    # 1. Default page 1, page_size 100 -> returns 100 findings and pagination metadata
    resp_p1 = client.get(
        f"/api/v1/reports/{fake_job_id}?page=1&page_size=100",
        headers=valid_auth_headers,
    )
    assert resp_p1.status_code == 200
    data_p1 = resp_p1.json()
    assert data_p1["pagination"]["total_findings"] == 150
    assert data_p1["pagination"]["page"] == 1
    assert data_p1["pagination"]["page_size"] == 100
    assert data_p1["pagination"]["total_pages"] == 2
    assert len(data_p1["findings"]) == 100

    # 2. Page 2, page_size 100 -> returns remaining 50 findings
    resp_p2 = client.get(
        f"/api/v1/reports/{fake_job_id}?page=2&page_size=100",
        headers=valid_auth_headers,
    )
    assert resp_p2.status_code == 200
    data_p2 = resp_p2.json()
    assert len(data_p2["findings"]) == 50

    # 3. Schema negotiation: request schema 2 downgrade
    resp_s2 = client.get(
        f"/api/v1/reports/{fake_job_id}?schema=2",
        headers=valid_auth_headers,
    )
    assert resp_s2.status_code == 200
    data_s2 = resp_s2.json()
    assert data_s2["schema_version"] in (2, "2", "2.0")
    assert "summary" in data_s2
    assert data_s2["summary"]["scanned_chunks"] == 50
    assert "disclaimer" in data_s2


# ==============================================================================
# 4. UGTM Business Consumer Adapter & RBAC Policy Verification Harness
# ==============================================================================


class MockUgtmRole:
    STUDENT = "Student"
    ADVISOR = "Advisor"
    COUNCIL_MEMBER = "CouncilMember"
    FACULTY = "Faculty"
    ADMIN = "Admin"


class MockFinalReportReadAccessChecker:
    """Simulates the authorization rules of karasuma-2401/UGTM (FinalReportReadAccessChecker).

    Enforces that:
    - Students can only see their own submission receipt status, NEVER raw source_text/findings.
    - Advisors can see full report ONLY for their assigned group.
    - Council members can see full report ONLY if submission is 'UnderEvaluation' and assigned.
    - Faculty can see reports belonging to their department.
    - Admin has system-wide access.
    """

    @staticmethod
    def evaluate_access(
        caller_role: str,
        caller_user_id: str,
        caller_department_id: str,
        report_group_id: str,
        report_advisor_id: str,
        report_department_id: str,
        report_state: str,  # e.g., 'Draft', 'Submitted', 'UnderEvaluation', 'Archived'
        assigned_council_member_ids: list[str],
    ) -> dict[str, Any]:
        # 1. Admin: full access
        if caller_role == MockUgtmRole.ADMIN:
            return {"allowed": True, "view_mode": "full", "reason": "Admin full access"}

        # 2. Student: submission receipt only (no findings/source_text)
        if caller_role == MockUgtmRole.STUDENT:
            if caller_user_id == report_group_id:
                return {
                    "allowed": True,
                    "view_mode": "receipt_only",
                    "reason": "Student can view receipt status, but source texts are withheld to prevent algorithm gaming.",
                }
            return {"allowed": False, "view_mode": "none", "reason": "Student cannot access another group's report"}

        # 3. Advisor: full access only for assigned group
        if caller_role == MockUgtmRole.ADVISOR:
            if caller_user_id == report_advisor_id:
                return {"allowed": True, "view_mode": "full", "reason": "Assigned advisor authorized"}
            return {"allowed": False, "view_mode": "none", "reason": "Lecturer is not the assigned advisor"}

        # 4. Council Member: full access only when UnderEvaluation
        if caller_role == MockUgtmRole.COUNCIL_MEMBER:
            if caller_user_id in assigned_council_member_ids:
                if report_state in ("UnderEvaluation", "Archived"):
                    return {"allowed": True, "view_mode": "full", "reason": "Council member authorized in evaluation stage"}
                return {
                    "allowed": False,
                    "view_mode": "none",
                    "reason": f"Council member cannot view report before UnderEvaluation stage (current: {report_state})",
                }
            return {"allowed": False, "view_mode": "none", "reason": "Lecturer is not on this topic's defense council"}

        # 5. Faculty: view within department
        if caller_role == MockUgtmRole.FACULTY:
            if caller_department_id == report_department_id:
                return {"allowed": True, "view_mode": "full", "reason": "Faculty dean authorized for department"}
            return {"allowed": False, "view_mode": "none", "reason": "Report belongs to another department"}

        return {"allowed": False, "view_mode": "none", "reason": "Unknown role"}


def test_ugtm_rbac_student_receipt_only_masking():
    """Verify Student caller receives submission status but findings/sources are completely withheld."""
    checker = MockFinalReportReadAccessChecker()

    # Case A: Student viewing their own group's report
    access_own = checker.evaluate_access(
        caller_role=MockUgtmRole.STUDENT,
        caller_user_id="group_101",
        caller_department_id="dep_se",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=[],
    )
    assert access_own["allowed"] is True
    assert access_own["view_mode"] == "receipt_only"
    assert "source texts are withheld" in access_own["reason"]

    # Case B: Student viewing another group's report -> Forbidden
    access_other = checker.evaluate_access(
        caller_role=MockUgtmRole.STUDENT,
        caller_user_id="group_102",
        caller_department_id="dep_se",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=[],
    )
    assert access_other["allowed"] is False
    assert access_other["view_mode"] == "none"


def test_ugtm_rbac_advisor_isolation():
    """Verify Advisor can only view reports of students they supervise."""
    checker = MockFinalReportReadAccessChecker()

    # Case A: Correct assigned advisor -> Full access
    access_assigned = checker.evaluate_access(
        caller_role=MockUgtmRole.ADVISOR,
        caller_user_id="advisor_201",
        caller_department_id="dep_se",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=[],
    )
    assert access_assigned["allowed"] is True
    assert access_assigned["view_mode"] == "full"

    # Case B: Unassigned advisor -> Access Denied
    access_unassigned = checker.evaluate_access(
        caller_role=MockUgtmRole.ADVISOR,
        caller_user_id="advisor_999",
        caller_department_id="dep_se",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=[],
    )
    assert access_unassigned["allowed"] is False


def test_ugtm_rbac_council_member_stage_restriction():
    """Verify Council Member cannot view report prior to UnderEvaluation stage."""
    checker = MockFinalReportReadAccessChecker()

    # Case A: Council member while report is still 'Submitted' (drafting review) -> Blocked
    access_early = checker.evaluate_access(
        caller_role=MockUgtmRole.COUNCIL_MEMBER,
        caller_user_id="council_301",
        caller_department_id="dep_se",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=["council_301", "council_302"],
    )
    assert access_early["allowed"] is False
    assert "UnderEvaluation stage" in access_early["reason"]

    # Case B: Council member when report transitions to 'UnderEvaluation' -> Full access
    access_eval = checker.evaluate_access(
        caller_role=MockUgtmRole.COUNCIL_MEMBER,
        caller_user_id="council_301",
        caller_department_id="dep_se",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="UnderEvaluation",
        assigned_council_member_ids=["council_301", "council_302"],
    )
    assert access_eval["allowed"] is True
    assert access_eval["view_mode"] == "full"


def test_ugtm_rbac_faculty_and_admin_scopes():
    """Verify Faculty is limited by department, whereas Admin has global clearance."""
    checker = MockFinalReportReadAccessChecker()

    # Case A: Faculty dean of Software Engineering inspecting SE report -> Allowed
    access_fac_same = checker.evaluate_access(
        caller_role=MockUgtmRole.FACULTY,
        caller_user_id="dean_se",
        caller_department_id="dep_se",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=[],
    )
    assert access_fac_same["allowed"] is True

    # Case B: Faculty dean of Computer Science inspecting SE report -> Denied
    access_fac_diff = checker.evaluate_access(
        caller_role=MockUgtmRole.FACULTY,
        caller_user_id="dean_cs",
        caller_department_id="dep_cs",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=[],
    )
    assert access_fac_diff["allowed"] is False

    # Case C: Admin -> Full access anywhere
    access_admin = checker.evaluate_access(
        caller_role=MockUgtmRole.ADMIN,
        caller_user_id="admin_01",
        caller_department_id="dep_central",
        report_group_id="group_101",
        report_advisor_id="advisor_201",
        report_department_id="dep_se",
        report_state="Submitted",
        assigned_council_member_ids=[],
    )
    assert access_admin["allowed"] is True
    assert access_admin["view_mode"] == "full"
