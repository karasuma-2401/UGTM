# Observability, Telemetry & Disaster Recovery Specification (CP-05.5)

Tài liệu này đặc tả kiến trúc quan sát (Observability), đo lường hiệu năng từng chặng (Stage Telemetry), chính sách bảo vệ dữ liệu văn bản học thuật trong nhật ký (Zero Raw-Text Logging Policy) và quy trình diễn tập sao lưu / phục hồi thảm họa (Disaster Recovery Drill) của hệ thống **UGTM-Plagiarism-AI**.

---

## 1. Mục tiêu & Nguyên tắc Giám sát (Monitoring Objectives)

### 1.1. Nguyên tắc Bảo mật Nhật ký (Zero Raw-Text Leakage Policy)
Để tuân thủ quyền riêng tư học thuật và sở hữu trí tuệ luận văn:
- **Tuyệt đối không in toàn bộ văn bản thô (raw text)** của bài nộp sinh viên hay tài liệu nguồn vào stdout, log file hoặc Celery task logs.
- Nhật ký chỉ được phép ghi nhận các trường số liệu tổng hợp:
  - `text_length`: Chiều dài chuỗi ký tự.
  - `chunks_count`: Số lượng phân đoạn.
  - `file_sha256`: Mã băm toàn vẹn tệp.
  - `tokens_estimated`: Ước lượng số token.

### 1.2. Nhận diện Đầy đủ Ngữ cảnh Thực thi (Full Traceability Context)
Mỗi bản ghi log hoặc telemetry metric bắt buộc mang đầy đủ các trường ngữ cảnh:
- `job_id`: Mã định danh duy nhất của job (UUIDv4).
- `run_id`: Mã định danh phiên thực thi (chuỗi ngẫu nhiên phân biệt giữa các lần retry).
- `attempt`: Thứ tự lần thử hiện tại trong vòng đời job ($1 \dots 5$).
- `stage`: Tên giai đoạn xử lý (`parse`, `encode`, `retrieve`, `rerank`, `align_and_citation`, `report_generation`, `index_db`).
- `worker_id`: Định danh tiến trình worker đảm nhiệm xử lý.
- `duration_ms`: Thời gian thực thi chính xác của giai đoạn (đơn vị mili-giây, độ chính xác 2 chữ số thập phân).
- `error_code`: Mã lỗi chuẩn hóa khi có sự cố.

---

## 2. Kiến trúc Đo lường Hiệu năng Từng Chặng (Stage Telemetry)

Module [`app/observability.py`](../app/observability.py) cung cấp lớp `StageTimer` quản lý việc bấm giờ từng chặng thông qua context manager:

```python
from app.observability import StageTimer

timer = StageTimer(job_id=job_id)

with timer.time_stage("parse", {"file_size_kb": 128}):
    chunks = parse_job(job)

with timer.time_stage("encode", {"chunks_count": len(chunks)}):
    embeddings = encode_texts([c.text for c in chunks])

with timer.time_stage("retrieve"):
    candidates = retrieve(...)

with timer.time_stage("rerank", {"pairs_count": len(candidates)}):
    scores = rerank_pairs(...)

with timer.time_stage("align_and_citation", {"findings_count": len(findings)}):
    align_and_evaluate_citations(...)

with timer.time_stage("report_generation"):
    report = build_report_v3(..., execution_telemetry=timer.to_telemetry_dict())
```

### 2.1. Cấu trúc Telemetry trong Báo cáo (Report Schema v3)
Thời gian thực thi và thống kê từng chặng được ghi trực tiếp vào `report.provenance.execution_telemetry`:

```json
{
  "execution_telemetry": {
    "job_id": "9f8c2d14-71a4-4e3a-8c05-3e19a72b4d90",
    "run_id": "a1b2c3d4e5f6",
    "total_duration_ms": 1420.50,
    "stages_ms": {
      "parse": 145.20,
      "encode": 310.80,
      "retrieve": 82.40,
      "rerank": 650.30,
      "align_and_citation": 185.10,
      "report_generation": 46.70
    },
    "metadata": {
      "parse": { "chunks_count": 48 },
      "encode": { "chunks_count": 48 },
      "rerank": { "pairs_count": 25 },
      "align_and_citation": { "findings_count": 12 }
    }
  }
}
```

---

