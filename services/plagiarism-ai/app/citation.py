"""Citation-Aware Context Detection and Attribution Verification (CP-04.3).

Responsibilities:
1. Detecting academic citation markers across standard styles:
   - Numeric bracket citations (IEEE, ACM, TCVN): [1], [1, 2], [3-5]
   - Author-Year parenthetical citations (APA, Harvard): (Vaswani et al., 2017)
   - Narrative citations: "Theo Vaswani và cộng sự (2017)...", "Nghiên cứu của [1]..."
   - Direct quotations: "..." or “...” or «...»
2. Negative filtering:
   - Excludes table/figure references: [Bảng 1], [Hình 2], [Figure 3]
   - Excludes programming array/variable indices: arr[i], x[0]
3. Distinguishing citation status:
   - "present": Definite citation marker detected within the passage
   - "absent": Text reused without any citation or attribution markers
   - "uncertain": Ambiguous context (generic reference, vague lead-in, unanchored brackets)
4. Source Attribution Verification:
   - Verifies whether the detected citation marker matches the candidate source
   - Flags potential secondary source plagiarism or misattribution (e.g. citing paper A while copying from thesis B)
   - Never auto-assumes validity simply because a [n] marker exists
5. Orthogonality to text reuse:
   - Citation context does NOT delete or mask text reuse findings
   - Evidence is preserved for evaluators with full citation context metadata
"""

from __future__ import annotations

import re
from dataclasses import asdict, dataclass, field
from typing import Any

# --- Regular Expressions for Citation Detection ---

# Negative lookahead/lookbehind for table/figure labels before [
RX_NON_CITATION_PREFIX = re.compile(
    r"(?:\bbảng|\bhình|\bsơ\s+đồ|\bbiểu\s+đồ|\btable|\bfigure|\bfig|\btab|\bmục|\bchương|\bbài|\bequation|\beq)\.?\s*$",
    re.IGNORECASE,
)

# Numeric citations: [1], [1, 2], [1-3], [4–7]
# Avoids matching arr[0] by requiring non-identifier before [
RX_NUMERIC_CITATION = re.compile(
    r"(?<![a-zA-Z0-9_])\[\s*(\d{1,4}(?:[-–—]\d{1,4})?(?:\s*,\s*\d{1,4}(?:[-–—]\d{1,4})?)*)\s*\]"
)

# Author-Year parenthetical citations: (Vaswani et al., 2017), (Nguyễn và cộng sự, 2021)
RX_AUTHOR_YEAR = re.compile(
    r"\(\s*([A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]*(?:\s+[A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]*)*(?:\s+(?:et\s+al\.?|và\s+cs\.?|và\s+cộng\s+sự|và\s+[A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]+))?)\s*,\s*((?:19|20)\d{2}(?:[a-z])?(?:\s*,\s*(?:tr\.|p\.|pp\.)\s*\d+)?)\s*\)",
    re.UNICODE,
)

# Narrative author citations: "Theo Vaswani et al. (2017)", "Nghiên cứu của Lê Văn B (2020)"
RX_NARRATIVE_CITATION = re.compile(
    r"\b((?:theo|theo\s+nghiên\s+cứu\s+của|nghiên\s+cứu\s+của|tác\s+giả|nhóm\s+tác\s+giả)\s+([A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]*(?:\s+[A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]*)*(?:\s+(?:et\s+al\.?|và\s+cs\.?|và\s+cộng\s+sự))?))\s*\(\s*((?:19|20)\d{2}(?:[a-z])?)\s*\)",
    re.IGNORECASE | re.UNICODE,
)

# Narrative numeric citations: "Theo [1]", "Trong [2, 3]", "Nghiên cứu [4]"
RX_NARRATIVE_NUMERIC = re.compile(
    r"\b(?:theo|trong|nghiên\s+cứu|tác\s+giả|công\s+bố)\s+(\[\s*\d{1,4}(?:[-–—]\d{1,4})?(?:\s*,\s*\d{1,4}(?:[-–—]\d{1,4})?)*\s*\])",
    re.IGNORECASE,
)

# Direct quotation marks around text: "..." or “...” or «...»
RX_QUOTATION = re.compile(r'("[^"\n]{4,}"|“[^”\n]{4,}”|«[^»\n]{4,}»)')

# Ambiguous / Uncertain lead-ins without formal citation:
# e.g., "theo một số nghiên cứu gần đây", "các tài liệu tham khảo cho thấy"
RX_UNCERTAIN_LEADIN = re.compile(
    r"\b(?:theo\s+(?:các|một\s+số)?\s*(?:tài\s+liệu\s+tham\s+khảo|nghiên\s+cứu)(?:\s+gần\s+đây)?|theo\s+(?:tác\s+giả|chuyên\s+gia|giới\s+chuyên\s+môn)\b|dựa\s+trên\s+(?:các\s+)?(?:nghiên\s+cứu|tài\s+liệu|khảo\s+sát)\b|các\s+tác\s+giả\s+khác\s+cho\s+rằng|theo\s+lý\s+thuyết\s+chung)\b",
    re.IGNORECASE,
)

