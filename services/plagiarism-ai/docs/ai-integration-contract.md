# UGTM ↔ UGTM-Plagiarism-AI — Hợp đồng Tích hợp & Ranh giới Hệ thống (`CP-00.2`)

- **Phiên bản hợp đồng:** `v1.0-draft` (khởi tạo tại `CP-00.2`, kiểm chứng thực tế ở `CP-05.4`)
- **Ngày cập nhật:** `2026-10-30`
- **Căn cứ mã nguồn thực tế đã đối chiếu:**
  - **Hệ thống nghiệp vụ UGTM:** [`karasuma-2401/UGTM`](https://github.com/karasuma-2401/UGTM) — nhánh `dev`, commit `799d6f910e795fb9b6fd49dd332e3de2511d914b` (`ASP.NET Core .NET 10`, Modular Monolith, PostgreSQL/Neon, S3/MinIO private storage, React 19).
  - **Dịch vụ AI đối chiếu tương đồng:** `UGTM-Plagiarism-AI` — nhánh `dev`, commit `78e4a94f173a32f178bab70568d397625227533d` (`FastAPI`, `Celery`, `Neon pgvector + pg_trgm`, `Colab Remote GPU Worker`).

---

## 1. Nguyên tắc Ranh giới Kiến trúc (System Boundary & Ownership)

Dịch vụ `UGTM-Plagiarism-AI` là **công cụ phân tích và truy xuất bằng chứng tương đồng nội bộ (headless internal AI service)**. Dịch vụ AI **không** thay thế backend nghiệp vụ UGTM, **không** quản lý tài khoản người dùng cuối và **không** tự động đưa ra phán quyết kỷ luật đạo văn.

| Trách nhiệm | Hệ thống nắm giữ | Quy tắc bắt buộc |
|---|---|---|
| **Xác thực người dùng & Phân quyền (RBAC/ABAC)** | **UGTM (`.NET 10`)** — Module `Identity`, `ThesisManagement`, `TopicCatalog`, `DefenseCouncil` | Trình duyệt (React SPA của sinh viên/giảng viên) chỉ giao tiếp với UGTM qua JWT/Cookie của UGTM. Trình duyệt **tuyệt đối không** giữ `X-API-Key` và không gọi trực tiếp sang FastAPI. |
| **Lưu trữ tệp gốc (Source of Truth for Files)** | **UGTM (`.NET 10`)** — `S3ThesisSubmissionObjectStorage` (private bucket) + bảng `SubmissionFile` | UGTM giữ tệp gốc vĩnh viễn theo vòng đời học vụ (`thesis-submissions/finalreport/{entityId:N}/v{versionNo}/{guid:N}{ext}`). AI worker chỉ tải tệp vào thư mục tạm để parse rồi xóa ngay sau khi xử lý xong. |
| **Giao tác nộp bài của sinh viên** | **UGTM (`.NET 10`)** — `SubmitFinalReportHandler` | Sinh viên nộp bài (`POST /api/thesis/final-reports`) phải thành công độc lập 100% với trạng thái của AI service. Khi AI hoặc Colab GPU đang offline, yêu cầu quét được xếp hàng/chờ xử lý bất đồng bộ, **không bao giờ chặn hoặc làm lỗi thao tác nộp bài**. |
| **Quản lý chỉ mục vector/lexical & Xử lý suy luận** | **UGTM-Plagiarism-AI** — Schema `integrity` trên Neon riêng + Remote GPU Worker | Database `integrity` (`jobs`, `sources`, `chunks`) tách biệt hoàn toàn khỏi 6 `DbContext` nghiệp vụ của UGTM. Không chia sẻ bảng hay EF Core entity qua lại. |
| **Quyết định học thuật & Trạng thái đánh giá** | **Giảng viên trên UGTM** (`GVHD`, `GVPB`, `Hội đồng`, `Khoa`) | Output của AI chỉ là **bằng chứng tương đồng (`similarity evidence`)** kèm ngữ cảnh trích dẫn và cờ `review_required`. Phê duyệt/yêu cầu sửa/chấm điểm thuộc độc quyền nghiệp vụ UGTM (`FinalReport`, `SubmissionReview`, `DefenseCouncil`). |

---

## 2. Đối chiếu Thực thể thực tế từ Repo `karasuma-2401/UGTM`

Qua kiểm tra mã nguồn nhánh `dev` của `karasuma-2401/UGTM`, luồng báo cáo cuối kỳ (`SCRUM-102`, migration `20261027174826_AddFinalReportSubmissionFoundation`) đã có sẵn các điểm neo dữ liệu sau:

### 2.1. Ánh xạ định danh (Identifier Mapping) giữa UGTM và AI Service

Regex kiểm tra khóa hiện tại của AI Service (`app/main.py`): `^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$` — tương thích hoàn toàn với `Guid.ToString("D")` (36 ký tự hex + dấu `-`) của `.NET`.

| Khái niệm | Thực thể / Trường thực tế trong `UGTM` (`.NET 10`) | Trường tương ứng trong `UGTM-Plagiarism-AI` | Quy ước ánh xạ chuẩn |
|---|---|---|---|
| **Kho đối chiếu** | Đợt/Khoa hoặc Kho lưu chiểu thư viện (`KltnBatch.Id` / Library Corpus) | `corpus_id` (`str`) | Dùng mã định danh ổn định theo phạm vi được cấp phép, ví dụ: `uit-kltn-library-v1` (hoặc `batch-{BatchId:D}` nếu kho đóng theo đợt). |
| **Bài nộp cần quét** | `FinalReport.Id` (`Guid`) — duy nhất cho mỗi `(GroupId, VersionType)` (`InitialFinal` / `RevisedAfterReviewer`) | `submission_id` (`document_key` khi `job_type='scan'`) | Truyền `FinalReport.Id.ToString("D")` (ví dụ: `d84b1c2e-5f7a-4b1e-9c0d-123456789abc`). *(Tùy chọn mở rộng cho `MidtermReport`: tiền tố `midterm-{MidtermReport.Id:D}`)*. |
| **Phiên bản bài nộp** | `FinalReport.CurrentVersionNo` / `SubmissionFile.VersionNo` (`int >= 1`, tăng `1 -> 2 -> 3` khi nộp lại trước `UnderEvaluation`) | `version` (`int >= 1`) | Ánh xạ 1–1 (`version = SubmissionFile.VersionNo`). Mỗi lần sinh viên nộp lại tạo một job scan mới với `version` mới, giữ nguyên báo cáo của version cũ. |
| **Tài liệu nguồn trong kho** | Khóa luận lưu chiểu đã duyệt (`FinalSubmission` ở trạng thái `ArchiveConfirmed` được phép đưa vào kho) hoặc tài liệu thư viện cấp | `source_id` (`document_key` khi `job_type='index'`) | Truyền mã nguồn đã khử định danh cá nhân nhạy cảm (ví dụ `kltn-2025-0042` hoặc `archive-{FinalSubmission.Id:D}`) kèm `version`. |
| **Toàn vẹn tệp** | `SubmissionFile.ChecksumSha256` (`string`, hex SHA-256) | `file_sha256` (`str`, hex SHA-256) | Dùng để kiểm tra idempotency (`202 existing` nếu cùng `(job_type, corpus_id, id, version, sha256)`, trả `409 Conflict` nếu cùng `id + version` nhưng khác `sha256`). |
| **Sự kiện kích hoạt** | `SubmitFinalReportHandler` đã ghi `AuditIntegrationEntry("FinalReport", report.Id, FinalReportSubmitted, ...)` vào `IThesisManagementAuditOutbox` cùng transaction DB | Background adapter gọi `POST /api/v1/scans` | UGTM kích hoạt gọi AI **sau khi** transaction PostgreSQL commit (qua background outbox/worker của UGTM), không gọi HTTP tới AI bên trong transaction nộp bài. |

### 2.2. Xử lý khác biệt định dạng tệp (`.doc` vs `.pdf` / `.docx`) — Phát hiện quan trọng

- **Thực tế trong `UGTM` (`ThesisSubmissionObjectKeyFactory.cs` & `SCRUM-102`):** `FinalReport` chấp nhận **`.pdf`, `.doc`, `.docx`** có kích thước `<= 20 MiB` (`20,971,520` bytes).
- **Thực tế trong `UGTM-Plagiarism-AI`:** Chỉ hỗ trợ trích xuất văn bản chuẩn xác cho **`.pdf`** (có text layer) và **`.docx`** (`<= 20 MiB`); không hỗ trợ định dạng nhị phân cũ `.doc` của Word 97–2003.
- **Quy ước hợp đồng:**
  1. Nếu tệp nộp là `.doc`, AI Service trả `HTTP 415 Unsupported Media Type` kèm mã lỗi chuẩn `UNSUPPORTED_FILE_FORMAT`.
  2. Adapter phía UGTM khi thấy `.doc` (hoặc nhận `415`) sẽ đánh dấu trạng thái kiểm tra tương đồng của phiên bản đó là `UnsupportedFormat` (hiển thị ghi chú cho GVHD), **không retry vô hạn** và **không báo lỗi nộp bài cho sinh viên**.

---

## 3. Ma trận Phân quyền Truy cập Báo cáo Tương đồng (Authorization Policy)

Căn cứ hệ thống Role (`SystemRoleName.cs`), Permission (`PermissionCodes.cs`) và logic kiểm tra quyền đọc báo cáo (`FinalReportReadAccessChecker.cs`) đang có trong `karasuma-2401/UGTM`, quyền đối với báo cáo AI được phân tách như sau:

| Vai trò trong UGTM | Gọi trực tiếp FastAPI? | Quyền kích hoạt quét / quét lại | Quyền xem Evidence Report qua UGTM API | Lý do bảo mật & học thuật |
|---|---|---|---|---|
| **UGTM Backend Service** | **Có** (`X-API-Key` nội bộ) | Tự động khi có bản nộp mới hoặc theo lệnh hợp lệ từ GVHD/Admin | Đọc toàn bộ JSON report để lưu snapshot / phân phối theo quyền | Điểm kiểm soát truy cập (PEP) duy nhất trước người dùng cuối. |
| **Student** *(thành viên `Confirmed` của `StudentGroup`)* | Không | Gián tiếp khi nộp/nộp lại `FinalReport` | **Mặc định: KHÔNG xem `findings` / `source_text`** (chỉ biết bài nộp đã tiếp nhận hợp lệ) | Báo cáo chứa đoạn trích (`source_text`) từ khóa luận của nhóm khác hoặc kho nội bộ thư viện; mở cho sinh viên sẽ làm lộ tài liệu nguồn và tạo vòng lặp lách thuật toán. |
| **Lecturer — GVHD hiện tại** *(khớp `AdvisorId` qua `ITopicAdvisorAssignmentContract`)* | Không | Được yêu cầu quét lại (`rescan`) khi kho nguồn cập nhật | **Xem đầy đủ** (`summary`, `findings`, `source_text`, `citation_status`) và ghi nhận xét thẩm định | Đúng nguyên tắc `FinalReportReadAccessChecker`: nếu đổi GVHD, GVHD cũ tự động mất quyền xem. |
| **CouncilMember / GVPB** *(được phân công ở `DefenseCouncil` / `SCRUM-103`)* | Không | Không | **Xem đầy đủ** trên phiên bản đã khóa (`UnderEvaluation`) của nhóm được phân công | Phục vụ chấm phản biện và hội đồng bảo vệ. |
| **Faculty** *(có quyền `thesis.review-gates.review`)* | Không | Chỉ xem / giám sát học vụ | **Xem đầy đủ** theo phạm vi khoa quản lý | Khớp chính sách quyền đọc `FinalReport` hiện có của UGTM. |
| **Admin** *(hoặc Quản trị thư viện)* | Không (đi qua UGTM Admin API) | Quản lý nạp/ẩn nguồn corpus (`POST /api/v1/corpus/documents`), retry job lỗi | **Xem đầy đủ** & xem trạng thái vận hành job | Quản trị hệ thống và tuân thủ quyền thu hồi tài liệu nguồn của thư viện. |

---

## 4. Đặc tả Giao diện API (Service-to-Service Contract)

### 4.1. Xác thực & Headers chung
- **Header bắt buộc cho mọi endpoint nghiệp vụ:**
  - `X-API-Key: <UGTM_AI_SERVICE_KEY>` *(khóa bí mật chia sẻ riêng giữa backend UGTM và FastAPI; endpoint `/api/v1/client-config` sẽ bỏ trả `api_key` ở `CP-01.6`)*.
  - `X-Request-ID: <uuid>` *(khuyến nghị từ `CP-01.6` để nối log giữa `.NET` và `FastAPI/Celery`)*.
- **Phương thức truyền tệp:**
  - **Chế độ chuẩn hiện hành (`Multipart Push` — dùng cho Client Test & MVP):** UGTM hoặc test harness stream tệp qua `multipart/form-data`.
  - **Chế độ lưu trữ dùng chung (`Presigned URL / Object Reference` — bổ sung ở `CP-01.4`):** UGTM cấp presigned GET URL ngắn hạn từ `IThesisSubmissionObjectStorage.CreateDownloadUriAsync(...)` hoặc shared storage key kèm `file_sha256`; khi retry job quá hạn URL, worker yêu cầu URL mới hoặc đọc qua storage adapter đã phân quyền.

### 4.2. Danh mục Endpoints cốt lõi

| Method | Endpoint | Mục đích | Mã phản hồi chính |
|---|---|---|---|
| `GET` | `/healthz` | Kiểm tra liveness của API điều phối (không phụ thuộc GPU đang bật hay tắt) | `200 OK` |
| `POST` | `/api/v1/corpus/documents` | Đăng ký / cập nhật phiên bản tài liệu nguồn vào kho tham chiếu | `202 Accepted`, `400`, `401`, `409`, `413`, `415`, `422` |
| `GET` | `/api/v1/corpus/documents` | Liệt kê danh mục nguồn và trạng thái `is_active` theo `corpus_id` | `200 OK`, `401` |
| `POST` | `/api/v1/scans` | Tạo job quét tương đồng cho một phiên bản bài nộp | `202 Accepted`, `400`, `401`, `409`, `413`, `415`, `422` |
| `GET` | `/api/v1/jobs/{job_id}` | Tra cứu trạng thái vòng đời job | `200 OK`, `401`, `404` |
| `GET` | `/api/v1/reports/{job_id}` | Lấy báo cáo bằng chứng tương đồng khi `status == completed` *(hỗ trợ query phân trang ở v3)* | `200 OK`, `401`, `404`, `409` |

---

## 5. Request / Response Mẫu & Lộ trình Schema (`v2` → `v3`)

Các tệp JSON mẫu độc lập dùng cho automated contract test được lưu tại [`docs/contracts/`](./contracts/).

### 5.1. Tạo Job Quét bài nộp — `POST /api/v1/scans`

**Request (`multipart/form-data`):**
```http
POST /api/v1/scans HTTP/1.1
Host: ai-internal.ugtm.local
X-API-Key: <REDACTED_SERVICE_API_KEY>
X-Request-ID: 6b1e0f4c-2a3d-4c89-9b5e-91c2b8f7a101
Content-Type: multipart/form-data; boundary=----UGTMBoundary

------UGTMBoundary
Content-Disposition: form-data; name="corpus_id"

uit-kltn-library-v1
------UGTMBoundary
Content-Disposition: form-data; name="submission_id"

d84b1c2e-5f7a-4b1e-9c0d-123456789abc
------UGTMBoundary
Content-Disposition: form-data; name="version"

2
------UGTMBoundary
Content-Disposition: form-data; name="file"; filename="KLTN_Nhom12_v2.pdf"
Content-Type: application/pdf

%PDF-1.7 ... <binary stream <= 20 MiB> ...
------UGTMBoundary--
```

**Response `202 Accepted` (tạo mới hoặc gọi lại idempotent cùng SHA-256):**
```json
{
  "job_id": "9f8c2d14-71a4-4e3a-8c05-3e19a72b4d90",
  "status": "queued"
}
```
*(Nếu gọi trùng `(corpus_id, submission_id, version)` với cùng `file_sha256`, API trả `202` với `"status": "existing"` và đúng `job_id` cũ; nếu khác `file_sha256`, trả `409 Conflict`)*

### 5.2. Tra cứu Trạng thái Job — `GET /api/v1/jobs/{job_id}`

**Response `200 OK`:**
```json
{
  "job_id": "9f8c2d14-71a4-4e3a-8c05-3e19a72b4d90",
  "type": "scan",
  "status": "processing",
  "error_code": null,
  "updated_at": "2026-10-30T03:05:12.410284+00:00"
}
```

- **Tập trạng thái hiện có ở B0 (`v2`):** `queued` | `processing` | `completed` | `failed`.
- **Tập trạng thái mở rộng ở `CP-05.1` (`v3`):** bổ sung `waiting_for_compute` (khi Colab GPU tạm thời offline, job được bảo toàn không thất bại giả) và `cancelled`. Client UGTM nên map mọi trạng thái khác `completed`/`failed`/`cancelled` vào nhóm *Đang xử lý (`InProgress`)*.
- **Mã lỗi chuẩn (`error_code`):** `CORPUS_EMPTY`, `EMPTY_TEXT`, `TOO_MANY_PAGES`, `DOCUMENT_TOO_LONG`, `UNSUPPORTED_FILE_FORMAT`, `COMPUTE_UNAVAILABLE_TIMEOUT`, `INDEX_ERROR`, `SCAN_ERROR`.

### 5.3. Báo cáo Bằng chứng Tương đồng — `GET /api/v1/reports/{job_id}`

#### A. Hợp đồng Baseline hiện hành (`schema_version: 2` — đang chạy trong code B0)
```json
{
  "schema_version": 2,
  "job_id": "9f8c2d14-71a4-4e3a-8c05-3e19a72b4d90",
  "corpus_id": "uit-kltn-library-v1",
  "submission_id": "d84b1c2e-5f7a-4b1e-9c0d-123456789abc",
  "submission_version": 2,
  "model": {
    "embedding": "BAAI/bge-m3",
    "reranker": "BAAI/bge-reranker-v2-m3",
    "classifier_version": null,
    "detector_mode": "heuristic"
  },
  "summary": {
    "scanned_chunks": 84,
    "flagged_pairs": 3,
    "findings_returned": 3,
    "matched_sources": 2,
    "passage_groups": 2
  },
  "findings": [
    {
      "passage_group_id": "PG-0001",
      "submission_chunk_no": 14,
      "submission_page": 8,
      "submission_start": 1240,
      "submission_end": 1665,
      "submission_text": "Mô hình Transformer sử dụng cơ chế Self-Attention để biểu diễn quan hệ phụ thuộc xa...",
      "chunk_id": 1042,
      "source_key": "kltn-2025-0019",
      "source_version": 1,
      "source_filename": "kltn-2025-0019.pdf",
      "source_chunk_no": 21,
      "source_page": 11,
      "source_start": 890,
      "source_end": 1318,
      "source_text": "Kiến trúc Transformer áp dụng cơ chế tự chú ý (Self-Attention) nhằm mô hình hóa phụ thuộc xa...",
      "rrf_score": 0.03252,
      "dense_score": 0.8142,
      "lexical_score": 0.4918,
      "reranker_score": 0.8634,
      "classifier_score": null,
      "decision_source": "heuristic",
      "review_required": true
    }
  ],
  "disclaimer": "Similarity evidence only; teachers must verify sources, citations and context. No automatic plagiarism verdict."
}
```

#### B. Quy tắc chuyển tiếp lên `schema_version: 3` (`CP-02.4` & `CP-04.3`)
Ở `CP-02.4`, schema báo cáo sẽ nâng lên `schema_version: 3` với `compatibility adapter` để không làm gãy client thử nghiệm, tập trung bổ sung 4 điểm bắt buộc:
1. **`provenance` đầy đủ:** ghi rõ `parser_version`, `chunker_config_hash`, `embedding_revision`, `reranker_revision`, `classifier_version`, `corpus_snapshot_id`.
2. **Ngữ nghĩa `coverage` chuẩn hóa:** tỷ lệ độ dài hợp các vùng ký tự khớp đủ điều kiện (đã khử chồng lấn overlap) trên tổng độ dài văn bản đủ điều kiện (sau khi loại bìa/mục lục/tài liệu tham khảo); **tuyệt đối không** lấy `max_rerank_score * 100` gọi là `% đạo văn`.
3. **Tách toàn bộ kết quả lưu DB khỏi phân trang UI:** không cắt cứng `[:200]` trước khi tính nhóm và độ phủ; hỗ trợ phân trang `passage_groups` và `findings`.
4. **Trường `citation_context` (`CP-04.3`):** gắn `citation_status` (`present` | `absent` | `uncertain`) và dấu hiệu marker trích dẫn độc lập với nhãn tái sử dụng nội dung.

---

## 6. Kiểm chứng Khả năng Gọi Độc lập (Standalone Testability)

Theo tiêu chí hoàn tất của `CP-00.2`, dịch vụ AI không phụ thuộc vào việc sửa code của repo `karasuma-2401/UGTM` mới có thể kiểm thử:
- Có sẵn client kiểm thử độc lập chỉ dùng thư viện chuẩn Python tại [`scripts/demo_e2e_test.py`](../scripts/demo_e2e_test.py) và bộ payload mẫu tại [`docs/contracts/`](./contracts/).
- Mọi tính năng nghiên cứu và vận hành từ `CP-01` đến `CP-06` sẽ được kiểm thử xuyên suốt qua đúng giao diện HTTP REST trên; đến `CP-05.4` sẽ chạy bộ contract test mô phỏng đầy đủ hành vi của `SubmitFinalReport` / RBAC từ UGTM.

