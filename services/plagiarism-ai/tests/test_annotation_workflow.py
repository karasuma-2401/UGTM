import csv
from pathlib import Path

import pytest

from app.documents import (
    TextPage,
    classify_text_section,
    extract_document_metadata,
    is_duplicate_document,
    make_chunks,
)
from scripts.create_annotation_candidates import (
    ANNOTATION_FIELDS,
    SourceChunk,
    candidate_pairs,
)
from scripts.finalize_annotation_pairs import finalize_rows


def test_candidates_are_cross_document_and_unlabeled():
    chunks = [
        SourceChunk("source-a", 1, 0, "mang neuron hoc sau phan loai anh y te"),
        SourceChunk("source-b", 2, 1, "mang neuron hoc sau phan loai anh y te"),
        SourceChunk("source-c", 3, 2, "quan ly lich hoc va thong bao mon hoc"),
    ]

    rows = candidate_pairs(
        chunks,
        max_tasks=10,
        candidates_per_chunk=2,
        minimum_similarity=0,
    )

    assert rows
    assert all(row["query_source_key"] != row["source_key"] for row in rows)
    assert all(row["final_label"] == "" for row in rows)
    assert all(row["reviewer_1_label"] == "" for row in rows)
    assert all(row["reviewer_2_label"] == "" for row in rows)
    assert all(row["status"] == "pending" for row in rows)
    assert set(rows[0].keys()) == set(ANNOTATION_FIELDS)


def test_section_classification_detects_noise_and_metadata():
    cover_text = (
        "ĐẠI HỌC QUỐC GIA TP. HỒ CHÍ MINH TRƯỜNG ĐẠI HỌC CÔNG NGHỆ THÔNG TIN "
        "KHOA CÔNG NGHỆ PHẦN MỀM KHÓA LUẬN TỐT NGHIỆP ĐỀ TÀI XÂY DỰNG ỨNG DỤNG"
    )
    assert classify_text_section(cover_text, page_number=1) == "cover"

    toc_text = "MỤC LỤC Chương 1. Giới thiệu .... 1 Chương 2. Cơ sở lý thuyết .... 15"
    assert classify_text_section(toc_text) == "table_of_contents"

    abbr_text = "DANH MỤC TỪ VIẾT TẮT API Application Programming Interface AI Artificial Intelligence"
    assert classify_text_section(abbr_text) == "abbreviations"

    ref_text = (
        "TÀI LIỆU THAM KHẢO [1] Nguyen et al. Machine learning in healthcare, 2024. "
        "[2] Smith et al. Deep learning, https://example.com/paper, accessed 2025."
    )
    assert classify_text_section(ref_text) == "references"

    body_text = (
        "Trong phần này chúng tôi trình bày thuật toán phân cụm KMeans kết hợp "
        "với lọc cộng tác ma trận ALS để giải quyết vấn đề cold-start trong hệ thống gợi ý."
    )
    assert classify_text_section(body_text, page_number=25) == "body"


def test_make_chunks_with_excluded_sections():
    pages = [
        TextPage(1, "ĐẠI HỌC QUỐC GIA TP. HỒ CHÍ MINH KHOA CÔNG NGHỆ PHẦN MỀM KHÓA LUẬN", section_type="cover"),
        TextPage(2, "MỤC LỤC Chương 1 .... 1 Chương 2 .... 10 Chương 3 .... 25", section_type="table_of_contents"),
        TextPage(3, "Nội dung chương một giới thiệu bối cảnh thực tế và mục tiêu nghiên cứu của đề tài hệ thống thương mại điện tử với nhiều chi tiết kỹ thuật quan trọng.", section_type="body"),
        TextPage(4, "TÀI LIỆU THAM KHẢO [1] Reference A [2] Reference B https://example.com/ref", section_type="references"),
    ]

    all_chunks = make_chunks(pages)
    assert len(all_chunks) >= 2
    assert any(c.section_type == "cover" for c in all_chunks)

    filtered_chunks = make_chunks(
        pages,
        exclude_sections={"cover", "table_of_contents", "references", "abbreviations"},
    )
    assert len(filtered_chunks) >= 1
    assert all(c.section_type == "body" for c in filtered_chunks)
    assert all(c.page_number == 3 for c in filtered_chunks)


