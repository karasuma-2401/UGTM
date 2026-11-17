"""Constrained Local Sentence and Span Alignment (CP-04.2).

Replaces naive chunk adjacency grouping with fine-grained sentence/span alignment:
1. Exact character-level sentence segmentation with abbreviation-protected boundaries.
2. Local monotonic diagonal matching (Smith-Waterman / island extension).
3. Explicit edge-case handling:
   - One-to-many: 1 query sentence rewritten/translated from 2+ contiguous source sentences.
   - Many-to-one: 2+ query sentences synthesizing/summarizing 1 source sentence.
   - Non-overlapping interval union: Merges overlapping spans from chunk sliding windows
     to ensure true character span coverage with zero double-counting.
   - Multi-source separation: Isolates spans from different sources into separate
     passage groups, strictly preventing cross-source contamination.
4. Backward compatibility adapter for legacy `group_adjacent_findings`.
"""

from __future__ import annotations

import difflib
import re
from collections import defaultdict
from dataclasses import asdict, dataclass, field
from typing import Any

from app.documents import normalize, split_sentences


@dataclass
class AlignedSpan:
    """A fine-grained character-aligned span between query and source text."""

    query_start: int
    query_end: int
    source_start: int
    source_end: int
    alignment_type: str  # "verbatim", "near_verbatim", "paraphrase", "one_to_many", "many_to_one", "summary"
    confidence: float  # 0.0 to 1.0
    source_key: str = ""
    source_version: int = 1
    source_filename: str = ""
    passage_group_id: str | None = None
    query_text: str = ""
    source_text: str = ""
    query_sentence_indices: list[int] = field(default_factory=list)
    source_sentence_indices: list[int] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        d = asdict(self)
        d["confidence"] = round(float(self.confidence), 4)
        return d

    @property
    def query_span(self) -> tuple[int, int]:
        return (self.query_start, self.query_end)

    @property
    def source_span(self) -> tuple[int, int]:
        return (self.source_start, self.source_end)


def compute_char_ngrams(text: str, n: int = 3) -> set[str]:
    """Extract character n-grams from normalized text."""
    if len(text) < n:
        return {text} if text else set()
    return {text[i : i + n] for i in range(len(text) - n + 1)}


_RX_WORD = re.compile(r"\w+")


def _prepare_sentence(text: str) -> tuple[str, set[str], set[str]]:
    """Precompute normalized text, char 3-grams, and word token set once per sentence."""
    norm = normalize(text).lower().strip()
    if not norm:
        return "", set(), set()
    return norm, compute_char_ngrams(norm, 3), set(_RX_WORD.findall(norm))


def _prepared_sentence_similarity(
    prep_a: tuple[str, set[str], set[str]],
    prep_b: tuple[str, set[str], set[str]],
    min_needed: float = 0.0,
) -> float:
    """Lossless fast similarity using SequenceMatcher upper-bound pruning."""
    na, ga, wa = prep_a
    nb, gb, wb = prep_b

    if not na or not nb:
        return 0.0
    if na == nb:
        return 1.0

    union_len = len(ga) + len(gb)
    dice = (2.0 * len(ga & gb) / union_len) if union_len > 0 else 0.0

    w_union = len(wa) + len(wb) - len(wa & wb)
    jaccard = (len(wa & wb) / w_union) if w_union > 0 else 0.0

    shorter, longer = (len(na), len(nb)) if len(na) <= len(nb) else (len(nb), len(na))
    containment = (shorter / longer) if (shorter / longer > 0.4 and (na in nb or nb in na)) else 0.0

    base = max(
        dice,
        0.65 * jaccard + 0.35 * dice,
        containment * 0.95 if containment > 0.4 else 0.0,
    )

    # SequenceMatcher theorem: ratio() <= quick_ratio() <= real_quick_ratio().
    # Only instantiate SequenceMatcher and compute O(L^2) ratio() if upper bounds exceed threshold.
    threshold = base if base >= min_needed else min_needed
    real_quick = (2.0 * shorter) / (shorter + longer)
    if real_quick <= threshold:
        sm_ratio = 0.0
    else:
        sm = difflib.SequenceMatcher(None, na, nb)
        sm_ratio = 0.0 if sm.quick_ratio() <= threshold else sm.ratio()

    composite = base if base >= sm_ratio else sm_ratio
    return min(1.0, max(0.0, float(composite)))


