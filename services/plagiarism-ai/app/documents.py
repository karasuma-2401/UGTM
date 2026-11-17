from __future__ import annotations

import difflib
import hashlib
import re
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import pymupdf
from docx import Document
from docx.table import Table
from docx.text.paragraph import Paragraph


@dataclass
class DocumentBlock:
    """A distinct structural block in a document (paragraph, table row/cell, heading, etc.)."""

    block_index: int
    block_type: str  # "paragraph", "table", "heading", "header", "footer"
    page_number: int | None  # 1-based page number for PDF, None for DOCX (no fake page numbers)
    paragraph_index: int | None  # 0-based paragraph index for DOCX, None for PDF/tables
    raw_text: str  # Raw text as extracted from source
    text: str  # Normalized text
    section_type: str = "body"  # "cover", "table_of_contents", "abbreviations", "body", "references", "appendix", "empty"
    start_char_offset: int = 0  # Start character offset in section/page text
    end_char_offset: int = 0  # End character offset in section/page text
    metadata: dict[str, Any] = field(default_factory=dict)

    def to_mapping_dict(self) -> dict[str, Any]:
        return {
            "block_index": self.block_index,
            "block_type": self.block_type,
            "page_number": self.page_number,
            "paragraph_index": self.paragraph_index,
            "section_type": self.section_type,
            "start_char_offset": self.start_char_offset,
            "end_char_offset": self.end_char_offset,
        }


@dataclass
class TextPage:
    """Logical page or section grouping for chunking and display."""

    page_number: int | None
    text: str
    section_type: str = "body"
    blocks: list[DocumentBlock] = field(default_factory=list)


@dataclass
class Chunk:
    """A sliding window text chunk with bidirectional block & section location mapping."""

    chunk_no: int
    page_number: int | None  # 1-based for PDF, None for DOCX
    start_offset: int  # Character start offset in the section/page text
    end_offset: int  # Character end offset in the section/page text
    text: str
    section_type: str = "body"
    # Bidirectional location mappings (CP-02.1)
    start_block_index: int | None = None
    end_block_index: int | None = None
    start_paragraph_index: int | None = None  # For DOCX
    end_paragraph_index: int | None = None  # For DOCX
    page_start: int | None = None  # For multi-page or PDF
    page_end: int | None = None
    block_mapping: list[dict[str, Any]] = field(default_factory=list)


# --- Section Classification Regular Expressions ---

RX_COVER_HEADER = re.compile(
    r"^(?:đại học quốc gia|bộ giáo dục(?: và đào tạo)?|trường đại học|university of|vietnam national university)\b",
    re.I,
)
RX_COVER_KEYWORDS = re.compile(
    r"\b(?:khóa luận tốt nghiệp|khoá luận tốt nghiệp|báo cáo tốt nghiệp|đề tài\s*:|tên đề tài\b)|^đề tài\b",
    re.I,
)
RX_TOC_HEADER = re.compile(
    r"^(?:mục lục|danh mục (?:hình|bảng|biểu đồ|sơ đồ|các bảng|các hình))\b",
    re.I,
)
RX_TOC_DOTS = re.compile(r"(?:\.{4,}|…{2,}|\.\s*\.\s*\.\s*\.)")
RX_BIB_HEADER = re.compile(
    r"^(?:tài liệu tham khảo|tham khảo|references|bibliography)\b",
    re.I,
)
RX_ABBR_HEADER = re.compile(
    r"^(?:danh mục từ viết tắt|bảng từ viết tắt|các từ viết tắt|danh mục ký hiệu|bảng các ký hiệu|từ viết tắt)\b",
    re.I,
)
RX_TABLE_STRUCT = re.compile(
    r"(?:\bstt\b.{0,60}(?:kiểu dữ liệu|thuộc tính|ràng buộc|mô tả|diễn giải)|\bbảng\s+\d+(?:[.\-]\d+){0,3}\s*:\s*bảng\b)",
    re.I,
)
RX_CHAPTER_HEADER = re.compile(
    r"^(?:chương\s+\d+|chương\s+[ivxlcdm]+|phần\s+\d+|chapter\s+\d+|mở đầu|giới thiệu|lời cảm ơn|lời cam đoan|tóm tắt|abstract)\b",
    re.I,
)
RX_APPENDIX_HEADER = re.compile(
    r"^(?:phụ lục|appendix)\b",
    re.I,
)
RX_TITLE = re.compile(
    r"(?:ĐỀ TÀI|Đề tài|ĐỀ TÀI:|Đề tài:)[\s\n]+(.*?)(?=\s+(?:Building|Developing|Applying|Researching|Design|Constructing|An |A |CỬ NHÂN|KỸ SƯ|GIẢNG VIÊN|GVHD|TP\.|THÀNH PHỐ|\d{4}|$))",
    re.I | re.DOTALL,
)
RX_TITLE_FALLBACK = re.compile(
    r"(?:KHÓA|KHOÁ) LUẬN TỐT NGHIỆP[\s\n]+(.*?)(?=\s+(?:Building|Developing|Applying|Researching|Design|Constructing|An |A |CỬ NHÂN|KỸ SƯ|GIẢNG VIÊN|GVHD|TP\.|THÀNH PHỐ|\d{4}|$))",
    re.I | re.DOTALL,
)
RX_AUTHORS = re.compile(
    r"(?:KHOA[^\n\r]+?)(?:[\s\n]+)((?:[A-ZÀ-Ỹ\s–\-\d]{4,90}))[\s\n]+(?:KHÓA|KHOÁ)\s+LUẬN",
    re.DOTALL,
)


