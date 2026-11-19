"""GitHub runner entry point for monitor, backup, restore, smoke and rollback."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import sys

from production_pipeline import Remote, vercel, previous_frontend
from production_smoke import health, authenticated


def rollback_frontend(record):
    previous = record["previous_vercel_url"]
    candidate = record["vercel_url"]
    if not all(
        re.fullmatch(r"https://[A-Za-z0-9.-]+\.vercel\.app", url)
        for url in (previous, candidate)
    ):
        raise ValueError("Invalid Vercel release URL")
    active = previous_frontend()
    if active == previous:
        return
    if active != candidate:
        raise ValueError(
            "Vercel is serving a different deployment; inspect it before recovery"
        )
    vercel("rollback", previous, "--yes")


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument(
        "action",
        choices=[
            "monitor",
            "backup",
            "restore-drill",
            "smoke-auth",
            "smoke-gpu",
            "rollback",
            "recover",
            "images",
            "cleanup",
        ],
    )
    parser.add_argument("--attempt")
    args = parser.parse_args()
    directory = Path(os.environ.get("RUNNER_TEMP", "artifacts")) / "ugtm-operations"
    os.umask(0o077)
    directory.mkdir(parents=True, exist_ok=True)
    try:
        remote = Remote(directory)
        current = remote.execute('readlink -f "$HOME/ugtm/production/current"')
        if not re.fullmatch(
            r"/home/[a-z_][a-z0-9_-]*/ugtm/production/releases/[0-9a-f]{40}-\d+-\d+",
            current,
        ):
            raise ValueError("Operations require a release from the new pipeline")
        if args.action in ("smoke-auth", "smoke-gpu"):
            health(os.environ["APP_URL"], os.environ["API_HOST"])
            authenticated(os.environ["APP_URL"], scan=args.action == "smoke-gpu")
        elif args.action == "monitor":
            result = json.loads(
                remote.execute(
                    f'python3 "{current}/scripts/production_monitor.py" || true'
                )
            )
            (directory / "monitor.json").write_text(json.dumps(result, indent=2))
            print(json.dumps(result))
            health(os.environ["APP_URL"], os.environ["API_HOST"])
            if result["alerts"]:
                raise ValueError("Production monitoring reported alerts")
        elif args.action == "backup":
            print(
                remote.execute(
                    f'bash "{current}/scripts/Backup-Production.sh"', timeout=3500
                )
            )
        elif args.action == "restore-drill":
            print(
                remote.execute(
                    f'bash "{current}/scripts/Restore-Drill.sh"', timeout=3500
                )
            )
        elif args.action == "cleanup":
            print(
                remote.execute(
                    f'python3 "{current}/scripts/production_cleanup.py" --execute'
                )
            )
        elif args.action == "rollback":
            print(
                remote.execute(
                    f'bash "{current}/scripts/Deploy-Production.sh" check-rollback'
                )
            )
            record = json.loads(remote.execute(f'cat "{current}/deployment.json"'))
            rollback_frontend(record)
            # Use an absolute path directly; Remote.phase normally uses a home-relative attempt.
            print(
                remote.execute(
                    f'bash "{current}/scripts/Deploy-Production.sh" rollback-current'
                )
            )
            health(os.environ["APP_URL"], os.environ["API_HOST"])
        elif args.action == "recover":
            if not args.attempt or not re.fullmatch(
                r"[0-9a-f]{40}-\d+-\d+", args.attempt
            ):
                raise ValueError(
                    "Recover requires the unfinished deployment attempt ID"
                )
            path = f"ugtm/production/releases/{args.attempt}"
            remote.phase(path, "check-candidate-rollback")
            record = json.loads(remote.execute(f'cat "$HOME/{path}/deployment.json"'))
            rollback_frontend(record)
            remote.phase(path, "rollback")
            health(os.environ["APP_URL"], os.environ["API_HOST"])
        else:
            # No resolved env or connection strings leave the VM.
            record = json.loads(remote.execute(f'cat "{current}/release.json"'))
            images = [
                {"component": name, "image": image["reference"]}
                for name, image in record["images"].items()
            ]
            images.extend(
                {"component": name, "image": image}
                for name, image in record["rollback_policy"]["infrastructure"].items()
            )
            (directory / "images.json").write_text(json.dumps(images))
            print(json.dumps(images))
    except Exception as error:
        print(
            str(error)
            if isinstance(error, ValueError)
            else f"Operation failed ({type(error).__name__}); inspect the public status summary and private VM configuration."
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