def compute_sentence_similarity(text_a: str, text_b: str) -> float:
    """Compute multi-perspective lexical and surface similarity between two sentences.

    Combines:
    1. Exact normalized identity (1.0).
    2. Character 3-gram Dice coefficient (robust to morphology, typos, and slight word order).
    3. Word token Jaccard similarity (vocabulary overlap).
    4. difflib SequenceMatcher ratio (order-preserving fuzzy matching).
    5. Asymmetric substring containment.
    """
    return _prepared_sentence_similarity(
        _prepare_sentence(text_a),
        _prepare_sentence(text_b),
        min_needed=0.0,
    )


def _extract_sentences_with_offsets(text: str) -> list[tuple[int, int, str]]:
    """Segment text into sentences with exact character offsets trimmed of boundary spaces."""
    if not text.strip():
        return []

    raw_sents = split_sentences(text)
    if not raw_sents:
        # Fallback for single phrase without ending punctuation
        l_sp = len(text) - len(text.lstrip())
        r_sp = len(text) - len(text.rstrip())
        return [(l_sp, len(text) - r_sp, text.strip())]

    trimmed_sents: list[tuple[int, int, str]] = []
    for start, end, s_text in raw_sents:
        sub = text[start:end]
        l_sp = len(sub) - len(sub.lstrip())
        r_sp = len(sub) - len(sub.rstrip())
        trimmed_sents.append((start + l_sp, end - r_sp, s_text))

    return trimmed_sents


