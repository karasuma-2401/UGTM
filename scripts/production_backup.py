"""Encrypted off-VM backups and isolated restore drills. Configuration is private JSON."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import tempfile
from urllib.parse import urlsplit, unquote, parse_qs


def call(args, **kwargs):
    result = subprocess.run(
        args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, **kwargs
    )
    if result.returncode:
        raise RuntimeError(f"{Path(args[0]).name} failed with exit {result.returncode}")
    return result.stdout


def env_file(path, values):
    if any("\n" in str(v) or "\r" in str(v) for v in values.values()):
        raise ValueError("Multiline credentials are not supported")
    path.write_text("".join(f"{key}={value}\n" for key, value in values.items()))
    path.chmod(0o600)


def pg_settings(dsn):
    url = urlsplit(dsn)
    if (
        url.scheme not in ("postgres", "postgresql")
        or not url.hostname
        or not url.username
        or not url.password
    ):
        raise ValueError("Configure a direct PostgreSQL URI for each backup database")
    if "-pooler." in url.hostname:
        raise ValueError("Backups require direct database endpoints")
    sslmode = parse_qs(url.query).get("sslmode", ["require"])[0]
    if sslmode not in ("require", "verify-ca", "verify-full"):
        raise ValueError("Database backups require TLS")
    return {
        "PGHOST": url.hostname,
        "PGPORT": str(url.port or 5432),
        "PGUSER": unquote(url.username),
        "PGPASSWORD": unquote(url.password),
        "PGDATABASE": unquote(url.path.lstrip("/")),
        "PGSSLMODE": sslmode,
        "PGCONNECT_TIMEOUT": "10",
    }


class Backup:
    def __init__(self, root):
        self.root = root.resolve()
        path = root / "backup.json"
        if path.stat().st_mode & 0o077:
            raise ValueError("backup.json must have mode 0600")
        self.config = json.loads(path.read_text())
        repository = self.config["restic"]["RESTIC_REPOSITORY"]
        if not repository.startswith("s3:https://"):
            raise ValueError("Configure an external HTTPS S3 restic repository")
        self.env = {**os.environ, **self.config["restic"]}
        self.env["RESTIC_CACHE_DIR"] = str(root / "restic-cache")
        self.current = (root / "current").resolve()
        if not self.current.is_relative_to(self.root / "releases"):
            raise ValueError("No active versioned release")
        self.release = json.loads((self.current / "release.json").read_text())
        self.pg_image = self.config["postgres_image"]
        if not re.fullmatch(r"pgvector/pgvector@sha256:[0-9a-f]{64}", self.pg_image):
            raise ValueError(
                "Pin the pgvector PostgreSQL 18 tools/server image by digest"
            )

    def restic(self, *args):
        return call(["restic", *args], env=self.env)

    def backup(self):
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        directory = self.root / "backups" / stamp
        directory.mkdir(parents=True, mode=0o700)
        try:
            for name in ("ugtm", "ai"):
                settings = directory / f"{name}.env"
                env_file(settings, pg_settings(self.config[f"{name}_direct_dsn"]))
                try:
                    with (directory / f"{name}.dump").open("wb") as output:
                        result = subprocess.run(
                            [
                                "docker",
                                "run",
                                "--rm",
                                "--env-file",
                                str(settings),
                                self.pg_image,
                                "pg_dump",
                                "--format=custom",
                                "--no-owner",
                                "--no-privileges",
                            ],
                            stdout=output,
                            stderr=subprocess.PIPE,
                            timeout=900,
                        )
                    if result.returncode:
                        raise RuntimeError(f"{name} dump failed")
                finally:
                    settings.unlink(missing_ok=True)
            settings = directory / "objects.env"
            env_file(
                settings,
                {
                    "BACKUP_MINIO_USER": self.config["minio_read_user"],
                    "BACKUP_MINIO_PASSWORD": self.config["minio_read_password"],
                    "BACKUP_BUCKETS": ",".join(self.config["buckets"]),
                },
            )
            try:
                # A read-only MinIO service account, scoped to these two buckets, is provisioned separately.
                call(
                    [
                        "docker",
                        "run",
                        "--rm",
                        "--network",
                        "ugtm-production_default",
                        "--user",
                        f"{os.getuid()}:{os.getgid()}",
                        "--env-file",
                        str(settings),
                        "-v",
                        f"{directory}:/backup",
                        self.release["images"]["ai"]["reference"],
                        "python",
                        "-m",
                        "scripts.production_ops",
                        "backup-objects",
                    ],
                    timeout=1800,
                )
            finally:
                settings.unlink(missing_ok=True)
            volumes = self.config["key_volumes"]
            if set(volumes) != {"keys", "caddy-data", "caddy-config"} or any(
                not re.fullmatch(r"[A-Za-z0-9_-]+", v) for v in volumes.values()
            ):
                raise ValueError(
                    "Inspect and configure the three persistent key/TLS volumes"
                )
            for volume in volumes.values():
                call(["docker", "volume", "inspect", volume], timeout=15)
            mounts = [
                item
                for name, volume in volumes.items()
                for item in ("-v", f"{volume}:/source/{name}:ro")
            ]
            call(
                [
                    "docker",
                    "run",
                    "--rm",
                    *mounts,
                    "-v",
                    f"{self.root / 'ai-artifacts'}:/source/ai-artifacts:ro",
                    "-v",
                    f"{directory}:/backup",
                    self.pg_image,
                    "sh",
                    "-c",
                    'set -eu; umask 077; tar -czf /backup/keys-and-tls.tar.gz -C /source keys caddy-data caddy-config ai-artifacts; chown "$1:$2" /backup/keys-and-tls.tar.gz',
                    "sh",
                    str(os.getuid()),
                    str(os.getgid()),
                ],
                timeout=120,
            )
            # Copy the current release bundle and private runtime config for disaster recovery.
            shutil.copytree(
                self.current,
                directory / "release",
                ignore=shutil.ignore_patterns(".docker", "*.tar.gz", "settings.json"),
            )
            checksums = {
                p.relative_to(directory)
                .as_posix(): hashlib.sha256(p.read_bytes())
                .hexdigest()
                for p in directory.rglob("*")
                if p.is_file()
            }
            (directory / "checksums.json").write_text(json.dumps(checksums))
            self.restic("backup", str(directory), "--tag", "ugtm-production", "--json")
            self.restic("check", "--read-data-subset=1/20")
            (self.root / "backup-status.json").write_text(
                json.dumps(
                    {"status": "backed_up", "time": stamp, "local_path": str(directory)}
                )
            )
            # Retention is restricted to this application's tag in a dedicated repository.
            self.restic(
                "forget",
                "--tag",
                "ugtm-production",
                "--keep-daily",
                "7",
                "--keep-weekly",
                "4",
                "--keep-monthly",
                "3",
                "--prune",
            )
            completed = sorted(
                p
                for p in (self.root / "backups").iterdir()
                if p.is_dir() and re.fullmatch(r"\d{8}T\d{6}Z", p.name)
            )
            for old in completed[:-2]:
                if old.resolve().is_relative_to((self.root / "backups").resolve()):
                    shutil.rmtree(old)
            print("Encrypted off-VM backup and repository integrity check completed.")
        except Exception:
            (self.root / "backup-status.json").write_text(
                json.dumps({"status": "failed", "time": stamp})
            )
            raise

    def drill(self):
        # No production database URLs, volumes or networks are passed to drill containers.
        memory = {
            line.split(":")[0]: int(line.split()[1])
            for line in Path("/proc/meminfo").read_text().splitlines()
            if line.startswith("MemAvailable:")
        }
        if (
            memory["MemAvailable"]
            < int(self.config.get("drill_min_free_mib", 1800)) * 1024
            or shutil.disk_usage(self.root).free < 4 * 1024**3
        ):
            raise RuntimeError(
                "Restore drill needs at least 1800 MiB available RAM and 4 GiB free disk"
            )
        work = Path(tempfile.mkdtemp(prefix="ugtm-restore-"))
        identifier = "ugtm-restore-" + secrets.token_hex(6)
        network, postgres, minio = identifier, identifier + "-pg", identifier + "-minio"
        password = secrets.token_urlsafe(24)
        created = []
        try:
            self.restic(
                "restore", "latest", "--tag", "ugtm-production", "--target", str(work)
            )
            snapshots = list(work.rglob("checksums.json"))
            # Release seals are also named checksums.json. Select only the backup root manifest.
            snapshots = [
                p
                for p in snapshots
                if (p.parent / "ugtm.dump").exists() and (p.parent / "ai.dump").exists()
            ]
            if len(snapshots) != 1:
                raise RuntimeError("Expected one backup snapshot")
            source = snapshots[0].parent
            for relative, expected in json.loads(snapshots[0].read_text()).items():
                path = (source / relative).resolve()
                if (
                    not path.is_relative_to(source.resolve())
                    or hashlib.sha256(path.read_bytes()).hexdigest() != expected
                ):
                    raise RuntimeError("Backup checksum mismatch")
            call(["docker", "network", "create", "--internal", network])
            created.append(("network", network))
            settings = work / "drill.env"
            env_file(settings, {"POSTGRES_PASSWORD": password})
            call(
                [
                    "docker",
                    "run",
                    "-d",
                    "--name",
                    postgres,
                    "--network",
                    network,
                    "--memory",
                    "512m",
                    "--cpus",
                    "0.5",
                    "--env-file",
                    str(settings),
                    self.pg_image,
                ]
            )
            created.append(("container", postgres))
            settings.unlink()
            import time

            for attempt in range(30):
                if (
                    subprocess.run(
                        ["docker", "exec", postgres, "pg_isready", "-U", "postgres"],
                        stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL,
                    ).returncode
                    == 0
                ):
                    break
                time.sleep(2)
            else:
                raise RuntimeError("Restore database did not start")
            for name in ("ugtm", "ai"):
                call(
                    [
                        "docker",
                        "exec",
                        postgres,
                        "createdb",
                        "-U",
                        "postgres",
                        f"drill_{name}",
                    ]
                )
                with (source / f"{name}.dump").open("rb") as stream:
                    call(
                        [
                            "docker",
                            "exec",
                            "-i",
                            postgres,
                            "pg_restore",
                            "-U",
                            "postgres",
                            "-d",
                            f"drill_{name}",
                            "--exit-on-error",
                            "--no-owner",
                            "--no-privileges",
                        ],
                        stdin=stream,
                        timeout=900,
                    )
            # Tar listing exercises decompression; never extract TLS keys into a live volume.
            call(["tar", "-tzf", str(source / "keys-and-tls.tar.gz")], timeout=30)
            config = self.config
            env_file(
                work / "minio.env",
                {"MINIO_ROOT_USER": "restore-test", "MINIO_ROOT_PASSWORD": password},
            )
            image = config["minio_image"]
            if "@sha256:" not in image:
                raise ValueError("Pin the restore MinIO image by digest")
            call(
                [
                    "docker",
                    "run",
                    "-d",
                    "--name",
                    minio,
                    "--network",
                    network,
                    "--memory",
                    "512m",
                    "--cpus",
                    "0.5",
                    "--network-alias",
                    "restore-minio",
                    "--env-file",
                    str(work / "minio.env"),
                    image,
                    "server",
                    "/data",
                ]
            )
            created.append(("container", minio))
            env_file(
                work / "restore.env",
                {
                    "BACKUP_MINIO_USER": "restore-test",
                    "BACKUP_MINIO_PASSWORD": password,
                },
            )
            call(
                [
                    "docker",
                    "run",
                    "--rm",
                    "--network",
                    network,
                    "--memory",
                    "384m",
                    "--cpus",
                    "0.5",
                    "--env-file",
                    str(work / "restore.env"),
                    "-v",
                    f"{source}:/restore:ro",
                    self.release["images"]["ai"]["reference"],
                    "python",
                    "-m",
                    "scripts.production_ops",
                    "restore-objects",
                    "--directory",
                    "/restore/objects",
                ],
                timeout=1200,
            )
            (self.root / "restore-status.json").write_text(
                json.dumps(
                    {
                        "status": "restored_in_isolation",
                        "time": datetime.now(timezone.utc).isoformat(),
                    }
                )
            )
            print(
                "Restore drill passed: both databases, object roundtrip checksums and key archive."
            )
        finally:
            for kind, name in reversed(created):
                call(
                    ["docker", kind, "rm", "-fv", name]
                    if kind == "container"
                    else ["docker", "network", "rm", name]
                )
            if work.name.startswith("ugtm-restore-") and work.parent == Path(
                tempfile.gettempdir()
            ):
                shutil.rmtree(work)


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("action", choices=["init", "backup", "restore-drill"])
    parser.add_argument("--root", type=Path, default=Path.home() / "ugtm/production")
    args = parser.parse_args()
    os.umask(0o077)
    try:
        backup = Backup(args.root)
        if args.action == "init":
            backup.restic("init")
            print("Initialized the configured encrypted backup repository.")
        elif args.action == "backup":
            backup.backup()
        else:
            backup.drill()
    except Exception:
        print(
            f"{args.action} failed. Check private backup.json, repository credentials and capacity; no credentials were logged."
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
