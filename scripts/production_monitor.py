"""Local monitoring evidence without tokens, document contents or signed URLs."""

from __future__ import annotations

import json
import os
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone


def collect(root):
    current = (root / "current").resolve()
    command = [
        "docker",
        "compose",
        "-p",
        "ugtm-production",
        "--env-file",
        str(current / "runtime.env"),
        "-f",
        str(current / "docker/docker-compose.prod.yml"),
    ]
    result = subprocess.run(
        [
            *command,
            "exec",
            "-T",
            "ai-api",
            "python",
            "-m",
            "scripts.production_ops",
            "metrics",
        ],
        capture_output=True,
        text=True,
        timeout=45,
    )
    alerts = []
    if result.returncode:
        alerts.append("AI database/broker metrics are unavailable")
        metrics = {}
    else:
        metrics = json.loads(result.stdout)
    backend = subprocess.run(
        [*command, "exec", "-T", "backend", "dotnet", "UGTM.Api.dll", "--check-schema"],
        capture_output=True,
        text=True,
        timeout=70,
    )
    if backend.returncode:
        alerts.append("UGTM database/schema preflight failed")
    usage = shutil.disk_usage(root)
    metrics["disk_free_bytes"] = usage.free
    memory = {
        line.split(":")[0]: int(line.split()[1]) * 1024
        for line in Path("/proc/meminfo").read_text().splitlines()
        if line.startswith(("MemTotal:", "MemAvailable:"))
    }
    metrics["memory_available_bytes"] = memory["MemAvailable"]
    metrics["load_average"] = os.getloadavg()
    if usage.free < 2 * 1024**3:
        alerts.append("Less than 2 GiB free disk")
    if memory["MemAvailable"] < 256 * 1024**2:
        alerts.append("Less than 256 MiB available RAM")
    if metrics.get("queue_depth", 0) > int(os.environ.get("MAX_QUEUE_DEPTH", "50")):
        alerts.append("Queue depth exceeded threshold")
    if any(
        r["oldest_seconds"] > int(os.environ.get("MAX_JOB_AGE_SECONDS", "1800"))
        for r in metrics.get("jobs", [])
    ):
        alerts.append("Unfinished job age exceeded threshold")
    processes = subprocess.run(
        [*command, "ps", "--format", "json"], capture_output=True, text=True, timeout=20
    )
    rows = [json.loads(line) for line in processes.stdout.splitlines() if line.strip()]
    expected = {
        "backend",
        "ai-api",
        "ai-worker",
        "ai-reconciler",
        "ai-redis",
        "minio",
        "gateway",
    }
    running = {
        r["Service"]
        for r in rows
        if r.get("State") == "running" and r.get("Health") != "unhealthy"
    }
    if expected - running:
        alerts.append("One or more required containers are stopped or unhealthy")
    metrics["containers"] = [
        {key: r.get(key) for key in ("Service", "State", "Health", "Image", "ExitCode")}
        for r in rows
    ]
    for name, max_hours in [
        ("backup-status.json", 36),
        ("restore-status.json", 40 * 24),
    ]:
        path = root / name
        if path.exists():
            record = json.loads(path.read_text())
            if (
                record.get("status") == "failed"
                or (datetime.now(timezone.utc).timestamp() - path.stat().st_mtime)
                > max_hours * 3600
            ):
                alerts.append(f"{name} failed or is stale")
        elif (root / "backup.json").exists():
            alerts.append(f"{name} has no successful run recorded")
    if (root / "pending").exists():
        alerts.append("An unfinished deployment requires inspection")
    return {
        "time": datetime.now(timezone.utc).isoformat(),
        "release": current.name,
        "metrics": metrics,
        "alerts": alerts,
    }


def main():
    root = Path(os.environ.get("UGTM_DEPLOY_ROOT", Path.home() / "ugtm/production"))
    try:
        data = collect(root)
    except Exception:
        data = {"alerts": ["Monitoring could not collect production status"]}
    (root / "monitor-status.json").write_text(json.dumps(data, indent=2))
    print(json.dumps(data))
    return 1 if data["alerts"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
