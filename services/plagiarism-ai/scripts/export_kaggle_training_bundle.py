"""Create a code-only bundle that can be uploaded as a private Kaggle Dataset."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
INCLUDED_FILES = (
    "app/__init__.py",
    "app/config.py",
    "app/features.py",
    "app/models.py",
    "training/__init__.py",
    "training/train_classifier.py",
    "kaggle/requirements-training.txt",
    "kaggle/kaggle_train_classifier.ipynb",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def export_bundle(output_dir: Path) -> dict[str, str]:
    output_dir = output_dir.resolve()
    if output_dir == REPO_ROOT:
        raise ValueError("Output directory must not be the repository root")
    output_dir.mkdir(parents=True, exist_ok=True)

    manifest: dict[str, str] = {}
    for relative_name in INCLUDED_FILES:
        source = REPO_ROOT / relative_name
        if not source.is_file():
            raise FileNotFoundError(f"Missing source file: {relative_name}")
        destination = output_dir / relative_name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        manifest[relative_name] = sha256(destination)

    (output_dir / "BUNDLE_MANIFEST.json").write_text(
        json.dumps(
            {
                "purpose": "UGTM private Kaggle training-code bundle",
                "contains_source_corpus": False,
                "files": manifest,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=REPO_ROOT / "kaggle_bundle",
        help="Empty or dedicated output directory; it is ignored by Git.",
    )
    args = parser.parse_args()
    manifest = export_bundle(args.output)
    print(
        json.dumps(
            {
                "output": str(args.output.resolve()),
                "file_count": len(manifest),
                "next_step": "Upload this directory as a PRIVATE Kaggle Dataset.",
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