def align_passage_pair(
    query_text: str,
    source_text: str,
    query_base_offset: int = 0,
    source_base_offset: int = 0,
    source_key: str = "",
    source_version: int = 1,
    source_filename: str = "",
    min_confidence: float = 0.40,
    max_gap: int = 1,
) -> list[AlignedSpan]:
    """Align sentences between a query text and a candidate source text.

    Supports 1-to-1, 1-to-many, many-to-one, and monotonic diagonal extension.
    Returns fine-grained `AlignedSpan` records with absolute character offsets.
    """
    q_sents = _extract_sentences_with_offsets(query_text)
    s_sents = _extract_sentences_with_offsets(source_text)

    if not q_sents or not s_sents:
        return []

    M = len(q_sents)
    N = len(s_sents)

    q_preps = [_prepare_sentence(q_sents[i][2]) for i in range(M)]
    s_preps = [_prepare_sentence(s_sents[j][2]) for j in range(N)]
    single_floor = min(0.20, min_confidence)

    # 1. Precompute pairwise 1-to-1 similarity matrix
    sim_1_1 = [
        [
            _prepared_sentence_similarity(q_preps[i], s_preps[j], min_needed=single_floor)
            for j in range(N)
        ]
        for i in range(M)
    ]

    candidate_units: list[tuple[list[int], list[int], float, str]] = []

    s_combo2 = [
        _prepare_sentence(f"{s_sents[j][2]} {s_sents[j+1][2]}") for j in range(N - 1)
    ]
    s_combo3 = [
        _prepare_sentence(f"{s_sents[j][2]} {s_sents[j+1][2]} {s_sents[j+2][2]}")
        for j in range(N - 2)
    ]
    q_combo2 = [
        _prepare_sentence(f"{q_sents[i][2]} {q_sents[i+1][2]}") for i in range(M - 1)
    ]
    q_combo3 = [
        _prepare_sentence(f"{q_sents[i][2]} {q_sents[i+1][2]} {q_sents[i+2][2]}")
        for i in range(M - 2)
    ]

    # 2. Evaluate 1-to-many units (1 query sentence matching 2 or 3 contiguous source sentences)
    for i in range(M):
        q_p = q_preps[i]
        # 1-to-2
        for j in range(N - 1):
            best_single = max(sim_1_1[i][j], sim_1_1[i][j + 1])
            worst_single = min(sim_1_1[i][j], sim_1_1[i][j + 1])
            needed = min_confidence if worst_single >= 0.20 else max(min_confidence, best_single + 0.05)
            score = _prepared_sentence_similarity(q_p, s_combo2[j], min_needed=needed)
            if score >= min_confidence and (
                score >= best_single + 0.05 or (score >= 0.70 and worst_single >= 0.20)
            ):
                candidate_units.append(([i], [j, j + 1], score, "one_to_many"))
        # 1-to-3
        for j in range(N - 2):
            best_single = max(sim_1_1[i][j], sim_1_1[i][j + 1], sim_1_1[i][j + 2])
            needed = max(min_confidence, best_single + 0.08)
            score = _prepared_sentence_similarity(q_p, s_combo3[j], min_needed=needed)
            if score >= min_confidence and score >= best_single + 0.08:
                candidate_units.append(([i], [j, j + 1, j + 2], score, "one_to_many"))

    # 3. Evaluate many-to-1 units (2 or 3 contiguous query sentences matching 1 source sentence)
    for j in range(N):
        s_p = s_preps[j]
        # 2-to-1
        for i in range(M - 1):
            best_single = max(sim_1_1[i][j], sim_1_1[i + 1][j])
            worst_single = min(sim_1_1[i][j], sim_1_1[i + 1][j])
            needed = min_confidence if worst_single >= 0.20 else max(min_confidence, best_single + 0.05)
            score = _prepared_sentence_similarity(q_combo2[i], s_p, min_needed=needed)
            if score >= min_confidence and (
                score >= best_single + 0.05 or (score >= 0.70 and worst_single >= 0.20)
            ):
                candidate_units.append(([i, i + 1], [j], score, "many_to_one"))
        # 3-to-1
        for i in range(M - 2):
            best_single = max(sim_1_1[i][j], sim_1_1[i + 1][j], sim_1_1[i + 2][j])
            needed = max(min_confidence, best_single + 0.08)
            score = _prepared_sentence_similarity(q_combo3[i], s_p, min_needed=needed)
            if score >= min_confidence and score >= best_single + 0.08:
                candidate_units.append(([i, i + 1, i + 2], [j], score, "many_to_one"))

    # 4. Evaluate 1-to-1 units
    for i in range(M):
        for j in range(N):
            score = sim_1_1[i][j]
            if score >= min_confidence:
                if score >= 0.92:
                    a_type = "verbatim"
                elif score >= 0.75:
                    a_type = "near_verbatim"
                elif score >= 0.50:
                    a_type = "paraphrase"
                else:
                    a_type = "summary"
                candidate_units.append(([i], [j], score, a_type))

    if not candidate_units:
        return []

    # 5. Greedy Selection of Non-overlapping Maximal Units
    candidate_units.sort(
        key=lambda u: (u[2], len(u[0]) + len(u[1])),
        reverse=True,
    )
    used_q: set[int] = set()
    used_s: set[int] = set()
    selected_units: list[tuple[list[int], list[int], float, str]] = []

    for q_idx_list, s_idx_list, score, a_type in candidate_units:
        if not (set(q_idx_list) & used_q) and not (set(s_idx_list) & used_s):
            selected_units.append((q_idx_list, s_idx_list, score, a_type))
            used_q.update(q_idx_list)
            used_s.update(s_idx_list)

    if not selected_units:
        return []

    # Sort selected units in reading order (query order first, then source order)
    selected_units.sort(key=lambda u: (u[0][0], u[1][0]))

    # 6. Monotonic Diagonal Chaining (Merging contiguous passage matches)
    chains: list[list[tuple[list[int], list[int], float, str]]] = []
    for unit in selected_units:
        if not chains:
            chains.append([unit])
            continue

        prev_chain = chains[-1]
        last_unit = prev_chain[-1]

        gap_q = unit[0][0] - last_unit[0][-1] - 1
        gap_s = unit[1][0] - last_unit[1][-1] - 1

        # Check monotonic condition and gap tolerance
        if 0 <= gap_q <= max_gap and 0 <= gap_s <= max_gap:
            prev_chain.append(unit)
        else:
            chains.append([unit])

    # 7. Convert Chained Units to AlignedSpan Records
    spans: list[AlignedSpan] = []
    for chain in chains:
        all_q_idx = sorted({idx for u in chain for idx in u[0]})
        all_s_idx = sorted({idx for u in chain for idx in u[1]})

        q_start_char = query_base_offset + q_sents[all_q_idx[0]][0]
        q_end_char = query_base_offset + q_sents[all_q_idx[-1]][1]

        s_start_char = source_base_offset + s_sents[all_s_idx[0]][0]
        s_end_char = source_base_offset + s_sents[all_s_idx[-1]][1]

        # Calculate length-weighted confidence
        total_q_len = sum(len(q_sents[idx][2]) for idx in all_q_idx)
        weighted_conf = (
            sum(u[2] * sum(len(q_sents[idx][2]) for idx in u[0]) for u in chain)
            / total_q_len
            if total_q_len > 0
            else chain[0][2]
        )

        # Determine composite alignment type
        if len(chain) == 1:
            a_type = chain[0][3]
        else:
            types = {u[3] for u in chain}
            if types == {"verbatim"}:
                a_type = "verbatim"
            elif "one_to_many" in types:
                a_type = "one_to_many"
            elif "many_to_one" in types:
                a_type = "many_to_one"
            elif weighted_conf >= 0.90:
                a_type = "verbatim"
            elif weighted_conf >= 0.75:
                a_type = "near_verbatim"
            elif weighted_conf >= 0.50:
                a_type = "paraphrase"
            else:
                a_type = "summary"

        sub_q_text = query_text[
            q_sents[all_q_idx[0]][0] : q_sents[all_q_idx[-1]][1]
        ].strip()
        sub_s_text = source_text[
            s_sents[all_s_idx[0]][0] : s_sents[all_s_idx[-1]][1]
        ].strip()

        spans.append(
            AlignedSpan(
                query_start=q_start_char,
                query_end=q_end_char,
                source_start=s_start_char,
                source_end=s_end_char,
                alignment_type=a_type,
                confidence=round(weighted_conf, 4),
                source_key=source_key,
                source_version=source_version,
                source_filename=source_filename,
                query_text=sub_q_text,
                source_text=sub_s_text,
                query_sentence_indices=all_q_idx,
                source_sentence_indices=all_s_idx,
            )
        )

    return spans


