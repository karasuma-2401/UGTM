"""HTTP smoke checks with cookies kept in memory and no response-body logging."""

from __future__ import annotations

import argparse
import hashlib
import http.cookiejar
import io
import json
import os
import time
import urllib.error
import urllib.request
from urllib.parse import urlsplit
from uuid import uuid4
import zipfile


class Client:
    def __init__(self, origin):
        self.origin = origin.rstrip("/")
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())
        )
        self.token = None

    def request(
        self,
        path,
        method="GET",
        body=None,
        content_type="application/json",
        csrf=None,
        expected=(200,),
    ):
        if not path.startswith("/") or path.startswith("//"):
            raise ValueError(
                "Smoke paths must be relative to the configured application"
            )
        headers = {"Content-Type": content_type, "Origin": self.origin}
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        if csrf:
            headers["X-CSRF-TOKEN"] = csrf
        data = json.dumps(body).encode() if isinstance(body, dict) else body
        request = urllib.request.Request(
            self.origin + path, data=data, method=method, headers=headers
        )
        try:
            with self.opener.open(request, timeout=15) as response:
                status, raw = response.status, response.read(4 * 1024 * 1024)
        except urllib.error.HTTPError as error:
            status, raw = error.code, b""
        if status not in expected:
            raise RuntimeError(
                f"Smoke request failed: HTTP {status}, expected {expected}"
            )
        return json.loads(raw) if raw and raw.lstrip().startswith((b"{", b"[")) else raw

    def csrf(self):
        return self.request("/api/identity/auth/csrf")["data"]["token"]


def health(origin, api_host):
    for url in (
        f"https://{api_host}/api/modules/Identity/ping",
        f"https://{api_host}:8443/minio/health/live",
    ):
        # curl bounds connection, each request, and the total retry budget.
        import subprocess

        subprocess.run(
            [
                "curl",
                "--fail",
                "--silent",
                "--show-error",
                "--output",
                os.devnull,
                "--connect-timeout",
                "5",
                "--max-time",
                "15",
                "--retry",
                "3",
                "--retry-delay",
                "2",
                "--retry-all-errors",
                "--retry-max-time",
                "55",
                url,
            ],
            check=True,
            timeout=75,
        )
    client = Client(origin)
    client.request("/")
    client.request("/api/modules/Identity/ping")
    client.request("/api/identity/auth/me", expected=(401,))
    client.csrf()


def document():
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w") as archive:
        archive.writestr(
            "[Content_Types].xml",
            '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>',
        )
        archive.writestr(
            "_rels/.rels",
            '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>',
        )
        paragraphs = "".join(
            f"<w:p><w:r><w:t>Release smoke test paragraph {i}. This synthetic document checks reliable academic document processing, storage durability, asynchronous scheduling and report delivery.</w:t></w:r></w:p>"
            for i in range(40)
        )
        archive.writestr(
            "word/document.xml",
            f'<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{paragraphs}</w:body></w:document>',
        )
    return output.getvalue()


def authenticated(origin, scan=False):
    client = Client(origin)
    user, password = os.environ["SMOKE_USER"], os.environ["SMOKE_PASSWORD"]
    if not user or not password:
        raise RuntimeError("Configure a dedicated smoke account")
    login = client.request(
        "/api/identity/auth/login",
        "POST",
        {"identifier": user, "password": password},
        csrf=client.csrf(),
    )
    client.token = login["data"]["accessToken"]
    try:
        client.request("/api/identity/auth/me")
        client.token = None
        refreshed = client.request(
            "/api/identity/auth/refresh", "POST", csrf=client.csrf()
        )
        client.token = refreshed["data"]["accessToken"]
        client.request("/api/identity/auth/me")
        if os.environ.get("SMOKE_DENIED_PATH"):
            client.request(os.environ["SMOKE_DENIED_PATH"], expected=(403,))
        if os.environ.get("SMOKE_FILE_ID"):
            result = client.request(
                f"/api/thesis/final-report-files/{os.environ['SMOKE_FILE_ID']}/download"
            )
            url = result["data"]["downloadUrl"]
            target = urlsplit(url)
            if (
                target.scheme != "https"
                or target.hostname != os.environ["API_HOST"]
                or target.port != 8443
            ):
                raise RuntimeError(
                    "Download URL is outside the configured storage origin"
                )
            with urllib.request.urlopen(url, timeout=20) as response:
                content = response.read(25 * 1024 * 1024)
            if not content:
                raise RuntimeError("Empty downloaded file")
            if (
                os.environ.get("SMOKE_FILE_SHA256")
                and hashlib.sha256(content).hexdigest()
                != os.environ["SMOKE_FILE_SHA256"]
            ):
                raise RuntimeError("Downloaded fixture checksum mismatch")
        if scan:
            # Explicit opt-in only. The group must be a dedicated test group in an open submission window.
            group = os.environ["SMOKE_GROUP_ID"]
            boundary = uuid4().hex
            body = (
                (
                    f'--{boundary}\r\nContent-Disposition: form-data; name="groupId"\r\n\r\n{group}\r\n'
                    f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="release-smoke.docx"\r\n'
                    "Content-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n"
                ).encode()
                + document()
                + f"\r\n--{boundary}--\r\n".encode()
            )
            result = client.request(
                "/api/thesis/final-reports",
                "POST",
                body,
                f"multipart/form-data; boundary={boundary}",
            )
            report = result["data"]["finalReportId"]
            deadline = time.monotonic() + 600
            while time.monotonic() < deadline:
                result = client.request(
                    f"/api/thesis/final-reports/{report}/similarity-report",
                    expected=(200, 404, 409),
                )
                if (
                    isinstance(result, dict)
                    and result.get("data", {}).get("status", "").lower() == "completed"
                ):
                    break
                time.sleep(10)
            else:
                raise RuntimeError("Scan/report did not complete within 600 seconds")
    finally:
        client.token = None
        client.request(
            "/api/identity/auth/logout", "POST", csrf=client.csrf(), expected=(200, 204)
        )


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--origin", default=os.environ.get("APP_URL"))
    parser.add_argument("--auth", action="store_true")
    parser.add_argument("--scan", action="store_true")
    args = parser.parse_args()
    try:
        health(args.origin, os.environ["API_HOST"])
        if args.auth or args.scan:
            authenticated(args.origin, args.scan)
        print("Smoke checks passed; credentials and response bodies were not logged.")
    except Exception:
        print(
            "Smoke checks failed. Check the configured test account, permissions, endpoints and infrastructure."
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
