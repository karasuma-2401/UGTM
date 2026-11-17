from __future__ import annotations

from pathlib import Path
import pytest

from app.documents import (
    DocumentBlock,
    TextPage,
    extract_blocks,
    extract_pages,
    make_chunks,
    normalize,
)

FIXTURES_DIR = Path(__file__).resolve().parent / "fixtures"
BASELINE_SAMPLES_DIR = Path(__file__).resolve().parent.parent / "data" / "baseline_b0" / "samples"


def test_docx_with_cover_does_not_swallow_body():
    """Verify that a DOCX with cover page does NOT cause body text to be discarded (CP-02.1)."""
    docx_path = BASELINE_SAMPLES_DIR / "cover_plus_body_b0.docx"
    assert docx_path.exists(), f"Sample file not found at {docx_path}"

    pages = extract_pages(docx_path)
    assert len(pages) >= 2, f"Expected at least 2 logical sections, got {len(pages)}"

    sections = [p.section_type for p in pages]
    assert "cover" in sections
    assert "body" in sections

    # In B0, filtering cover resulted in 0 chunks (100% body loss).
    # With CP-02.1 block parser, body chunks MUST be preserved!
    filtered_chunks = make_chunks(pages, exclude_sections={"cover"})
    assert len(filtered_chunks) == 4, f"Expected 4 body chunks, got {len(filtered_chunks)}"

    for c in filtered_chunks:
        assert c.section_type == "body"
        # Strictly NO fake page numbers for DOCX
        assert c.page_number is None
        assert c.page_start is None
        assert c.page_end is None


def test_chunk_bidirectional_block_and_paragraph_mapping():
    """Verify that chunks accurately map back to paragraph and block indices in DOCX."""
    docx_path = FIXTURES_DIR / "cover_body_refs.docx"
    assert docx_path.exists(), f"Fixture not found at {docx_path}"

    blocks = extract_blocks(docx_path)
    assert len(blocks) >= 10

    pages = extract_pages(docx_path)
    body_page = next((p for p in pages if p.section_type == "body"), None)
    assert body_page is not None
    assert len(body_page.blocks) >= 4

    chunks = make_chunks([body_page])
    assert len(chunks) >= 1

    for c in chunks:
        # Offsets in section text must match chunk.text character-for-character
        assert body_page.text[c.start_offset : c.end_offset] == c.text
        # Block mapping
        assert c.start_block_index is not None
        assert c.end_block_index is not None
        assert c.start_block_index <= c.end_block_index
        # Paragraph mapping (DOCX body paragraphs start at index 5)
        assert c.start_paragraph_index is not None
        assert c.end_paragraph_index is not None
        assert c.start_paragraph_index >= 5
        assert c.end_paragraph_index >= c.start_paragraph_index
        assert len(c.block_mapping) > 0


def test_docx_table_extraction():
    """Verify Word tables are extracted into distinct DocumentBlock objects without loss."""
    docx_path = FIXTURES_DIR / "cover_body_refs.docx"
    blocks = extract_blocks(docx_path)

    table_blocks = [b for b in blocks if b.block_type == "table"]
    assert len(table_blocks) == 1, "Expected 1 table block"

    tbl = table_blocks[0]
    assert tbl.page_number is None
    assert tbl.paragraph_index is None  # Tables do not have paragraph indices
    assert "Embedding" in tbl.text
    assert "Reranker" in tbl.text
    assert "Cross-Encoder" in tbl.text


def test_pdf_reading_order_and_page_mapping():
    """Verify PDF parser keeps 1-based page numbers, bounding boxes, and reading order."""
    pdf_path = BASELINE_SAMPLES_DIR / "cover_plus_body_b0.pdf"
    assert pdf_path.exists(), f"Sample PDF not found at {pdf_path}"

    blocks = extract_blocks(pdf_path)
    page_nums = {b.page_number for b in blocks if b.page_number is not None}
    assert page_nums == {1, 2}

    pages = extract_pages(pdf_path)
    assert len(pages) == 2
    assert pages[0].page_number == 1
    assert pages[0].section_type == "cover"
    assert pages[1].page_number == 2
    assert pages[1].section_type == "body"

    chunks = make_chunks(pages, exclude_sections={"cover"})
    assert len(chunks) == 4
    for c in chunks:
        assert c.page_number == 2
        assert c.page_start == 2
        assert c.page_end == 2
        assert c.start_block_index is not None
        assert c.end_block_index is not None
        assert c.start_block_index <= c.end_block_index


def test_pdf_repeating_header_footer_detection():
    """Verify multi-page PDF detects repeating header/footer and strips them from content text."""
    pdf_path = FIXTURES_DIR / "multi_page_with_header_footer.pdf"
    assert pdf_path.exists(), f"Fixture PDF not found at {pdf_path}"

    blocks = extract_blocks(pdf_path)
    header_blocks = [b for b in blocks if b.block_type == "header"]
    footer_blocks = [b for b in blocks if b.block_type == "footer"]

    assert len(header_blocks) >= 2, f"Expected at least 2 header blocks, got {len(header_blocks)}"
    assert len(footer_blocks) >= 2, f"Expected at least 2 footer blocks, got {len(footer_blocks)}"

    pages = extract_pages(pdf_path)
    # Ensure header text was excluded from page 2 and page 4 content
    for p in pages:
        if p.page_number in (2, 4):
            # Page text should not start with repeating header
            assert not p.text.startswith("TRƯỜNG ĐẠI HỌC CÔNG NGHỆ THÔNG TIN - KHÓA LUẬN")


def test_pdf_empty_page_handling():
    """Verify empty/blank PDF pages are identified and produce zero chunks."""
    pdf_path = FIXTURES_DIR / "multi_page_with_header_footer.pdf"
    pages = extract_pages(pdf_path)

    empty_pages = [p for p in pages if p.section_type == "empty"]
    assert len(empty_pages) >= 1
    assert empty_pages[0].page_number == 3
    assert len(empty_pages[0].text.strip()) == 0

    chunks = make_chunks(empty_pages)
    assert len(chunks) == 0, "Empty pages must produce 0 chunks"


def test_cover_body_references_separation_and_filtering():
    """Verify complete lifecycle of cover + body + references in a DOCX."""
    docx_path = FIXTURES_DIR / "cover_body_refs.docx"
    pages = extract_pages(docx_path)

    sec_map = {p.section_type: p for p in pages}
    assert "cover" in sec_map
    assert "body" in sec_map
    assert "references" in sec_map

    # Check cover content
    assert "KHÓA LUẬN TỐT NGHIỆP" in sec_map["cover"].text
    # Check body content
    assert "Chương 1" in sec_map["body"].text
    assert "Transformer" in sec_map["body"].text
    # Check references content
    assert "TÀI LIỆU THAM KHẢO" in sec_map["references"].text

    # Filtering cover and references leaves only body content
    chunks = make_chunks(pages, exclude_sections={"cover", "references"})
    assert len(chunks) >= 2
    for c in chunks:
        assert c.section_type in ("body", "structured_table")
        assert "TÀI LIỆU THAM KHẢO" not in c.text
        assert "KHÓA LUẬN TỐT NGHIỆP" not in c.text