_VI_VOWEL_LOWER = "aăâeêioôơuưyàáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵ"
_VI_ACCENTED_LOWER = "àáảãạắằẳẵặấầẩẫậèéẻẽẹếềểễệìíỉĩịòóỏõọốồổỗộớờởỡợùúủũụứừửữựỳýỷỹỵ"
_VI_VOWEL_UPPER = _VI_VOWEL_LOWER.upper()
_VI_ACCENTED_UPPER = _VI_ACCENTED_LOWER.upper()
_VI_VOWEL_ALL = _VI_VOWEL_LOWER + _VI_VOWEL_UPPER

_RX_VI_GLUED_LOWER = re.compile(
    rf"([{_VI_VOWEL_LOWER}]*[{_VI_ACCENTED_LOWER}][{_VI_VOWEL_LOWER}]*)"
    rf"(?=(?:ngh|ng|nh|ch|gh|gi|kh|ph|th|tr|qu|[bcdđghklmnpqrstvx])[{_VI_VOWEL_LOWER}]"
    rf"|[{_VI_VOWEL_UPPER}BCDĐGHKLMNPQRSTVX0-9])"
)
_RX_VI_GLUED_UPPER = re.compile(
    rf"([{_VI_VOWEL_UPPER}]*[{_VI_ACCENTED_UPPER}][{_VI_VOWEL_UPPER}]*)"
    rf"(?=(?:NGH|NG|NH|CH|GH|GI|KH|PH|TH|TR|QU|[BCDĐGHKLMNPQRSTVX])[{_VI_VOWEL_ALL}]|[0-9])"
)


def repair_glued_vietnamese_text(text: str) -> str:
    """Repair missing spaces after accented Vietnamese vowels caused by LaTeX T5 PDF font extraction."""
    if not text:
        return ""
    repaired = _RX_VI_GLUED_LOWER.sub(r"\1 ", text)
    repaired = _RX_VI_GLUED_UPPER.sub(r"\1 ", repaired)
    return repaired


def normalize(text: str) -> str:
    """Normalize whitespace and clean text."""
    clean = repair_glued_vietnamese_text((text or "").replace("\x00", ""))
    return re.sub(r"\s+", " ", clean).strip()


def classify_text_section(text: str, page_number: int | None = None) -> str:
    """Classify the structural section type of a given text segment."""
    s = unicodedata.normalize("NFKC", text or "").strip()
    s_lower = s.casefold()
    if not s_lower:
        return "empty"

    if page_number is not None and page_number <= 2:
        if (
            RX_COVER_HEADER.search(s_lower[:200])
            or RX_COVER_KEYWORDS.search(s_lower[:250])
            or ("khoa công nghệ phần mềm" in s_lower[:250] and "khóa luận" in s_lower)
        ):
            return "cover"

    if RX_COVER_HEADER.search(s_lower[:150]):
        return "cover"
    if RX_ABBR_HEADER.search(s_lower[:120]):
        return "abbreviations"
    if RX_BIB_HEADER.search(s_lower[:120]) or (
        len(re.findall(r"\[\s*\d+\s*\]", s)) >= 3
        and len(re.findall(r"https?://|\baccessed\b|\bavailable\b", s_lower)) >= 1
    ):
        return "references"
    if RX_TOC_HEADER.search(s_lower[:120]) or (
        len(RX_TOC_DOTS.findall(s)) >= 2
        and re.search(r"\b(?:bảng|hình|use\s*case|mục|chương)\b", s_lower)
    ):
        return "table_of_contents"
    if RX_TABLE_STRUCT.search(s_lower[:350]):
        return "structured_table"
    return "body"


