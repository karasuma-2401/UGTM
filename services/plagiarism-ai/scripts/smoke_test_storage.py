"""Smoke test script for Cloudflare R2 / S3-compatible Object Storage (CP-01.4).

Verifies:
1. Bucket connectivity and credentials (R2/S3).
2. File upload with object key convention.
3. Existence and object metadata retrieval.
4. Presigned HTTPS URL generation and download test.
5. Temp file context manager and automatic cleanup on worker exit.
6. Object deletion.
"""

from __future__ import annotations

import hashlib
import json
import logging
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Any

import requests

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from app.config import settings
from app.storage import (
    download_to_temp_file,
    get_storage,
    object_key_for_submission,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger("smoke_test_storage")


def run_storage_smoke_test() -> dict[str, Any]:
    storage = get_storage()
    logger.info("Storage backend initialized: %s", storage.name)

    test_content = (
        f"UGTM Plagiarism AI Storage Verification {uuid.uuid4().hex}\n"
        "Testing Cloudflare R2 S3-compatible integration for thesis files."
    ).encode("utf-8")
    expected_sha = hashlib.sha256(test_content).hexdigest()
    expected_len = len(test_content)

    with tempfile.NamedTemporaryFile(suffix=".txt", delete=False) as f:
        f.write(test_content)
        temp_upload_path = Path(f.name)

    test_job_id = str(uuid.uuid4())
    object_key = object_key_for_submission(
        test_job_id, "thesis_sample.txt", expected_sha
    )
    logger.info("Target object key: %s", object_key)

    try:
        # 1. Upload
        logger.info("1. Uploading file to storage...")
        uploaded_key = storage.upload_file(
            temp_upload_path, object_key, content_type="text/plain"
        )
        assert uploaded_key == object_key

        # 2. Exists & Size
        logger.info("2. Checking existence and file size...")
        assert storage.exists(object_key) is True, "Expected object to exist"
        size = storage.get_file_size(object_key)
        assert size == expected_len, f"Expected size {expected_len}, got {size}"
        logger.info("Object verified: size=%d bytes", size)

        # 3. Presigned URL
        logger.info("3. Generating presigned URL and testing HTTP GET...")
        presigned_url = storage.get_presigned_url(object_key, expires_in=300)
        assert presigned_url.startswith("http"), "Expected valid HTTP(S) URL"
        res = requests.get(presigned_url, timeout=15)
        assert res.status_code == 200, f"Presigned URL GET failed with {res.status_code}"
        assert res.content == test_content, "Presigned URL content mismatch"
        logger.info("Presigned URL verified successfully (HTTP 200)")

        # 4. Context manager download & auto-cleanup
        logger.info("4. Testing download_to_temp_file with auto-cleanup...")
        temp_captured_path: Path | None = None
        with download_to_temp_file(
            object_key, expected_sha256=expected_sha, suffix=".txt"
        ) as local_temp:
            temp_captured_path = local_temp
            assert local_temp.is_file(), "Downloaded temp file should exist"
            assert local_temp.read_bytes() == test_content, "Downloaded content mismatch"
            logger.info("Inside context: temp file verified (%s)", local_temp)

        assert (
            not temp_captured_path.is_file()
        ), "Temp file was NOT cleaned up after context exit!"
        logger.info("Outside context: temp file verified deleted")

        # 5. Delete
        logger.info("5. Deleting test object from storage...")
        storage.delete_file(object_key)
        assert (
            storage.exists(object_key) is False
        ), "Expected object to be deleted"
        logger.info("Object deleted successfully")

        return {
            "status": "PASSED",
            "backend": storage.name,
            "bucket": getattr(storage, "bucket", "local"),
            "object_key_tested": object_key,
            "size_bytes": size,
            "sha256": expected_sha,
            "presigned_url": "verified",
            "auto_cleanup": "verified",
        }
    finally:
        temp_upload_path.unlink(missing_ok=True)
        try:
            storage.delete_file(object_key)
        except Exception:
            pass


def main():
    print("==================================================")
    print("UGTM AI — Object Storage Smoke Test (CP-01.4)")
    print(f"Configured Backend: {settings.effective_storage_backend}")
    print(f"Target Bucket:      {settings.effective_s3_bucket}")
    print(f"Endpoint URL:       {settings.effective_s3_endpoint or '<default S3>'}")
    print("==================================================")

    results = run_storage_smoke_test()
    print("\n=== STORAGE SMOKE TEST SUMMARY ===")
    print(json.dumps(results, indent=2))
    print("==================================\n")


if __name__ == "__main__":
    main()

