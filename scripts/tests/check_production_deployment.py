import base64
import json
import os
from pathlib import Path
import shutil
import subprocess

root = Path(__file__).resolve().parents[2]
test_root = root / "artifacts/monorepo-ai/deploy-harness"
release = test_root / "release"
fake_bin = test_root / "bin"
(release / "scripts").mkdir(parents=True, exist_ok=True)
fake_bin.mkdir(parents=True, exist_ok=True)
shutil.copy(root / "scripts/Deploy-Production.sh", release / "scripts/Deploy-Production.sh")
for relative in ("docker/docker-compose.prod.yml", "docker/Caddyfile", "docker/initialize-minio.sh"):
    destination = release / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(root / relative, destination)
fake_docker = '''#!/bin/bash
printf '%s\n' "$*" >> "$DEPLOY_TEST_LOG"
case "$*" in
  *'--entrypoint id'*) echo 10001 ;;
  'login '*) cat >/dev/null ;;
  *'scripts.check_schema'*) if [[ "${FAIL_SCHEMA:-0}" == 1 ]]; then exit 1; fi ;;
esac
'''
for name, code in {"docker": fake_docker, "setfacl": "#!/bin/bash\nexit 0\n"}.items():
    (fake_bin / name).write_text(code, encoding="utf-8", newline="\n")
    (fake_bin / name).chmod(0o755)

env = os.environ.copy()
settings = {
    "SOURCE_SHA": "a" * 40,
    "IMAGE_OWNER": "example",
    "API_HOST": "api.example.test",
    "ACME_EMAIL": "ops@example.test",
    "APP_URL": "https://frontend.example.test",
    "DATABASE_CONNECTION_STRING": "Host=db;Database=ugtm;Username=ci;Password=ci-only",
    "AI_POSTGRES_DSN": "postgresql://ci:ci-only@db/ai",
    "AI_POSTGRES_DIRECT_DSN": "postgresql://ci:ci-only@db/ai",
    "PLAGIARISM_AI_API_KEY": "x" * 32,
    "PLAGIARISM_AI_DEFAULT_CORPUS": "ci-only",
    "REMOTE_GPU_URL": "https://gpu.example.test",
    "REMOTE_GPU_API_KEY": "ci-only",
    "MINIO_ROOT_USER": "ci-admin",
    "MINIO_ROOT_PASSWORD": "ci-only-password",
    "S3_ACCESS_KEY": "ci-ugtm",
    "S3_SECRET_KEY": "trailing-backslash\\",
    "AI_S3_ACCESS_KEY": "ci-ai",
    "AI_S3_SECRET_KEY": "slash\\'quote\" and $$dollars",
    "IDENTITY_IP_HASH_KEY": "x" * 32,
    "SMTP_HOST": "smtp.example.test",
    "SMTP_FROM_ADDRESS": "ops@example.test",
    "SMTP_PASSWORD": "literal$Dollar#Hash'Quote\\Backslash\"DoubleQuote and spaces\tTab",
    "JWT_PRIVATE_KEY_PEM_BASE64": base64.b64encode(b"synthetic-test-key").decode(),
    "DEPLOY_GITHUB_TOKEN": "ci-only",
    "DEPLOY_MODE": "prepare",
    "UGTM_DEPLOY_ROOT": (test_root / "app").as_posix(),
    "DEPLOY_TEST_LOG": (test_root / "calls.log").as_posix(),
}
env.update(settings)
env["PATH"] = str(fake_bin) + os.pathsep + env["PATH"]
bash = r"C:\Program Files\Git\bin\bash.exe" if os.name == "nt" else "bash"
log = test_root / "calls.log"
if log.exists():
    log.unlink()
env["DEPLOY_FAKE_BIN"] = fake_bin.as_posix()
env["DEPLOY_TEST_SCRIPT"] = (release / "scripts/Deploy-Production.sh").as_posix()
command = [bash, "-c", 'export PATH="$(cygpath -u "$DEPLOY_FAKE_BIN"):$PATH"; hash -r; exec bash "$DEPLOY_TEST_SCRIPT"'] if os.name == "nt" else [bash, env["DEPLOY_TEST_SCRIPT"]]
result = subprocess.run(command, env=env, capture_output=True, text=True)
assert result.returncode == 0, result.stdout + result.stderr
calls = log.read_text()
assert " up " not in calls and "scripts.check_schema" not in calls
assert not (test_root / "app/current").exists()

# Real Compose parser, without inherited values masking dotenv serialization bugs.
clean = os.environ.copy()
for name in settings:
    clean.pop(name, None)
config = subprocess.run(
    ["docker", "compose", "--env-file", str(release / "runtime.env"), "-f",
     str(root / "docker/docker-compose.prod.yml"), "config", "--format", "json"],
    env=clean, capture_output=True, text=True,
)
assert config.returncode == 0, config.stderr
resolved = json.loads(config.stdout)
actual = resolved["services"]["backend"]["environment"]["Notifications__Smtp__Password"]
assert actual.replace("$$", "$") == settings["SMTP_PASSWORD"], repr((actual, settings["SMTP_PASSWORD"]))
assert resolved["services"]["backend"]["environment"]["S3Storage__SecretKey"] == settings["S3_SECRET_KEY"]
assert resolved["services"]["ai-api"]["environment"]["S3_SECRET_ACCESS_KEY"].replace("$$", "$") == settings["AI_S3_SECRET_KEY"]

env["DEPLOY_MODE"] = "deploy"
env["FAIL_SCHEMA"] = "1"
log.write_text("")
result = subprocess.run(command, env=env, capture_output=True, text=True)
assert result.returncode != 0
assert "scripts.check_schema" in log.read_text()
assert " up " not in log.read_text()
assert not (test_root / "app/current").exists()
print("Deployment harness passed: prepare mode, literal secrets, schema-failure guard")