def deduplicate_overlapping_spans(
    spans: list[AlignedSpan],
    max_gap_chars: int = 2,
) -> list[AlignedSpan]:
    """Merge overlapping or touching intervals belonging to the SAME source document.

    Ensures non-overlapping interval union:
    - Merges spans from sliding chunk overlaps for identical (source_key, source_version).
    - Prevents double-counting character coverage.
    - Strictly preserves distinct (source_key, source_version) pairs (multi-source separation).
    """
    if not spans:
        return []

    # Group strictly by source identity
    by_source: dict[tuple[str, int], list[AlignedSpan]] = defaultdict(list)
    for s in spans:
        by_source[(s.source_key, s.source_version)].append(s)

    merged_all: list[AlignedSpan] = []

    for _, group in by_source.items():
        # Sort by query_start, then query_end
        group.sort(key=lambda s: (s.query_start, s.query_end))
        merged_group: list[AlignedSpan] = []

        for s in group:
            if not merged_group:
                merged_group.append(s)
                continue

            prev = merged_group[-1]
            # Check if current span overlaps or is directly contiguous with prev span
            if s.query_start <= prev.query_end + max_gap_chars:
                # Merge overlapping intervals
                new_q_start = prev.query_start
                new_q_end = max(prev.query_end, s.query_end)
                new_s_start = min(prev.source_start, s.source_start)
                new_s_end = max(prev.source_end, s.source_end)

                # Combine confidences
                new_conf = max(prev.confidence, s.confidence)

                # Prioritize stronger alignment classification
                if prev.confidence >= s.confidence:
                    new_type = prev.alignment_type
                else:
                    new_type = s.alignment_type

                combined_q_indices = sorted(
                    set(prev.query_sentence_indices + s.query_sentence_indices)
                )
                combined_s_indices = sorted(
                    set(prev.source_sentence_indices + s.source_sentence_indices)
                )

                merged_span = AlignedSpan(
                    query_start=new_q_start,
                    query_end=new_q_end,
                    source_start=new_s_start,
                    source_end=new_s_end,
                    alignment_type=new_type,
                    confidence=round(new_conf, 4),
                    source_key=prev.source_key,
                    source_version=prev.source_version,
                    source_filename=prev.source_filename or s.source_filename,
                    passage_group_id=prev.passage_group_id,
                    query_text=prev.query_text
                    if len(prev.query_text) >= len(s.query_text)
                    else s.query_text,
                    source_text=prev.source_text
                    if len(prev.source_text) >= len(s.source_text)
                    else s.source_text,
                    query_sentence_indices=combined_q_indices,
                    source_sentence_indices=combined_s_indices,
                )
                merged_group[-1] = merged_span
            else:
                merged_group.append(s)

        merged_all.extend(merged_group)

    # Sort final spans by query position and source key
    merged_all.sort(key=lambda s: (s.query_start, s.source_key))
    return merged_all