def test_duplicate_document_detection():
    meta_pdf = {
        "title": "XÂY DỰNG ỨNG DỤNG SHOP THỜI TRANG",
        "norm_title": "xây dựng ứng dụng shop thời trang",
        "authors": "ĐINH NHƯ THÔNG TĂNG THANH THIỆN",
        "norm_authors": "đinh như thông tăng thanh thiện",
        "cover_prefix": "đại học quốc gia tp hồ chí minh trường đại học công nghệ thông tin khoa công nghệ phần mềm",
        "text_sha256": "abc123",
    }
    meta_docx = {
        "title": "XÂY DỰNG ỨNG DỤNG SHOP THỜI TRANG",
        "norm_title": "xây dựng ứng dụng shop thời trang",
        "authors": "ĐINH NHƯ THÔNG TĂNG THANH THIỆN",
        "norm_authors": "đinh như thông tăng thanh thiện",
        "cover_prefix": "đại học quốc gia tp hồ chí minh trường đại học công nghệ thông tin khoa công nghệ phần mềm",
        "text_sha256": "different_hash_due_to_docx_format",
    }
    meta_different = {
        "title": "PHÁT TRIỂN HỆ THỐNG SÀN GIAO DỊCH LOGISTICS",
        "norm_title": "phát triển hệ thống sàn giao dịch logistics",
        "authors": "HỒ HUỲNH THIỆN TRẦN THỊ MỘNG TRÚC NGÂN",
        "norm_authors": "hồ huỳnh thiện trần thị mộng trúc ngân",
        "cover_prefix": "đại học quốc gia tp hồ chí minh trường đại học công nghệ thông tin khoa công nghệ phần mềm",
        "text_sha256": "xyz789",
    }

    is_dup, reason = is_duplicate_document(meta_pdf, meta_docx)
    assert is_dup is True
    assert "TITLE_AND_AUTHORS_MATCH" in reason

    is_diff, _ = is_duplicate_document(meta_pdf, meta_different)
    assert is_diff is False


def test_candidate_pairs_filters_duplicate_sources():
    # source-a-pdf and source-a-docx are two ingested versions of the same thesis
    chunks = [
        SourceChunk("source-a-pdf", 10, 0, "thuat toan phan cum kmeans ket hop loc cong tac ma tran"),
        SourceChunk("source-a-docx", None, 0, "thuat toan phan cum kmeans ket hop loc cong tac ma tran"),
        SourceChunk("source-b", 15, 1, "thuat toan phan cum kmeans ket hop loc cong tac ma tran"),
        SourceChunk("source-c", 20, 2, "mo hinh ngon ngu lon truy van tri thuc thoi gian thuc"),
    ]
    duplicate_map = {
        "source-a-pdf": "source-a-canonical",
        "source-a-docx": "source-a-canonical",
        "source-b": "source-b",
        "source-c": "source-c",
    }

    rows = candidate_pairs(
        chunks,
        max_tasks=20,
        candidates_per_chunk=3,
        minimum_similarity=0.1,
        duplicate_map=duplicate_map,
    )

    # Must NOT pair source-a-pdf with source-a-docx
    for row in rows:
        pair = {row["query_source_key"], row["source_key"]}
        assert pair != {"source-a-pdf", "source-a-docx"}
        # Both must not have the same canonical key
        assert duplicate_map[row["query_source_key"]] != duplicate_map[row["source_key"]]


