from __future__ import annotations

import json
from pathlib import Path
import pytest

from app.documents import Chunk, extract_pages
from app.evidence import (
    audit_report_evidence,
    check_vietnamese_integrity,
    trace_evidence_to_file,
    verify_passage_group_overlap,
)
from app.preprocessing import prepare_document_chunks
from app.reports import build_report_v3
from app.tasks import parse_job, scan_document

SAMPLES_DIR = Path(__file__).resolve().parent.parent / "app" / "static" / "samples"
FIXTURES_DIR = Path(__file__).resolve().parent / "fixtures"


def test_vietnamese_diacritics_integrity_and_mojibake_detection():
    """Verify that Vietnamese diacritics are preserved and Mojibake corruption is detected."""
    # 1. Clean Vietnamese text
    clean_text = "Nghiên cứu kiến trúc học sâu Transformer ứng dụng trong phân tích cú pháp tiếng Việt."
    is_ok, issues = check_vietnamese_integrity(clean_text)
    assert is_ok is True
    assert len(issues) == 0

    # 2. Corrupted Mojibake text (e.g. UTF-8 decoded as CP1252)
    mojibake_text = "NghiÃªn cÃºu kiáº¿n trÃºc há»c sÃ¢u Transformer"
    is_ok, issues = check_vietnamese_integrity(mojibake_text)
    assert is_ok is False
    assert any("MOJIBAKE_DETECTED" in issue for issue in issues)


def test_trace_evidence_to_physical_docx():
    """Verify end-to-end tracing of evidence finding back to physical DOCX file."""
    source_path = SAMPLES_DIR / "source_doc.docx"
    assert source_path.exists(), f"Sample DOCX missing at {source_path}"

    chunks, stats = prepare_document_chunks(str(source_path))
    target_chunk = chunks[0]

    mock_finding = {
        "passage_group_id": "PG-0001",
        "submission_chunk_no": 0,
        "submission_page": None,
        "submission_start": target_chunk.start_offset,
        "submission_end": target_chunk.end_offset,
        "submission_text": target_chunk.text,
        "chunk_id": 101,
        "source_key": "source_doc",
        "source_version": 1,
        "source_filename": "source_doc.docx",
        "source_chunk_no": target_chunk.chunk_no,
        "source_page": None,
        "source_start": target_chunk.start_offset,
        "source_end": target_chunk.end_offset,
        "source_text": target_chunk.text,
        "reranker_score": 0.95,
    }

    res = trace_evidence_to_file(
        finding=mock_finding,
        source_file_path=source_path,
        submission_file_path=source_path,
    )

    assert res.source_verified is True
    assert res.source_fidelity == 1.0
    assert res.submission_verified is True
    assert res.submission_fidelity == 1.0
    assert res.vietnamese_intact is True
    assert len(res.notes) == 0


def test_trace_evidence_to_physical_pdf():
    """Verify end-to-end tracing of evidence finding back to physical multi-page PDF file."""
    pdf_path = FIXTURES_DIR / "multi_page_with_header_footer.pdf"
    assert pdf_path.exists(), f"Fixture PDF missing at {pdf_path}"

    chunks, stats = prepare_document_chunks(str(pdf_path))
    # Pick a chunk from page 1 or 2
    target_chunk = chunks[0]

    mock_finding = {
        "passage_group_id": "PG-0001",
        "submission_chunk_no": target_chunk.chunk_no,
        "submission_page": target_chunk.page_number,
        "submission_start": target_chunk.start_offset,
        "submission_end": target_chunk.end_offset,
        "submission_text": target_chunk.text,
        "chunk_id": 202,
        "source_key": "multi_page_pdf",
        "source_version": 1,
        "source_filename": "multi_page_with_header_footer.pdf",
        "source_chunk_no": target_chunk.chunk_no,
        "source_page": target_chunk.page_number,
        "source_start": target_chunk.start_offset,
        "source_end": target_chunk.end_offset,
        "source_text": target_chunk.text,
        "reranker_score": 0.92,
    }

    res = trace_evidence_to_file(
        finding=mock_finding,
        source_file_path=pdf_path,
        submission_file_path=pdf_path,
    )

    assert res.source_verified is True
    assert res.source_fidelity >= 0.95
    assert res.source_location["page"] == target_chunk.page_number
    assert res.vietnamese_intact is True


