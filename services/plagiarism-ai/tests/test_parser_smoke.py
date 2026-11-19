"""Portable parser checks using synthetic documents, without private research datasets."""
import fitz
from docx import Document

from app.documents import extract_blocks, extract_pages


def test_docx_parser_keeps_synthetic_body_and_table(tmp_path):
    path = tmp_path / "sample.docx"
    document = Document()
    document.add_heading("Research introduction", level=1)
    document.add_paragraph("A synthetic academic paragraph for portable parser verification.")
    table = document.add_table(rows=1, cols=2)
    table.cell(0, 0).text = "Synthetic label"
    table.cell(0, 1).text = "Synthetic value"
    document.save(path)
    blocks = extract_blocks(path)
    text = " ".join(block.text for block in blocks)
    assert "synthetic academic paragraph" in text
    assert "Synthetic label" in text
    assert "Synthetic value" in text


def test_pdf_parser_keeps_page_mapping_without_external_fixtures(tmp_path):
    path = tmp_path / "sample.pdf"
    document = fitz.open()
    for text in ("Synthetic first page", "Synthetic second page"):
        page = document.new_page()
        page.insert_text((72, 100), text)
    document.save(path)
    document.close()
    blocks = extract_blocks(path)
    assert {block.page_number for block in blocks if block.text.strip()} == {1, 2}
    pages = extract_pages(path)
    assert len(pages) == 2
