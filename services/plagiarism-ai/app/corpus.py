"""Corpus Management, Sanitization, Duplicate Audit, and Processing Permissions (CP-02.3).

Responsibilities:
1. Manifest Schema v2 with document_family, normalized_text_sha256, and processing_permission.
2. Multi-tier duplicate audit: exact binary SHA-256, normalized text SHA-256, and fuzzy title/author matching.
3. Distinguish between same-thesis revisions (same document_family), duplicate submissions, and independent works.
4. Guarantee that all versions of the same thesis stay within the same cross-validation split (no data leakage).
5. Self-match exclusion rules for similarity scans.
6. Processing permission enforcement (cloud_allowed vs local_only).
"""

from __future__ import annotations

import csv
import hashlib
import json
import logging
import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any, Literal

from app.documents import (
    extract_document_metadata,
    extract_pages,
    is_duplicate_document,
    normalize,
)

logger = logging.getLogger(__name__)

MANIFEST_V2_FIELDS = (
    "source_key",
    "document_family",
    "corpus_id",
    "version",
    "staged_filename",
    "file_sha256",
    "normalized_text_sha256",
    "bytes",
    "page_count",
    "extracted_characters",
    "title",
    "authors",
    "status",
    "duplicate_of",
    "duplicate_reason",
    "processing_permission",
    "notes",
)


@dataclass
class ManifestRecord:
    source_key: str
    document_family: str
    corpus_id: str
    version: int
    staged_filename: str
    file_sha256: str
    normalized_text_sha256: str
    bytes: int
    page_count: int | str
    extracted_characters: int
    title: str = ""
    authors: str = ""
    status: Literal["ready", "duplicate", "needs_review", "needs_ocr"] = "ready"
    duplicate_of: str | None = None
    duplicate_reason: str | None = None
    processing_permission: Literal["all", "local_only"] = "all"
    notes: str = ""

    def to_dict(self) -> dict[str, Any]:
        d = asdict(self)
        d["page_count"] = str(self.page_count) if self.page_count is not None else ""
        d["duplicate_of"] = self.duplicate_of or ""
        d["duplicate_reason"] = self.duplicate_reason or ""
        d["notes"] = self.notes or ""
        return d

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> ManifestRecord:
        raw_pages = data.get("page_count", "")
        page_count: int | str
        if isinstance(raw_pages, int):
            page_count = raw_pages
        elif str(raw_pages).isdigit():
            page_count = int(raw_pages)
        else:
            page_count = ""

        return cls(
            source_key=str(data["source_key"]),
            document_family=str(data.get("document_family", data["source_key"])),
            corpus_id=str(data.get("corpus_id", "uit-library-v1")),
            version=int(data.get("version", 1)),
            staged_filename=str(data.get("staged_filename", "")),
            file_sha256=str(data.get("file_sha256", data.get("sha256", ""))),
            normalized_text_sha256=str(data.get("normalized_text_sha256", "")),
            bytes=int(data.get("bytes", 0)),
            page_count=page_count,
            extracted_characters=int(data.get("extracted_characters", 0)),
            title=str(data.get("title", "")),
            authors=str(data.get("authors", "")),
            status=data.get("status", "ready"),
            duplicate_of=data.get("duplicate_of") or None,
            duplicate_reason=data.get("duplicate_reason") or None,
            processing_permission=data.get("processing_permission", "all"),
            notes=str(data.get("notes", "")),
        )


def compute_file_sha256(path: Path) -> str:
    """Compute standard SHA-256 hash of raw file bytes."""
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def compute_normalized_text_sha256(path: Path) -> tuple[str, int]:
    """Compute SHA-256 of normalized body text (ignoring boilerplate cover, references, etc.).

    Returns:
        (text_sha256, character_count)
    """
    try:
        pages = extract_pages(path)
        body_parts = []
        for p in pages:
            if p.section_type not in (
                "cover",
                "table_of_contents",
                "abbreviations",
                "references",
                "appendix",
                "header",
                "footer",
                "empty",
            ):
                body_parts.append(p.text)
        if not body_parts:
            # If all were classified as boilerplate, use full text as fallback
            body_parts = [p.text for p in pages]

        joined = normalize(" ".join(body_parts))
        if not joined:
            return "", 0
        digest = hashlib.sha256(joined.encode("utf-8")).hexdigest()
        return digest, len(joined)
    except Exception as exc:
        logger.warning("Failed to compute normalized text sha256 for %s: %s", path, exc)
        return "", 0