# Ambiguous parenthetical note: (Nghiên cứu ứng dụng tại phân hệ val đợt 2)
RX_PARENTHETICAL_NOTE = re.compile(
    r"\((?:nghiên\s+cứu|ứng\s+dụng|tham\s+khảo|xem\s+thêm|theo\s+nguồn)[^)\n]{5,}\)",
    re.IGNORECASE,
)


@dataclass
class CitationMarker:
    """A detected citation token or marker within text."""

    marker_text: str
    marker_type: str  # "numeric", "author_year", "narrative", "generic"
    start_char: int
    end_char: int
    author: str | None = None
    year: str | None = None
    indices: list[int] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass
class CitationContext:
    """Full citation context metadata associated with a finding or passage."""

    status: str  # "present", "absent", "uncertain"
    markers: list[CitationMarker] = field(default_factory=list)
    has_quotation_marks: bool = False
    citation_style: str = "none"  # "numeric", "author_year", "narrative", "mixed", "generic", "none"
    source_attributed: bool | None = None  # True (matches candidate), False (misattributed), None (unverified)
    attribution_details: str = ""
    is_verbatim_quote: bool = False

    def to_dict(self) -> dict[str, Any]:
        return {
            "status": self.status,
            "markers": [m.to_dict() for m in self.markers],
            "has_quotation_marks": self.has_quotation_marks,
            "citation_style": self.citation_style,
            "source_attributed": self.source_attributed,
            "attribution_details": self.attribution_details,
            "is_verbatim_quote": self.is_verbatim_quote,
        }


def _parse_numeric_ranges(raw_str: str) -> list[int]:
    """Parse comma-separated numbers and hyphenated ranges: '1, 3-5' -> [1, 3, 4, 5]."""
    indices: set[int] = set()
    parts = raw_str.split(",")
    for p in parts:
        p = p.strip()
        if not p:
            continue
        range_match = re.match(r"^(\d{1,4})\s*[-–—]\s*(\d{1,4})$", p)
        if range_match:
            start_num = int(range_match.group(1))
            end_num = int(range_match.group(2))
            if start_num <= end_num and end_num - start_num <= 50:
                indices.update(range(start_num, end_num + 1))
            else:
                indices.add(start_num)
                indices.add(end_num)
        elif p.isdigit():
            indices.add(int(p))
    return sorted(indices)


def extract_citation_markers(text: str) -> list[CitationMarker]:
    """Scan text for all academic citation markers with boundary offsets."""
    if not text or not text.strip():
        return []

    markers: list[CitationMarker] = []
    seen_spans: set[tuple[int, int]] = set()

    # 1. Scan Numeric Citations: [1], [1, 2], [3-5]
    for m in RX_NUMERIC_CITATION.finditer(text):
        start, end = m.span()
        prefix = text[:start]
        # Check if preceded by "Bảng", "Hình", "Table", etc.
        if RX_NON_CITATION_PREFIX.search(prefix):
            continue

        raw_inner = m.group(1)
        indices = _parse_numeric_ranges(raw_inner)
        if indices:
            markers.append(
                CitationMarker(
                    marker_text=m.group(0),
                    marker_type="numeric",
                    start_char=start,
                    end_char=end,
                    indices=indices,
                )
            )
            seen_spans.add((start, end))

    # 2. Scan Narrative Citations: "Theo Vaswani et al. (2017)"
    for m in RX_NARRATIVE_CITATION.finditer(text):
        start, end = m.span()
        author = m.group(2).strip()
        year = m.group(3).strip()
        markers.append(
            CitationMarker(
                marker_text=m.group(0),
                marker_type="narrative",
                start_char=start,
                end_char=end,
                author=author,
                year=year,
            )
        )
        seen_spans.add((start, end))

    # 3. Scan Author-Year Citations: (Vaswani et al., 2017)
    for m in RX_AUTHOR_YEAR.finditer(text):
        start, end = m.span()
        # Avoid double-counting if span overlaps with narrative citation
        if any(s <= start and end <= e for s, e in seen_spans):
            continue
        author = m.group(1).strip()
        year = m.group(2).strip()
        markers.append(
            CitationMarker(
                marker_text=m.group(0),
                marker_type="author_year",
                start_char=start,
                end_char=end,
                author=author,
                year=year,
            )
        )
        seen_spans.add((start, end))

    # 4. Scan Ambiguous / Generic Parenthetical Notes
    for m in RX_PARENTHETICAL_NOTE.finditer(text):
        start, end = m.span()
        if any(s <= start and end <= e for s, e in seen_spans):
            continue
        markers.append(
            CitationMarker(
                marker_text=m.group(0),
                marker_type="generic",
                start_char=start,
                end_char=end,
            )
        )
        seen_spans.add((start, end))

    # Sort by character position in text
    markers.sort(key=lambda x: (x.start_char, x.end_char))
    return markers