def _classify_docx_blocks_statefully(blocks: list[DocumentBlock]) -> None:
    """Classify section types for DOCX blocks using document structure sequence analysis."""
    state = "init"
    for i, block in enumerate(blocks):
        norm = block.text
        norm_l = norm.casefold()
        if not norm_l:
            block.section_type = "empty"
            continue

        if state in ("init", "cover"):
            is_cover_element = (
                i <= 20
                and (
                    RX_COVER_HEADER.search(norm_l[:250])
                    or RX_COVER_KEYWORDS.search(norm_l[:250])
                    or ("khoa công nghệ phần mềm" in norm_l[:250] and "khóa luận" in norm_l)
                    or ("sinh viên thực hiện" in norm_l or "giảng viên hướng dẫn" in norm_l)
                    or ("bộ môn" in norm_l and "trường đại học" in norm_l)
                )
            )
            if is_cover_element:
                state = "cover"
                block.section_type = "cover"
                continue

            if RX_TOC_HEADER.search(norm_l[:120]) or len(RX_TOC_DOTS.findall(norm)) >= 2:
                state = "table_of_contents"
                block.section_type = "table_of_contents"
                continue

            if RX_ABBR_HEADER.search(norm_l[:120]):
                state = "abbreviations"
                block.section_type = "abbreviations"
                continue

            if RX_CHAPTER_HEADER.search(norm_l[:120]) or (
                state == "cover" and len(norm.split()) >= 30 and not is_cover_element
            ):
                state = "body"
                block.section_type = "body"
                continue

            if state == "init":
                state = "body"
                block.section_type = "body"
            else:
                if len(norm.split()) < 20 and i <= 15:
                    block.section_type = "cover"
                else:
                    state = "body"
                    block.section_type = "body"

        elif state == "table_of_contents":
            if RX_CHAPTER_HEADER.search(norm_l[:120]) or (
                len(RX_TOC_DOTS.findall(norm)) == 0 and len(norm.split()) >= 25
            ):
                state = "body"
                block.section_type = "body"
            else:
                block.section_type = "table_of_contents"

        elif state == "abbreviations":
            if RX_CHAPTER_HEADER.search(norm_l[:120]) or len(norm.split()) >= 30:
                state = "body"
                block.section_type = "body"
            else:
                block.section_type = "abbreviations"

        elif state == "body":
            if RX_BIB_HEADER.search(norm_l[:120]) or (
                len(re.findall(r"\[\s*\d+\s*\]", norm)) >= 3
                and len(re.findall(r"https?://|\baccessed\b|\bavailable\b|\btrans\b|\bvol\b", norm_l)) >= 1
            ):
                state = "references"
                block.section_type = "references"
            elif RX_APPENDIX_HEADER.search(norm_l[:120]):
                state = "appendix"
                block.section_type = "appendix"
            elif RX_TABLE_STRUCT.search(norm_l[:350]):
                block.section_type = "structured_table"
            else:
                block.section_type = "body"

        elif state == "references":
            if RX_APPENDIX_HEADER.search(norm_l[:120]):
                state = "appendix"
                block.section_type = "appendix"
            else:
                block.section_type = "references"

        elif state == "appendix":
            block.section_type = "appendix"


def extract_table_text(table: Table) -> tuple[str, str]:
    """Extract raw and normalized text from a Word table."""
    raw_rows: list[str] = []
    norm_rows: list[str] = []
    for row in table.rows:
        cell_texts = [c.text.strip() for c in row.cells]
        unique_cells: list[str] = []
        for ct in cell_texts:
            if not unique_cells or ct != unique_cells[-1]:
                unique_cells.append(ct)
        if any(unique_cells):
            raw_rows.append(" | ".join(unique_cells))
            norm_rows.append(" | ".join(normalize(c) for c in unique_cells if c))
    return "\n".join(raw_rows), "\n".join(norm_rows)


def _detect_pdf_repeating_headers_footers(pdf_doc) -> tuple[set[str], set[str]]:
    """Detect repeated header and footer text lines across multi-page PDFs."""
    if len(pdf_doc) < 3:
        return set(), set()
    top_candidates: dict[str, int] = {}
    bottom_candidates: dict[str, int] = {}
    for page in pdf_doc:
        height = page.rect.height
        blocks = page.get_text("blocks")
        for b in blocks:
            if b[6] != 0:
                continue
            text = normalize(b[4])
            if not text or len(text) < 3:
                continue
            y0, y1 = b[1], b[3]
            if y1 <= 75:
                top_candidates[text] = top_candidates.get(text, 0) + 1
            elif y0 >= height - 70:
                bottom_candidates[text] = bottom_candidates.get(text, 0) + 1

    headers = {text for text, count in top_candidates.items() if count >= 2}
    footers = {text for text, count in bottom_candidates.items() if count >= 2}
    return headers, footers


