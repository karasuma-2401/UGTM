"""Copy objects from the configured S3/R2 source into a private MinIO bucket.

Run from the project root. Source credentials come from the app's normal storage
settings. Destination credentials are separate AI_MINIO_* environment variables.
The destination bucket must already exist. Source objects are never deleted.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import sys
import tempfile
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT_ROOT))

from dotenv import load_dotenv
from botocore.exceptions import ClientError

from app.storage.s3 import S3StorageBackend


def sha256_body(body) -> str:
    digest = hashlib.sha256()
    try:
        while block := body.read(1024 * 1024):
            digest.update(block)
    finally:
        body.close()
    return digest.hexdigest()


def object_hash(store: S3StorageBackend, key: str) -> str:
    return sha256_body(store.client.get_object(Bucket=store.bucket, Key=key)["Body"])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--execute", action="store_true", help="Copy after a dry run and backup")
    args = parser.parse_args()
    load_dotenv(PROJECT_ROOT / ".env")

    names = ("AI_MINIO_ENDPOINT", "AI_MINIO_BUCKET", "AI_MINIO_ACCESS_KEY", "AI_MINIO_SECRET_KEY")
    missing = [name for name in names if not os.environ.get(name)]
    if missing:
        raise SystemExit("Missing destination settings: " + ", ".join(missing))

    source = S3StorageBackend()
    target = S3StorageBackend(
        endpoint_url=os.environ["AI_MINIO_ENDPOINT"],
        bucket_name=os.environ["AI_MINIO_BUCKET"],
        access_key_id=os.environ["AI_MINIO_ACCESS_KEY"],
        secret_access_key=os.environ["AI_MINIO_SECRET_KEY"],
        region_name="us-east-1",
    )
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