def audit_corpus_manifest(
    manifest_records: list[dict[str, Any]],
    staging_dir: Path,
    corpus_id: str = "uit-library-v1",
) -> tuple[list[ManifestRecord], dict[str, Any]]:
    """Audit and enrich corpus records into Manifest Schema v2 with family grouping and duplicate resolution.

    Tiers:
    - Tier 1: Exact Binary File Hash
    - Tier 2: Exact Normalized Body Text Hash
    - Tier 3: Fuzzy Title + Authors Matching
    """
    records_by_key: dict[str, ManifestRecord] = {}
    family_members: dict[str, list[str]] = {}  # family_id -> list of source_keys
    file_hash_map: dict[str, str] = {}  # file_sha256 -> first seen source_key
    text_hash_map: dict[str, str] = {}  # norm_text_sha256 -> first seen source_key
    metadata_cache: dict[str, dict[str, str]] = {}

    audit_summary: dict[str, Any] = {
        "total_records": len(manifest_records),
        "canonical_ready": 0,
        "duplicates_flagged": 0,
        "same_family_revisions": 0,
        "needs_review": 0,
        "document_families_count": 0,
        "duplicate_pairs": [],
    }

    # Pass 1: Parse, compute hashes, extract metadata
    for raw in manifest_records:
        source_key = raw["source_key"]
        file_path = staging_dir / raw["staged_filename"]
        file_sha = raw.get("file_sha256") or raw.get("sha256") or ""
        if not file_sha and file_path.is_file():
            file_sha = compute_file_sha256(file_path)

        text_sha = raw.get("normalized_text_sha256", "")
        extracted_chars = int(raw.get("extracted_characters", 0))

        title = raw.get("title", "")
        authors = raw.get("authors", "")

        if file_path.is_file():
            if not text_sha:
                computed_text_sha, chars = compute_normalized_text_sha256(file_path)
                text_sha = computed_text_sha
                if not extracted_chars:
                    extracted_chars = chars

            if not title or not authors:
                try:
                    meta = extract_document_metadata(file_path)
                    metadata_cache[source_key] = meta
                    if not title:
                        title = meta.get("title", "")
                    if not authors:
                        authors = meta.get("authors", "")
                except Exception:
                    pass

        status = raw.get("status", "ready")
        permission = raw.get("processing_permission", "all")

        record = ManifestRecord(
            source_key=source_key,
            document_family="",  # Will assign in pass 2
            corpus_id=raw.get("corpus_id", corpus_id),
            version=int(raw.get("version", 1)),
            staged_filename=raw["staged_filename"],
            file_sha256=file_sha,
            normalized_text_sha256=text_sha,
            bytes=int(raw.get("bytes", file_path.stat().st_size if file_path.is_file() else 0)),
            page_count=raw.get("page_count", ""),
            extracted_characters=extracted_chars,
            title=title,
            authors=authors,
            status=status,
            duplicate_of=None,
            duplicate_reason=None,
            processing_permission=permission,
            notes=raw.get("notes", raw.get("reason", "")),
        )
        records_by_key[source_key] = record

    # Pass 2: Detect duplicates and cluster into document_families
    # Group items by family cluster (connected components via exact hash or fuzzy metadata)
    keys = list(records_by_key.keys())
    adj_graph: dict[str, set[str]] = {k: set() for k in keys}
    edge_reasons: dict[tuple[str, str], str] = {}

    for i in range(len(keys)):
        k1 = keys[i]
        r1 = records_by_key[k1]
        m1 = metadata_cache.get(k1) or {
            "title": r1.title,
            "authors": r1.authors,
            "norm_title": normalize(r1.title).casefold(),
            "norm_authors": normalize(r1.authors).casefold(),
            "cover_prefix": "",
            "text_sha256": r1.normalized_text_sha256,
        }

        # Check binary file hash
        if r1.file_sha256:
            if r1.file_sha256 in file_hash_map:
                prev = file_hash_map[r1.file_sha256]
                adj_graph[k1].add(prev)
                adj_graph[prev].add(k1)
                edge_reasons[(min(k1, prev), max(k1, prev))] = "EXACT_FILE_HASH"
            else:
                file_hash_map[r1.file_sha256] = k1

        # Check normalized text hash
        if r1.normalized_text_sha256:
            if r1.normalized_text_sha256 in text_hash_map:
                prev = text_hash_map[r1.normalized_text_sha256]
                adj_graph[k1].add(prev)
                adj_graph[prev].add(k1)
                edge_reasons[(min(k1, prev), max(k1, prev))] = "EXACT_TEXT_HASH"
            else:
                text_hash_map[r1.normalized_text_sha256] = k1

        # Check fuzzy metadata matching against remaining
        for j in range(i + 1, len(keys)):
            k2 = keys[j]
            r2 = records_by_key[k2]
            m2 = metadata_cache.get(k2) or {
                "title": r2.title,
                "authors": r2.authors,
                "norm_title": normalize(r2.title).casefold(),
                "norm_authors": normalize(r2.authors).casefold(),
                "cover_prefix": "",
                "text_sha256": r2.normalized_text_sha256,
            }

            is_dup, reason = is_duplicate_document(m1, m2)
            if is_dup:
                adj_graph[k1].add(k2)
                adj_graph[k2].add(k1)
                edge_reasons[(min(k1, k2), max(k1, k2))] = f"FUZZY_METADATA:{reason}"

    # Find connected components (families)
    visited = set()
    for root in keys:
        if root in visited:
            continue
        component = []
        queue = [root]
        visited.add(root)
        while queue:
            curr = queue.pop(0)
            component.append(curr)
            for neighbor in adj_graph[curr]:
                if neighbor not in visited:
                    visited.add(neighbor)
                    queue.append(neighbor)

        # Generate stable family identifier
        # Use sha256 of sorted keys in family
        fam_hash = hashlib.sha256("::".join(sorted(component)).encode("utf-8")).hexdigest()[:12]
        family_id = f"fam-{fam_hash}"
        family_members[family_id] = component

        # Elect canonical representative:
        # Prefer 'ready', prefer higher extracted characters, prefer PDF over DOCX
        def score_candidate(k: str) -> tuple[int, int, int]:
            r = records_by_key[k]
            status_score = 10 if r.status == "ready" else 0
            fmt_score = 2 if r.staged_filename.endswith(".pdf") else 1
            return (status_score, r.extracted_characters, fmt_score)

        sorted_by_canonical = sorted(component, key=score_candidate, reverse=True)
        canonical_key = sorted_by_canonical[0]

        # Assign family and resolve duplicates
        for version_idx, k in enumerate(sorted_by_canonical, start=1):
            rec = records_by_key[k]
            rec.document_family = family_id
            rec.version = version_idx

            if k == canonical_key:
                # Canonical member remains as its base status
                if rec.status == "ready":
                    audit_summary["canonical_ready"] += 1
                elif rec.status == "needs_review":
                    audit_summary["needs_review"] += 1
            else:
                # Secondary member
                pair_key = (min(k, canonical_key), max(k, canonical_key))
                reason_str = edge_reasons.get(pair_key, "SAME_FAMILY_REVISION")
                rec.status = "duplicate"
                rec.duplicate_of = canonical_key
                rec.duplicate_reason = reason_str
                audit_summary["duplicates_flagged"] += 1
                if "SAME_FAMILY_REVISION" in reason_str or "FUZZY_METADATA" in reason_str:
                    audit_summary["same_family_revisions"] += 1
                audit_summary["duplicate_pairs"].append(
                    {
                        "duplicate_key": k,
                        "canonical_key": canonical_key,
                        "family_id": family_id,
                        "reason": reason_str,
                    }
                )

    audit_summary["document_families_count"] = len(family_members)
    return [records_by_key[k] for k in keys], audit_summary


