"""Private deployment settings and non-secret deployment state on the VM."""

from __future__ import annotations

import argparse
import base64
from datetime import datetime, timezone
import json
import hashlib
import os
from pathlib import Path
import re

from release import dotenv, read, validate, write

REQUIRED = "API_HOST ACME_EMAIL APP_URL DATABASE_CONNECTION_STRING AI_POSTGRES_DSN AI_POSTGRES_DIRECT_DSN PLAGIARISM_AI_API_KEY PLAGIARISM_AI_DEFAULT_CORPUS REMOTE_GPU_URL REMOTE_GPU_API_KEY MINIO_ROOT_USER MINIO_ROOT_PASSWORD S3_ACCESS_KEY S3_SECRET_KEY AI_S3_ACCESS_KEY AI_S3_SECRET_KEY JWT_PRIVATE_KEY_PEM_BASE64 IDENTITY_IP_HASH_KEY SMTP_HOST SMTP_FROM_ADDRESS".split()
OPTIONAL = "S3_BUCKET_NAME AI_S3_BUCKET_NAME MINIO_IMAGE MINIO_MC_IMAGE AI_CLASSIFIER_ENABLED SMTP_PORT SMTP_FROM_NAME SMTP_USERNAME SMTP_PASSWORD SMTP_SECURITY SMTP_TIMEOUT_SECONDS DATA_PROTECTION_VOLUME CADDY_DATA_VOLUME CADDY_CONFIG_VOLUME MINIO_DATA_VOLUME".split()


def initialize(directory):
    if (directory / "runtime.env").exists():
        raise ValueError("Deployment attempt already exists; use a new attempt ID")
    payload = directory / "settings.json"
    values = read(payload)
    for name in REQUIRED:
        if not values.get(name):
            raise ValueError(f"Missing deployment setting: {name}")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9.-]*[A-Za-z0-9]", values["API_HOST"]):
        raise ValueError("Invalid API_HOST")
    from urllib.parse import urlsplit

    origin = urlsplit(values["APP_URL"])
    if (
        origin.scheme != "https"
        or not origin.hostname
        or origin.username
        or origin.password
        or origin.path not in ("", "/")
        or origin.query
        or origin.fragment
    ):
        raise ValueError("APP_URL must be an HTTPS origin")
    values["APP_URL"] = f"{origin.scheme}://{origin.netloc}"
    if (
        min(len(values["IDENTITY_IP_HASH_KEY"]), len(values["PLAGIARISM_AI_API_KEY"]))
        < 32
    ):
        raise ValueError("Service and HMAC keys must have at least 32 characters")
    manifest = validate(read(directory / "release.json"))
    minio = manifest.get("rollback_policy", {}).get("infrastructure", {}).get("minio")
    if values.get("MINIO_IMAGE") and values["MINIO_IMAGE"] != minio:
        raise ValueError(
            "Change the reviewed release policy and Compose together instead of overriding MINIO_IMAGE"
        )
    if values.get("AI_CLASSIFIER_ENABLED", "") not in ("", "true", "false"):
        raise ValueError("AI_CLASSIFIER_ENABLED must be true or false")
    if (Path.home() / "ugtm/secrets/ugtm-jwt-private.pem").exists() and not values.get(
        "DATA_PROTECTION_VOLUME"
    ):
        raise ValueError(
            "Configure the inspected existing Data Protection volume before cutover"
        )
    settings = {
        name: values.get(name, "")
        for name in REQUIRED + OPTIONAL
        if name != "JWT_PRIVATE_KEY_PEM_BASE64"
    }
    settings.update(
        API_IMAGE=manifest["images"]["api"]["reference"],
        AI_IMAGE=manifest["images"]["ai"]["reference"],
        GATEWAY_IMAGE=(
            manifest["images"]["gateway"]["reference"]
            if "gateway" in manifest["images"]
            else manifest.get("rollback_policy", {})
            .get("infrastructure", {})
            .get("gateway", "")
        ),
        JWT_PRIVATE_KEY_FILE=str(directory / "secrets/jwt.pem"),
        AI_ARTIFACTS_DIR=str(directory.parent.parent / "ai-artifacts"),
    )
    (directory / "secrets").mkdir(mode=0o700)
    key = directory / "secrets/jwt.pem"
    key.write_bytes(
        base64.b64decode(values["JWT_PRIVATE_KEY_PEM_BASE64"], validate=True)
    )
    key.chmod(0o600)
    (directory / "runtime.env").write_text(dotenv(settings), encoding="utf-8")
    (directory / "runtime.env").chmod(0o600)
    write(
        directory / "backend-storage.json",
        {
            "bucket": values.get("S3_BUCKET_NAME") or "ugtm-thesis-submissions",
            "access_key": values["S3_ACCESS_KEY"],
            "secret_key": values["S3_SECRET_KEY"],
        },
    )
    write(
        directory / "public.json",
        {
            "api_host": values["API_HOST"],
            "app_url": values["APP_URL"],
            "classifier_enabled": values.get("AI_CLASSIFIER_ENABLED") or "false",
        },
    )
    # Keep config equality evidence private. Credential, bucket or volume changes disable automatic rollback.
    context = {key: values.get(key, "") for key in REQUIRED + OPTIONAL}
    write(
        directory / "rollback-context.json",
        {
            "sha256": hashlib.sha256(
                json.dumps(context, sort_keys=True).encode()
            ).hexdigest(),
            "runtime_sha256": hashlib.sha256(
                (directory / "runtime.env").read_bytes()
            ).hexdigest(),
            "key_sha256": hashlib.sha256(key.read_bytes()).hexdigest(),
            "artifacts": {},
        },
    )
    payload.unlink()


