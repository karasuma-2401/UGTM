"""Compose parsing and isolated container environment contract, using synthetic settings."""

import json
import os
from pathlib import Path
import subprocess
import tempfile

from release_fixtures import ROOT, manifest, settings
from production_metadata import initialize

with tempfile.TemporaryDirectory(prefix="ugtm-compose-test-") as temporary:
    directory = Path(temporary) / "releases" / "attempt"
    directory.mkdir(parents=True)
    values = settings()
    (directory / "release.json").write_text(json.dumps(manifest()))
    (directory / "settings.json").write_text(json.dumps(values))
    initialize(directory)
    # Inherited application variables must not mask dotenv escaping bugs.
    environment = {
        key: value
        for key, value in os.environ.items()
        if key not in values
        and key
        not in ("API_IMAGE", "AI_IMAGE", "AI_ARTIFACTS_DIR", "JWT_PRIVATE_KEY_FILE")
    }
    config = subprocess.run(
        [
            "docker",
            "compose",
            "--env-file",
            str(directory / "runtime.env"),
            "-f",
            str(ROOT / "docker/docker-compose.prod.yml"),
            "config",
            "--format",
            "json",
        ],
        env=environment,
        capture_output=True,
        text=True,
    )
    assert config.returncode == 0, config.stderr
    services = json.loads(config.stdout)["services"]
    assert "frontend" not in services
    backend = services["backend"]["environment"]
    ai = services["ai-api"]["environment"]
    assert (
        backend["Notifications__Smtp__Password"].replace("$$", "$")
        == values["SMTP_PASSWORD"]
    )
    assert backend["S3Storage__SecretKey"] == values["S3_SECRET_KEY"]
    assert ai["S3_SECRET_ACCESS_KEY"].replace("$$", "$") == values["AI_S3_SECRET_KEY"]
    assert "@sha256:" in services["backend"]["image"]
    assert "@sha256:" in services["ai-api"]["image"]
    assert services["gateway"]["environment"]["APP_URL"] == values["APP_URL"]
    infrastructure = json.loads((ROOT / "docker/release-policy.json").read_text())[
        "infrastructure"
    ]
    assert services["gateway"]["image"] == infrastructure["gateway"]
    assert services["ai-redis"]["image"] == infrastructure["redis"]
    assert services["minio"]["image"] == infrastructure["minio"]
    probe = directory / "probe.json"
    probe.write_text(
        json.dumps(
            {
                "services": {
                    "probe": {
                        "image": "python:3.12-slim",
                        "network_mode": "none",
                        "environment": {
                            "SMTP_PASSWORD": "${SMTP_PASSWORD}",
                            "S3_SECRET_KEY": "${S3_SECRET_KEY}",
                            "AI_S3_SECRET_KEY": "${AI_S3_SECRET_KEY}",
                        },
                        "command": [
                            "python",
                            "-c",
                            'import json,os; print(json.dumps({key:os.environ[key] for key in ("SMTP_PASSWORD","S3_SECRET_KEY","AI_S3_SECRET_KEY")}))',
                        ],
                    }
                }
            }
        )
    )
    result = subprocess.run(
        [
            "docker",
            "compose",
            "-p",
            "ugtm-literal-env-test",
            "--env-file",
            str(directory / "runtime.env"),
            "-f",
            str(probe),
            "run",
            "--rm",
            "--no-deps",
            "probe",
        ],
        env=environment,
        capture_output=True,
        text=True,
        timeout=120,
    )
    assert result.returncode == 0, result.stderr
    actual = json.loads(result.stdout)
    assert all(
        actual[key] == values[key] for key in actual
    ), "Literal credentials changed between dotenv and container environment"
print(
    "Compose contract passed: digests, literal secrets, Vercel redirect and no frontend container"
)
