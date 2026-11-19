"""Script to audit end-to-end evidence traceability, Vietnamese text fidelity, and passage overlap (CP-02.5).

Outputs:
- data/evidence_audit_report.json
- Console audit summary
"""

from __future__ import annotations

import json
import logging
import sys
from pathlib import Path

# Add project root to sys.path
PROJECT_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT_ROOT))

# Configure UTF-8 output on Windows
sys.stdout.reconfigure(encoding="utf-8")

from app.documents import Chunk, extract_pages
from app.evidence import audit_report_evidence, trace_evidence_to_file
from app.preprocessing import prepare_document_chunks
from app.reports import build_report_v3

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

PROJECT_ROOT = Path(__file__).resolve().parent.parent
SAMPLES_DIR = PROJECT_ROOT / "app" / "static" / "samples"
CORPUS_STAGING_DIR = PROJECT_ROOT / "corpus_staging"
OUTPUT_REPORT_PATH = PROJECT_ROOT / "data" / "evidence_audit_report.json"


def main():
    logger.info("Starting Evidence Traceability and Pipeline Integrity Audit (CP-02.5)...")

    suspect_path = SAMPLES_DIR / "suspect_doc.docx"
    source_path = SAMPLES_DIR / "source_doc.docx"

    if not suspect_path.exists() or not source_path.exists():
        logger.error("Sample documents not found in %s", SAMPLES_DIR)
        sys.exit(1)

    logger.info("1. Parsing suspect document and extracting candidate chunks...")
    suspect_chunks, suspect_stats = prepare_document_chunks(str(suspect_path))
    source_chunks, source_stats = prepare_document_chunks(str(source_path))

    logger.info("Suspect chunks: %d, Source chunks: %d", len(suspect_chunks), len(source_chunks))

    # Simulate realistic finding pairs between suspect and source chunks
    # Chunk 0 of suspect has identical text to chunk 0 of source; chunk 1 to chunk 1
    mock_findings = []
    for sc_idx, sc in enumerate(suspect_chunks[:3]):
        # Match with corresponding source chunk
        src_c = source_chunks[sc_idx]
        mock_findings.append({
            "submission_chunk_no": sc.chunk_no,
            "submission_page": sc.page_number,
            "submission_start": sc.start_offset,
            "submission_end": sc.end_offset,
            "submission_text": sc.text,
            "chunk_id": 9000 + sc_idx,
            "source_key": "source_doc",
            "source_version": 1,
            "source_filename": "source_doc.docx",
            "source_chunk_no": src_c.chunk_no,
            "source_page": src_c.page_number,
            "source_start": src_c.start_offset,
            "source_end": src_c.end_offset,
            "source_text": src_c.text,
            "rrf_score": 0.033,
            "dense_score": 0.95,
            "lexical_score": 0.98,
            "reranker_score": 0.965,
            "classifier_score": None,
            "decision_source": "near_exact_override",
            "citation_status": "unexamined",
            "review_required": True,
        })

    logger.info("2. Building canonical Report Schema v3...")
    mock_job = {
        "corpus_id": "uit-library-v1",
        "document_key": "suspect_doc",
        "version": 1,
    }
    report = build_report_v3(
        job_id="audit-job-cp025",
        job=mock_job,
        query_chunks=suspect_chunks,
        all_findings=mock_findings,
    )

    logger.info("3. Executing deep evidence tracing back to physical DOCX files...")
    audit_results = audit_report_evidence(
        report=report,
        corpus_dir=SAMPLES_DIR,
        submission_file_path=suspect_path,
    )

    logger.info("4. Writing audit output to %s...", OUTPUT_REPORT_PATH)
    OUTPUT_REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_REPORT_PATH.write_text(
        json.dumps(audit_results, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print("\n" + "=" * 60)
    print("UGTM AI — EVIDENCE TRACEABILITY AUDIT SUMMARY (CP-02.5)")
    print("=" * 60)
    print(f"Total findings audited:         {audit_results['total_findings_audited']}")
    print(f"Source traceability rate:       {audit_results['source_traceability_rate'] * 100:.1f}%")
    print(f"Submission traceability rate:   {audit_results['submission_traceability_rate'] * 100:.1f}%")
    print(f"Vietnamese integrity rate:      {audit_results['vietnamese_integrity_rate'] * 100:.1f}%")
    print(f"Passage groups audited:         {audit_results['passage_groups_overlap_audited']}")
    print(f"All evidence traceable:         {audit_results['all_evidence_traceable']}")
    print("=" * 60 + "\n")

    if not audit_results["all_evidence_traceable"]:
        logger.error("Audit FAILED: Some evidence could not be traced or Vietnamese text was corrupted!")
        sys.exit(1)

    logger.info("Evidence audit passed 100% successfully!")


if __name__ == "__main__":
    main()