def test_passage_group_overlap_verification():
    """Verify continuity and exact overlap calculation between contiguous chunks."""
    findings = [
        {
            "submission_chunk_no": 0,
            "submission_start": 0,
            "submission_end": 400,
            "submission_text": "A" * 320 + "B" * 80,
        },
        {
            "submission_chunk_no": 1,
            "submission_start": 320,
            "submission_end": 720,
            "submission_text": "B" * 80 + "C" * 320,
        },
    ]

    res = verify_passage_group_overlap(findings, full_text="")
    assert res["contiguous"] is True
    assert res["overlap_verified"] is True
    assert len(res["overlaps"]) == 1
    assert res["overlaps"][0]["overlap_length"] == 80
    assert res["overlaps"][0]["is_match"] is True


def test_edge_case_clean_document_no_match():
    """Verify that a document with 0 matches cleanly produces a completed report with 0 coverage."""
    query_chunks = [
        Chunk(chunk_no=0, page_number=1, start_offset=0, end_offset=150, text="Nội dung hoàn toàn mới lạ không hề có trong cơ sở dữ liệu.")
    ]

    report = build_report_v3(
        job_id="job-clean-123",
        job={"corpus_id": "uit-v1", "document_key": "clean_doc", "version": 1},
        query_chunks=query_chunks,
        all_findings=[],  # 0 matches
    )

    assert report["schema_version"] == 3
    assert report["summary"]["total_findings"] == 0
    assert report["summary"]["flagged_pairs"] == 0
    assert report["summary"]["matched_sources"] == 0
    assert report["summary"]["similarity_coverage"] == 0.0
    assert report["summary"]["max_reranker_score"] == 0.0
    assert len(report["passage_groups"]) == 0
    assert len(report["findings"]) == 0


def test_edge_case_empty_and_corrupted_file_handling(tmp_path, monkeypatch):
    """Verify that empty and corrupted files fail gracefully with specific error codes."""
    empty_file = tmp_path / "empty.docx"
    empty_file.write_bytes(b"")

    corrupted_file = tmp_path / "bad.docx"
    corrupted_file.write_bytes(b"NOT_A_VALID_DOCX_OR_ZIP_FILE_RANDOM_GARBAGE")

    # 1. Test empty file raises EMPTY_TEXT
    empty_job = {
        "id": "empty-1",
        "file_path": str(empty_file),
        "filename": "empty.docx",
    }
    with pytest.raises(ValueError) as exc:
        parse_job(empty_job)
    assert "EMPTY_TEXT" in str(exc.value)

    # 2. Test corrupted file raises CORRUPTED_DOCUMENT
    corrupted_job = {
        "id": "bad-1",
        "file_path": str(corrupted_file),
        "filename": "bad.docx",
    }
    with pytest.raises(ValueError) as exc:
        parse_job(corrupted_job)
    assert "CORRUPTED_DOCUMENT" in str(exc.value)


def test_edge_case_empty_corpus_in_scan(monkeypatch):
    """Verify scan_document fails cleanly with error_code='CORPUS_EMPTY' when no sources exist."""
    fake_job_id = "test-empty-corpus-job"
    recorded_status = {}

    def mock_get_job(j_id):
        return {
            "id": fake_job_id,
            "corpus_id": "non-existent-corpus",
            "document_key": "doc-1",
            "version": 1,
        }

    def mock_set_status(j_id, status, error_code=None):
        recorded_status["status"] = status
        recorded_status["error_code"] = error_code

    class MockCon:
        def execute(self, query, params=None):
            return self
        def fetchone(self):
            return {"n": 0}  # 0 active sources in corpus
        def __enter__(self):
            return self
        def __exit__(self, *args):
            pass

    monkeypatch.setattr("app.tasks.get_job", mock_get_job)
    monkeypatch.setattr("app.tasks.set_status", mock_set_status)
    monkeypatch.setattr("app.tasks.connect", lambda: MockCon())

    scan_document(fake_job_id)

    assert recorded_status["status"] == "failed"
    assert recorded_status["error_code"] == "CORPUS_EMPTY"

