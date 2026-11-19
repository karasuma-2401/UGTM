"""Coordinate GitHub release evidence, pinned SSH, Azure and staged Vercel deployment."""

from __future__ import annotations

import io
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
import tarfile
import time
import urllib.parse
import urllib.request

from release import read, validate, write
from production_metadata import REQUIRED, OPTIONAL
from production_smoke import health, authenticated


def run(args, **kwargs):
    # Never echo command arguments: CLI tokens and connection settings can be sensitive.
    result = subprocess.run(
        args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, **kwargs
    )
    if result.returncode:
        if args[0] == "ssh" and "Host key verification failed" in result.stderr:
            raise RuntimeError(
                "SSH host verification failed; configure the trusted VPS_KNOWN_HOSTS value"
            )
        if args[0] == "ssh" and "Permission denied" in result.stderr:
            raise RuntimeError(
                "SSH authentication failed; verify VPS_USERNAME and VPS_SSH_KEY"
            )
        raise RuntimeError(
            f"Command failed: {Path(args[0]).name}; exit {result.returncode}"
        )
    return result.stdout.strip()


def github(path):
    return json.loads(run(["gh", "api", path], timeout=60))


def validate_run(info, repository, comparison):
    if (
        info.get("conclusion") != "success"
        or info.get("head_branch") != "production"
        or info.get("path") != ".github/workflows/publish.yml"
        or info.get("repository", {}).get("full_name") != repository
        or comparison.get("status") not in ("ahead", "identical")
    ):
        raise ValueError(
            "Release must be a successful production publish and an ancestor of production"
        )


class Remote:
    def __init__(self, directory):
        host, user = os.environ["VPS_HOST"], os.environ.get("VPS_USERNAME") or "ubuntu"
        port = os.environ.get("VPS_PORT") or "22"
        if (
            not re.fullmatch(r"[A-Za-z0-9.-]+", host)
            or not re.fullmatch(r"[a-z_][a-z0-9_-]*", user)
            or not port.isdigit()
            or not 1 <= int(port) <= 65535
        ):
            raise ValueError("Invalid SSH destination")
        for name, value in [
            ("key", os.environ["VPS_SSH_KEY"]),
            ("known_hosts", os.environ["VPS_KNOWN_HOSTS"]),
        ]:
            path = directory / name
            path.write_text(value.replace("\r\n", "\n").strip() + "\n")
            path.chmod(0o600)
        self.options = [
            "-i",
            str(directory / "key"),
            "-o",
            "IdentitiesOnly=yes",
            "-o",
            "BatchMode=yes",
            "-o",
            "StrictHostKeyChecking=yes",
            "-o",
            f'UserKnownHostsFile={directory / "known_hosts"}',
            "-o",
            "ConnectTimeout=10",
            "-o",
            "ServerAliveInterval=15",
            "-o",
            "ServerAliveCountMax=3",
        ]
        self.destination, self.port = f"{user}@{host}", port

    def execute(self, command, timeout=1000, input=None):
        return run(
            ["ssh", *self.options, "-p", self.port, self.destination, command],
            timeout=timeout,
            input=input,
        )

    def upload(self, file, path):
        run(
            [
                "scp",
                *self.options,
                "-P",
                self.port,
                str(file),
                f"{self.destination}:{path}",
            ],
            timeout=120,
        )

    def phase(self, path, phase):
        result = self.execute(
            f'DOCKER_CONFIG="$HOME/{path}/.docker" bash "$HOME/{path}/scripts/Deploy-Production.sh" {shlex.quote(phase)}'
        )
        print(result)


def vercel(*args, cwd=None):
    return run(
        ["vercel", *args, "--token", os.environ["VERCEL_TOKEN"]], cwd=cwd, timeout=600
    )


def previous_frontend():
    project = urllib.parse.quote(os.environ["VERCEL_PROJECT_ID"], safe="")
    team = (
        urllib.parse.urlencode({"teamId": os.environ["VERCEL_ORG_ID"]})
        if os.environ["VERCEL_ORG_ID"].startswith("team_")
        else ""
    )
    request = urllib.request.Request(
        f"https://api.vercel.com/v9/projects/{project}?{team}",
        headers={"Authorization": f"Bearer {os.environ['VERCEL_TOKEN']}"},
    )
    with urllib.request.urlopen(request, timeout=20) as response:
        info = json.load(response)
    production = info.get("targets", {}).get("production", {})
    url = production.get("url")
    if not url:
        raise ValueError("Cannot resolve the previous Vercel production deployment")
    return f"https://{url}"


