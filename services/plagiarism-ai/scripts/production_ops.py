"""Bounded production operations. Never logs driver errors or credentials."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import time


def drain(timeout, worker):
    from app.celery_app import celery_app
    from app.config import settings

    if not worker:
        raise ValueError("An inspected production worker identity is required")
    inspector = celery_app.control.inspect(destination=[worker], timeout=5)
    nodes = inspector.ping()
    if not nodes or set(nodes) != {worker}:
        raise RuntimeError("Expected exactly one responsive production worker")
    destinations = list(nodes)
    try:
        replies = celery_app.control.cancel_consumer(
            settings.celery_queue, destination=destinations, reply=True, timeout=5
        )
        if not replies or any(
            "error" in reply for item in replies for reply in item.values()
        ):
            raise RuntimeError("Worker did not acknowledge pausing consumption")
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            active = celery_app.control.inspect(
                destination=destinations, timeout=5
            ).active()
            reserved = celery_app.control.inspect(
                destination=destinations, timeout=5
            ).reserved()
            scheduled = celery_app.control.inspect(
                destination=destinations, timeout=5
            ).scheduled()
            if active is None or reserved is None or scheduled is None:
                raise RuntimeError("Worker stopped responding during drain")
            if (
                not any(active.values())
                and not any(reserved.values())
                and not any(scheduled.values())
            ):
                print("Production worker drained; queued jobs remain in Redis.")
                return
            time.sleep(3)
        raise RuntimeError("Worker drain deadline exceeded")
    except Exception:
        celery_app.control.add_consumer(
            settings.celery_queue, destination=destinations, reply=False
        )
        raise


def metrics():
    import redis
    from app.config import settings
    from app.db import connect

    broker = redis.Redis.from_url(
        settings.redis_url, socket_connect_timeout=5, socket_timeout=5
    )
    with connect(direct=True) as connection:
        connection.execute("SET statement_timeout = '10s'")
        rows = connection.execute(
            """SELECT status, count(*) AS count,
            coalesce(extract(epoch FROM now() - min(created_at)), 0) AS oldest_seconds
            FROM integrity.jobs WHERE status IN ('queued','processing','waiting_for_compute') GROUP BY status"""
        ).fetchall()
    return {
        "time": datetime.now(timezone.utc).isoformat(),
        "queue_depth": broker.llen(settings.celery_queue),
        "jobs": [
            {
                "status": r["status"],
                "count": r["count"],
                "oldest_seconds": float(r["oldest_seconds"]),
            }
            for r in rows
        ],
    }


def backup_objects(destination):
    """Download using ETag preconditions; never map untrusted object keys to paths."""
    import boto3
    import os

    endpoint = "http://minio:9000"
    client = boto3.client(
        "s3",
        endpoint_url=endpoint,
        aws_access_key_id=os.environ["BACKUP_MINIO_USER"],
        aws_secret_access_key=os.environ["BACKUP_MINIO_PASSWORD"],
    )
    destination.mkdir(parents=True, exist_ok=True)
    objects = []
    buckets = os.environ["BACKUP_BUCKETS"].split(",")
    for bucket in buckets:
        for page in client.get_paginator("list_objects_v2").paginate(Bucket=bucket):
            for item in page.get("Contents", []):
                key = item["Key"]
                target = hashlib.sha256((bucket + "\0" + key).encode()).hexdigest()
                response = client.get_object(
                    Bucket=bucket, Key=key, IfMatch=item["ETag"]
                )
                digest = hashlib.sha256()
                with (destination / target).open("wb") as output:
                    for block in response["Body"].iter_chunks(1024 * 1024):
                        if block:
                            output.write(block)
                            digest.update(block)
                response["Body"].close()
                objects.append(
                    {
                        "bucket": bucket,
                        "key": key,
                        "file": target,
                        "sha256": digest.hexdigest(),
                        "size": item["Size"],
                        "metadata": response.get("Metadata", {}),
                        "content_type": response.get(
                            "ContentType", "application/octet-stream"
                        ),
                    }
                )
    (destination / "objects.json").write_text(json.dumps(objects), encoding="utf-8")
    (destination / "buckets.json").write_text(json.dumps(buckets), encoding="utf-8")


def restore_objects(directory):
    import boto3
    import os

    client = boto3.client(
        "s3",
        endpoint_url="http://restore-minio:9000",
        aws_access_key_id=os.environ["BACKUP_MINIO_USER"],
        aws_secret_access_key=os.environ["BACKUP_MINIO_PASSWORD"],
    )
    for attempt in range(30):
        try:
            client.list_buckets()
            break
        except Exception:
            time.sleep(2)
    else:
        raise RuntimeError("Isolated MinIO did not start")
    records = json.loads((directory / "objects.json").read_text())
    for bucket in json.loads((directory / "buckets.json").read_text()):
        client.create_bucket(Bucket=bucket)
    for record in records:
        if len(record["file"]) != 64 or any(
            c not in "0123456789abcdef" for c in record["file"]
        ):
            raise ValueError("Invalid backup object filename")
        data = (directory / record["file"]).read_bytes()
        if (
            len(data) != record["size"]
            or hashlib.sha256(data).hexdigest() != record["sha256"]
        ):
            raise RuntimeError("Backup object checksum mismatch")
        client.put_object(
            Bucket=record["bucket"],
            Key=record["key"],
            Body=data,
            Metadata=record["metadata"],
            ContentType=record["content_type"],
        )
        restored = client.get_object(Bucket=record["bucket"], Key=record["key"])[
            "Body"
        ].read()
        if hashlib.sha256(restored).hexdigest() != record["sha256"]:
            raise RuntimeError("Restored object checksum mismatch")
    print(f"Restored and verified {len(records)} objects in isolated MinIO.")


def classifier_fingerprint(directory=Path("/artifacts")):
    return {
        name: hashlib.sha256((directory / name).read_bytes()).hexdigest()
        for name in (
            "suspicious_passage_classifier.joblib",
            "suspicious_passage_classifier.manifest.json",
        )
    }


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument(
        "command",
        choices=[
            "drain",
            "metrics",
            "backup-objects",
            "restore-objects",
            "check-ugtm-storage",
            "classifier-fingerprint",
        ],
    )
    parser.add_argument("--timeout", type=int, default=300)
    parser.add_argument("--worker")
    parser.add_argument("--directory", type=Path, default=Path("/backup/objects"))
    args = parser.parse_args()
    try:
        if args.command == "drain":
            drain(args.timeout, args.worker)
        elif args.command == "metrics":
            print(json.dumps(metrics()))
        elif args.command == "backup-objects":
            backup_objects(args.directory)
        elif args.command == "restore-objects":
            restore_objects(args.directory)
        elif args.command == "classifier-fingerprint":
            print(json.dumps(classifier_fingerprint()))
        else:
            import boto3
            import sys

            values = json.load(sys.stdin)
            client = boto3.client(
                "s3",
                endpoint_url="http://minio:9000",
                aws_access_key_id=values["access_key"],
                aws_secret_access_key=values["secret_key"],
            )
            client.head_bucket(Bucket=values["bucket"])
            print("UGTM bucket preflight passed (read-only).")
    except Exception:
        print(
            f"Production operation failed: {args.command}. Inspect infrastructure without publishing secrets."
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