def extract_blocks(path: str | Path) -> list[DocumentBlock]:
    """Extract all structural document blocks in logical reading order.

    - For DOCX: Extracts each paragraph and table element; strictly page_number=None.
    - For PDF: Extracts text blocks per page with coordinates and reading order.
    """
    file = Path(path)
    if not file.exists():
        raise FileNotFoundError(f"File not found: {file}")

    ext = file.suffix.lower()
    if ext == ".docx":
        doc = Document(str(file))
        blocks: list[DocumentBlock] = []
        block_idx = 0
        paragraph_idx = 0
        total_len = 0

        for child in doc.element.body:
            tag = child.tag.split("}")[-1]
            if tag == "p":
                p = Paragraph(child, doc)
                raw_text = p.text
                norm_text = normalize(raw_text)
                if not norm_text:
                    paragraph_idx += 1
                    continue
                total_len += len(norm_text)
                if total_len > 2_000_000:
                    raise ValueError("DOCUMENT_TOO_LONG")
                blocks.append(
                    DocumentBlock(
                        block_index=block_idx,
                        block_type="paragraph",
                        page_number=None,  # Do not assign fake page numbers to DOCX
                        paragraph_index=paragraph_idx,
                        raw_text=raw_text,
                        text=norm_text,
                    )
                )
                block_idx += 1
                paragraph_idx += 1

            elif tag == "tbl":
                tbl = Table(child, doc)
                raw_tbl, norm_tbl = extract_table_text(tbl)
                if not norm_tbl:
                    continue
                total_len += len(norm_tbl)
                if total_len > 2_000_000:
                    raise ValueError("DOCUMENT_TOO_LONG")
                blocks.append(
                    DocumentBlock(
                        block_index=block_idx,
                        block_type="table",
                        page_number=None,
                        paragraph_index=None,
                        raw_text=raw_tbl,
                        text=norm_tbl,
                    )
                )
                block_idx += 1

        _classify_docx_blocks_statefully(blocks)
        return blocks

    if ext == ".pdf":
        blocks: list[DocumentBlock] = []
        block_idx = 0
        with pymupdf.open(str(file)) as pdf:
            if len(pdf) > 250:
                raise ValueError("TOO_MANY_PAGES")

            headers, footers = _detect_pdf_repeating_headers_footers(pdf)

            for i, page in enumerate(pdf):
                page_num = i + 1
                page_blocks = page.get_text("blocks")
                # Sort blocks by vertical position y0, then horizontal x0 (reading order)
                text_blocks = [b for b in page_blocks if b[6] == 0]
                text_blocks.sort(key=lambda b: (b[1], b[0]))

                if not text_blocks:
                    # Empty page
                    blocks.append(
                        DocumentBlock(
                            block_index=block_idx,
                            block_type="empty_page",
                            page_number=page_num,
                            paragraph_index=None,
                            raw_text="",
                            text="",
                            section_type="empty",
                        )
                    )
                    block_idx += 1
                    continue

                for b in text_blocks:
                    raw_text = b[4]
                    norm_text = normalize(raw_text)
                    if not norm_text:
                        continue

                    # Check for repeating header or footer
                    if norm_text in headers or (b[3] <= 70 and len(norm_text.split()) < 15 and "trường" in norm_text.casefold()):
                        b_type = "header"
                        sec_type = "header"
                    elif norm_text in footers or re.match(r"^\s*(?:trang\s+)?\d+\s*(?:/\s*\d+)?\s*$", norm_text, re.I):
                        b_type = "footer"
                        sec_type = "footer"
                    else:
                        b_type = "paragraph"
                        sec_type = classify_text_section(norm_text, page_number=page_num)

                    blocks.append(
                        DocumentBlock(
                            block_index=block_idx,
                            block_type=b_type,
                            page_number=page_num,
                            paragraph_index=None,
                            raw_text=raw_text,
                            text=norm_text,
                            section_type=sec_type,
                            metadata={"bbox": (b[0], b[1], b[2], b[3])},
                        )
                    )
                    block_idx += 1

        return blocks

    raise ValueError("Only PDF and DOCX are supported")


