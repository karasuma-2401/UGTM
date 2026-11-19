"""Release metadata and private dotenv serialization; Python standard library only."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
COMPONENTS = {
    "api": "backend",
    "ai": "services/plagiarism-ai",
    "gateway": "docker/gateway",
}
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
SHA = re.compile(r"^[0-9a-f]{40}$")


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            dir=path.parent,
            prefix=f".{path.name}.",
            delete=False,
        ) as output:
            temporary = Path(output.name)
            json.dump(value, output, indent=2)
            output.write("\n")
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def fingerprint(directory):
    # git ls-files excludes local secrets, caches and build output.
    files = (
        subprocess.check_output(["git", "ls-files", "-z", directory], cwd=ROOT)
        .decode()
        .split("\0")
    )
    digest = hashlib.sha256()
    for name in sorted(filter(None, files)):
        digest.update(
            name.encode() + b"\0" + hashlib.sha256((ROOT / name).read_bytes()).digest()
        )
    return digest.hexdigest()


def schemas():
    return {
        "ai": {
            p.stem: hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted((ROOT / "services/plagiarism-ai/sql").glob("*.sql"))
        },
        "ugtm": {
            p.stem: hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(
                (ROOT / "backend/Database.Migrations/Migrations").glob("*.cs")
            )
        },
    }


def validate(data, repository=None, sha=None):
    if data.get("version") not in (1, 2) or not SHA.fullmatch(
        data.get("source_sha", "")
    ):
        raise ValueError("Unsupported or invalid release manifest")
    if data.get("branch") != "production" and repository is not None:
        raise ValueError("Only production releases may be deployed")
    if repository and data.get("repository") != repository:
        raise ValueError("Release repository mismatch")
    if sha and data["source_sha"] != sha:
        raise ValueError("Release source SHA mismatch")
    # V1 remains readable for planning/recovery; every V2 release needs a scanned gateway.
    expected = {"api", "ai"} if data["version"] == 1 else set(COMPONENTS)
    if set(data.get("images", {})) != expected:
        raise ValueError("Release images do not match the manifest version")
    owner = data["repository"].split("/")[0].lower()
    for component, item in data["images"].items():
        prefix = f"ghcr.io/{owner}/ugtm-{component}@"
        if not item.get("reference", "").startswith(prefix) or not DIGEST.fullmatch(
            item["reference"][len(prefix) :]
        ):
            raise ValueError("Invalid image reference")
        if not DIGEST.fullmatch(item.get("platforms", {}).get("arm64", "")):
            raise ValueError("A scanned ARM64 image is required")
    if not data.get("schemas", {}).get("ai") or not data["schemas"].get("ugtm"):
        raise ValueError("Schema fingerprints are required")
    return data


def dotenv(values):
    lines = []
    for name, value in values.items():
        if not re.fullmatch(r"[A-Z][A-Z0-9_]*", name) or not isinstance(value, str):
            raise ValueError("Invalid environment setting")
        if "\n" in value or "\r" in value or "\0" in value:
            raise ValueError(f"Multiline setting is unsupported: {name}")
        value = (
            value.replace("\\", "\\\\")
            .replace('"', '\\"')
            .replace("$", "$$")
            .replace("\t", "\\t")
        )
        lines.append(f'{name}="{value}"\n')
    return "".join(lines)


def main():
    parser = argparse.ArgumentParser(__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    plan = sub.add_parser("plan")
    plan.add_argument("--previous")
    plan.add_argument("--force", action="store_true")
    plan.add_argument("--amd64", action="store_true")
    create = sub.add_parser("manifest")
    create.add_argument("--records", required=True)
    create.add_argument("--output", required=True)
    check = sub.add_parser("validate")
    check.add_argument("path")
    check.add_argument("--repository")
    check.add_argument("--sha")
    bundle = sub.add_parser("seal")
    bundle.add_argument("directory")
    verify = sub.add_parser("verify")
    verify.add_argument("directory")
    args = parser.parse_args()
    if args.command == "plan":
        previous = (
            read(args.previous)
            if args.previous and Path(args.previous).exists()
            else {}
        )
        if previous:
            validate(previous)
        matrix = []
        for component, context in COMPONENTS.items():
            source_hash = fingerprint(context)
            old = previous.get("images", {}).get(component, {})
            fresh = (
                datetime.now(timezone.utc)
                - datetime.fromisoformat(
                    old.get("built_at", "1970-01-01T00:00:00+00:00")
                )
            ).days < 7
            # Reuse only within the same branch and architecture set, for at most 7 days.
            reuse = (
                not args.force
                and fresh
                and previous.get("branch") == os.environ["GITHUB_REF_NAME"]
                and old.get("input_hash") == source_hash
                and set(old.get("platforms", {}))
                == ({"amd64", "arm64"} if args.amd64 else {"arm64"})
            )
            for arch in (["amd64", "arm64"] if args.amd64 else ["arm64"]):
                matrix.append(
                    {
                        "component": component,
                        "context": context,
                        "dockerfile": (
                            f"{context}/host/Api/Dockerfile"
                            if component == "api"
                            else f"{context}/Dockerfile"
                        ),
                        "arch": arch,
                        "runner": (
                            "ubuntu-24.04-arm" if arch == "arm64" else "ubuntu-24.04"
                        ),
                        "input_hash": source_hash,
                        "reuse": reuse,
                        "built_at": (
                            old.get("built_at", "")
                            if reuse
                            else datetime.now(timezone.utc).isoformat()
                        ),
                        "digest": (
                            old.get("platforms", {}).get(arch, "") if reuse else ""
                        ),
                        "built_from": (
                            old.get("built_from", previous.get("source_sha", ""))
                            if reuse
                            else os.environ["GITHUB_SHA"]
                        ),
                    }
                )
        print(json.dumps({"include": matrix}))
    elif args.command == "manifest":
        records = [read(p) for p in Path(args.records).glob("*.json")]
        images = {}
        for component in COMPONENTS:
            items = [r for r in records if r["component"] == component]
            if not items or len({r["reference"] for r in items}) != 1:
                raise ValueError("Missing or inconsistent image records")
            images[component] = {
                key: items[0][key]
                for key in ("reference", "input_hash", "built_from", "built_at")
            }
            images[component]["platforms"] = {r["arch"]: r["digest"] for r in items}
        data = {
            "version": 2,
            "source_sha": os.environ["GITHUB_SHA"],
            "repository": os.environ["GITHUB_REPOSITORY"],
            "branch": os.environ["GITHUB_REF_NAME"],
            "publish_run_id": os.environ["GITHUB_RUN_ID"],
            "publish_run_attempt": int(os.environ["GITHUB_RUN_ATTEMPT"]),
            "created_at": datetime.now(timezone.utc).isoformat(),
            "schemas": schemas(),
            "images": images,
            "rollback_policy": read(ROOT / "docker/release-policy.json"),
        }
        validate(data)
        write(args.output, data)
    elif args.command == "validate":
        validate(read(args.path), args.repository, args.sha)
    elif args.command == "seal":
        directory = Path(args.directory)
        write(
            directory / "checksums.json",
            {
                p.relative_to(directory)
                .as_posix(): hashlib.sha256(p.read_bytes())
                .hexdigest()
                for p in sorted(directory.rglob("*"))
                if p.is_file() and p.name != "checksums.json"
            },
        )
    else:
        directory = Path(args.directory).resolve()
        for relative, digest in read(directory / "checksums.json").items():
            path = (directory / relative).resolve()
            if (
                not path.is_relative_to(directory)
                or hashlib.sha256(path.read_bytes()).hexdigest() != digest
            ):
                raise ValueError("Release bundle checksum mismatch")


if __name__ == "__main__":
    main()
