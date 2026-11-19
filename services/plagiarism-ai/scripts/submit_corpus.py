"""Upload only ready, sanitized corpus files to the internal UGTM AI API."""

from __future__ import annotations

import argparse
import csv
import json
import mimetypes
import os
import time
import uuid
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def multipart_body(fields: dict[str, str], file_path: Path) -> tuple[bytes, str]:
    boundary = f"----UGTMFormBoundary{uuid.uuid4().hex}"
    line = f"--{boundary}\r\n".encode()
    chunks: list[bytes] = []
    for name, value in fields.items():
        chunks.extend(
            [
                line,
                f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode(),
                str(value).encode(),
                b"\r\n",
            ]
        )
    content_type = mimetypes.guess_type(file_path.name)[0] or "application/octet-stream"
    chunks.extend(
        [
            line,
            (
                'Content-Disposition: form-data; name="file"; '
                f'filename="{file_path.name}"\r\n'
            ).encode(),
            f"Content-Type: {content_type}\r\n\r\n".encode(),
            file_path.read_bytes(),
            b"\r\n",
            f"--{boundary}--\r\n".encode(),
        ]
    )
    return b"".join(chunks), boundary


def request_json(url: str, api_key: str, *, data: bytes | None = None, content_type: str | None = None) -> dict:
    headers = {"X-API-Key": api_key}
    if content_type:
        headers["Content-Type"] = content_type
    request = Request(url, data=data, headers=headers, method="POST" if data else "GET")
    try:
        with urlopen(request, timeout=60) as response:
            return json.loads(response.read().decode("utf-8"))
    except HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")[:500]
        raise RuntimeError(f"HTTP_{error.code}: {detail}") from error
    except URLError as error:
        raise RuntimeError(f"API_UNREACHABLE: {error.reason}") from error


def wait_for_job(base_url: str, api_key: str, job_id: str, interval: float, timeout: float) -> str:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        job = request_json(f"{base_url}/api/v1/jobs/{job_id}", api_key)
        if job["status"] in {"completed", "failed"}:
            return str(job["status"])
        time.sleep(interval)
    return "timeout"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--staging-dir", type=Path, required=True)
    parser.add_argument("--api-base-url", default="http://127.0.0.1:8000")
    parser.add_argument("--api-key-env", default="AI_API_KEY")
    parser.add_argument(
        "--corpus-id",
        default=None,
        help="Override corpus_id from manifest (e.g., 'uit_thesis')",
    )
    parser.add_argument("--limit", type=int)
    parser.add_argument("--poll", action="store_true")
    parser.add_argument("--poll-interval", type=float, default=3.0)
    parser.add_argument("--poll-timeout", type=float, default=1800.0)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    api_key = os.environ.get(args.api_key_env) or os.environ.get("API_KEY")
    if not api_key:
        try:
            import sys
            sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
            from app.config import settings
            api_key = settings.api_key
        except Exception:
            pass
    if not args.dry_run and not api_key:
        raise RuntimeError(f"Set the {args.api_key_env} environment variable first")
    if not args.manifest.is_file():
        raise FileNotFoundError(args.manifest)

    with args.manifest.open(newline="", encoding="utf-8") as source:
        rows = [row for row in csv.DictReader(source) if row.get("status") == "ready"]
    if args.limit is not None:
        rows = rows[: args.limit]
    if not rows:
        raise RuntimeError("No ready documents in the manifest")

    base_url = args.api_base_url.rstrip("/")
    outcomes: dict[str, str] = {}
    for row in rows:
        file_path = args.staging_dir / row["staged_filename"]
        if not file_path.is_file():
            outcomes[row["source_key"]] = "STAGED_FILE_MISSING"
            continue
        if args.dry_run:
            outcomes[row["source_key"]] = "DRY_RUN"
            continue

        target_corpus_id = args.corpus_id or row["corpus_id"]
        body, boundary = multipart_body(
            {
                "corpus_id": target_corpus_id,
                "source_id": row["source_key"],
                "version": row["version"],
            },
            file_path,
        )
        try:
            response = request_json(
                f"{base_url}/api/v1/corpus/documents",
                api_key,
                data=body,
                content_type=f"multipart/form-data; boundary={boundary}",
            )
            state = str(response["status"])
            if args.poll and state in {"queued", "existing"}:
                state = wait_for_job(
                    base_url,
                    api_key,
                    str(response["job_id"]),
                    args.poll_interval,
                    args.poll_timeout,
                )
            outcomes[row["source_key"]] = state
        except RuntimeError as error:
            outcomes[row["source_key"]] = str(error)

    print(json.dumps({"submitted": len(rows), "outcomes": outcomes}, ensure_ascii=False, indent=2))
    if any(state.startswith(("HTTP_", "API_", "STAGED_")) or state in {"failed", "timeout"} for state in outcomes.values()):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
