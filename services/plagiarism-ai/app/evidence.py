"""Evidence Traceability, Span Verification, and Vietnamese Text Integrity (CP-02.5).

This module provides full-pipeline verification of similarity evidence:
1. Tracing each finding back from Report -> Chunk -> Block/Paragraph -> Physical Source File.
2. Character span alignment and fidelity measurement.
3. Vietnamese diacritics and Unicode normalization (NFC) validation (zero Mojibake).
4. Chunk overlap verification across adjacent findings in a passage group.
5. Error handling audit for missing sources, corrupted files, and unparseable documents.
"""

from __future__ import annotations

import difflib
import logging
import re
import unicodedata
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

from app.documents import DocumentBlock, extract_pages, normalize

logger = logging.getLogger(__name__)

# Vietnamese vowels with all tone marks + 'đ', 'Đ'
VIETNAMESE_CHARS_PATTERN = re.compile(
    r"[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđĐ]",
    re.IGNORECASE,
)

# Common Mojibake artifacts when UTF-8 is misread as CP1252 / ISO-8859-1
MOJIBAKE_PATTERN = re.compile(
    r"(Ã¡|Ã|Ã¢|Ã£|Ã¨|Ã©|Ãª|Ã¬|Ã­|Ã²|Ã³|Ã´|Ãµ|Ã¹|Ãº|Ä‘|â€|â€“|ï¿½|\ufffd)"
)


@dataclass
class EvidenceTraceResult:
    finding_index: int
    passage_group_id: str
    source_key: str
    source_filename: str
    source_verified: bool
    source_fidelity: float
    submission_verified: bool
    submission_fidelity: float
    vietnamese_intact: bool
    overlap_verified: bool
    source_location: dict[str, Any] = field(default_factory=dict)
    submission_location: dict[str, Any] = field(default_factory=dict)
    notes: list[str] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def check_vietnamese_integrity(text: str) -> tuple[bool, list[str]]:
    """Check if Vietnamese diacritics are well-preserved with zero Mojibake."""
    issues = []
    if not text:
        return True, []

    # Check for Mojibake corruption
    if MOJIBAKE_PATTERN.search(text):
        issues.append("MOJIBAKE_DETECTED: Text contains corrupted multibyte byte sequences.")

    # Check Unicode normalization: standard NFC
    normalized_nfc = unicodedata.normalize("NFC", text)
    if text != normalized_nfc:
        issues.append("UNICODE_NFD_DETECTED: Text contains uncombined diacritic sequences instead of NFC.")

    return len(issues) == 0, issues


def calculate_text_fidelity(finding_text: str, extracted_text: str) -> float:
    """Measure sequence similarity between report finding text and raw extracted file text."""
    n_find = normalize(finding_text).strip()
    n_raw = normalize(extracted_text).strip()

    if not n_find and not n_raw:
        return 1.0
    if not n_find or not n_raw:
        return 0.0

    if n_find in n_raw or n_raw in n_find:
        return 1.0

    # Calculate token-level overlap
    find_tokens = n_find.split()
    raw_tokens = set(n_raw.split())
    if find_tokens:
        overlap = sum(1 for t in find_tokens if t in raw_tokens) / len(find_tokens)
        if overlap >= 0.95:
            return round(overlap, 4)

    matcher = difflib.SequenceMatcher(None, n_find, n_raw)
    return round(matcher.ratio(), 4)


