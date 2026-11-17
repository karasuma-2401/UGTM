"""Report Schema v3 and Provenance Management (CP-02.4).

Key Responsibilities:
1. Full persistence of findings before UI display limits (no early [:200] truncation).
2. Interval union calculation for similarity coverage (true character span coverage, ignoring overlap).
3. Rich provenance tracking: parser version, preprocessing profile/hash, embedding/reranker revisions, classifier version.
4. Strict semantics: Reranker score is a semantic similarity metric, NOT a 'plagiarism percentage'.
5. Passage group extraction and findings pagination.
6. Backward-compatibility adapter for Schema v2 clients.
"""

from __future__ import annotations

import datetime
import hashlib
import json
from pathlib import Path
from typing import Any, Literal
from pydantic import BaseModel, Field

from app.alignment import group_adjacent_findings
from app.config import settings
from app.preprocessing import DEFAULT_CONFIG_PATH, get_preprocessing_config

PARSER_VERSION = "parser-v2.1-block"
DEFAULT_DISCLAIMER = (
    "Similarity evidence only; teachers and evaluators must verify sources, "
    "citations and context. No automatic plagiarism verdict."
)


def get_preprocessing_config_hash(profile_name: str = "production_v1") -> str:
    """Compute 12-char SHA-256 fingerprint of the preprocessing config file."""
    if DEFAULT_CONFIG_PATH.exists():
        raw_bytes = DEFAULT_CONFIG_PATH.read_bytes()
        return hashlib.sha256(raw_bytes).hexdigest()[:12]
    return "default-hash"


def compute_similarity_coverage(
    findings: list[dict[str, Any]],
    total_characters: int,
) -> tuple[int, float]:
    """Compute non-overlapping union of matched submission text character spans.

    Returns:
        (matched_characters, coverage_ratio) where coverage_ratio is in [0.0, 1.0].
    """
    if not findings or total_characters <= 0:
        return 0, 0.0

    intervals: list[tuple[int, int]] = []
    for f in findings:
        s = f.get("submission_start")
        e = f.get("submission_end")
        if s is not None and e is not None and e > s:
            intervals.append((int(s), int(e)))

    if not intervals:
        return 0, 0.0

    # Sort intervals by start offset
    intervals.sort(key=lambda x: (x[0], x[1]))

    # Merge overlapping or contiguous intervals
    merged: list[tuple[int, int]] = []
    curr_start, curr_end = intervals[0]

    for next_start, next_end in intervals[1:]:
        if next_start <= curr_end:
            curr_end = max(curr_end, next_end)
        else:
            merged.append((curr_start, curr_end))
            curr_start, curr_end = next_start, next_end
    merged.append((curr_start, curr_end))

    matched_chars = sum(e - s for s, e in merged)
    coverage = round(min(matched_chars / total_characters, 1.0), 4)
    return matched_chars, coverage