def extract_pages(path: str | Path) -> list[TextPage]:
    """Extract pages or structural sections from a document with full block traceability.

    - For PDF: Returns one TextPage per PDF page (page_number=1..N).
    - For DOCX: Groups contiguous blocks into logical sections (cover, body, references),
      guaranteeing that a cover page never swallows the rest of the body. page_number is None.
    """
    file = Path(path)
    blocks = extract_blocks(file)

    if file.suffix.lower() == ".pdf":
        pages_dict: dict[int, list[DocumentBlock]] = {}
        for b in blocks:
            p_num = b.page_number or 1
            pages_dict.setdefault(p_num, []).append(b)

        result: list[TextPage] = []
        for p_num in sorted(pages_dict.keys()):
            p_blocks = pages_dict[p_num]
            # Exclude header/footer blocks from the primary text string to avoid false positives
            content_blocks = [b for b in p_blocks if b.block_type not in ("header", "footer")]
            if not content_blocks:
                content_blocks = p_blocks

            # Determine dominant section type
            sec_types = [b.section_type for b in content_blocks if b.section_type not in ("empty", "header", "footer")]
            dominant_section = sec_types[0] if sec_types else (content_blocks[0].section_type if content_blocks else "body")

            current_offset = 0
            text_parts = []
            for b in content_blocks:
                b.start_char_offset = current_offset
                b.end_char_offset = current_offset + len(b.text)
                text_parts.append(b.text)
                current_offset = b.end_char_offset + 1  # Separator " " length 1

            page_text = " ".join(text_parts)
            result.append(
                TextPage(
                    page_number=p_num,
                    text=page_text,
                    section_type=dominant_section,
                    blocks=content_blocks,
                )
            )
        return result

    if file.suffix.lower() == ".docx":
        # Group contiguous blocks by section_type
        sections: list[list[DocumentBlock]] = []
        current_section: list[DocumentBlock] = []
        current_sec_type: str | None = None

        for b in blocks:
            if b.section_type == "empty":
                continue
            if current_sec_type is None:
                current_sec_type = b.section_type
                current_section.append(b)
            elif b.section_type == current_sec_type:
                current_section.append(b)
            else:
                sections.append(current_section)
                current_section = [b]
                current_sec_type = b.section_type
        if current_section:
            sections.append(current_section)

        if not sections:
            return []

        result = []
        for sec_blocks in sections:
            sec_type = sec_blocks[0].section_type
            current_offset = 0
            text_parts = []
            for b in sec_blocks:
                b.start_char_offset = current_offset
                b.end_char_offset = current_offset + len(b.text)
                text_parts.append(b.text)
                current_offset = b.end_char_offset + 1

            sec_text = " ".join(text_parts)
            result.append(
                TextPage(
                    page_number=None,  # DOCX strictly has no fake page numbers
                    text=sec_text,
                    section_type=sec_type,
                    blocks=sec_blocks,
                )
            )
        return result

    raise ValueError("Only PDF and DOCX are supported")


def _map_blocks_to_chunk(
    page: TextPage,
    start: int,
    end: int,
    chunk_no: int,
    chunk_text: str,
    chunk_section: str,
) -> Chunk:
    start_block_idx: int | None = None
    end_block_idx: int | None = None
    start_para_idx: int | None = None
    end_para_idx: int | None = None
    page_start: int | None = page.page_number
    page_end: int | None = page.page_number
    mapping_records: list[dict[str, Any]] = []

    if page.blocks:
        overlapping = [
            b
            for b in page.blocks
            if max(start, b.start_char_offset) < min(end, b.end_char_offset)
        ]
        if not overlapping:
            overlapping = [min(page.blocks, key=lambda b: abs(b.start_char_offset - start))]

        start_block_idx = overlapping[0].block_index
        end_block_idx = overlapping[-1].block_index

        para_indices = [b.paragraph_index for b in overlapping if b.paragraph_index is not None]
        if para_indices:
            start_para_idx = min(para_indices)
            end_para_idx = max(para_indices)

        page_nums = [b.page_number for b in overlapping if b.page_number is not None]
        if page_nums:
            page_start = min(page_nums)
            page_end = max(page_nums)

        mapping_records = [b.to_mapping_dict() for b in overlapping]

    return Chunk(
        chunk_no=chunk_no,
        page_number=page.page_number,
        start_offset=start,
        end_offset=end,
        text=chunk_text,
        section_type=chunk_section,
        start_block_index=start_block_idx,
        end_block_index=end_block_idx,
        start_paragraph_index=start_para_idx,
        end_paragraph_index=end_para_idx,
        page_start=page_start,
        page_end=page_end,
        block_mapping=mapping_records,
    )


ABBR_RE = re.compile(
    r"\b(?:TP|ThS|TS|PGS|GS|GVHD|KS|CN|al|vol|no|fig|tab|e\.g|i\.e|v\.v)\.",
    re.I,
)


def split_sentences(text: str) -> list[tuple[int, int, str]]:
    """Split text into sentences returning (start_offset, end_offset, sentence_text).
    Protects scientific, Vietnamese, and common abbreviations from false splits.
    """
    if not text.strip():
        return []

    abbr_spans = {m.end() for m in ABBR_RE.finditer(text)}
    decimal_spans = {m.start() + 1 for m in re.finditer(r"\d\.\d", text)}
    protected_dots = abbr_spans | decimal_spans

    cand_spans = list(re.finditer(r"([.!?]+)(\s+)(?=[A-ZÀ-Ỹ0-9]|$)", text))
    boundaries = [0]
    for m in cand_spans:
        if m.end(1) not in protected_dots:
            boundaries.append(m.end())
    boundaries.append(len(text))
    boundaries = sorted(set(boundaries))

    sentences = []
    for i in range(len(boundaries) - 1):
        start = boundaries[i]
        end = boundaries[i + 1]
        s_text = text[start:end].strip()
        if s_text:
            sentences.append((start, end, s_text))
    return sentences