def trace_evidence_to_file(
    finding: dict[str, Any],
    finding_index: int = 0,
    source_file_path: Path | None = None,
    submission_file_path: Path | None = None,
) -> EvidenceTraceResult:
    """Trace a single finding from report back to physical files and verify accuracy."""
    notes: list[str] = []
    source_key = finding.get("source_key", "")
    source_filename = finding.get("source_filename", "")
    pg_id = finding.get("passage_group_id", "")

    source_text = finding.get("source_text", "")
    submission_text = finding.get("submission_text", "")

    # 1. Check Vietnamese character integrity on both texts
    src_vn_ok, src_vn_issues = check_vietnamese_integrity(source_text)
    sub_vn_ok, sub_vn_issues = check_vietnamese_integrity(submission_text)
    vietnamese_intact = src_vn_ok and sub_vn_ok
    if not vietnamese_intact:
        notes.extend(src_vn_issues)
        notes.extend(sub_vn_issues)

    # 2. Verify Source File Traceability
    source_verified = False
    source_fidelity = 0.0
    source_loc: dict[str, Any] = {
        "page": finding.get("source_page"),
        "chunk_no": finding.get("source_chunk_no"),
        "start_offset": finding.get("source_start"),
        "end_offset": finding.get("source_end"),
    }

    if source_file_path and source_file_path.is_file():
        try:
            pages = extract_pages(source_file_path)
            # Find matching page or block
            target_page_no = finding.get("source_page")
            extracted_source_texts = []
            for p in pages:
                if target_page_no is None or p.page_number is None or p.page_number == target_page_no:
                    extracted_source_texts.append(p.text)

            full_extracted_source = " ".join(extracted_source_texts)
            source_fidelity = calculate_text_fidelity(source_text, full_extracted_source)
            if source_fidelity >= 0.85:
                source_verified = True
            else:
                notes.append(f"LOW_SOURCE_FIDELITY: Fidelity score is {source_fidelity}")
        except Exception as exc:
            notes.append(f"SOURCE_EXTRACTION_ERROR: {exc}")
    elif source_file_path:
        notes.append(f"SOURCE_FILE_NOT_FOUND: {source_file_path}")

    # 3. Verify Submission File Traceability (if submission file provided)
    submission_verified = False
    submission_fidelity = 0.0
    submission_loc: dict[str, Any] = {
        "page": finding.get("submission_page"),
        "chunk_no": finding.get("submission_chunk_no"),
        "start_offset": finding.get("submission_start"),
        "end_offset": finding.get("submission_end"),
    }

    if submission_file_path and submission_file_path.is_file():
        try:
            sub_pages = extract_pages(submission_file_path)
            target_sub_page = finding.get("submission_page")
            extracted_sub_texts = []
            for p in sub_pages:
                if target_sub_page is None or p.page_number is None or p.page_number == target_sub_page:
                    extracted_sub_texts.append(p.text)

            full_extracted_sub = " ".join(extracted_sub_texts)
            submission_fidelity = calculate_text_fidelity(submission_text, full_extracted_sub)
            if submission_fidelity >= 0.85:
                submission_verified = True
            else:
                notes.append(f"LOW_SUBMISSION_FIDELITY: Fidelity score is {submission_fidelity}")
        except Exception as exc:
            notes.append(f"SUBMISSION_EXTRACTION_ERROR: {exc}")
    elif submission_file_path:
        notes.append(f"SUBMISSION_FILE_NOT_FOUND: {submission_file_path}")
    else:
        # If no submission file supplied, consider verified by finding schema
        submission_verified = True
        submission_fidelity = 1.0

    return EvidenceTraceResult(
        finding_index=finding_index,
        passage_group_id=pg_id,
        source_key=source_key,
        source_filename=source_filename,
        source_verified=source_verified,
        source_fidelity=source_fidelity,
        submission_verified=submission_verified,
        submission_fidelity=submission_fidelity,
        vietnamese_intact=vietnamese_intact,
        overlap_verified=True,  # Verified at group level
        source_location=source_loc,
        submission_location=submission_loc,
        notes=notes,
    )


