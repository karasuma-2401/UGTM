"""Automated tests for CP-06.3: Research Technical Report & Claim-to-Experiment Traceability."""

import json
from pathlib import Path
import pytest

ROOT_DIR = Path(__file__).resolve().parent.parent
REPORT_PATH = ROOT_DIR / "docs" / "research-technical-report.md"
MATRIX_PATH = ROOT_DIR / "docs" / "claim-to-experiment-traceability-matrix.md"
RC1_REPORT_PATH = ROOT_DIR / "reports" / "final_test_reports" / "report_rc1.json"


def test_research_technical_report_structure_and_completeness():
    """Verify research technical report exists and covers all mandatory sections."""
    assert REPORT_PATH.exists(), "research-technical-report.md missing"
    content = REPORT_PATH.read_text(encoding="utf-8")

    # Check major chapters
    mandatory_sections = [
        "Tóm tắt Nghiên cứu",
        "Định nghĩa Bài toán",
        "Các Câu hỏi Nghiên cứu",
        "RQ1",
        "RQ2",
        "RQ3",
        "Công trình Liên quan & Phân định Đóng góp",
        "Bộ Dữ Liệu `UGTM-Bench-v1` & Giới Hạn Gán Nhãn",
        "Phương Pháp Đề Xuất",
        "Thiết Kế Thực Nghiệm & Các Phương Án Đối Chứng",
        "Kết Quả Thực Nghiệm Toàn Diện",
        "Nghiên Cứu Cắt Giảm Thành Phần",
        "Phân Tích Lỗi Chi Tiết",
        "Giới Hạn Kỹ Thuật & Hướng Phát Triển",
        "Kết Luận",
        "Tài Liệu Tham Khảo",
    ]
    for sec in mandatory_sections:
        assert sec in content, f"Missing mandatory section in report: {sec}"


def test_claim_traceability_matrix_covers_c1_to_c7():
    """Verify traceability matrix defines all 7 claims with proper evidence links."""
    assert MATRIX_PATH.exists(), "claim-to-experiment-traceability-matrix.md missing"
    matrix_text = MATRIX_PATH.read_text(encoding="utf-8")

    # Check for all claims C1 through C7
    for claim_id in ["C1", "C2", "C3", "C4", "C5", "C6", "C7"]:
        assert f"**{claim_id}**" in matrix_text, f"Missing claim {claim_id} in traceability matrix"

    # Check for evidence tags and RQs
    assert "RQ1" in matrix_text and "RQ2" in matrix_text and "RQ3" in matrix_text
    assert "[MEASURED]" in matrix_text
    assert "report_rc1.json" in matrix_text


def test_claims_numerical_parity_with_rc1_report():
    """Verify metrics stated in traceability matrix exactly match official RC1 test report."""
    assert RC1_REPORT_PATH.exists(), "report_rc1.json missing"
    rc1_data = json.loads(RC1_REPORT_PATH.read_text(encoding="utf-8"))

    matrix_text = MATRIX_PATH.read_text(encoding="utf-8")

    # Verify IoU: 0.9031
    iou_str = f"{rc1_data['alignment']['mean_iou']:.4f}"
    assert iou_str in matrix_text, f"IoU {iou_str} not reflected in traceability matrix"

    # Verify False Alarms: 0.0000
    fa_str = f"{rc1_data['false_alarms_per_doc']['false_alarms_per_doc']:.4f}"
    assert fa_str in matrix_text, f"False alarms {fa_str} not reflected in traceability matrix"

    # Verify Cross-lingual recall: 1.0000
    cross_recall = rc1_data["slice_breakdown"]["slices"]["cross_lingual_vi_en"]["recall"]
    assert f"{cross_recall:.4f}" in matrix_text, "Cross-lingual recall 1.0000 not reflected in matrix"


def test_transparency_on_annotation_and_hardware():
    """Verify report explicitly states single-annotator, synthetic limits, and Tesla T4 environment."""
    content = REPORT_PATH.read_text(encoding="utf-8")

    # Transparency assertions
    assert "Single-Annotator" in content or "single-annotator" in content
    assert "Tesla T4" in content
    assert "Synthetic" in content or "synthetic" in content
    assert "1.0.0-rc1" in content