def make_sentence_chunks(
    pages: list[TextPage],
    target_words: int = 90,
    overlap_sentences: int = 1,
    min_words: int = 8,
    max_words: int = 140,
    exclude_sections: set[str] | tuple[str, ...] | None = None,
) -> list[Chunk]:
    """Generate chunks respecting complete sentence boundaries."""
    excluded = set(exclude_sections) if exclude_sections else set()
    result: list[Chunk] = []
    chunk_no = 0

    for page in pages:
        if page.section_type in excluded:
            continue

        sentences = split_sentences(page.text)
        if not sentences:
            continue

        sent_words = [len(s[2].split()) for s in sentences]
        i = 0
        n = len(sentences)

        while i < n:
            curr_words = 0
            j = i
            while j < n:
                w = sent_words[j]
                if curr_words > 0 and (curr_words + w > max_words):
                    break
                curr_words += w
                j += 1
                if curr_words >= target_words:
                    break

            if curr_words < min_words and result:
                break

            start = sentences[i][0]
            end = sentences[j - 1][1]
            chunk_text = page.text[start:end].strip()

            chunk_obj = _map_blocks_to_chunk(
                page=page,
                start=start,
                end=end,
                chunk_no=chunk_no,
                chunk_text=chunk_text,
                chunk_section=page.section_type,
            )
            result.append(chunk_obj)
            chunk_no += 1

            if j >= n:
                break

            step = max(1, (j - i) - overlap_sentences)
            i += step

    return result


def make_paragraph_chunks(
    pages: list[TextPage],
    target_words: int = 90,
    min_words: int = 8,
    max_words: int = 150,
    exclude_sections: set[str] | tuple[str, ...] | None = None,
) -> list[Chunk]:
    """Generate chunks respecting structural paragraph boundaries."""
    excluded = set(exclude_sections) if exclude_sections else set()
    result: list[Chunk] = []
    chunk_no = 0

    for page in pages:
        if page.section_type in excluded:
            continue

        blocks = [b for b in page.blocks if b.section_type not in excluded and b.text.strip()]
        if not blocks:
            page_chunks = make_sentence_chunks(
                [page],
                target_words=target_words,
                overlap_sentences=1,
                min_words=min_words,
                max_words=max_words,
                exclude_sections=excluded,
            )
            for c in page_chunks:
                c.chunk_no = chunk_no
                result.append(c)
                chunk_no += 1
            continue

        i = 0
        n = len(blocks)
        while i < n:
            curr_words = 0
            j = i
            while j < n:
                b_words = len(blocks[j].text.split())
                if curr_words > 0 and (curr_words + b_words > max_words):
                    break
                curr_words += b_words
                j += 1
                if curr_words >= target_words:
                    break

            # If a single paragraph is excessively long, sub-chunk it via sentence window
            if j == i + 1 and curr_words > max_words:
                single_block_page = TextPage(
                    page_number=page.page_number,
                    text=blocks[i].text,
                    section_type=blocks[i].section_type,
                    blocks=[blocks[i]],
                )
                sub_chunks = make_sentence_chunks(
                    [single_block_page],
                    target_words=target_words,
                    min_words=min_words,
                    max_words=max_words,
                )
                for sc in sub_chunks:
                    sc.chunk_no = chunk_no
                    sc.start_offset += blocks[i].start_char_offset
                    sc.end_offset += blocks[i].start_char_offset
                    sc.start_block_index = blocks[i].block_index
                    sc.end_block_index = blocks[i].block_index
                    sc.start_paragraph_index = blocks[i].paragraph_index
                    sc.end_paragraph_index = blocks[i].paragraph_index
                    result.append(sc)
                    chunk_no += 1
                i += 1
                continue

            if curr_words < min_words and result:
                break

            start = blocks[i].start_char_offset
            end = blocks[j - 1].end_char_offset
            chunk_text = page.text[start:end].strip()

            chunk_obj = _map_blocks_to_chunk(
                page=page,
                start=start,
                end=end,
                chunk_no=chunk_no,
                chunk_text=chunk_text,
                chunk_section=page.section_type,
            )
            result.append(chunk_obj)
            chunk_no += 1

            i = j

    return result


