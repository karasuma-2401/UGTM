from __future__ import annotations

import hashlib
import tempfile
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from app.storage import (
    LocalStorageBackend,
    StorageConfigurationError,
    StorageError,
    StorageFileNotFoundError,
    download_to_temp_file,
    object_key_for_corpus,
    object_key_for_submission,
)
from app.storage.s3 import S3StorageBackend


def test_object_key_conventions():
    sub_key = object_key_for_submission("job-123", "my_thesis.docx", "a" * 64)
    assert sub_key == f"submissions/job-123/{'a'*16}_my_thesis.docx"

    corp_key = object_key_for_corpus("uit_thesis", "doc-456", 2, "source.pdf")
    assert corp_key == "corpus/uit_thesis/doc-456/v2/source.pdf"


def test_local_storage_backend(tmp_path):
    storage = LocalStorageBackend(base_dir=tmp_path)

    sample_file = tmp_path / "origin.txt"
    sample_file.write_text("Hello UGTM Storage", encoding="utf-8")

    key = "folder/sample.txt"
    uploaded = storage.upload_file(sample_file, key)
    assert uploaded == key
    assert storage.exists(key) is True
    assert storage.get_file_size(key) == len("Hello UGTM Storage")

    # Download
    download_target = tmp_path / "downloads" / "retrieved.txt"
    storage.download_file(key, download_target)
    assert download_target.is_file()
    assert download_target.read_text(encoding="utf-8") == "Hello UGTM Storage"

    # Presigned URL format
    url = storage.get_presigned_url(key)
    assert url.startswith("file://")

    # Delete
    assert storage.delete_file(key) is True
    assert storage.exists(key) is False

    with pytest.raises(StorageFileNotFoundError):
        storage.download_file("non_existent.txt", tmp_path / "x.txt")


def test_local_storage_path_traversal_prevention(tmp_path):
    storage = LocalStorageBackend(base_dir=tmp_path / "store")
    with pytest.raises(StorageError, match="Path traversal"):
        storage._resolve("../../etc/passwd")


def test_download_to_temp_file_checksum_validation(tmp_path):
    source = tmp_path / "source.txt"
    source.write_text("Integrity Check Data", encoding="utf-8")
    valid_sha = hashlib.sha256(b"Integrity Check Data").hexdigest()
    invalid_sha = "0" * 64

    # Valid SHA
    with download_to_temp_file(str(source), expected_sha256=valid_sha) as temp_f:
        assert temp_f.is_file()
        assert temp_f.read_text(encoding="utf-8") == "Integrity Check Data"

    # Invalid SHA should raise and cleanup
    with pytest.raises(StorageError, match="Checksum mismatch"):
        with download_to_temp_file(str(source), expected_sha256=invalid_sha):
            pass


def test_s3_storage_missing_config_raises():
    with pytest.raises(StorageConfigurationError):
        S3StorageBackend(bucket_name="", access_key_id="", secret_access_key="")