## 3. Diễn tập Sao lưu & Phục hồi Thảm họa (Disaster Recovery Drill)

Script diễn tập [`scripts/backup_restore_drill.py`](../scripts/backup_restore_drill.py) được thực thi nhằm xác thực khả năng phục hồi dữ liệu khi xảy ra sự cố:

### 3.1. Phạm vi Sao lưu & Kiểm chứng Toàn vẹn
1. **Neon PostgreSQL Metadata**:
   - Bảng `integrity.sources` (toàn bộ danh mục tài liệu nguồn, hash SHA-256, cờ `is_active`).
   - Bảng `integrity.chunks` (số lượng chunk, tọa độ offset, page number, embedding vectors).
   - Bảng `integrity.jobs` (lịch sử trạng thái, attempt, heartbeats, checkpoints).
2. **Model Artifacts & Core Manifests**:
   - `artifacts/ai_release_candidate_rc1.manifest.json`
   - `artifacts/suspicious_passage_classifier.manifest.json`
   - `configs/retrieval_config.json`
   - `configs/preprocessing_config.json`
   - `data/benchmark_v1/split_manifest.json`

### 3.2. Bằng chứng Phục hồi Thực tế (`data/restore_drill_evidence.json`)
Kết quả đo đạc từ kịch bản diễn tập:
- **Tình trạng diễn tập**: `drill_status: SUCCESS`
- **Tính toàn vẹn (Parity Check)**: 100% khớp mã băm SHA-256 giữa bản snapshot và bản phục hồi in-memory.
- **RPO (Recovery Point Objective)**: $< 1$ phút.
- **RTO (Recovery Time Objective)**: $< 10$ giây đối với metadata và artifacts cấu hình.
- **Tất cả các manifest cốt lõi**: `status: intact`.

---

## 4. Danh mục Mã lỗi Chuẩn hóa (Error Code Directory)

Khi có sự cố xảy ra, worker và API gán mã lỗi chuẩn hóa và ghi log có cấu trúc để phục vụ điều tra nguyên nhân gốc rễ (Root Cause Analysis - RCA):

| Nhóm lỗi | Mã lỗi (`error_code`) | Nguyên nhân kích hoạt | Cơ chế tự phục hồi / Xử lý |
|---|---|---|---|
| **Dữ liệu đầu vào** | `EMPTY_TEXT` | Tệp nộp không chứa ký tự văn bản có thể trích xuất. | Terminal `failed`, không retry. |
| | `CORRUPTED_DOCUMENT` | Tệp PDF/DOCX bị hỏng cấu trúc nhị phân. | Terminal `failed`, không retry. |
| | `UNSUPPORTED_FILE_FORMAT` | Tệp `.doc` (Word 97–2003) hoặc định dạng không hỗ trợ. | Terminal `failed`, trả HTTP 415. |
| **Lưu trữ tệp** | `STORAGE_FILE_NOT_FOUND` | Khóa `object_key` không tồn tại trong S3/R2 storage. | Terminal `failed`, thông báo UGTM kiểm tra bucket. |
| | `FILE_CHECKSUM_MISMATCH` | Tệp tải về bị sai lệch mã băm SHA-256 so với DB. | Terminal `failed`, cảnh báo vi phạm toàn vẹn tệp. |
| | `STORAGE_TRANSIENT_ERROR` | Lỗi timeout hoặc gián đoạn mạng khi tải tệp từ S3. | Tự động chuyển `waiting_for_compute`, retry backoff. |
| **Hạ tầng AI/GPU** | `GPU_UNAVAILABLE` | Endpoint suy luận Colab GPU trả HTTP 503 hoặc timeout. | Tự động chuyển `waiting_for_compute`, retry backoff. |
| | `COLAB_DISCONNECTED` | Kết nối WebSocket / HTTP tới worker GPU bị đứt gãy. | Tự động chuyển `waiting_for_compute`, retry backoff. |
| | `GPU_UNAVAILABLE_EXHAUSTED`| Vượt quá số lần thử tối đa (`max_attempts = 5`). | Chuyển sang `failed`, báo động quản trị viên. |
| **Worker / Hệ thống** | `WORKER_HEARTBEAT_TIMEOUT` | Worker bị crash hoặc mất nguồn khi đang xử lý job. | Reconciler phát hiện heartbeat hết hạn, tự reclaim. |

