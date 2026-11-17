from __future__ import annotations

import csv
import hashlib
from pathlib import Path
import pytest

from app.corpus import (
    MANIFEST_V2_FIELDS,
    ManifestRecord,
    assert_processing_allowed,
    audit_corpus_manifest,
    compute_file_sha256,
    compute_normalized_text_sha256,
    save_corpus_manifest_v2,
    should_exclude_source_for_query,
    split_corpus_by_family,
)

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
STAGING_DIR = Path(__file__).resolve().parent.parent / "corpus_staging"


def test_manifest_schema_v2_fields():
    """Verify Schema v2 fields definition and serialization."""
    expected_fields = [
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
    ]
    assert list(MANIFEST_V2_FIELDS) == expected_fields

    record = ManifestRecord(
        source_key="kltn-1234",
        document_family="fam-test1",
        corpus_id="uit-library-v1",
        version=1,
        staged_filename="kltn-1234.pdf",
        file_sha256="abc",
        normalized_text_sha256="def",
        bytes=1024,
        page_count=20,
        extracted_characters=5000,
        title="Test Thesis",
        authors="Nguyen Van A",
        status="ready",
        duplicate_of=None,
        duplicate_reason=None,
        processing_permission="all",
        notes="",
    )
    d = record.to_dict()
    assert d["source_key"] == "kltn-1234"
    assert d["document_family"] == "fam-test1"
    assert d["processing_permission"] == "all"

    reconstructed = ManifestRecord.from_dict(d)
    assert reconstructed.source_key == record.source_key
    assert reconstructed.document_family == record.document_family
    assert reconstructed.page_count == 20


def test_file_and_normalized_text_hashing(tmp_path):
    """Verify exact binary hashing and normalized text hashing."""
    test_file = tmp_path / "sample.txt"
    test_file.write_text("Dữ liệu thử nghiệm bài báo khoa học.", encoding="utf-8")

    file_hash = compute_file_sha256(test_file)
    expected_hash = hashlib.sha256("Dữ liệu thử nghiệm bài báo khoa học.".encode("utf-8")).hexdigest()
    assert file_hash == expected_hash


def test_self_match_exclusion_rules():
    """Verify query self-match exclusion policy."""
    # 1. Exact match by source_key
    assert should_exclude_source_for_query(
        query_key="doc-1",
        query_family="fam-1",
        candidate_key="doc-1",
        candidate_family="fam-1",
    ) is True

    # 2. Match by document_family (e.g. earlier draft / pdf conversion of same thesis)
    assert should_exclude_source_for_query(
        query_key="doc-1-docx",
        query_family="fam-1",
        candidate_key="doc-1-pdf",
        candidate_family="fam-1",
    ) is True

    # 3. Different document and different family -> do not exclude
    assert should_exclude_source_for_query(
        query_key="doc-1",
        query_family="fam-1",
        candidate_key="doc-2",
        candidate_family="fam-2",
    ) is False


def test_processing_permission_enforcement():
    """Verify local_only permission prevents data leak to cloud / remote GPU."""
    # Local only should raise PermissionError when targeting cloud
    with pytest.raises(PermissionError) as exc_info:
        assert_processing_allowed("local_only", target="remote_gpu")
    assert "rejected" in str(exc_info.value)

    with pytest.raises(PermissionError):
        assert_processing_allowed("local_only", target="cloud_storage")

    # Cloud allowed or 'all' should succeed without exception
    assert_processing_allowed("all", target="remote_gpu")
    assert_processing_allowed("cloud_allowed", target="cloud_storage")


def test_group_aware_split_no_family_leakage():
    """Verify that all members of a document family are placed in the same split."""
    records = [
        # Family 1 (2 versions: docx and pdf)
        ManifestRecord(
            source_key="f1_v1",
            document_family="family-alpha",
            corpus_id="uit",
            version=1,
            staged_filename="f1_v1.docx",
            file_sha256="h1",
            normalized_text_sha256="th1",
            bytes=10,
            page_count=5,
            extracted_characters=100,
        ),
        ManifestRecord(
            source_key="f1_v2",
            document_family="family-alpha",
            corpus_id="uit",
            version=2,
            staged_filename="f1_v2.pdf",
            file_sha256="h2",
            normalized_text_sha256="th1",
            bytes=12,
            page_count=5,
            extracted_characters=100,
        ),
        # Family 2 (single doc)
        ManifestRecord(
            source_key="f2_v1",
            document_family="family-beta",
            corpus_id="uit",
            version=1,
            staged_filename="f2_v1.pdf",
            file_sha256="h3",
            normalized_text_sha256="th2",
            bytes=10,
            page_count=5,
            extracted_characters=100,
        ),
        # Family 3 (single doc)
        ManifestRecord(
            source_key="f3_v1",
            document_family="family-gamma",
            corpus_id="uit",
            version=1,
            staged_filename="f3_v1.pdf",
            file_sha256="h4",
            normalized_text_sha256="th3",
            bytes=10,
            page_count=5,
            extracted_characters=100,
        ),
    ]

    splits = split_corpus_by_family(records, train_ratio=0.5, val_ratio=0.25, test_ratio=0.25, seed=123)

    train_families = {r.document_family for r in splits["train"]}
    val_families = {r.document_family for r in splits["val"]}
    test_families = {r.document_family for r in splits["test"]}

    # Crucial property: disjoint family sets across train, val, and test
    assert train_families.isdisjoint(val_families)
    assert train_families.isdisjoint(test_families)
    assert val_families.isdisjoint(test_families)

    # Both f1_v1 and f1_v2 must be in the same split
    f1_locations = [
        name for name, split_records in splits.items()
        if any(r.document_family == "family-alpha" for r in split_records)
    ]
    assert len(f1_locations) == 1, "Family alpha was leaked across multiple splits!"