def make_chunks(
    pages: list[TextPage],
    size: int = 90,
    overlap: int = 20,
    min_words: int = 8,
    exclude_sections: set[str] | tuple[str, ...] | None = None,
    strategy: str = "sliding_window",
    **kwargs: Any,
) -> list[Chunk]:
    """Generate chunks with the selected chunking strategy and block location mapping.

    Supported strategies:
    - sliding_window: Fixed-size word sliding window (Baseline B0 compatible)
    - sentence_window: Complete sentence boundary preservation
    - paragraph_boundary: Paragraph structural block preservation
    """
    if strategy == "sentence_window":
        return make_sentence_chunks(
            pages,
            target_words=kwargs.get("target_words", size),
            overlap_sentences=kwargs.get("overlap_sentences", 1),
            min_words=min_words,
            max_words=kwargs.get("max_words", 140),
            exclude_sections=exclude_sections,
        )
    if strategy == "paragraph_boundary":
        return make_paragraph_chunks(
            pages,
            target_words=kwargs.get("target_words", size),
            min_words=min_words,
            max_words=kwargs.get("max_words", 150),
            exclude_sections=exclude_sections,
        )

    if not (0 <= overlap < size):
        raise ValueError("overlap must be smaller than size")

    excluded = set(exclude_sections) if exclude_sections else set()
    result: list[Chunk] = []
    chunk_no = 0

    for page in pages:
        if page.section_type in excluded:
            continue

        spans = list(re.finditer(r"\S+", page.text))
        if not spans:
            continue

        for first in range(0, len(spans), size - overlap):
            last = min(first + size, len(spans))
            if last - first < min_words:
                break
            start = spans[first].start()
            end = spans[last - 1].end()
            chunk_text = page.text[start:end]

            chunk_section = page.section_type
            if chunk_section == "body" and page.page_number is None and not page.blocks:
                detected = classify_text_section(chunk_text, page_number=None)
                if detected != "body":
                    chunk_section = detected

            if chunk_section in excluded:
                continue

            chunk_obj = _map_blocks_to_chunk(
                page=page,
                start=start,
                end=end,
                chunk_no=chunk_no,
                chunk_text=chunk_text,
                chunk_section=chunk_section,
            )
            result.append(chunk_obj)
            chunk_no += 1
            if last == len(spans):
                break

    return result


def extract_document_metadata(
    path: str | Path,
    pages: list[TextPage] | None = None,
) -> dict[str, str]:
    """Extract core document metadata (title, authors, cover prefix, SHA-256)."""
    path = Path(path)
    if pages is None:
        if path.suffix.lower() == ".pdf":
            pages = extract_pages(path)[:2]
        elif path.suffix.lower() == ".docx":
            pages = extract_pages(path)[:2]
        else:
            pages = []

    full_text = " ".join(p.text for p in pages)
    cover = pages[0].text[:1500] if pages else ""

    m_title = RX_TITLE.search(cover) or RX_TITLE_FALLBACK.search(cover)
    title = m_title.group(1).strip() if m_title else ""

    m_authors = RX_AUTHORS.search(cover)
    authors = m_authors.group(1).strip() if m_authors else ""
    authors = re.sub(r"[\d–\-]", "", authors).strip()

    def norm_field(s: str) -> str:
        s_clean = unicodedata.normalize("NFKC", s or "").casefold()
        s_clean = re.sub(r"[^\w\s]", " ", s_clean)
        return re.sub(r"\s+", " ", s_clean).strip()

    norm_title = norm_field(title)
    norm_authors = norm_field(authors)
    cover_prefix = norm_field(cover[:350])
    text_sha256 = hashlib.sha256(full_text.encode("utf-8")).hexdigest()

    return {
        "title": title[:150],
        "norm_title": norm_title,
        "authors": authors[:100],
        "norm_authors": norm_authors,
        "cover_prefix": cover_prefix,
        "text_sha256": text_sha256,
    }


def is_duplicate_document(
    meta1: dict[str, str], meta2: dict[str, str]
) -> tuple[bool, str]:
    """Determine whether two documents represent duplicate submissions."""
    if meta1.get("text_sha256") and meta1["text_sha256"] == meta2.get("text_sha256"):
        return True, "SAME_TEXT_HASH"

    t1 = meta1.get("norm_title", "")
    t2 = meta2.get("norm_title", "")
    if len(t1) > 15 and len(t2) > 15:
        t_ratio = difflib.SequenceMatcher(None, t1, t2).ratio()
        if t_ratio >= 0.85:
            a1 = meta1.get("norm_authors", "")
            a2 = meta2.get("norm_authors", "")
            if a1 and a2:
                a_ratio = difflib.SequenceMatcher(None, a1, a2).ratio()
                if a_ratio >= 0.75:
                    return True, f"TITLE_AND_AUTHORS_MATCH (title={t_ratio:.2f}, authors={a_ratio:.2f})"
            else:
                c1 = meta1.get("cover_prefix", "")
                c2 = meta2.get("cover_prefix", "")
                c_ratio = difflib.SequenceMatcher(None, c1, c2).ratio() if (c1 and c2) else 0.0
                if c_ratio >= 0.80:
                    return True, f"TITLE_AND_COVER_MATCH (title={t_ratio:.2f}, cover={c_ratio:.2f})"
    return False, ""


