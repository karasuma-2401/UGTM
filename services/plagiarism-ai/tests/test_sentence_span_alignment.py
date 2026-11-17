"""Regression and Unit Tests for Constrained Sentence/Span Alignment (CP-04.2).

Verifies the 4 mandatory edge cases and operational constraints:
1. One-to-many: 1 query sentence rewritten/translated from 2 contiguous source sentences.
2. Many-to-one: 2 query sentences synthesizing/summarizing 1 source sentence.
3. Non-overlapping interval union: Merges overlapping spans from chunk sliding windows,
   ensuring zero double-counting in character coverage.
4. Multi-source separation: A query passage borrowing from two different sources
   is partitioned into two independent spans with distinct source_keys and passage_group_ids.
5. Exact verbatim alignment: Confidence >= 0.95 with type 'verbatim'.
6. Paraphrase alignment: Surface variations identified with type 'near_verbatim' or 'paraphrase'.
7. Monotonic diagonal chaining: Multiple consecutive sentence matches chained into one span.
8. End-to-end document findings alignment: `align_document_findings` workflow.
9. Disjoint / non-plagiarized texts: Yields empty spans.
10. Backward compatibility with `group_adjacent_findings`.
"""

from __future__ import annotations

import pytest
from app.alignment import (
    AlignedSpan,
    align_document_findings,
    align_passage_pair,
    compute_sentence_similarity,
    deduplicate_overlapping_spans,
    group_adjacent_findings,
)


def test_edge_case_one_to_many():
    """Case 1: One query sentence rewritten/translated from 2 contiguous source sentences."""
    query = (
        "Mô hình xử lý ngôn ngữ tự nhiên hiện đại đòi hỏi tài nguyên tính toán lớn, "
        "mặt khác nó phụ thuộc nặng nề vào chất lượng dữ liệu huấn luyện."
    )
    source = (
        "Mô hình xử lý ngôn ngữ tự nhiên hiện đại đòi hỏi tài nguyên tính toán lớn. "
        "Mặt khác nó phụ thuộc nặng nề vào chất lượng dữ liệu huấn luyện."
    )

    spans = align_passage_pair(
        query_text=query,
        source_text=source,
        source_key="src_paper_a",
        source_version=1,
    )

    assert len(spans) == 1, "Should align into a single composite span"
    span = spans[0]
    assert span.alignment_type == "one_to_many"
    assert span.confidence >= 0.85
    assert span.source_key == "src_paper_a"
    assert len(span.source_sentence_indices) == 2
    assert len(span.query_sentence_indices) == 1


def test_edge_case_many_to_one():
    """Case 2: Two query sentences summarizing/splitting 1 complex source sentence."""
    query = (
        "Kiến trúc Transformer loại bỏ hoàn toàn các lớp đệ quy. "
        "Cơ chế Self-Attention xử lý song song toàn bộ chuỗi đầu vào."
    )
    source = (
        "Kiến trúc Transformer loại bỏ hoàn toàn các lớp đệ quy, "
        "đồng thời cơ chế Self-Attention cho phép xử lý song song toàn bộ chuỗi đầu vào."
    )

    spans = align_passage_pair(
        query_text=query,
        source_text=source,
        source_key="src_paper_b",
        source_version=1,
    )

    assert len(spans) == 1, "Should align into a single composite span"
    span = spans[0]
    assert span.alignment_type == "many_to_one"
    assert span.confidence >= 0.85
    assert len(span.query_sentence_indices) == 2
    assert len(span.source_sentence_indices) == 1


def test_edge_case_non_overlapping_interval_union():
    """Case 3: Merges overlapping intervals from sliding chunk windows with zero double-counting."""
    # Chunk 1 (query chars 100 to 350)
    span1 = AlignedSpan(
        query_start=100,
        query_end=350,
        source_start=1000,
        source_end=1250,
        alignment_type="verbatim",
        confidence=0.98,
        source_key="doc_shared",
        source_version=1,
    )
    # Chunk 2 (query chars 300 to 520, overlapping on 300..350)
    span2 = AlignedSpan(
        query_start=300,
        query_end=520,
        source_start=1200,
        source_end=1420,
        alignment_type="verbatim",
        confidence=0.95,
        source_key="doc_shared",
        source_version=1,
    )

    deduped = deduplicate_overlapping_spans([span1, span2])

    assert len(deduped) == 1, "Overlapping spans on same source must be merged into one"
    merged = deduped[0]
    assert merged.query_start == 100
    assert merged.query_end == 520
    assert merged.source_start == 1000
    assert merged.source_end == 1420

    # True union length is 420, not (250 + 220 = 470)
    union_len = merged.query_end - merged.query_start
    assert union_len == 420


