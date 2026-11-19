"""Conservative release-directory retention. Dry run by default; never removes volumes or images."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil


def candidates(root, keep=5, days=30):
    root = root.resolve()
    protected = {
        (root / name).resolve()
        for name in ("current", "previous")
        if (root / name).exists()
    }
    pending = root / "pending"
    if pending.exists():
        raise ValueError("Resolve the pending deployment before cleanup")
    completed = []
    for directory in (root / "releases").iterdir():
        if (
            directory.is_symlink()
            or not directory.is_dir()
            or not re.fullmatch(r"[0-9a-f]{40}-\d+-\d+", directory.name)
        ):
            continue
        state = directory / "status.json"
        if state.exists() and json.loads(state.read_text()).get("status") in (
            "deployed",
            "rolled_back",
        ):
            completed.append(directory)
    completed.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    protected.update(completed[:keep])
    return [
        p
        for p in completed
        if p not in protected
        and p.resolve().is_relative_to(root / "releases")
        and datetime.now(timezone.utc).timestamp() - p.stat().st_mtime > days * 86400
    ]


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--root", type=Path, default=Path.home() / "ugtm/production")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args()
    try:
        import fcntl

        with (args.root / "deploy.lock").open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            for path in candidates(args.root):
                print(
                    f"{'Remove' if args.execute else 'Would remove'} release {path.name}"
                )
                if args.execute:
                    backup = args.root / "backup-status.json"
                    if (
                        not backup.exists()
                        or json.loads(backup.read_text()).get("status") != "backed_up"
                        or datetime.now(timezone.utc).timestamp()
                        - backup.stat().st_mtime
                        > 36 * 3600
                    ):
                        raise ValueError(
                            "A successful off-VM backup within 36 hours is required"
                        )
                    shutil.rmtree(path)
    except Exception as error:
        print(
            str(error)
            if isinstance(error, ValueError)
            else "Release cleanup could not complete"
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