def compatible(before, after):
    # Conservative: schema-changing releases need a reviewed/manual rollback.
    return (
        before.get("version") == after.get("version")
        and after.get("version") in (1, 2)
        and before.get("schemas") == after.get("schemas")
        and before.get("rollback_policy", {}).get("auto_rollback") is True
        and after.get("rollback_policy", {}).get("auto_rollback") is True
        and before["rollback_policy"]["data_contract"]
        == after["rollback_policy"]["data_contract"]
        and before["rollback_policy"].get("infrastructure")
        == after["rollback_policy"].get("infrastructure")
    )


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument(
        "command",
        choices=[
            "init",
            "state",
            "get",
            "compatible",
            "recoverable",
            "summary",
            "record-artifacts",
            "check-artifacts",
        ],
    )
    parser.add_argument("values", nargs="*")
    args = parser.parse_args()
    directory = args.directory.resolve()
    os.umask(0o077)
    if args.command == "init":
        initialize(directory)
    elif args.command == "get":
        # Only non-secret fields are readable through this interface.
        fields = {
            **read(directory / "public.json"),
            "api_image": read(directory / "release.json")["images"]["api"]["reference"],
            "deployment_status": (
                read(directory / "status.json").get("status", "unknown")
                if (directory / "status.json").exists()
                else "unprepared"
            ),
        }
        print(fields[args.values[0]])
    elif args.command == "state":
        path = directory / "status.json"
        status = (
            read(path)
            if path.exists()
            else {
                "attempt": directory.name,
                "source_sha": read(directory / "release.json")["source_sha"],
                "events": [],
            }
        )
        status["status"] = args.values[0]
        status["events"].append(
            {"stage": args.values[0], "time": datetime.now(timezone.utc).isoformat()}
        )
        write(path, status)
        print(f"Deployment {directory.name}: {args.values[0]}")
    elif args.command in ("record-artifacts", "check-artifacts"):
        import sys

        artifacts = json.load(sys.stdin)
        expected = {
            "suspicious_passage_classifier.joblib",
            "suspicious_passage_classifier.manifest.json",
        }
        if set(artifacts) != expected or any(
            not re.fullmatch(r"[0-9a-f]{64}", digest) for digest in artifacts.values()
        ):
            raise ValueError("Invalid classifier fingerprint")
        context = read(directory / "rollback-context.json")
        if args.command == "record-artifacts":
            context["artifacts"] = artifacts
            write(directory / "rollback-context.json", context)
        else:
            raise SystemExit(0 if context["artifacts"] == artifacts else 1)
    elif args.command in ("compatible", "recoverable"):
        previous = Path(args.values[0])
        context = directory / "rollback-context.json"
        previous_context = previous / "rollback-context.json"

        def intact(path):
            stored = read(path / "rollback-context.json")
            return (
                stored["runtime_sha256"]
                == hashlib.sha256((path / "runtime.env").read_bytes()).hexdigest()
                and stored["key_sha256"]
                == hashlib.sha256((path / "secrets/jwt.pem").read_bytes()).hexdigest()
            )

        safe = (
            context.exists()
            and previous_context.exists()
            and read(context)["sha256"] == read(previous_context)["sha256"]
            and read(context)["artifacts"] == read(previous_context)["artifacts"]
            and intact(previous)
            and intact(directory)
        )
        before, after = read(previous / "release.json"), read(
            directory / "release.json"
        )
        if args.command == "compatible":
            safe = safe and compatible(before, after)
        else:
            # Forward recovery permits schema differences, never different storage,
            # credentials, artifacts or data contracts. New images must still pass
            # their real read-only DB gates before the VM transfers pending ownership.
            safe = (
                safe
                and before.get("version") == after.get("version")
                and after.get("version") in (1, 2)
                and before["rollback_policy"]["data_contract"]
                == after["rollback_policy"]["data_contract"]
                and before["rollback_policy"].get("infrastructure")
                == after["rollback_policy"].get("infrastructure")
            )
        raise SystemExit(0 if safe else 1)
    else:
        print(json.dumps(read(directory / "status.json")))


if __name__ == "__main__":
    main()
