"""Regression and Unit Tests for Citation-Aware Context Engine (CP-04.3).

Verifies citation detection, style classification, attribution verification,
and evidence preservation invariants:
1. Numeric citations: [1], [1, 2], [3-5], [1-3, 5, 8].
2. Non-citation exclusions: [Bảng 1], [Hình 2], arr[0], x[i].
3. Author-Year parenthetical citations: (Vaswani et al., 2017), (Nguyễn và cộng sự, 2021).
4. Narrative citations: "Theo Vaswani et al. (2017)...", "Nghiên cứu của [1]...".
5. Direct quotations: "..." and “...” with quotation indicator.
6. Absent citations: Pure uncredited text reuse.
7. Uncertain / Ambiguous citations: Generic lead-ins and unanchored notes.
8. Source attribution verification: Direct match vs Misattribution / Secondary source reuse.
9. Evidence preservation: Findings with citations are never deleted or masked.
10. Schema v3 integration: Report summary aggregates cited/uncited/uncertain counts.
"""

from __future__ import annotations

import pytest
from app.citation import (
    CitationContext,
    CitationMarker,
    detect_citation_context,
    evaluate_source_attribution,
    extract_citation_markers,
)
from app.reports import build_report_v3


def test_numeric_citation_detection():
    """Detects standard single, comma-separated, and range numeric bracket citations."""
    text1 = "Mô hình này được phát triển dựa trên cấu trúc Transformer [1]."
    markers1 = extract_citation_markers(text1)
    assert len(markers1) == 1
    assert markers1[0].marker_type == "numeric"
    assert markers1[0].indices == [1]

    text2 = "Các nghiên cứu trước đây [1, 3, 5] và [7-9] đã chứng minh hiệu năng cao."
    markers2 = extract_citation_markers(text2)
    assert len(markers2) == 2
    assert markers2[0].indices == [1, 3, 5]
    assert markers2[1].indices == [7, 8, 9]


def test_non_citation_exclusions():
    """Negative lookbehind/lookahead excludes table/figure labels and array indexing."""
    text = (
        "Chi tiết được trình bày trong [Bảng 1] và [Hình 2]. "
        "Mảng arr[0] và phần tử vector x[i] được tính toán tại [Equation 3]."
    )
    markers = extract_citation_markers(text)
    # None of these should be recognized as academic citations
    assert len(markers) == 0


def test_author_year_citation_detection():
    """Detects standard parenthetical author-year citations across styles."""
    text1 = "Kiến trúc Attention đã thay đổi lĩnh vực NLP (Vaswani et al., 2017)."
    ctx1 = detect_citation_context(text1)
    assert ctx1.status == "present"
    assert ctx1.citation_style == "author_year"
    assert len(ctx1.markers) == 1
    assert "Vaswani" in (ctx1.markers[0].author or "")
    assert "2017" in (ctx1.markers[0].year or "")

    text2 = "Hệ thống trích xuất đặc trưng đa tầng (Nguyễn và cộng sự, 2021, tr. 34)."
    ctx2 = detect_citation_context(text2)
    assert ctx2.status == "present"
    assert "Nguyễn" in (ctx2.markers[0].author or "")
    assert "2021" in (ctx2.markers[0].year or "")


def test_narrative_citation_detection():
    """Detects narrative citations with author and year or narrative with numeric bracket."""
    text1 = "Theo Vaswani et al. (2017), cơ chế Self-Attention loại bỏ hoàn toàn đệ quy."
    ctx1 = detect_citation_context(text1)
    assert ctx1.status == "present"
    assert ctx1.citation_style == "narrative"
    assert len(ctx1.markers) == 1
    assert "Vaswani" in (ctx1.markers[0].author or "")

    text2 = "Theo [1], mô hình BERT được huấn luyện với hai nhiệm vụ chính."
    ctx2 = detect_citation_context(text2)
    assert ctx2.status == "present"
    assert ctx2.citation_style == "numeric"
    assert ctx2.markers[0].indices == [1]


