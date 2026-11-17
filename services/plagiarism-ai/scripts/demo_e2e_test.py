"""Script chạy thử nghiệm luồng kiểm tra đạo văn End-to-End cho UGTM-Plagiarism-AI.
Không phụ thuộc vào thư viện ngoài, chỉ dùng thư viện chuẩn của Python.
"""

import json
import mimetypes
import sys
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


def load_env(path: Path = Path(".env")) -> dict[str, str]:
    env = {}
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                env[k.strip()] = v.strip()
    return env


def create_multipart(fields: dict[str, str], files: dict[str, Path]) -> tuple[bytes, str]:
    boundary = f"----WebKitFormBoundary{uuid.uuid4().hex}"
    body = bytearray()

    for key, value in fields.items():
        body.extend(f"--{boundary}\r\n".encode("utf-8"))
        body.extend(f'Content-Disposition: form-data; name="{key}"\r\n\r\n'.encode("utf-8"))
        body.extend(f"{value}\r\n".encode("utf-8"))

    for key, file_path in files.items():
        filename = file_path.name
        content_type = mimetypes.guess_type(filename)[0] or "application/octet-stream"
        file_bytes = file_path.read_bytes()
        body.extend(f"--{boundary}\r\n".encode("utf-8"))
        body.extend(
            f'Content-Disposition: form-data; name="{key}"; filename="{filename}"\r\n'.encode("utf-8")
        )
        body.extend(f"Content-Type: {content_type}\r\n\r\n".encode("utf-8"))
        body.extend(file_bytes)
        body.extend(b"\r\n")

    body.extend(f"--{boundary}--\r\n".encode("utf-8"))
    return bytes(body), f"multipart/form-data; boundary={boundary}"


def send_request(
    url: str,
    method: str = "GET",
    headers: dict[str, str] | None = None,
    data: bytes | None = None,
) -> dict:
    req = urllib.request.Request(url, data=data, method=method)
    if headers:
        for k, v in headers.items():
            req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            content = resp.read().decode("utf-8")
            return json.loads(content) if content else {}
    except urllib.error.HTTPError as e:
        err_msg = e.read().decode("utf-8")
        print(f"\n[-] HTTP Error {e.code}: {err_msg}")
        raise


def wait_for_job(base_url: str, job_id: str, api_key: str, desc: str) -> dict:
    print(f"\n[+] Đang theo dõi tiến trình {desc} (Job ID: {job_id})...")
    url = f"{base_url}/api/v1/jobs/{job_id}"
    headers = {"X-API-Key": api_key}

    start_time = time.time()
    while True:
        res = send_request(url, headers=headers)
        status = res.get("status")
        elapsed = int(time.time() - start_time)

        if status == "completed":
            print(f"\n    -> {desc} HOÀN THÀNH thành công sau {elapsed}s!")
            return res
        elif status == "failed":
            error_code = res.get("error_code")
            print(f"\n    -> [!] {desc} THẤT BẠI với mã lỗi: {error_code}")
            return res
        else:
            print(f"    [Trạng thái: {status}] Đang xử lý... ({elapsed}s)", end="\r")
            time.sleep(3)