def assign_passage_groups(spans: list[AlignedSpan]) -> list[AlignedSpan]:
    """Assign monotonic passage group IDs (PG-0001, PG-0002) preserving multi-source separation."""
    if not spans:
        return []

    # Group by (source_key, source_version)
    by_source: dict[tuple[str, int], list[AlignedSpan]] = defaultdict(list)
    for s in spans:
        by_source[(s.source_key, s.source_version)].append(s)

    next_id = 1
    for _, group in sorted(by_source.items(), key=lambda item: item[0]):
        group.sort(key=lambda s: (s.query_start, s.source_start))
        prev: AlignedSpan | None = None
        for s in group:
            # If contiguous query or source passage
            if prev is not None and s.query_start <= prev.query_end + 50:
                s.passage_group_id = prev.passage_group_id
            else:
                s.passage_group_id = f"PG-{next_id:04d}"
                next_id += 1
            prev = s

    return spans


def align_document_findings(
    findings: list[dict[str, Any]],
    min_confidence: float = 0.40,
) -> list[AlignedSpan]:
    """Convert raw chunk findings into non-overlapping fine-grained AlignedSpan objects."""
    all_spans: list[AlignedSpan] = []

    for f in findings:
        q_text = f.get("submission_text", "")
        s_text = f.get("source_text", "")
        q_base = int(f.get("submission_start", 0))
        s_base = int(f.get("source_start", 0))
        s_key = f.get("source_key", "")
        s_ver = int(f.get("source_version", 1))
        s_fn = f.get("source_filename", "")

        spans = align_passage_pair(
            query_text=q_text,
            source_text=s_text,
            query_base_offset=q_base,
            source_base_offset=s_base,
            source_key=s_key,
            source_version=s_ver,
            source_filename=s_fn,
            min_confidence=min_confidence,
        )

        if spans:
            all_spans.extend(spans)
        elif float(f.get("reranker_score", 0.0)) >= 0.70:
            # Fallback for chunk-level semantic match when sentence boundaries deviate
            all_spans.append(
                AlignedSpan(
                    query_start=q_base,
                    query_end=int(f.get("submission_end", q_base + len(q_text))),
                    source_start=s_base,
                    source_end=int(f.get("source_end", s_base + len(s_text))),
                    alignment_type="paraphrase",
                    confidence=round(float(f.get("reranker_score", 0.70)), 4),
                    source_key=s_key,
                    source_version=s_ver,
                    source_filename=s_fn,
                    query_text=q_text,
                    source_text=s_text,
                )
            )

    deduped = deduplicate_overlapping_spans(all_spans)
    assigned = assign_passage_groups(deduped)
    return assigned


def group_adjacent_findings(findings: list[dict]) -> list[dict]:
    """Legacy backward-compatibility adapter for chunk-level passage grouping."""
    by_source = defaultdict(list)
    for f in findings:
        by_source[(f["source_key"], f["source_version"])].append(f)

    next_id = 1
    for group in by_source.values():
        group.sort(
            key=lambda x: (x["submission_chunk_no"], x["source_chunk_no"])
        )
        previous = None
        for f in group:
            contiguous = (
                previous is not None
                and 0 <= f["submission_chunk_no"] - previous["submission_chunk_no"] <= 1
                and 0 <= f["source_chunk_no"] - previous["source_chunk_no"] <= 1
            )
            if contiguous:
                f["passage_group_id"] = previous["passage_group_id"]
            else:
                f["passage_group_id"] = f"PG-{next_id:04d}"
                next_id += 1
            previous = f
    return findings