def test_finalizer_requires_approved_balanced_labels():
    rows = []
    for index in range(6):
        final_label = str(index % 2)
        rows.append(
            {
                "task_id": f"TASK-{index}",
                "group_id": f"source-{index}",
                "query_text": "query text",
                "source_text": "source text",
                "reviewer_1_label": final_label,
                "reviewer_2_label": final_label,
                "final_label": final_label,
                "status": "approved",
            }
        )

    finalized = finalize_rows(rows, require_two_reviewers=True)

    assert len(finalized) == 6
    assert {row["label"] for row in finalized} == {"0", "1"}


def test_finalizer_rejects_unapproved_rows():
    row = {
        "task_id": "TASK-1",
        "group_id": "source-1",
        "query_text": "query text",
        "source_text": "source text",
        "reviewer_1_label": "1",
        "reviewer_2_label": "1",
        "final_label": "1",
        "status": "pending",
    }

    with pytest.raises(ValueError, match="status must be approved"):
        finalize_rows([row] * 6, require_two_reviewers=True)


def test_finalizer_preserves_raw_labels_during_adjudication():
    """Verify that raw labels are NEVER overwritten when resolving disagreements via adjudication."""
    rows = []
    for i in range(6):
        # Even rows have disagreement (1 vs 0) resolved by adjudication
        rev1 = "1" if i % 2 == 0 else "0"
        rev2 = "0"
        adj = "1" if i % 2 == 0 else ""
        reason = "Confirmed substantive paraphrase" if adj else ""
        rows.append({
            "task_id": f"TASK-{i}",
            "group_id": f"group-{i}",
            "query_text": f"Query passage text {i}",
            "source_text": f"Source passage text {i}",
            "reviewer_1_label": rev1,
            "reviewer_2_label": rev2,
            "adjudicated_label": adj,
            "adjudication_reason": reason,
            "final_label": "",
            "status": "approved",
        })

    finalized = finalize_rows(rows, require_two_reviewers=True)
    assert len(finalized) == 6
    # Row 0: rev1="1", rev2="0", adjudicated="1"
    row0 = finalized[0]
    assert row0["label"] == "1"
    assert row0["raw_annotator_label"] == "1"
    assert row0["audit_reviewer_label"] == "0"
    assert row0["adjudicated_label"] == "1"
    assert row0["adjudication_reason"] == "Confirmed substantive paraphrase"
    assert row0["provenance_mode"] == "dual_review_adjudicated"

    # Row 1: rev1="0", rev2="0" -> consensus
    row1 = finalized[1]
    assert row1["label"] == "0"
    assert row1["raw_annotator_label"] == "0"
    assert row1["audit_reviewer_label"] == "0"
    assert row1["provenance_mode"] == "dual_review_consensus"


def test_finalizer_requires_adjudication_reason_on_disagreement():
    """Verify that adjudication without an explicit rationale is strictly rejected."""
    row = {
        "task_id": "TASK-1",
        "group_id": "source-1",
        "query_text": "q text",
        "source_text": "s text",
        "reviewer_1_label": "1",
        "reviewer_2_label": "0",
        "adjudicated_label": "1",
        "adjudication_reason": "",  # Missing rationale!
        "status": "approved",
    }
    with pytest.raises(ValueError, match="adjudication_reason is required"):
        finalize_rows([row] * 6, require_two_reviewers=True)


def test_finalizer_supports_solo_annotator_mode():
    """Verify honest solo-annotator mode works cleanly with clear provenance."""
    rows = []
    for i in range(6):
        lbl = str(i % 2)
        rows.append({
            "task_id": f"SOLO-TASK-{i}",
            "group_id": f"solo-group-{i}",
            "query_text": f"Query solo {i}",
            "source_text": f"Source solo {i}",
            "raw_annotator_label": lbl,
            "reviewer_2_label": "",  # No fake reviewer!
            "final_label": "",
            "status": "approved",
        })

    finalized = finalize_rows(rows, require_two_reviewers=False, allow_solo_annotator=True)
    assert len(finalized) == 6
    assert all(r["provenance_mode"] == "solo_annotator" for r in finalized)
    assert {r["label"] for r in finalized} == {"0", "1"}