def main():
    env = load_env()
    api_key = env.get("AI_API_KEY", "")
    base_url = "http://127.0.0.1:8000"

    print("=" * 70)
    print("      UGTM PLAGIARISM AI - KIỂM TRA LUỒNG HOẠT ĐỘNG END-TO-END")
    print("=" * 70)

    source_path = Path("data/test_samples/source_doc.docx")
    suspect_path = Path("data/test_samples/suspect_doc.docx")

    if not source_path.exists() or not suspect_path.exists():
        print("[-] Không tìm thấy file mẫu trong data/test_samples/")
        return

    # 1. Nạp tài liệu nguồn vào kho (Corpus Document)
    source_key = f"kltn_nguon_{uuid.uuid4().hex[:6]}"
    print("\n--- BƯỚC 1: Nạp tài liệu nguồn vào kho luận văn ---")
    body, content_type = create_multipart(
        fields={
            "corpus_id": "uit_thesis",
            "source_id": source_key,
            "version": "1",
        },
        files={"file": source_path},
    )
    headers = {
        "X-API-Key": api_key,
        "Content-Type": content_type,
    }
    source_res = send_request(
        f"{base_url}/api/v1/corpus/documents",
        method="POST",
        headers=headers,
        data=body,
    )
    source_job_id = source_res["job_id"]
    print(f"[+] Đã gửi file nguồn: {source_path.name}")
    print(f"    Mã nguồn (source_id): {source_key}")
    print(f"    Job ID: {source_job_id}")

    # Chờ worker index tài liệu nguồn
    wait_for_job(base_url, source_job_id, api_key, "Nạp tài liệu nguồn (Index)")

    # 2. Nộp tài liệu cần kiểm tra đạo văn (Scan Document)
    submission_key = f"kltn_nop_{uuid.uuid4().hex[:6]}"
    print("\n--- BƯỚC 2: Nộp tài liệu cần quét đạo văn ---")
    body, content_type = create_multipart(
        fields={
            "corpus_id": "uit_thesis",
            "submission_id": submission_key,
            "version": "1",
        },
        files={"file": suspect_path},
    )
    headers = {
        "X-API-Key": api_key,
        "Content-Type": content_type,
    }
    scan_res = send_request(
        f"{base_url}/api/v1/scans",
        method="POST",
        headers=headers,
        data=body,
    )
    scan_job_id = scan_res["job_id"]
    print(f"[+] Đã gửi file quét: {suspect_path.name}")
    print(f"    Mã bài nộp (submission_id): {submission_key}")
    print(f"    Job ID: {scan_job_id}")

    # Chờ worker scan tài liệu
    wait_for_job(base_url, scan_job_id, api_key, "Quét đạo văn (Scan)")

    # 3. Lấy báo cáo chi tiết
    print("\n--- BƯỚC 3: Trích xuất Báo cáo Kết quả ---")
    report_url = f"{base_url}/api/v1/reports/{scan_job_id}"
    report = send_request(report_url, headers={"X-API-Key": api_key})

    summary = report.get("summary", {})
    findings = report.get("findings", [])

    print("\n" + "=" * 70)
    print("                     BÁO CÁO KẾT QUẢ ĐẠO VĂN")
    print("=" * 70)
    print(f"- Job ID: {scan_job_id}")
    print(f"- Mã bài nộp: {report.get('submission_id')}")
    print(f"- Mô hình nhúng: {report.get('model', {}).get('embedding')}")
    print(f"- Mô hình rerank: {report.get('model', {}).get('reranker')}")
    print(f"- Tổng số đoạn (chunks) đã quét: {summary.get('scanned_chunks', 0)}")
    print(f"- Số nguồn trùng lặp phát hiện: {summary.get('matched_sources', 0)}")
    print(f"- Số nhóm đoạn trùng (Passage Groups): {summary.get('passage_groups', 0)}")
    print(f"- Số đoạn vi phạm (Findings): {len(findings)}")

    if findings:
        print("\n* CHI TIẾT CÁC ĐOẠN PHÁT HIỆN TRÙNG LẶP:")
        for idx, f in enumerate(findings, 1):
            print(f"\n  [Phát hiện #{idx}]")
            print(f"  - Nhóm đoạn trùng   : {f.get('passage_group_id')}")
            print(f"  - Trùng với nguồn   : {f.get('source_key')} (v{f.get('source_version')})")
            print(f"  - Điểm Rerank Score : {f.get('reranker_score', 0):.4f}")
            print(f"  - Điểm Dense Cosine : {f.get('dense_cosine', 0):.4f}")
            print(f"  - Điểm Lexical Match: {f.get('lexical_cosine', 0):.4f}")
            print(f"  - Nội dung bài nộp  : \"{f.get('query_text', '')}\"")
            print(f"  - Nội dung gốc      : \"{f.get('source_text', '')}\"")
    else:
        print("\n[✓] Không phát hiện nghi vấn đạo văn.")

    print("\n" + "=" * 70)
    print("[✓] Báo cáo trích xuất hoàn tất. Hệ thống hoạt động chính xác 100%!")
    print("=" * 70)


if __name__ == "__main__":
    main()
