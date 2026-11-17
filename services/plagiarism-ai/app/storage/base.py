from __future__ import annotations

import logging
from contextlib import contextmanager
from pathlib import Path
from typing import Generator, Protocol

logger = logging.getLogger(__name__)


class StorageError(Exception):
    """Base exception for storage backend operations."""

    default_code = "STORAGE_ERROR"

    def __init__(self, message: str, code: str | None = None):
        super().__init__(message)
        self.code = code or self.default_code


class StorageFileNotFoundError(StorageError):
    default_code = "STORAGE_FILE_NOT_FOUND"


class StorageConfigurationError(StorageError):
    default_code = "STORAGE_CONFIG_ERROR"


class StorageBackend(Protocol):
    """Protocol for abstract storage backends (Cloudflare R2, S3, Azure Blob, Local)."""

    name: str

    def upload_file(
        self,
        local_path: Path,
        object_key: str,
        content_type: str | None = None,
    ) -> str:
        """Upload a local file to storage and return the object key."""
        ...

    def download_file(
        self,
        object_key: str,
        target_path: Path,
    ) -> Path:
        """Download an object from storage to a local path."""
        ...

    def get_presigned_url(
        self,
        object_key: str,
        expires_in: int = 3600,
    ) -> str:
        """Generate a time-limited HTTPS download URL for the object."""
        ...

    def exists(self, object_key: str) -> bool:
        """Check if an object exists in storage."""
        ...

    def delete_file(self, object_key: str) -> bool:
        """Delete an object from storage."""
        ...

    def get_file_size(self, object_key: str) -> int:
        """Retrieve the byte size of an object."""
        ...

