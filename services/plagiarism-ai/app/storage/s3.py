from __future__ import annotations

import logging
from pathlib import Path

import boto3
from botocore.client import Config
from botocore.exceptions import ClientError, EndpointConnectionError

from app.config import settings
from app.storage.base import (
    StorageBackend,
    StorageConfigurationError,
    StorageError,
    StorageFileNotFoundError,
)

logger = logging.getLogger(__name__)


class S3StorageBackend:
    """S3-compatible object storage backend (Cloudflare R2, MinIO, AWS S3)."""

    name = "s3"

    def __init__(
        self,
        *,
        bucket_name: str | None = None,
        endpoint_url: str | None = None,
        access_key_id: str | None = None,
        secret_access_key: str | None = None,
        region_name: str | None = None,
    ):
        self.bucket = (
            bucket_name
            if bucket_name is not None
            else settings.effective_s3_bucket
        ).strip()
        self.endpoint_url = (
            endpoint_url
            if endpoint_url is not None
            else settings.effective_s3_endpoint
        )
        self.access_key_id = (
            access_key_id
            if access_key_id is not None
            else settings.effective_s3_access_key
        )
        self.secret_access_key = (
            secret_access_key
            if secret_access_key is not None
            else settings.effective_s3_secret_key
        )
        self.region_name = (
            region_name
            if region_name is not None
            else settings.s3_region_name or "auto"
        )

        if not self.bucket:
            raise StorageConfigurationError("S3 bucket name is not configured")
        if not self.access_key_id or not self.secret_access_key:
            raise StorageConfigurationError(
                "S3 access key ID or secret access key is missing"
            )

        client_kwargs = {
            "service_name": "s3",
            "aws_access_key_id": self.access_key_id,
            "aws_secret_access_key": self.secret_access_key,
            "region_name": self.region_name,
            "config": Config(
                signature_version="s3v4",
                s3={"addressing_style": "path"},
                connect_timeout=15,
                read_timeout=60,
                retries={"max_attempts": 3, "mode": "standard"},
            ),
        }
        if self.endpoint_url:
            client_kwargs["endpoint_url"] = self.endpoint_url

        self.client = boto3.client(**client_kwargs)

    def upload_file(
        self,
        local_path: Path,
        object_key: str,
        content_type: str | None = None,
    ) -> str:
        key = object_key.lstrip("/")
        extra_args = {}
        if content_type:
            extra_args["ContentType"] = content_type

        try:
            self.client.upload_file(
                str(local_path),
                self.bucket,
                key,
                ExtraArgs=extra_args if extra_args else None,
            )
            logger.info("Uploaded %s to s3://%s/%s", local_path, self.bucket, key)
            return key
        except EndpointConnectionError as exc:
            raise StorageError(
                f"Cannot connect to S3/R2 endpoint: {exc}", code="STORAGE_UNAVAILABLE"
            ) from exc
        except ClientError as exc:
            code = exc.response.get("Error", {}).get("Code", "UNKNOWN")
            raise StorageError(
                f"Failed to upload to s3://{self.bucket}/{key}: {exc} ({code})",
                code=f"STORAGE_{code}",
            ) from exc

    def download_file(
        self,
        object_key: str,
        target_path: Path,
    ) -> Path:
        key = object_key.lstrip("/")
        target_path.parent.mkdir(parents=True, exist_ok=True)
        try:
            self.client.download_file(self.bucket, key, str(target_path))
            return target_path
        except ClientError as exc:
            code = exc.response.get("Error", {}).get("Code", "UNKNOWN")
            if code in ("404", "NoSuchKey", "NotFound"):
                raise StorageFileNotFoundError(
                    f"Object s3://{self.bucket}/{key} not found",
                    code="STORAGE_FILE_NOT_FOUND",
                ) from exc
            raise StorageError(
                f"Failed to download s3://{self.bucket}/{key}: {exc} ({code})",
                code=f"STORAGE_{code}",
            ) from exc

    def get_presigned_url(
        self,
        object_key: str,
        expires_in: int = 3600,
    ) -> str:
        key = object_key.lstrip("/")
        try:
            url = self.client.generate_presigned_url(
                "get_object",
                Params={"Bucket": self.bucket, "Key": key},
                ExpiresIn=expires_in,
            )
            return url
        except ClientError as exc:
            raise StorageError(f"Failed to generate presigned URL for {key}: {exc}") from exc

    def exists(self, object_key: str) -> bool:
        key = object_key.lstrip("/")
        try:
            self.client.head_object(Bucket=self.bucket, Key=key)
            return True
        except ClientError as exc:
            code = exc.response.get("Error", {}).get("Code", "UNKNOWN")
            if code in ("404", "NoSuchKey", "NotFound"):
                return False
            raise StorageError(f"Error checking object s3://{self.bucket}/{key}: {exc}") from exc

    def delete_file(self, object_key: str) -> bool:
        key = object_key.lstrip("/")
        try:
            self.client.delete_object(Bucket=self.bucket, Key=key)
            return True
        except ClientError as exc:
            raise StorageError(f"Failed to delete s3://{self.bucket}/{key}: {exc}") from exc

    def get_file_size(self, object_key: str) -> int:
        key = object_key.lstrip("/")
        try:
            res = self.client.head_object(Bucket=self.bucket, Key=key)
            return int(res.get("ContentLength", 0))
        except ClientError as exc:
            code = exc.response.get("Error", {}).get("Code", "UNKNOWN")
            if code in ("404", "NoSuchKey", "NotFound"):
                raise StorageFileNotFoundError(f"Object s3://{self.bucket}/{key} not found") from exc
            raise StorageError(f"Error fetching size of s3://{self.bucket}/{key}: {exc}") from exc
