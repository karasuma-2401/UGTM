from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any

import pytest

from app.observability import StageTimer, sanitize_log_payload
from app.reports import build_report_v3

PROJECT_ROOT = Path(__file__).resolve().parent.parent


def test_stage_timer_tracks_durations_and_telemetry_dict():
    """Verify StageTimer accurately records timing and produces valid telemetry structure."""
    timer = StageTimer(job_id="test-obs-job-01", run_id="run-123")

    with timer.time_stage("parse", {"chunks_count": 10}):
        # Simulate tiny sleep
        sum(i * i for i in range(10000))

    with timer.time_stage("encode", {"chunks_count": 10}):
        sum(i * i for i in range(10000))

    telemetry = timer.to_telemetry_dict()

    assert telemetry["job_id"] == "test-obs-job-01"
    assert telemetry["run_id"] == "run-123"
    assert "parse" in telemetry["stages_ms"]
    assert "encode" in telemetry["stages_ms"]
    assert telemetry["stages_ms"]["parse"] >= 0.0
    assert telemetry["stages_ms"]["encode"] >= 0.0
    assert telemetry["total_duration_ms"] >= 0.0
    assert telemetry["metadata"]["parse"]["chunks_count"] == 10


def test_sanitize_log_payload_prevents_raw_text_leak():
    """Verify sanitize_log_payload replaces raw text with length counters, ensuring zero text leakage."""
    sensitive_payload = {
        "job_id": "test-leak-01",
        "submission_text": "Đây là toàn bộ nội dung luận văn bí mật của sinh viên.",
        "source_text": "Nội dung gốc trong sách xuất bản năm 2020.",
        "raw_passage": "Đoạn văn thô chứa thông tin cá nhân",
        "chunk_count": 5,
        "nested": {
            "content": "Nội dung con cần che chắn",
            "score": 0.88,
        },
    }

    sanitized = sanitize_log_payload(sensitive_payload)

    # 1. Original sensitive keys must NOT exist as raw strings
    assert "submission_text" not in sanitized
    assert "source_text" not in sanitized
    assert "raw_passage" not in sanitized
    assert "content" not in sanitized["nested"]

    # 2. Length counters must be recorded instead
    assert sanitized["submission_text_length"] == len(sensitive_payload["submission_text"])
    assert sanitized["source_text_length"] == len(sensitive_payload["source_text"])
    assert sanitized["raw_passage_length"] == len(sensitive_payload["raw_passage"])
    assert sanitized["nested"]["content_length"] == len(sensitive_payload["nested"]["content"])

    # 3. Non-sensitive keys remain intact
    assert sanitized["job_id"] == "test-leak-01"
    assert sanitized["chunk_count"] == 5
    assert sanitized["nested"]["score"] == 0.88


def test_stage_timer_log_emission_zero_raw_text(caplog):
    """Verify that StageTimer's structured log emission never prints raw text."""
    caplog.set_level(logging.INFO)
    timer = StageTimer(job_id="test-log-clean", run_id="run-clean")

    with timer.time_stage("rerank", {"pairs_count": 5, "raw_text": "Secret dissertation content"}):
        pass

    log_text = caplog.text
    assert "StageCompleted: job_id=test-log-clean" in log_text
    assert "stage=rerank" in log_text
    assert "pairs_count" in log_text
    # Sensitive raw_text field must be completely omitted from the log string
    assert "Secret dissertation content" not in log_text


def test_restore_drill_evidence_file_integrity():
    """Verify data/restore_drill_evidence.json exists and confirms disaster recovery readiness."""
    evidence_path = PROJECT_ROOT / "data" / "restore_drill_evidence.json"
    assert evidence_path.exists(), "Restore drill evidence file missing! Run scripts/backup_restore_drill.py"

    data = json.loads(evidence_path.read_text(encoding="utf-8"))
    assert data["drill_status"] == "SUCCESS"
    assert data["verification_summary"]["zero_data_loss"] is True
    assert data["verification_summary"]["all_manifests_valid"] is True
    assert data["database_metadata"]["restoration_parity_verified"] is True
    assert len(data["artifacts_verified"]) >= 4


def test_report_schema_v3_contains_execution_telemetry():
    """Verify build_report_v3 cleanly attaches execution_telemetry to provenance."""
    sample_job = {
        "id": "job-telemetry-01",
        "corpus_id": "uit_thesis",
        "document_key": "sub_telemetry",
        "version": 1,
    }
    telemetry_mock = {
        "job_id": "job-telemetry-01",
        "total_duration_ms": 520.4,
        "stages_ms": {"parse": 50.0, "encode": 120.0, "retrieve": 30.0, "rerank": 320.4},
    }

    report = build_report_v3(
        job_id="job-telemetry-01",
        job=sample_job,
        query_chunks=[],
        all_findings=[],
        execution_telemetry=telemetry_mock,
    )

    assert "execution_telemetry" in report["provenance"]
    assert report["provenance"]["execution_telemetry"]["total_duration_ms"] == 520.4
    assert report["provenance"]["execution_telemetry"]["stages_ms"]["rerank"] == 320.4

