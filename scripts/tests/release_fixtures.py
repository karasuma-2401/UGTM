"""Synthetic release data. Never reads local .env or developer credentials."""

import base64
from datetime import datetime, timezone
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from production_metadata import REQUIRED


def manifest():
    return {
        "version": 2,
        "source_sha": "a" * 40,
        "repository": "example/ugtm",
        "branch": "production",
        "publish_run_id": "100",
        "publish_run_attempt": 1,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "schemas": {"ai": {"001_init": "checksum"}, "ugtm": {"initial": "checksum"}},
        "rollback_policy": {"data_contract": 1, "auto_rollback": True},
        "images": {
            name: {
                "reference": f"ghcr.io/example/ugtm-{name}@sha256:" + character * 64,
                "platforms": {"arm64": "sha256:" + character * 64},
                "input_hash": character * 64,
                "built_from": "a" * 40,
                "built_at": datetime.now(timezone.utc).isoformat(),
            }
            for name, character in [("api", "a"), ("ai", "b"), ("gateway", "c")]
        },
    }


def settings():
    values = {key: "synthetic-test-only" for key in REQUIRED}
    values.update(
        API_HOST="api.example.test",
        APP_URL="https://frontend.example.test",
        ACME_EMAIL="ops@example.test",
        DATABASE_CONNECTION_STRING="Host=database;Database=ugtm;Username=test;Password=ci-only",
        AI_POSTGRES_DSN="postgresql://test:ci-only@database/ai",
        AI_POSTGRES_DIRECT_DSN="postgresql://test:ci-only@database/ai",
        IDENTITY_IP_HASH_KEY="a" * 32,
        PLAGIARISM_AI_API_KEY="b" * 32,
        REDIS_CACHE_PASSWORD="c" * 64,
        JWT_PRIVATE_KEY_PEM_BASE64=base64.b64encode(b"synthetic-test-key").decode(),
        SMTP_PASSWORD="literal$Dollar#Hash'Quote\\Backslash\"DoubleQuote and spaces\tTab",
        S3_SECRET_KEY="trailing-backslash\\",
        AI_S3_SECRET_KEY="slash\\'quote\" and $$dollars",
    )
    return values