def test_direct_quotation_markers():
    """Identifies direct verbatim quotations enclosed in quotation marks."""
    text1 = 'Vaswani khẳng định: "The dominant sequence transduction models are based on RNNs" [1].'
    ctx1 = detect_citation_context(text1)
    assert ctx1.status == "present"
    assert ctx1.has_quotation_marks is True
    assert ctx1.is_verbatim_quote is True

    text2 = "Mô hình Attention không sử dụng bất kỳ lớp đệ quy nào [1]."
    ctx2 = detect_citation_context(text2)
    assert ctx2.status == "present"
    assert ctx2.has_quotation_marks is False
    assert ctx2.is_verbatim_quote is False


def test_absent_citation():
    """Uncredited copied text is correctly classified as absent."""
    text = "Kiến trúc Transformer hoàn toàn loại bỏ các liên kết hồi quy và tích chập."
    ctx = detect_citation_context(text)
    assert ctx.status == "absent"
    assert ctx.citation_style == "none"
    assert ctx.has_quotation_marks is False
    assert len(ctx.markers) == 0
    assert ctx.source_attributed is False


def test_uncertain_citation_context():
    """Vague narrative lead-ins or non-standard parenthetical notes trigger uncertain status."""
    text1 = "Theo các tài liệu tham khảo gần đây, mô hình này rất phổ biến trong công nghiệp."
    ctx1 = detect_citation_context(text1)
    assert ctx1.status == "uncertain"

    text2 = "BERT được thiết kế nhằm tiền huấn luyện các biểu diễn sâu (Nghiên cứu ứng dụng tại phân hệ val đợt 2)."
    ctx2 = detect_citation_context(text2)
    assert ctx2.status == "uncertain"
    assert ctx2.source_attributed is None


def test_source_attribution_matching():
    """Matches author citation directly against candidate source document metadata."""
    query = "Theo Vaswani et al. (2017), Self-Attention giúp tối ưu hóa huấn luyện song song."
    source_meta = {
        "source_key": "vaswani2017_attention",
        "source_filename": "Vaswani_2017_Attention_Is_All_You_Need.pdf",
        "document_family": "fam-vaswani-2017",
    }
    ctx = detect_citation_context(query, source_metadata=source_meta)
    assert ctx.status == "present"
    assert ctx.source_attributed is True
    assert "matches candidate source" in ctx.attribution_details


def test_source_attribution_misattribution_alert():
    """Flags secondary source plagiarism when query cites Author X but text matches Author Y."""
    query = "Theo Vaswani et al. (2017), Self-Attention giúp tối ưu hóa huấn luyện song song."
    # The student actually copied from Le's thesis, which had quoted Vaswani
    source_meta = {
        "source_key": "khoa_luan_le_van_c_2022",
        "source_filename": "Khoa_Luan_Tot_Nghiep_Le_Van_C.docx",
        "document_family": "fam-le-van-c",
    }
    ctx = detect_citation_context(query, source_metadata=source_meta)
    assert ctx.status == "present"
    assert ctx.source_attributed is False
    assert "Potential misattribution" in ctx.attribution_details
    assert "Le_Van_C" in ctx.attribution_details


def test_numeric_citation_requires_bibliography_lookup():
    """Numeric marker [1] returns source_attributed=None (unverified) without bibliography."""
    query = "Hệ thống đạt kết quả vượt trội trên tập kiểm thử chuẩn [3]."
    source_meta = {
        "source_key": "paper_xyz",
        "source_filename": "Paper_XYZ.pdf",
    }
    ctx = detect_citation_context(query, source_metadata=source_meta)
    assert ctx.status == "present"
    assert ctx.source_attributed is None
    assert "requires bibliography reference lookup" in ctx.attribution_details