def test_edge_case_multi_source_separation():
    """Case 4: Multi-source synthesis within a query passage must produce separate spans & passage groups."""
    # Query passage where first sentence is from Source 1 and second sentence is from Source 2
    query_text = (
        "Thuật toán di truyền mô phỏng quá trình tiến hóa tự nhiên của loài sinh vật. "
        "Mạng nơ-ron tích chập trích xuất các đặc trưng không gian đa tỷ lệ từ ảnh số."
    )
    source1_text = "Thuật toán di truyền mô phỏng quá trình tiến hóa tự nhiên của loài sinh vật."
    source2_text = "Mạng nơ-ron tích chập trích xuất các đặc trưng không gian đa tỷ lệ từ ảnh số."

    findings = [
        {
            "submission_text": query_text,
            "submission_start": 0,
            "submission_end": len(query_text),
            "source_text": source1_text,
            "source_start": 0,
            "source_end": len(source1_text),
            "source_key": "source_genetic",
            "source_version": 1,
            "source_filename": "genetic_algorithms.pdf",
            "reranker_score": 0.95,
        },
        {
            "submission_text": query_text,
            "submission_start": 0,
            "submission_end": len(query_text),
            "source_text": source2_text,
            "source_start": 0,
            "source_end": len(source2_text),
            "source_key": "source_cnn",
            "source_version": 1,
            "source_filename": "deep_cnn.pdf",
            "reranker_score": 0.94,
        },
    ]

    spans = align_document_findings(findings)

    assert len(spans) == 2, "Must produce two independent spans"
    keys = {s.source_key for s in spans}
    assert keys == {"source_genetic", "source_cnn"}

    pg_ids = {s.passage_group_id for s in spans}
    assert len(pg_ids) == 2, "Distinct sources must receive strictly distinct passage_group_ids"

    span_gen = next(s for s in spans if s.source_key == "source_genetic")
    span_cnn = next(s for s in spans if s.source_key == "source_cnn")

    # Spans must not cross-contaminate offsets
    assert span_gen.query_start < span_cnn.query_start
    assert span_gen.query_end <= span_cnn.query_start + 5


def test_verbatim_alignment():
    """Verbatim copied passage produces high confidence and 'verbatim' alignment type."""
    text = (
        "Học tăng cường sâu kết hợp mạng nơ-ron nhân tạo với khung tối ưu hóa ra quyết định Markov. "
        "Tác tử học chính sách tối ưu thông qua tương tác liên tục với môi trường."
    )

    spans = align_passage_pair(
        query_text=text,
        source_text=text,
        query_base_offset=50,
        source_base_offset=500,
        source_key="src_rl",
    )

    assert len(spans) == 1
    s = spans[0]
    assert s.alignment_type == "verbatim"
    assert s.confidence >= 0.98
    assert s.query_start == 50
    assert s.query_end == 50 + len(text.strip())
    assert s.source_start == 500
    assert s.source_end == 500 + len(text.strip())


def test_paraphrase_alignment():
    """Paraphrased sentence is detected with confidence between 0.50 and 0.92."""
    query = "Hệ thống hỗ trợ phát hiện gian lận học thuật bằng kỹ thuật đối sánh văn bản hiện đại."
    source = "Hệ thống giúp nhận diện gian lận học thuật thông qua phương pháp đối sánh văn bản tiên tiến."

    spans = align_passage_pair(
        query_text=query,
        source_text=source,
        source_key="src_audit",
    )

    assert len(spans) == 1
    s = spans[0]
    assert s.alignment_type in {"near_verbatim", "paraphrase"}
    assert 0.50 <= s.confidence < 0.95


