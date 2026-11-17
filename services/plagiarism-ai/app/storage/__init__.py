from __future__ import annotations

import hashlib
import logging
import os
import shutil
import tempfile
from contextlib import contextmanager
from pathlib import Path
from typing import Generator
from urllib.parse import urlparse

import requests

from app.config import settings
from app.storage.base import (
    StorageBackend,
    StorageConfigurationError,
    StorageError,
    StorageFileNotFoundError,
)
from app.storage.local import LocalStorageBackend
from app.storage.s3 import S3StorageBackend

logger = logging.getLogger(__name__)

__all__ = [
    "LocalStorageBackend",
    "S3StorageBackend",
    "StorageBackend",
    "StorageConfigurationError",
    "StorageError",
    "StorageFileNotFoundError",
    "download_to_temp_file",
    "get_storage",
    "object_key_for_corpus",
    "object_key_for_submission",
]

_STORAGE_INSTANCE: StorageBackend | None = None


def reset_storage_instance() -> None:
    global _STORAGE_INSTANCE
    _STORAGE_INSTANCE = None


def get_storage() -> StorageBackend:
    """Retrieve singleton StorageBackend configured by environment."""
    global _STORAGE_INSTANCE
    if _STORAGE_INSTANCE is not None:
        return _STORAGE_INSTANCE

    backend_type = settings.effective_storage_backend
    if backend_type == "s3":
        _STORAGE_INSTANCE = S3StorageBackend()
    elif backend_type == "local":
        _STORAGE_INSTANCE = LocalStorageBackend()
    else:
        raise StorageConfigurationError(f"Unknown storage backend: {backend_type}")

    return _STORAGE_INSTANCE


def object_key_for_submission(job_id: str, filename: str, sha256: str) -> str:
    """Standardized S3 object key convention for student thesis submissions."""
    safe_filename = Path(filename).name
    return f"submissions/{job_id}/{sha256[:16]}_{safe_filename}"


def object_key_for_corpus(
    corpus_id: str, source_key: str, version: int, filename: str
) -> str:
    """Standardized S3 object key convention for corpus documents."""
    safe_filename = Path(filename).name
    return f"corpus/{corpus_id}/{source_key}/v{version}/{safe_filename}"


@contextmanager
def download_to_temp_file(
    file_reference: str,
    *,
    expected_sha256: str | None = None,
    suffix: str | None = None,
) -> Generator[Path, None, None]:
    """Context manager that downloads an object key or HTTPS URL to a local temporary file,

    yields the Path for processing, and guarantees cleanup on exit.
    """
    ext = suffix or Path(file_reference).suffix or ".tmp"
    temp_file = tempfile.NamedTemporaryFile(suffix=ext, delete=False)
    temp_path = Path(temp_file.name)
    temp_file.close()

    try:
        parsed = urlparse(file_reference)
        # Case 1: HTTPS URL (e.g. from UGTM Presigned URL or S3 presigned URL)
        if parsed.scheme in ("http", "https"):
            logger.info("Downloading file from remote URL: %s...", file_reference[:60])
            with requests.get(file_reference, stream=True, timeout=60) as res:
                res.raise_for_status()
                with temp_path.open("wb") as out:
                    for chunk in res.iter_content(chunk_size=1024 * 1024):
                        if chunk:
                            out.write(chunk)
        # Case 2: Existing local file path (for offline tests / local dev)
        elif Path(file_reference).is_file():
            shutil.copy2(file_reference, str(temp_path))
        # Case 3: S3 / R2 object key
        else:
            storage = get_storage()
            storage.download_file(file_reference, temp_path)

        # Integrity check if expected_sha256 is provided
        if expected_sha256:
            hasher = hashlib.sha256()
            with temp_path.open("rb") as f:
                while chunk := f.read(1024 * 1024):
                    hasher.update(chunk)
            actual_sha = hasher.hexdigest().lower()
            if actual_sha != expected_sha256.lower():
                raise StorageError(
                    f"Checksum mismatch for {file_reference}: "
                    f"expected {expected_sha256}, got {actual_sha}",
                    code="FILE_CHECKSUM_MISMATCH",
                )

        yield temp_path
    finally:
        if temp_path.is_file():
            try:
                temp_path.unlink()
            except Exception as exc:
                logger.warning("Failed to delete temp file %s: %s", temp_path, exc)

