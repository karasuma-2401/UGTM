"""Copy objects from the configured S3/R2 source into a private MinIO bucket.

Run from the project root. Source credentials use S3_* / R2_* environment variables
or the AI project's .env. Destination credentials use AI_MINIO_* variables.
The destination bucket must already exist. Source objects are never deleted.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import tempfile
from dataclasses import dataclass
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent

from dotenv import load_dotenv
import boto3
from botocore.config import Config
from botocore.exceptions import ClientError


@dataclass
class CopyStore:
    bucket: str
    endpoint_url: str | None
    client: object


def copy_stores() -> tuple[CopyStore, CopyStore]:
    # This utility needs storage only; importing app.storage initializes DB/API settings.
    load_dotenv(PROJECT_ROOT / ".env")

    def source_value(suffix: str) -> str:
        return os.environ.get("S3_" + suffix) or os.environ.get("R2_" + suffix, "")

    endpoint = source_value("ENDPOINT_URL")
    if not endpoint and os.environ.get("R2_ACCOUNT_ID"):
        endpoint = f"https://{os.environ['R2_ACCOUNT_ID']}.r2.cloudflarestorage.com"
    source_values = (
        endpoint, source_value("BUCKET_NAME"), source_value("ACCESS_KEY_ID"),
        source_value("SECRET_ACCESS_KEY"),
    )
    names = ("AI_MINIO_ENDPOINT", "AI_MINIO_BUCKET", "AI_MINIO_ACCESS_KEY", "AI_MINIO_SECRET_KEY")
    missing = [name for name in names if not os.environ.get(name)]
    missing += [
        name for name, value in zip(
            ("S3_ENDPOINT_URL/R2_ENDPOINT_URL", "S3_BUCKET_NAME/R2_BUCKET_NAME",
             "S3_ACCESS_KEY_ID/R2_ACCESS_KEY_ID", "S3_SECRET_ACCESS_KEY/R2_SECRET_ACCESS_KEY"),
            source_values,
        ) if not value
    ]
    if missing:
        raise SystemExit("Missing storage settings: " + ", ".join(missing))

    def store(endpoint_url, bucket, access_key, secret_key, region):
        return CopyStore(bucket, endpoint_url, boto3.client(
            "s3", endpoint_url=endpoint_url, aws_access_key_id=access_key,
            aws_secret_access_key=secret_key, region_name=region,
            config=Config(signature_version="s3v4", s3={"addressing_style": "path"},
                          connect_timeout=15, read_timeout=60,
                          retries={"max_attempts": 3, "mode": "standard"}),
        ))

    return (
        store(*source_values, os.environ.get("S3_REGION_NAME", "auto")),
        store(*(os.environ[name] for name in names), "us-east-1"),
    )


def sha256_body(body) -> str:
    digest = hashlib.sha256()
    try:
        while block := body.read(1024 * 1024):
            digest.update(block)
    finally:
        body.close()
    return digest.hexdigest()


def object_hash(store: CopyStore, key: str) -> str:
    return sha256_body(store.client.get_object(Bucket=store.bucket, Key=key)["Body"])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--execute", action="store_true", help="Copy after a dry run and backup")
    args = parser.parse_args()
    source, target = copy_stores()
    if source.endpoint_url == target.endpoint_url and source.bucket == target.bucket:
        raise SystemExit("Source and destination must be different buckets or endpoints")

    # Check access before enumerating. Never create or delete the destination bucket.
    target.client.head_bucket(Bucket=target.bucket)
    copied = existing = objects = total_bytes = 0
    pages = source.client.get_paginator("list_objects_v2").paginate(Bucket=source.bucket)
    for page in pages:
        for obj in page.get("Contents", []):
            key, size = obj["Key"], obj["Size"]
            objects += 1
            total_bytes += size
            if not args.execute:
                continue
            try:
                target.client.head_object(Bucket=target.bucket, Key=key)
                exists = True
            except ClientError as exc:
                if exc.response.get("ResponseMetadata", {}).get("HTTPStatusCode") != 404:
                    raise
                exists = False
            if exists:
                if object_hash(source, key) != object_hash(target, key):
                    raise RuntimeError(f"Destination conflict at object #{objects}; no object was overwritten")
                existing += 1
                continue
            with tempfile.TemporaryFile() as temp:
                response = source.client.get_object(Bucket=source.bucket, Key=key, IfMatch=obj["ETag"])
                body = response["Body"]
                digest = hashlib.sha256()
                try:
                    while block := body.read(1024 * 1024):
                        temp.write(block)
                        digest.update(block)
                finally:
                    body.close()
                temp.seek(0)
                # Prevent overwriting if another process created this key after HEAD.
                metadata = {
                    name: response[name]
                    for name in ("ContentType", "ContentEncoding", "ContentDisposition", "CacheControl", "Metadata")
                    if name in response
                }
                target.client.put_object(Bucket=target.bucket, Key=key, Body=temp, IfNoneMatch="*", **metadata)
                if object_hash(target, key) != digest.hexdigest():
                    raise RuntimeError(f"Checksum mismatch at object #{objects}; source was not deleted")
            copied += 1
            print(f"Verified {copied + existing}/{objects} objects", flush=True)

    action = "Copy verified" if args.execute else "Dry run"
    print(f"{action}: objects={objects}, bytes={total_bytes}, copied={copied}, already_matching={existing}")


if __name__ == "__main__":
    main()
