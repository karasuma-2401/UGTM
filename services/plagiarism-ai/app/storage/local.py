from __future__ import annotations

import logging
import shutil
from pathlib import Path

from app.config import settings
from app.storage.base import (
    StorageBackend,
    StorageError,
    StorageFileNotFoundError,
)

logger = logging.getLogger(__name__)


class LocalStorageBackend:
    """Local filesystem storage backend for local testing and offline fallback."""

    name = "local"

    def __init__(self, base_dir: Path | None = None):
        self.base_dir = (base_dir or (settings.data_dir / "storage")).resolve()
        self.base_dir.mkdir(parents=True, exist_ok=True)

    def _resolve(self, object_key: str) -> Path:
        key = object_key.lstrip("/\\").replace("\\", "/")
        path = (self.base_dir / key).resolve()
        if not str(path).startswith(str(self.base_dir)):
            raise StorageError(f"Path traversal detected in object key: {object_key}")
        return path

    def upload_file(
        self,
        local_path: Path,
        object_key: str,
        content_type: str | None = None,
    ) -> str:
        target = self._resolve(object_key)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(str(local_path), str(target))
        return object_key

    def download_file(
        self,
        object_key: str,
        target_path: Path,
    ) -> Path:
        source = self._resolve(object_key)
        if not source.is_file():
            raise StorageFileNotFoundError(f"Local storage object '{object_key}' not found")
        target_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(str(source), str(target_path))
        return target_path

    def get_presigned_url(
        self,
        object_key: str,
        expires_in: int = 3600,
    ) -> str:
        source = self._resolve(object_key)
        return source.as_uri()

    def exists(self, object_key: str) -> bool:
        return self._resolve(object_key).is_file()

    def delete_file(self, object_key: str) -> bool:
        target = self._resolve(object_key)
        if target.is_file():
            target.unlink()
            return True
        return False

    def get_file_size(self, object_key: str) -> int:
        source = self._resolve(object_key)
        if not source.is_file():
            raise StorageFileNotFoundError(f"Local storage object '{object_key}' not found")
        return source.stat().st_size

