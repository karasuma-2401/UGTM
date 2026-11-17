"""Validate approved annotation tasks and create the classifier training/benchmark CSV (CP-03.1).

Upgraded in CP-03.1:
1. Strict label provenance: preserves raw reviewer labels even after adjudication (never overwrite raw data).
2. Supports both dual independent reviewers and solo-annotator mode with external audit.
3. Enriches output with reuse_type, citation_status, and adjudication provenance.
"""

from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path
from typing import Any

BASE_INPUT_COLUMNS = {
    "task_id",
    "group_id",
    "query_text",
    "source_text",
    "status",
}

OUTPUT_FIELDS = (
    "pair_id",
    "group_id",
    "label",
    "query_text",
    "source_text",
    "reuse_type",
    "citation_status",
    "raw_annotator_label",
    "audit_reviewer_label",
    "adjudicated_label",
    "adjudication_reason",
    "provenance_mode",
)


def clean_str(value: Any) -> str:
    return str(value or "").strip()


def validate_and_extract_label(
    row: dict[str, str],
    require_two_reviewers: bool = False,
    allow_solo_annotator: bool = True,
) -> tuple[str, str]:
    """Determine the approved label and provenance mode without overwriting raw labels."""
    task_id = clean_str(row.get("task_id", "<unknown>"))
    status = clean_str(row.get("status")).casefold()

    if status != "approved":
        raise ValueError(f"{task_id}: status must be approved")

    final = clean_str(row.get("final_label"))
    rev1 = clean_str(row.get("reviewer_1_label") or row.get("raw_annotator_label"))
    rev2 = clean_str(row.get("reviewer_2_label") or row.get("audit_reviewer_label"))
    adj_label = clean_str(row.get("adjudicated_label"))
    adj_reason = clean_str(row.get("adjudication_reason"))

    # Case 1: Two reviewers are provided
    if rev1 and rev2:
        if rev1 == rev2:
            resolved = rev1
            provenance = "dual_review_consensus"
        elif adj_label:
            if not adj_reason:
                raise ValueError(f"{task_id}: adjudication_reason is required when resolving reviewer disagreement")
            resolved = adj_label
            provenance = "dual_review_adjudicated"
        elif final and final == rev1 and final == rev2:
            resolved = final
            provenance = "dual_review_consensus"
        else:
            if require_two_reviewers:
                raise ValueError(
                    f"{task_id}: two reviewers must agree with final_label or provide adjudicated_label with adjudication_reason"
                )
            # If not requiring strict dual agreement, check if final_label exists
            if final:
                resolved = final
                provenance = "dual_review_adjudicated"
            else:
                raise ValueError(f"{task_id}: reviewers disagree ({rev1} vs {rev2}) and no adjudication provided")

    # Case 2: Solo annotator (only rev1 or raw_annotator_label exists)
    elif rev1:
        if not allow_solo_annotator and require_two_reviewers:
            raise ValueError(f"{task_id}: two reviewers are required in strict dual review mode")
        resolved = adj_label if adj_label else rev1
        provenance = "solo_annotator_adjudicated" if adj_label else "solo_annotator"

    # Case 3: Only final_label exists
    elif final:
        resolved = final
        provenance = "direct_final_label"
    else:
        raise ValueError(f"{task_id}: no label provided (reviewer_1, raw_annotator, or final_label required)")

    if resolved not in {"0", "1"}:
        raise ValueError(f"{task_id}: resolved label must be '0' or '1', got '{resolved}'")

    return resolved, provenance


def finalize_rows(
    rows: list[dict[str, str]],
    require_two_reviewers: bool = False,
    allow_solo_annotator: bool = True,
) -> list[dict[str, str]]:
    """Validate and finalize annotation rows, preserving full audit trail."""
    if not rows:
        raise ValueError("No annotation rows found")

    row_keys = set(rows[0].keys())
    missing = BASE_INPUT_COLUMNS - row_keys
    if missing:
        raise ValueError(f"Missing required columns: {sorted(missing)}")

    result: list[dict[str, str]] = []
    errors: list[str] = []
    seen_task_ids: set[str] = set()

    for row in rows:
        task_id = clean_str(row.get("task_id"))
        try:
            if not task_id:
                raise ValueError("task_id is required")
            if task_id in seen_task_ids:
                raise ValueError(f"{task_id}: duplicate task_id")
            seen_task_ids.add(task_id)

            group_id = clean_str(row.get("group_id"))
            if not group_id:
                raise ValueError(f"{task_id}: group_id is required")

            q_text = clean_str(row.get("query_text"))
            s_text = clean_str(row.get("source_text"))
            if not q_text or not s_text:
                raise ValueError(f"{task_id}: passage text is required")

            label_val, provenance_mode = validate_and_extract_label(
                row,
                require_two_reviewers=require_two_reviewers,
                allow_solo_annotator=allow_solo_annotator,
            )

            reuse_type = clean_str(row.get("reuse_type"))
            if not reuse_type:
                reuse_type = "independent" if label_val == "0" else "verbatim"

            citation_status = clean_str(row.get("citation_status"))
            if not citation_status:
                citation_status = "not_applicable" if label_val == "0" else "uncited"

            result.append(
                {
                    "pair_id": task_id,
                    "group_id": group_id,
                    "label": label_val,
                    "query_text": q_text,
                    "source_text": s_text,
                    "reuse_type": reuse_type,
                    "citation_status": citation_status,
                    "raw_annotator_label": clean_str(row.get("reviewer_1_label") or row.get("raw_annotator_label")),
                    "audit_reviewer_label": clean_str(row.get("reviewer_2_label") or row.get("audit_reviewer_label")),
                    "adjudicated_label": clean_str(row.get("adjudicated_label")),
                    "adjudication_reason": clean_str(row.get("adjudication_reason")),
                    "provenance_mode": provenance_mode,
                }
            )
        except ValueError as exc:
            errors.append(str(exc))

    if errors:
        raise ValueError("Annotation validation failed:\n- " + "\n- ".join(errors[:20]))

    unique_groups = {r["group_id"] for r in result}
    if len(unique_groups) < 6:
        raise ValueError(f"Need at least six independent group_id values (got {len(unique_groups)})")

    label_counts = Counter(r["label"] for r in result)
    if set(label_counts.keys()) != {"0", "1"}:
        raise ValueError("Need approved examples for both label 0 and label 1")

    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True, help="Input raw annotation CSV")
    parser.add_argument("--output", type=Path, required=True, help="Output finalized CSV")
    parser.add_argument(
        "--require-two-reviewers",
        action="store_true",
        help="Strictly enforce two reviewers agreement or explicit adjudication",
    )
    parser.add_argument(
        "--allow-solo-annotator",
        action="store_true",
        default=True,
        help="Permit solo-annotator records with provenance tracking",
    )
    args = parser.parse_args()

    with args.input.open(newline="", encoding="utf-8") as source:
        finalized = finalize_rows(
            list(csv.DictReader(source)),
            require_two_reviewers=args.require_two_reviewers,
            allow_solo_annotator=args.allow_solo_annotator,
        )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", newline="", encoding="utf-8") as destination:
        writer = csv.DictWriter(destination, fieldnames=OUTPUT_FIELDS)
        writer.writeheader()
        writer.writerows(finalized)

    summary = {
        "output": str(args.output),
        "approved_pairs": len(finalized),
        "label_counts": dict(Counter(row["label"] for row in finalized)),
        "groups": len({row["group_id"] for row in finalized}),
        "provenance_modes": dict(Counter(row["provenance_mode"] for row in finalized)),
    }
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