def save_corpus_manifest_v2(records: list[ManifestRecord], destination_csv: Path) -> None:
    """Save records to CSV in Manifest Schema v2 format."""
    destination_csv.parent.mkdir(parents=True, exist_ok=True)
    with destination_csv.open("w", newline="", encoding="utf-8") as dest:
        writer = csv.DictWriter(dest, fieldnames=MANIFEST_V2_FIELDS)
        writer.writeheader()
        for r in records:
            writer.writerow(r.to_dict())


def load_corpus_manifest_v2(csv_path: Path) -> list[ManifestRecord]:
    """Load and parse records from a Manifest Schema v2 CSV file."""
    if not csv_path.is_file():
        return []
    with csv_path.open(newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        return [ManifestRecord.from_dict(row) for row in reader]


def should_exclude_source_for_query(
    query_key: str | None,
    query_family: str | None,
    candidate_key: str,
    candidate_family: str | None,
) -> bool:
    """Self-match exclusion policy:

    Excludes sources that:
    1. Match the exact query source key (self-document comparison).
    2. Belong to the same document_family (different revision/draft of the same thesis).
    """
    if query_key and query_key == candidate_key:
        return True
    if query_family and candidate_family and query_family == candidate_family:
        return True
    return False


def assert_processing_allowed(
    permission: str,
    target: Literal["cloud_storage", "remote_gpu"],
) -> None:
    """Enforce data sovereignty and processing permissions."""
    perm = permission.strip().lower()
    if perm == "local_only" and target in ("cloud_storage", "remote_gpu"):
        raise PermissionError(
            f"Processing operation '{target}' rejected: Document is marked with processing_permission='local_only'."
        )


def split_corpus_by_family(
    records: list[ManifestRecord],
    train_ratio: float = 0.70,
    val_ratio: float = 0.15,
    test_ratio: float = 0.15,
    seed: int = 42,
) -> dict[str, list[ManifestRecord]]:
    """Group-aware dataset split preventing data leakage across document families.

    All revisions, drafts, and duplicates belonging to the same document_family
    are strictly assigned together to the same partition (train, val, or test).
    """
    import random

    # Group records by family
    family_map: dict[str, list[ManifestRecord]] = {}
    for r in records:
        fam = r.document_family or r.source_key
        family_map.setdefault(fam, []).append(r)

    families = sorted(family_map.keys())
    rng = random.Random(seed)
    rng.shuffle(families)

    total_records = len(records)
    target_train = int(total_records * train_ratio)
    target_val = int(total_records * val_ratio)

    train_recs: list[ManifestRecord] = []
    val_recs: list[ManifestRecord] = []
    test_recs: list[ManifestRecord] = []

    for fam in families:
        members = family_map[fam]
        if len(train_recs) + len(members) <= target_train:
            train_recs.extend(members)
        elif len(val_recs) + len(members) <= target_val:
            val_recs.extend(members)
        else:
            test_recs.extend(members)

    return {
        "train": train_recs,
        "val": val_recs,
        "test": test_recs,
    }
