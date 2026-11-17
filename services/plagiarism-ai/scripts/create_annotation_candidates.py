"""Create local, unlabeled passage-pair tasks for human annotation."""

from __future__ import annotations

import argparse
import csv
import json
from dataclasses import dataclass
from pathlib import Path

from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.neighbors import NearestNeighbors

from app.documents import (
    classify_text_section,
    extract_document_metadata,
    extract_pages,
    is_duplicate_document,
    make_chunks,
)
from app.preprocessing import (
    DEFAULT_EXCLUDED_SECTIONS,
    get_preprocessing_config,
    prepare_document_chunks,
)

ANNOTATION_FIELDS = (
    "task_id",
    "group_id",
    "query_source_key",
    "query_page",
    "query_chunk_no",
    "query_text",
    "source_key",
    "source_page",
    "source_chunk_no",
    "source_text",
    "lexical_cosine",
    "candidate_type",
    "reviewer_1_label",
    "reviewer_2_label",
    "final_label",
    "status",
    "note",
)


@dataclass(frozen=True)
class SourceChunk:
    source_key: str
    page_number: int | None
    chunk_no: int
    text: str
    section_type: str = "body"


def evenly_spaced(items: list[SourceChunk], limit: int) -> list[SourceChunk]:
    if limit < 1:
        raise ValueError("max_chunks_per_source must be positive")
    if len(items) <= limit:
        return items
    if limit == 1:
        return [items[len(items) // 2]]
    indices = {
        round(index * (len(items) - 1) / (limit - 1))
        for index in range(limit)
    }
    return [items[index] for index in sorted(indices)]


def build_duplicate_map(
    rows: list[dict[str, str]], staging_dir: Path
) -> tuple[dict[str, str], list[tuple[str, str, str]]]:
    """Identify duplicate documents in staging and map them to a canonical key."""
    canonical_map: dict[str, str] = {}
    known_metas: dict[str, dict[str, str]] = {}
    duplicate_pairs: list[tuple[str, str, str]] = []

    for row in rows:
        key = row["source_key"]
        file_path = staging_dir / row["staged_filename"]
        if not file_path.is_file():
            continue

        meta = extract_document_metadata(file_path)
        dup_canonical = None
        dup_reason = ""
        for prev_key, prev_meta in known_metas.items():
            is_dup, reason = is_duplicate_document(meta, prev_meta)
            if is_dup:
                dup_canonical = prev_key
                dup_reason = reason
                break

        if dup_canonical:
            canonical_map[key] = dup_canonical
            duplicate_pairs.append((key, dup_canonical, dup_reason))
        else:
            canonical_map[key] = key
            known_metas[key] = meta

    return canonical_map, duplicate_pairs


def load_chunks(
    manifest_path: Path,
    staging_dir: Path,
    max_chunks_per_source: int,
    min_characters: int,
    exclude_sections: set[str] | tuple[str, ...] | None = None,
    filter_duplicates: bool = True,
) -> tuple[list[SourceChunk], dict[str, str], list[tuple[str, str, str]]]:
    with manifest_path.open(newline="", encoding="utf-8") as source:
        rows = [row for row in csv.DictReader(source) if row.get("status") == "ready"]

    canonical_map, dup_pairs = build_duplicate_map(rows, staging_dir)
    excluded = set(exclude_sections) if exclude_sections is not None else DEFAULT_EXCLUDED_SECTIONS

    all_chunks: list[SourceChunk] = []
    for row in rows:
        source_key = row["source_key"]
        if filter_duplicates and canonical_map.get(source_key) != source_key:
            # Skip non-canonical duplicate document sources
            continue

        file_path = staging_dir / row["staged_filename"]
        if not file_path.is_file():
            raise FileNotFoundError(f"Staged source is missing: {source_key}")

        cfg = get_preprocessing_config()
        if exclude_sections is not None:
            cfg.exclude_sections = set(exclude_sections)
        chunks, _ = prepare_document_chunks(file_path, config=cfg)
        usable_chunks = [
            SourceChunk(
                source_key=source_key,
                page_number=chunk.page_number,
                chunk_no=chunk.chunk_no,
                text=chunk.text,
                section_type=chunk.section_type,
            )
            for chunk in chunks
            if len(chunk.text) >= min_characters
        ]
        all_chunks.extend(evenly_spaced(usable_chunks, max_chunks_per_source))

    return all_chunks, canonical_map, dup_pairs


def candidate_pairs(
    chunks: list[SourceChunk],
    max_tasks: int,
    candidates_per_chunk: int,
    minimum_similarity: float,
    duplicate_map: dict[str, str] | None = None,
) -> list[dict[str, str]]:
    if len(chunks) < 2:
        raise ValueError("Need at least two usable chunks")
    if max_tasks < 1 or candidates_per_chunk < 1:
        raise ValueError("max_tasks and candidates_per_chunk must be positive")
    if not 0 <= minimum_similarity <= 1:
        raise ValueError("minimum_similarity must be in [0, 1]")

    canonical = duplicate_map or {}

    vectorizer = TfidfVectorizer(
        analyzer="word",
        ngram_range=(1, 2),
        min_df=1,
        max_features=40_000,
        sublinear_tf=True,
    )
    matrix = vectorizer.fit_transform([chunk.text for chunk in chunks])
    neighbors = NearestNeighbors(
        metric="cosine",
        algorithm="brute",
        n_neighbors=min(len(chunks), candidates_per_chunk + 1),
    ).fit(matrix)
    distances, indexes = neighbors.kneighbors(matrix)

    seen: set[tuple[str, str]] = set()
    suggestions: list[tuple[float, SourceChunk, SourceChunk]] = []
    for query_index, (row_distances, row_indexes) in enumerate(zip(distances, indexes)):
        query = chunks[query_index]
        for distance, source_index in zip(row_distances, row_indexes):
            source = chunks[int(source_index)]
            if source.source_key == query.source_key:
                continue
            # Also ensure query and source are not duplicates of the same thesis
            q_canon = canonical.get(query.source_key, query.source_key)
            s_canon = canonical.get(source.source_key, source.source_key)
            if q_canon == s_canon:
                continue

            similarity = 1.0 - float(distance)
            if similarity < minimum_similarity:
                continue
            identity = tuple(
                sorted(
                    (
                        f"{query.source_key}:{query.chunk_no}",
                        f"{source.source_key}:{source.chunk_no}",
                    )
                )
            )
            if identity in seen:
                continue
            seen.add(identity)
            suggestions.append((similarity, query, source))

    suggestions.sort(key=lambda item: item[0], reverse=True)
    rows: list[dict[str, str]] = []
    for index, (similarity, query, source) in enumerate(suggestions[:max_tasks], start=1):
        # Keep all variants linked to the canonical source document in one group.
        group_id = canonical.get(source.source_key, source.source_key)
        rows.append(
            {
                "task_id": f"TASK-{index:05d}",
                "group_id": group_id,
                "query_source_key": query.source_key,
                "query_page": str(query.page_number or ""),
                "query_chunk_no": str(query.chunk_no),
                "query_text": query.text,
                "source_key": source.source_key,
                "source_page": str(source.page_number or ""),
                "source_chunk_no": str(source.chunk_no),
                "source_text": source.text,
                "lexical_cosine": f"{similarity:.4f}",
                "candidate_type": "cross_document_lexical_candidate",
                "reviewer_1_label": "",
                "reviewer_2_label": "",
                "final_label": "",
                "status": "pending",
                "note": "",
            }
        )
    return rows


def write_candidates(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as destination:
        writer = csv.DictWriter(destination, fieldnames=ANNOTATION_FIELDS)
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--staging-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--max-chunks-per-source", type=int, default=10)
    parser.add_argument("--min-characters", type=int, default=280)
    parser.add_argument("--max-tasks", type=int, default=600)
    parser.add_argument("--candidates-per-chunk", type=int, default=8)
    parser.add_argument("--minimum-similarity", type=float, default=0.12)
    parser.add_argument(
        "--exclude-sections",
        default="cover,table_of_contents,abbreviations,references",
        help="Comma-separated section types to exclude prior to candidate generation",
    )
    parser.add_argument(
        "--dedup-documents",
        action="store_true",
        default=True,
        help="Exclude duplicate ingested documents before chunk sampling",
    )
    args = parser.parse_args()

    excluded = {
        s.strip() for s in args.exclude_sections.split(",") if s.strip()
    }

    chunks, duplicate_map, dup_pairs = load_chunks(
        args.manifest,
        args.staging_dir,
        args.max_chunks_per_source,
        args.min_characters,
        exclude_sections=excluded,
        filter_duplicates=args.dedup_documents,
    )
    rows = candidate_pairs(
        chunks,
        args.max_tasks,
        args.candidates_per_chunk,
        args.minimum_similarity,
        duplicate_map=duplicate_map,
    )
    write_candidates(args.output, rows)
    print(
        json.dumps(
            {
                "output": str(args.output),
                "sampled_chunks": len(chunks),
                "annotation_tasks": len(rows),
                "labels_assigned": 0,
                "detected_duplicate_theses": len(dup_pairs),
                "excluded_sections": sorted(excluded),
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
