import os
import subprocess
import sys
from pathlib import Path
from unittest.mock import MagicMock

from scripts import copy_storage_to_minio as copy_tool


def test_copy_help_does_not_initialize_application_settings(tmp_path):
    env = {key: value for key, value in os.environ.items()
           if key not in ("POSTGRES_DSN", "REDIS_URL", "API_KEY")}
    result = subprocess.run(
        [sys.executable, str(Path(copy_tool.__file__).resolve()), "--help"],
        cwd=tmp_path, env=env, capture_output=True, text=True,
    )
    assert result.returncode == 0, result.stderr


def test_dry_run_requires_only_storage_and_never_writes(monkeypatch, capsys):
    monkeypatch.setattr(copy_tool, "load_dotenv", lambda *args: None)
    for name in ("POSTGRES_DSN", "REDIS_URL", "API_KEY"):
        monkeypatch.delenv(name, raising=False)
    for name, value in {
        "S3_ENDPOINT_URL": "http://localhost:9000",
        "S3_BUCKET_NAME": "source-bucket",
        "S3_ACCESS_KEY_ID": "source-user",
        "S3_SECRET_ACCESS_KEY": "source-test-secret",
        "AI_MINIO_ENDPOINT": "http://localhost:19000",
        "AI_MINIO_BUCKET": "target-bucket",
        "AI_MINIO_ACCESS_KEY": "target-user",
        "AI_MINIO_SECRET_KEY": "target-test-secret",
    }.items():
        monkeypatch.setenv(name, value)
    source, target = MagicMock(), MagicMock()
    source.get_paginator.return_value.paginate.return_value = [
        {"Contents": [{"Key": "data/document.pdf", "Size": 123}]}
    ]
    client_factory = MagicMock(side_effect=[source, target])
    monkeypatch.setattr(copy_tool.boto3, "client", client_factory)
    monkeypatch.setattr(sys, "argv", ["copy_storage_to_minio.py"])

    copy_tool.main()

    target.head_bucket.assert_called_once_with(Bucket="target-bucket")
    source.get_paginator.return_value.paginate.assert_called_once_with(Bucket="source-bucket")
    target.put_object.assert_not_called()
    source.get_object.assert_not_called()
    assert "Dry run: objects=1, bytes=123, copied=0" in capsys.readouterr().out