def evaluate_source_attribution(
    markers: list[CitationMarker],
    source_metadata: dict[str, Any] | None,
) -> tuple[bool | None, str]:
    """Verify whether detected citation markers genuinely attribute the retrieved source document.

    Returns:
        (source_attributed, attribution_details)
        - True: Citation marker explicitly matches candidate source author/key.
        - False: Potential misattribution or secondary citation (cited author does not match candidate).
        - None: Unverified numeric citation; requires bibliography lookup.
    """
    if not source_metadata:
        if markers:
            return None, "Citation marker detected; source metadata unprovided for cross-referencing."
        return False, "No citation marker found for retrieved source."

    src_filename = str(source_metadata.get("source_filename", "")).lower()
    src_key = str(source_metadata.get("source_key", "")).lower()
    src_family = str(source_metadata.get("document_family", "")).lower()
    combined_source_info = f"{src_filename} {src_key} {src_family}"

    author_year_markers = [m for m in markers if m.author]
    if author_year_markers:
        for m in author_year_markers:
            if not m.author:
                continue
            # Extract primary author surname / significant token
            tokens = [t.lower() for t in re.findall(r"\w+", m.author) if t.lower() not in ("và", "et", "al", "cs", "cộng", "sự")]
            if not tokens:
                continue
            lead_token = tokens[0]
            if lead_token in combined_source_info:
                return (
                    True,
                    f"Citation author '{m.author}' matches candidate source document metadata.",
                )

        # Author mentioned does not match candidate source
        cited_authors = ", ".join(repr(m.author) for m in author_year_markers if m.author)
        display_source = source_metadata.get("source_filename") or source_metadata.get("source_key", "source document")
        return (
            False,
            f"Potential misattribution / secondary plagiarism: Query cites {cited_authors} but text was retrieved from '{display_source}' without direct attribution.",
        )

    # Only numeric markers present (e.g. [1])
    numeric_markers = [m for m in markers if m.marker_type == "numeric"]
    if numeric_markers:
        indices_str = ", ".join(f"[{i}]" for m in numeric_markers for i in m.indices)
        return (
            None,
            f"Numeric marker {indices_str} detected; requires bibliography reference lookup to verify candidate attribution.",
        )

    # Generic or ambiguous markers
    generic_markers = [m for m in markers if m.marker_type == "generic"]
    if generic_markers:
        return (
            None,
            "Ambiguous narrative note detected; lacks formal author-year or reference index.",
        )

    return False, "No citation markers found for retrieved source candidate."


def detect_citation_context(
    query_text: str,
    source_metadata: dict[str, Any] | None = None,
) -> CitationContext:
    """Analyze query text for citation markers, quotation style, and source attribution.

    Returns:
        CitationContext with status ('present', 'absent', 'uncertain'), markers,
        quotation indicators, and attribution evaluation.
    """
    if not query_text or not query_text.strip():
        return CitationContext(
            status="absent",
            markers=[],
            has_quotation_marks=False,
            citation_style="none",
            source_attributed=False,
            attribution_details="Empty or whitespace query text.",
            is_verbatim_quote=False,
        )

    markers = extract_citation_markers(query_text)
    has_quotes = bool(RX_QUOTATION.search(query_text))

    # Check for ambiguous lead-ins without standard markers
    has_uncertain_leadin = bool(RX_UNCERTAIN_LEADIN.search(query_text))

    has_standard_markers = any(
        m.marker_type in ("numeric", "author_year", "narrative") for m in markers
    )
    has_generic_markers = any(m.marker_type == "generic" for m in markers)

    # Determine Status
    if has_standard_markers:
        status = "present"
    elif has_generic_markers or has_uncertain_leadin:
        status = "uncertain"
    else:
        status = "absent"

    # Determine Style
    types = {m.marker_type for m in markers}
    if not types:
        style = "none"
    elif len(types) > 1:
        style = "mixed"
    elif "numeric" in types:
        style = "numeric"
    elif "author_year" in types:
        style = "author_year"
    elif "narrative" in types:
        style = "narrative"
    else:
        style = "generic"

    # Evaluate attribution against candidate source
    source_attributed, attribution_details = evaluate_source_attribution(
        markers, source_metadata
    )

    is_verbatim_quote = has_quotes and status == "present"

    return CitationContext(
        status=status,
        markers=markers,
        has_quotation_marks=has_quotes,
        citation_style=style,
        source_attributed=source_attributed,
        attribution_details=attribution_details,
        is_verbatim_quote=is_verbatim_quote,
    )