_MANIFEST_DISPLAY_CACHE: dict[str, str] | None = None

_MANIFEST_OVERRIDES: dict[str, tuple[str, str]] = {
    "kltn-71d65255b2d640c5": (
        "NỀN TẢNG THÔNG MINH HỖ TRỢ QUẢN LÝ TOÀN DIỆN ĐỀ TÀI NGHIÊN CỨU VÀ KHÓA LUẬN TỐT NGHIỆP",
        "HỒ PHẠM PHÚ AN - NGUYỄN NGUYÊN NGỌC ANH",
    ),
    "kltn-aa80cfdf64a9d50a": (
        "XÂY DỰNG HỆ THỐNG THƯƠNG MẠI ĐIỆN TỬ MICROSERVICES CLOUD-NATIVE: TRIỂN KHAI, GIÁM SÁT VÀ QUẢN LÝ VỚI KUBERNETES, HELM VÀ ELASTIC STACK",
        "TRƯƠNG LÊ VĨNH PHÚC - NGUYỄN THÀNH TÀI",
    ),
}


def _clean_manifest_title_and_authors(raw_title: str, raw_authors: str) -> tuple[str, str]:
    title = normalize(raw_title or "")
    # Remove trailing English subtitle fragments if present after Vietnamese title
    title = re.sub(
        r"\s+(?:BUILD(?:ING)?|DEVELOP(?:ING)?|APPLY(?:ING)?|RESEARCH(?:ING)?|DESIGN(?:ING)?|CONSTRUCT(?:ING)?|TOWARDS|ENHANC(?:ING|E)|IMPROV(?:ING|E)|AI-INTEGRATED)\s+[A-Za-z0-9,\-–—:&/()'\s]+$",
        "",
        title,
        flags=re.I,
    ).strip(" -–—:;,.")
    authors = normalize(raw_authors or "")
    authors = re.sub(
        r"^(?:KHOA\s+CÔNG\s+NGHỆ\s+PHẦN\s+MỀM|CÔNG\s+NGHỆ\s+PHẦN\s+MỀM|NGHỆ\s+PHẦN\s+MỀM|KỸ\s+THUẬT\s+PHẦN\s+MỀM|THUẬT\s+PHẦN\s+MỀM)\s+",
        "",
        authors,
        flags=re.I,
    ).strip(" -–—:;,.")
    return title, authors


def _load_manifest_display_map() -> dict[str, str]:
    global _MANIFEST_DISPLAY_CACHE
    if _MANIFEST_DISPLAY_CACHE is not None:
        return _MANIFEST_DISPLAY_CACHE

    import csv

    mapping: dict[str, str] = {}
    manifest_path = Path(__file__).resolve().parent.parent / "data" / "corpus_manifest.csv"
    if manifest_path.exists():
        with manifest_path.open("r", encoding="utf-8") as f:
            non_comment_lines = [
                line for line in f if line.strip() and not line.lstrip().startswith("#")
            ]
        reader = csv.DictReader(non_comment_lines)
        for row in reader:
            doc_id = (row.get("source_key") or row.get("doc_id") or "").strip()
            fname = (
                row.get("staged_filename") or row.get("filename") or f"{doc_id}.pdf"
            ).strip()
            if not doc_id:
                continue
            if doc_id in _MANIFEST_OVERRIDES:
                title, authors = _MANIFEST_OVERRIDES[doc_id]
            else:
                title, authors = _clean_manifest_title_and_authors(
                    row.get("title") or "",
                    row.get("authors") or "",
                )
            if title:
                display = f"{title} — {authors} ({fname})" if authors else f"{title} ({fname})"
                mapping[doc_id] = display
                mapping[fname] = display

    _MANIFEST_DISPLAY_CACHE = mapping
    return mapping


def resolve_source_display_name(
    source_key: str,
    filename: str,
    file_path: str | Path | None = None,
) -> str:
    """Resolve a human-readable thesis title and author display string for a corpus source."""
    raw_filename = (filename or "").strip()
    if " — " in raw_filename:
        return raw_filename

    mapping = _load_manifest_display_map()
    stem = Path(raw_filename).stem if raw_filename else ""
    for candidate in (source_key, raw_filename, stem):
        if candidate and candidate in mapping:
            return mapping[candidate]

    if file_path is not None:
        p = Path(file_path)
        if p.exists():
            try:
                meta = extract_document_metadata(p)
                title, authors = _clean_manifest_title_and_authors(
                    meta.get("title", ""),
                    meta.get("authors", ""),
                )
                if title:
                    base = raw_filename or p.name
                    return f"{title} — {authors} ({base})" if authors else f"{title} ({base})"
            except Exception:
                pass

    return raw_filename or source_key