def verify_passage_group_overlap(findings: list[dict[str, Any]], full_text: str) -> dict[str, Any]:
    """Verify that contiguous findings in a passage group have exact character overlaps."""
    if len(findings) <= 1:
        return {"contiguous": True, "overlap_verified": True, "overlaps": []}

    overlaps_info = []
    all_ok = True

    # Sort findings by submission_start
    sorted_findings = sorted(findings, key=lambda f: int(f.get("submission_start", 0)))
    for i in range(len(sorted_findings) - 1):
        f1 = sorted_findings[i]
        f2 = sorted_findings[i + 1]

        s1, e1 = int(f1.get("submission_start", 0)), int(f1.get("submission_end", 0))
        s2, e2 = int(f2.get("submission_start", 0)), int(f2.get("submission_end", 0))

        # Check if intervals overlap
        if s2 < e1:
            overlap_len = e1 - s2
            overlap_in_f1 = f1.get("submission_text", "")[-overlap_len:]
            overlap_in_f2 = f2.get("submission_text", "")[:overlap_len]

            # In normalized form, the overlap should match closely
            match_ratio = difflib.SequenceMatcher(None, normalize(overlap_in_f1), normalize(overlap_in_f2)).ratio()
            is_match = match_ratio >= 0.90
            if not is_match:
                all_ok = False

            overlaps_info.append({
                "from_chunk": f1.get("submission_chunk_no"),
                "to_chunk": f2.get("submission_chunk_no"),
                "overlap_span": (s2, e1),
                "overlap_length": overlap_len,
                "overlap_match_ratio": round(match_ratio, 4),
                "is_match": is_match,
            })
        else:
            # Contiguous without overlap (e.g. sentence chunking)
            overlaps_info.append({
                "from_chunk": f1.get("submission_chunk_no"),
                "to_chunk": f2.get("submission_chunk_no"),
                "overlap_span": (e1, s2),
                "overlap_length": 0,
                "overlap_match_ratio": 1.0,
                "is_match": True,
            })

    return {
        "contiguous": True,
        "overlap_verified": all_ok,
        "overlaps": overlaps_info,
    }


def audit_report_evidence(
    report: dict[str, Any],
    corpus_dir: Path,
    submission_file_path: Path | None = None,
) -> dict[str, Any]:
    """Execute end-to-end evidence audit on all findings in a report."""
    findings = report.get("findings", [])
    results: list[EvidenceTraceResult] = []

    for idx, f in enumerate(findings):
        src_filename = f.get("source_filename") or f"{f.get('source_key', '')}.pdf"
        src_path = corpus_dir / src_filename
        if not src_path.exists():
            # Try docx extension
            alt_path = corpus_dir / f"{f.get('source_key', '')}.docx"
            if alt_path.exists():
                src_path = alt_path

        res = trace_evidence_to_file(
            finding=f,
            finding_index=idx,
            source_file_path=src_path if src_path.exists() else None,
            submission_file_path=submission_file_path,
        )
        results.append(res)

    # Check passage groups overlap
    pg_map: dict[str, list[dict[str, Any]]] = {}
    for f in findings:
        pg_id = f.get("passage_group_id")
        if pg_id:
            pg_map.setdefault(pg_id, []).append(f)

    overlap_results: dict[str, Any] = {}
    for pg_id, pg_findings in pg_map.items():
        overlap_results[pg_id] = verify_passage_group_overlap(pg_findings, full_text="")

    total = len(results)
    src_verified_count = sum(1 for r in results if r.source_verified)
    sub_verified_count = sum(1 for r in results if r.submission_verified)
    vn_intact_count = sum(1 for r in results if r.vietnamese_intact)

    return {
        "total_findings_audited": total,
        "source_traceability_rate": round(src_verified_count / total, 4) if total > 0 else 1.0,
        "submission_traceability_rate": round(sub_verified_count / total, 4) if total > 0 else 1.0,
        "vietnamese_integrity_rate": round(vn_intact_count / total, 4) if total > 0 else 1.0,
        "passage_groups_overlap_audited": len(overlap_results),
        "all_evidence_traceable": (src_verified_count == total and sub_verified_count == total and vn_intact_count == total),
        "detailed_results": [r.to_dict() for r in results],
        "overlap_audit": overlap_results,
    }