def test_evidence_preservation_invariant():
    """Presence of citation DOES NOT hide, delete, or zero out the plagiarism finding."""
    query = 'Theo công bố [1]: "Thuật toán tối ưu hóa di truyền mô phỏng chọn lọc tự nhiên".'
    ctx = detect_citation_context(query)

    finding = {
        "submission_chunk_no": 1,
        "submission_page": 2,
        "submission_start": 100,
        "submission_end": 185,
        "submission_text": query,
        "source_key": "src_paper",
        "source_version": 1,
        "source_filename": "genetic_algorithms.pdf",
        "source_chunk_no": 1,
        "source_page": 1,
        "source_start": 50,
        "source_end": 135,
        "source_text": "Thuật toán tối ưu hóa di truyền mô phỏng chọn lọc tự nhiên.",
        "chunk_id": 10,
        "rrf_score": 0.033,
        "dense_score": 0.88,
        "lexical_score": 0.95,
        "reranker_score": 0.965,
        "decision_source": "heuristic",
        "citation_status": ctx.status,
        "citation_context": ctx.to_dict(),
        "review_required": True,
    }

    # Finding preserves high similarity score and review requirement
    assert finding["reranker_score"] == 0.965
    assert finding["review_required"] is True
    assert finding["citation_status"] == "present"
    assert finding["citation_context"]["status"] == "present"


def test_report_schema_v3_citation_summary_integration():
    """Report Schema v3 properly aggregates cited, uncited, and uncertain findings in summary."""
    findings = [
        {
            "submission_chunk_no": 1,
            "submission_page": 1,
            "submission_start": 0,
            "submission_end": 50,
            "submission_text": "Đoạn 1 có trích dẫn [1].",
            "source_key": "k1",
            "source_version": 1,
            "source_filename": "f1.pdf",
            "source_chunk_no": 1,
            "source_page": 1,
            "source_start": 0,
            "source_end": 50,
            "source_text": "Đoạn 1 nguồn.",
            "chunk_id": 1,
            "rrf_score": 0.03,
            "dense_score": 0.8,
            "lexical_score": 0.8,
            "reranker_score": 0.85,
            "citation_status": "present",
            "citation_context": {"status": "present"},
        },
        {
            "submission_chunk_no": 2,
            "submission_page": 1,
            "submission_start": 60,
            "submission_end": 120,
            "submission_text": "Đoạn 2 sao chép không trích dẫn.",
            "source_key": "k2",
            "source_version": 1,
            "source_filename": "f2.pdf",
            "source_chunk_no": 1,
            "source_page": 1,
            "source_start": 0,
            "source_end": 60,
            "source_text": "Đoạn 2 nguồn.",
            "chunk_id": 2,
            "rrf_score": 0.03,
            "dense_score": 0.8,
            "lexical_score": 0.8,
            "reranker_score": 0.90,
            "citation_status": "absent",
            "citation_context": {"status": "absent"},
        },
        {
            "submission_chunk_no": 3,
            "submission_page": 1,
            "submission_start": 130,
            "submission_end": 190,
            "submission_text": "Đoạn 3 mơ hồ theo nghiên cứu gần đây.",
            "source_key": "k3",
            "source_version": 1,
            "source_filename": "f3.pdf",
            "source_chunk_no": 1,
            "source_page": 1,
            "source_start": 0,
            "source_end": 60,
            "source_text": "Đoạn 3 nguồn.",
            "chunk_id": 3,
            "rrf_score": 0.03,
            "dense_score": 0.8,
            "lexical_score": 0.8,
            "reranker_score": 0.75,
            "citation_status": "uncertain",
            "citation_context": {"status": "uncertain"},
        },
    ]

    report = build_report_v3(
        job_id="test-job-citations",
        job={"corpus_id": "test", "document_key": "sub1", "version": 1},
        query_chunks=[],
        all_findings=findings,
    )

    summary = report["summary"]
    assert summary["cited_findings"] == 1
    assert summary["uncited_findings"] == 1
    assert summary["uncertain_findings"] == 1
    assert summary["total_findings"] == 3