def test_monotonic_diagonal_chaining():
    """Consecutive sentences matching along the diagonal chain into a single span."""
    query = (
        "Bước 1: Thu thập và tiền xử lý văn bản. "
        "Bước 2: Trích xuất đặc trưng và tính toán vector nhúng. "
        "Bước 3: Truy hồi ứng viên và phân loại mức độ tương đồng."
    )
    source = (
        "Bước 1: Thu thập và tiền xử lý văn bản. "
        "Bước 2: Trích xuất đặc trưng và tính toán vector nhúng. "
        "Bước 3: Truy hồi ứng viên và phân loại mức độ tương đồng."
    )

    spans = align_passage_pair(
        query_text=query,
        source_text=source,
        max_gap=1,
    )

    assert len(spans) == 1, "All 3 consecutive sentences should chain together"
    assert spans[0].alignment_type == "verbatim"
    assert len(spans[0].query_sentence_indices) == 3
    assert len(spans[0].source_sentence_indices) == 3


def test_disjoint_texts_yield_no_spans():
    """Unrelated texts below similarity threshold yield empty spans."""
    query = "Thành phố Hồ Chí Minh là trung tâm kinh tế lớn nhất tại Việt Nam."
    source = "Cơ học lượng tử nghiên cứu các quy luật vận động của các hạt vi mô ở thang nguyên tử."

    spans = align_passage_pair(
        query_text=query,
        source_text=source,
        min_confidence=0.40,
    )

    assert len(spans) == 0


def test_backward_compatibility_group_adjacent_findings():
    """Legacy group_adjacent_findings behavior remains 100% functional."""
    findings = [
        {"source_key": "k1", "source_version": 1, "submission_chunk_no": 1, "source_chunk_no": 5},
        {"source_key": "k1", "source_version": 1, "submission_chunk_no": 2, "source_chunk_no": 6},
        {"source_key": "k1", "source_version": 1, "submission_chunk_no": 10, "source_chunk_no": 20},
        {"source_key": "k2", "source_version": 1, "submission_chunk_no": 3, "source_chunk_no": 7},
    ]

    res = group_adjacent_findings(findings)
    assert res[0]["passage_group_id"] == res[1]["passage_group_id"]
    assert res[0]["passage_group_id"] != res[2]["passage_group_id"]
    assert res[0]["passage_group_id"] != res[3]["passage_group_id"]


def test_empty_and_whitespace_inputs():
    """Empty or pure whitespace text gracefully returns empty span list."""
    assert align_passage_pair("", "") == []
    assert align_passage_pair("   ", "   ") == []
    assert align_passage_pair("Nội dung hợp lệ.", "   ") == []
    assert deduplicate_overlapping_spans([]) == []


def test_single_phrase_without_terminating_punctuation():
    """Text without trailing period/punctuation is aligned correctly."""
    query = "đồ án tốt nghiệp đại học công nghệ thông tin"
    source = "đồ án tốt nghiệp đại học công nghệ thông tin"

    spans = align_passage_pair(
        query_text=query,
        source_text=source,
        query_base_offset=10,
        source_base_offset=20,
    )

    assert len(spans) == 1
    assert spans[0].alignment_type == "verbatim"
    assert spans[0].query_start == 10
    assert spans[0].query_end == 10 + len(query)


def test_monotonic_gap_bridging():
    """A small gap of 1 sentence between two strong matching sentences is bridged."""
    query = (
        "Thuật toán di truyền khởi tạo quần thể ngẫu nhiên. "
        "Người viết bổ sung một nhận xét cá nhân ngắn gọn. "
        "Toán tử lai ghép và đột biến tạo ra thế hệ kế tiếp."
    )
    source = (
        "Thuật toán di truyền khởi tạo quần thể ngẫu nhiên. "
        "Toán tử lai ghép và đột biến tạo ra thế hệ kế tiếp."
    )

    # When max_gap=1, sentence 0 and sentence 2 in query match sentence 0 and 1 in source
    spans = align_passage_pair(
        query_text=query,
        source_text=source,
        max_gap=1,
    )

    assert len(spans) == 1
    assert spans[0].query_start == 0
    assert spans[0].query_end == len(query.strip())


def test_aligned_span_dataclass_methods():
    """Test to_dict and property methods of AlignedSpan."""
    span = AlignedSpan(
        query_start=15,
        query_end=120,
        source_start=40,
        source_end=145,
        alignment_type="near_verbatim",
        confidence=0.88456,
        source_key="src_doc",
        source_version=2,
        source_filename="test.pdf",
        passage_group_id="PG-0005",
    )

    assert span.query_span == (15, 120)
    assert span.source_span == (40, 145)

    d = span.to_dict()
    assert d["query_start"] == 15
    assert d["confidence"] == 0.8846
    assert d["source_version"] == 2