def extract_passage_groups(findings: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Aggregate findings into summary passage groups."""
    groups_map: dict[str, dict[str, Any]] = {}
    for f in findings:
        pg_id = f.get("passage_group_id")
        if not pg_id:
            continue
        if pg_id not in groups_map:
            groups_map[pg_id] = {
                "passage_group_id": pg_id,
                "source_key": f.get("source_key", ""),
                "source_version": f.get("source_version", 1),
                "source_filename": f.get("source_filename", ""),
                "findings_count": 0,
                "submission_start": f.get("submission_start", 0),
                "submission_end": f.get("submission_end", 0),
                "source_start": f.get("source_start", 0),
                "source_end": f.get("source_end", 0),
                "max_reranker_score": float(f.get("reranker_score", 0.0)),
            }
        g = groups_map[pg_id]
        g["findings_count"] += 1
        g["submission_start"] = min(g["submission_start"], f.get("submission_start", g["submission_start"]))
        g["submission_end"] = max(g["submission_end"], f.get("submission_end", g["submission_end"]))
        g["source_start"] = min(g["source_start"], f.get("source_start", g["source_start"]))
        g["source_end"] = max(g["source_end"], f.get("source_end", g["source_end"]))
        g["max_reranker_score"] = max(g["max_reranker_score"], float(f.get("reranker_score", 0.0)))

    # Return sorted by passage_group_id
    return [groups_map[k] for k in sorted(groups_map.keys())]


# ============================================================
# Pydantic Models for Schema Validation
# ============================================================

class ProvenanceModel(BaseModel):
    parser_version: str = PARSER_VERSION
    preprocessing_profile: str = "production_v1"
    preprocessing_config_hash: str
    embedding_model: str
    embedding_revision: str = "main"
    reranker_model: str
    reranker_revision: str = "main"
    classifier_version: str | None = None
    detector_mode: Literal["heuristic", "custom_classifier", "near_exact_override"] = "heuristic"
    corpus_snapshot_id: str = "corpus-v2-73fams"


class ReportSummaryModel(BaseModel):
    scanned_chunks: int = Field(ge=0)
    scanned_characters: int = Field(ge=0)
    matched_characters: int = Field(ge=0)
    similarity_coverage: float = Field(ge=0.0, le=1.0)
    max_reranker_score: float = Field(ge=0.0, le=1.0)
    flagged_pairs: int = Field(ge=0)
    findings_returned: int = Field(ge=0)
    total_findings: int = Field(ge=0)
    matched_sources: int = Field(ge=0)
    passage_groups: int = Field(ge=0)
    cited_findings: int = 0
    uncited_findings: int = 0
    uncertain_findings: int = 0


class FindingItemModel(BaseModel):
    passage_group_id: str
    submission_chunk_no: int
    submission_page: int
    submission_start: int
    submission_end: int
    submission_text: str
    chunk_id: int
    source_key: str
    source_version: int
    source_filename: str
    document_family: str | None = None
    source_chunk_no: int
    source_page: int
    source_start: int
    source_end: int
    source_text: str
    rrf_score: float
    dense_score: float
    lexical_score: float
    reranker_score: float
    classifier_score: float | None = None
    decision_source: str = "heuristic"
    citation_status: str = "unexamined"
    citation_context: dict[str, Any] | None = None
    feature_values: dict[str, float] | None = None
    review_required: bool = True
    aligned_spans: list[dict[str, Any]] | None = None


class PassageGroupModel(BaseModel):
    passage_group_id: str
    source_key: str
    source_version: int
    source_filename: str
    findings_count: int
    submission_start: int
    submission_end: int
    source_start: int
    source_end: int
    max_reranker_score: float


class PaginationModel(BaseModel):
    page: int = Field(ge=1)
    page_size: int = Field(ge=1)
    total_pages: int = Field(ge=0)
    total_findings: int = Field(ge=0)
    has_next: bool = False
    has_prev: bool = False


class ReportV3Model(BaseModel):
    schema_version: Literal[3] = 3
    job_id: str
    corpus_id: str
    submission_id: str
    submission_version: int
    created_at: str
    provenance: ProvenanceModel
    summary: ReportSummaryModel
    passage_groups: list[PassageGroupModel]
    findings: list[FindingItemModel]
    pagination: PaginationModel
    disclaimer: str = DEFAULT_DISCLAIMER


# ============================================================
# Report Builders and Converters
# ============================================================

def build_report_v3(
    job_id: str,
    job: dict[str, Any],
    query_chunks: list[Any],
    all_findings: list[dict[str, Any]],
    preprocessing_profile: str = "production_v1",
    corpus_snapshot_id: str = "corpus-v2-73fams",
    classifier_version: str | None = None,
    detector_mode: Literal["heuristic", "custom_classifier", "near_exact_override"] = "heuristic",
    execution_telemetry: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Construct a full canonical Report Schema v3 without pre-truncating findings."""
    # 1. Group all findings into passage groups
    all_findings.sort(key=lambda f: float(f.get("reranker_score", 0.0)), reverse=True)
    grouped_findings = group_adjacent_findings(list(all_findings))

    # 2. Extract passage groups summary
    passage_groups = extract_passage_groups(grouped_findings)

    # 3. Compute scanned characters from query chunks
    scanned_characters = sum(len(c.text) for c in query_chunks) if query_chunks else 0

    # 4. Compute true non-overlapping similarity coverage
    matched_characters, similarity_coverage = compute_similarity_coverage(
        grouped_findings,
        scanned_characters,
    )

    max_reranker = (
        max(float(f.get("reranker_score", 0.0)) for f in grouped_findings)
        if grouped_findings
        else 0.0
    )

    matched_source_keys = {f["source_key"] for f in grouped_findings if "source_key" in f}

    provenance = {
        "parser_version": PARSER_VERSION,
        "preprocessing_profile": preprocessing_profile,
        "preprocessing_config_hash": get_preprocessing_config_hash(preprocessing_profile),
        "embedding_model": settings.embedding_model,
        "embedding_revision": "main",
        "reranker_model": settings.reranker_model,
        "reranker_revision": "main",
        "classifier_version": classifier_version,
        "detector_mode": detector_mode,
        "corpus_snapshot_id": corpus_snapshot_id,
    }

    if execution_telemetry:
        provenance["execution_telemetry"] = execution_telemetry

    cited_count = sum(1 for f in grouped_findings if f.get("citation_status") in ("present", "cited"))
    uncited_count = sum(1 for f in grouped_findings if f.get("citation_status") in ("absent", "uncited"))
    uncertain_count = sum(1 for f in grouped_findings if f.get("citation_status") in ("uncertain", "unexamined"))

    summary = {
        "scanned_chunks": len(query_chunks),
        "scanned_characters": scanned_characters,
        "matched_characters": matched_characters,
        "similarity_coverage": similarity_coverage,
        "max_reranker_score": round(max_reranker, 4),
        "flagged_pairs": len(grouped_findings),
        "findings_returned": len(grouped_findings),
        "total_findings": len(grouped_findings),
        "matched_sources": len(matched_source_keys),
        "passage_groups": len(passage_groups),
        "cited_findings": cited_count,
        "uncited_findings": uncited_count,
        "uncertain_findings": uncertain_count,
    }

    total_findings = len(grouped_findings)
    pagination = {
        "page": 1,
        "page_size": total_findings if total_findings > 0 else 1,
        "total_pages": 1,
        "total_findings": total_findings,
        "has_next": False,
        "has_prev": False,
    }

    now_iso = datetime.datetime.now(datetime.timezone.utc).isoformat()

    return {
        "schema_version": 3,
        "job_id": job_id,
        "corpus_id": job.get("corpus_id", "uit-library-v1"),
        "submission_id": job.get("document_key", ""),
        "submission_version": int(job.get("version", 1)),
        "created_at": now_iso,
        "provenance": provenance,
        "summary": summary,
        "passage_groups": passage_groups,
        "findings": grouped_findings,
        "pagination": pagination,
        "disclaimer": DEFAULT_DISCLAIMER,
    }


def paginate_report_findings(
    report_dict: dict[str, Any],
    page: int = 1,
    page_size: int = 100,
) -> dict[str, Any]:
    """Slice findings for API response according to requested pagination parameters."""
    if report_dict.get("schema_version") not in (3, "3", "3.0"):
        return report_dict

    full_findings = report_dict.get("findings", [])
    total_findings = len(full_findings)

    page = max(1, page)
    page_size = max(1, min(500, page_size))
    total_pages = (total_findings + page_size - 1) // page_size if total_findings > 0 else 1

    start_idx = (page - 1) * page_size
    end_idx = min(start_idx + page_size, total_findings)
    sliced_findings = full_findings[start_idx:end_idx]

    # Create a shallow copy with sliced findings and updated pagination
    paginated = dict(report_dict)
    paginated["findings"] = sliced_findings

    summary = dict(paginated.get("summary", {}))
    summary["findings_returned"] = len(sliced_findings)
    paginated["summary"] = summary

    paginated["pagination"] = {
        "page": page,
        "page_size": page_size,
        "total_pages": total_pages,
        "total_findings": total_findings,
        "has_next": page < total_pages,
        "has_prev": page > 1,
    }
    return paginated


def convert_report_to_schema_v2(report_v3: dict[str, Any]) -> dict[str, Any]:
    """Backward-compatibility adapter: Converts a Schema v3 report into Schema v2."""
    if report_v3.get("schema_version") == 2:
        return report_v3

    prov = report_v3.get("provenance", {})
    summary_v3 = report_v3.get("summary", {})

    return {
        "schema_version": 2,
        "job_id": report_v3.get("job_id", ""),
        "corpus_id": report_v3.get("corpus_id", ""),
        "submission_id": report_v3.get("submission_id", ""),
        "submission_version": report_v3.get("submission_version", 1),
        "model": {
            "embedding": prov.get("embedding_model", settings.embedding_model),
            "reranker": prov.get("reranker_model", settings.reranker_model),
            "classifier_version": prov.get("classifier_version"),
            "detector_mode": prov.get("detector_mode", "heuristic"),
        },
        "summary": {
            "scanned_chunks": summary_v3.get("scanned_chunks", 0),
            "flagged_pairs": summary_v3.get("flagged_pairs", 0),
            "findings_returned": summary_v3.get("findings_returned", 0),
            "matched_sources": summary_v3.get("matched_sources", 0),
            "passage_groups": summary_v3.get("passage_groups", 0),
        },
        "findings": report_v3.get("findings", []),
        "disclaimer": report_v3.get("disclaimer", DEFAULT_DISCLAIMER),
    }