def deploy(workspace):
    repository = os.environ["GITHUB_REPOSITORY"]
    mode = os.environ.get("DEPLOY_MODE", "deploy")
    if mode not in ("prepare", "deploy"):
        raise ValueError("DEPLOY_MODE must be prepare or deploy")
    publish_id = os.environ["PUBLISH_RUN_ID"]
    if not publish_id.isdigit():
        raise ValueError("Invalid publish run ID")
    info = github(f"repos/{repository}/actions/runs/{publish_id}")
    publish_attempt = int(info["run_attempt"])
    sha = info["head_sha"]
    comparison = github(f"repos/{repository}/compare/{sha}...production")
    validate_run(info, repository, comparison)
    if os.environ.get("AUTO_DEPLOY") == "true" and comparison["status"] != "identical":
        print(
            "A newer production commit exists; this automatic deployment was superseded."
        )
        return
    bundle = workspace / "bundle"
    run(
        [
            "gh",
            "run",
            "download",
            publish_id,
            "--repo",
            repository,
            "--name",
            f"release-bundle-{publish_attempt}",
            "--dir",
            str(bundle),
        ],
        timeout=120,
    )
    manifest = validate(read(bundle / "release.json"), repository, sha)
    if (
        str(manifest["publish_run_id"]) != publish_id
        or manifest.get("publish_run_attempt") != publish_attempt
    ):
        raise ValueError("Publish run/manifest mismatch")
    run(
        [sys.executable, str(bundle / "scripts/release.py"), "verify", str(bundle)],
        timeout=30,
    )
    if os.environ.get("DEPLOYMENT_SCOPE") != "production":
        raise ValueError("Set DEPLOYMENT_SCOPE=production")
    if os.environ.get("SMOKE_AUTH_ENABLED") == "true" and not all(
        os.environ.get(key) for key in ("SMOKE_USER", "SMOKE_PASSWORD")
    ):
        raise ValueError(
            "Configure SMOKE_USER and SMOKE_PASSWORD before enabling authenticated smoke checks"
        )
    # Fail before Azure mutation when coordinated frontend settings are missing.
    for key in REQUIRED + [
        "VPS_HOST",
        "VPS_SSH_KEY",
        "VPS_KNOWN_HOSTS",
        "VERCEL_TOKEN",
        "VERCEL_PROJECT_ID",
        "VERCEL_ORG_ID",
    ]:
        if not os.environ.get(key):
            raise ValueError(f"Missing setting: {key}")
    payload = {key: os.environ.get(key, "") for key in REQUIRED + OPTIONAL}
    write(bundle / "settings.json", payload)
    (bundle / "settings.json").chmod(0o600)
    attempt = f"{sha}-{os.environ['GITHUB_RUN_ID']}-{os.environ['GITHUB_RUN_ATTEMPT']}"
    remote_path = f"ugtm/production/releases/{attempt}"
    remote = Remote(workspace)
    print("Stage: SSH transfer and registry authentication", flush=True)
    # Do not reuse an attempt directory: its env/config must remain immutable.
    remote.execute(
        f'umask 077; test ! -e "$HOME/{remote_path}" && mkdir -p "$HOME/{remote_path}/.docker"'
    )
    archive = workspace / "release.tar.gz"
    with tarfile.open(archive, "w:gz") as tar:
        for file in bundle.rglob("*"):
            if file.is_file():
                tar.add(file, arcname=file.relative_to(bundle))
    remote.upload(archive, f"{remote_path}/release.tar.gz")
    archive.unlink()
    (bundle / "settings.json").unlink()
    remote.execute(
        f'tar -xzf "$HOME/{remote_path}/release.tar.gz" -C "$HOME/{remote_path}" && rm "$HOME/{remote_path}/release.tar.gz"'
    )
    try:
        remote.execute(
            f'docker --config "$HOME/{remote_path}/.docker" login ghcr.io -u {shlex.quote(repository.split("/")[0])} --password-stdin',
            input=os.environ["GH_TOKEN"],
        )
    except Exception:
        remote.execute(
            f'rm -f "$HOME/{remote_path}/.docker/config.json" "$HOME/{remote_path}/settings.json"'
        )
        raise
    candidate_started = False
    promoted = False
    previous = None
    summary = {
        "source_sha": sha,
        "attempt": attempt,
        "publish_run_id": publish_id,
        "publish_run_attempt": publish_attempt,
        "stages": [],
    }

    def stage(name, function):
        print(f"Stage: {name}", flush=True)
        start = time.monotonic()
        try:
            return function()
        finally:
            summary["stages"].append(
                {"stage": name, "seconds": round(time.monotonic() - start, 2)}
            )
            write(workspace / "summary.json", summary)

    try:
        if mode == "prepare":
            stage("prepare", lambda: remote.phase(remote_path, "prepare"))
            summary["result"] = "prepared"
            return
        previous = previous_frontend()
        # Archive the approved commit, never deploy the workflow's unrelated checkout.
        source = workspace / "source"
        source.mkdir()
        raw = subprocess.check_output(["git", "archive", sha], timeout=30)
        with tarfile.open(fileobj=io.BytesIO(raw)) as tar:
            tar.extractall(source, filter="data")
        (source / ".vercel").mkdir(exist_ok=True)
        write(
            source / ".vercel/project.json",
            {
                "orgId": os.environ["VERCEL_ORG_ID"],
                "projectId": os.environ["VERCEL_PROJECT_ID"],
            },
        )
        output = stage(
            "stage_frontend",
            lambda: vercel(
                "deploy",
                "--prod",
                "--skip-domain",
                "--yes",
                "--meta",
                f"githubCommitSha={sha}",
                cwd=source,
            ),
        )
        urls = re.findall(r"https://[A-Za-z0-9.-]+\.vercel\.app", output)
        if not urls:
            raise ValueError("Vercel did not return a deployment URL")
        frontend = urls[-1]
        summary.update(vercel_url=frontend, previous_vercel_url=previous)
        write(workspace / "summary.json", summary)
        write(workspace / "deployment.json", summary)
        remote.upload(workspace / "deployment.json", f"{remote_path}/deployment.json")
        candidate_started = True
        stage("backend_candidate", lambda: remote.phase(remote_path, "candidate"))
        # Public checks originate outside the VM and catch DNS/NSG/TLS failures.
        stage(
            "external_checks",
            lambda: health(os.environ["APP_URL"], os.environ["API_HOST"]),
        )
        promoted = True
        stage("promote_frontend", lambda: vercel("promote", frontend, "--yes"))
        stage(
            "production_http_smoke",
            lambda: health(os.environ["APP_URL"], os.environ["API_HOST"]),
        )
        if os.environ.get("SMOKE_AUTH_ENABLED") == "true":
            stage("authenticated_smoke", lambda: authenticated(os.environ["APP_URL"]))
        # Store public cross-service release evidence on the VM before committing current.
        write(workspace / "deployment.json", summary)
        remote.upload(workspace / "deployment.json", f"{remote_path}/deployment.json")
        try:
            stage("finalize", lambda: remote.phase(remote_path, "finalize"))
        except Exception:
            # SSH can disconnect after the remote commit. Reconcile before attempting rollback.
            status = json.loads(
                remote.execute(f'cat "$HOME/{remote_path}/status.json"')
            )
            if status.get("status") != "deployed":
                raise
        summary["result"] = "deployed"
    except Exception:
        summary["result"] = "failed"
        recovery_errors = []
        if promoted and previous:
            try:
                stage(
                    "check_rollback_compatibility",
                    lambda: remote.phase(remote_path, "check-candidate-rollback"),
                )
            except Exception:
                recovery_errors.append(
                    "Frontend rollback held: backend rollback compatibility or remote state could not be confirmed"
                )
            else:
                try:
                    stage(
                        "rollback_frontend",
                        lambda: vercel("rollback", previous, "--yes"),
                    )
                except Exception:
                    recovery_errors.append("Vercel rollback needs operator recovery")
        if candidate_started:
            try:
                stage("rollback_backend", lambda: remote.phase(remote_path, "rollback"))
            except Exception:
                recovery_errors.append(
                    "Backend rollback needs operator recovery; inspect pending/status"
                )
        summary["recovery_errors"] = recovery_errors
        for error in recovery_errors:
            print(error)
        raise
    finally:
        write(workspace / "summary.json", summary)
        try:
            status = remote.execute(f'cat "$HOME/{remote_path}/status.json"')
            write(workspace / "remote-status.json", json.loads(status))
        except Exception:
            pass
        # Remove the short-lived registry token even on failure; retained settings stay private on VM.
        try:
            remote.execute(
                f'rm -f "$HOME/{remote_path}/.docker/config.json" "$HOME/{remote_path}/settings.json"'
            )
        except Exception:
            print(
                "SSH cleanup could not complete; inspect the attempt directory on the VM."
            )


def main():
    workspace = (
        Path(os.environ.get("RUNNER_TEMP", "artifacts"))
        / f"ugtm-deploy-{os.environ.get('GITHUB_RUN_ID','local')}-{os.environ.get('GITHUB_RUN_ATTEMPT','1')}"
    )
    os.umask(0o077)
    workspace.mkdir(parents=True, exist_ok=True)
    try:
        deploy(workspace)
    except Exception as error:
        # Own validation errors are safe; subprocess/network exceptions can contain credentials.
        print(
            str(error)
            if isinstance(error, (ValueError, RuntimeError))
            else f"Deployment stopped ({type(error).__name__}); see stage/status summary."
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
