"""Script to audit duplicates, group document families, and sanitize corpus manifest (CP-02.3)."""

from __future__ import annotations

import csv
import json
import logging
import shutil
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from app.corpus import (
    ManifestRecord,
    audit_corpus_manifest,
    compute_file_sha256,
    save_corpus_manifest_v2,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("audit_corpus")


def run_audit() -> dict:
    manifest_path = PROJECT_ROOT / "data" / "corpus_manifest.csv"
    backup_path = PROJECT_ROOT / "data" / "corpus_manifest.b0.csv"
    staging_dir = PROJECT_ROOT / "corpus_staging"
    report_json_path = PROJECT_ROOT / "data" / "corpus_duplicate_audit.json"

    assert manifest_path.is_file(), f"Manifest file missing: {manifest_path}"
    assert staging_dir.is_dir(), f"Staging directory missing: {staging_dir}"

    logger.info("1. Backing up original manifest to %s...", backup_path.name)
    if not backup_path.exists():
        shutil.copy2(manifest_path, backup_path)

    # Read existing records
    with manifest_path.open(newline="", encoding="utf-8") as f:
        existing_rows = list(csv.DictReader(f))
    logger.info("Read %d records from existing manifest.", len(existing_rows))

    # Record hashes before audit to verify immutability of original staging files
    staging_hashes_before = {}
    for row in existing_rows:
        p = staging_dir / row["staged_filename"]
        if p.is_file():
            staging_hashes_before[row["staged_filename"]] = compute_file_sha256(p)

    logger.info("2. Running multi-tier duplicate audit and family clustering...")
    records, audit_summary = audit_corpus_manifest(existing_rows, staging_dir)

    # Verify original files in staging were untouched
    for filename, h_before in staging_hashes_before.items():
        p = staging_dir / filename
        h_after = compute_file_sha256(p)
        assert h_after == h_before, f"Original file {filename} was modified during audit!"
    logger.info("Staging file immutability verified (100% byte-identical before and after).")

    logger.info("3. Saving enriched Manifest Schema v2...")
    save_corpus_manifest_v2(records, manifest_path)

    logger.info("4. Saving audit summary to %s...", report_json_path)
    report_json_path.parent.mkdir(parents=True, exist_ok=True)
    report_json_path.write_text(json.dumps(audit_summary, indent=2, ensure_ascii=False), encoding="utf-8")

    logger.info("Corpus audit completed successfully: %s", audit_summary)
    return audit_summary


if __name__ == "__main__":
    summary = run_audit()
    print("\n" + "=" * 60)
    print("UGTM AI — CORPUS DUPLICATE AUDIT SUMMARY (CP-02.3)")
    print("=" * 60)
    print(json.dumps(summary, indent=2, ensure_ascii=False))

