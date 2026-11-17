# UGTM AI — Báo Cáo Smoke Test Môi Trường Phát Triển Toàn Luồng (`CP-01.6`)

- **Ngày thực nghiệm:** `2026-10-30` (`2026-10-30T07:43:18Z`)
- **Mã checkpoint:** `CP-01.6`
- **Mục tiêu:** Kiểm chứng toàn diện luồng End-to-End từ tiếp nhận tài liệu, lưu trữ object storage, lập chỉ mục vector, truy vấn đa phương thức (RRF), suy luận mô hình học sâu trên remote GPU, trích xuất báo cáo kết quả, xử lý lỗi ngoại lệ khi GPU ngắt kết nối, và loại bỏ hoàn toàn việc để lộ secret từ API.
- **Trạng thái:** **PASSED (100% các tiêu chí CP-01.6 được đáp ứng)**

---

## 1. Thông Tin Định Danh & Phiên Bản Các Thành Phần Hạ Tầng (Infrastructure Provenance)

| Thành phần | Dịch vụ & Nền tảng | Phiên bản / Thông số kỹ thuật | Vùng & Cấu hình mạng | Trạng thái xác nhận |
|---|---|---|---|---|
| **Cơ sở dữ liệu Vector & Quan hệ** | **Neon Serverless PostgreSQL** | `PostgreSQL 18.6` (aarch64)<br>• `pgvector`: `v0.8.6`<br>• `pg_trgm`: `v1.6` | AWS Singapore (`ap-southeast-1`), SSL required, Connection Pooling | **ONLINE & VERIFIED** |
| **Message Broker (Hàng đợi tác vụ)** | **Redis Cloud** | `Redis v8.6.2` (Linux 6.8.0 AWS aarch64)<br>• Dung lượng: 30MB Free Tier<br>• Protocol: `redis://` với AUTH | AWS Singapore (`ap-southeast-1`), Celery `backend=None` | **ONLINE & VERIFIED** |
| **Object Storage (Lưu trữ tệp)** | **Cloudflare R2** | Tương thích AWS S3 API (`boto3`)<br>• Bucket: `ugtm-ai`<br>• Zero Egress Fees | Toàn cầu (Edge CDN Cloudflare)<br>Prefixes: `uploads/`, `reports/` | **ONLINE & VERIFIED** |
| **Bộ tăng tốc tính toán AI** | **Google Colab Remote GPU** | **NVIDIA Tesla T4** (`15,360 MiB` VRAM)<br>• VRAM đã cấp phát: `2,177.2 MiB`<br>• CUDA: `True`, PyTorch 2.x | Cloudflare Tunnel (`pycloudflared`), REST API với token auth | **ONLINE & VERIFIED** |
| **Mô hình Dense Embedding** | `BAAI/bge-m3` | 1024 chiều, FP16, max token 8192 (batching: 32) | Revision: `main` | **MEASURED ON GPU** |
| **Mô hình Cross-Encoder Reranker** | `BAAI/bge-reranker-v2-m3` | Cross-encoder sequence classification, FP16 | Revision: `main` | **MEASURED ON GPU** |
| **Môi trường Host / API Backend** | Local Windows Dev | Python `3.13.15`, FastAPI `0.115.14`, Celery `5.4.0`, Psycopg `3.2.5` | Host x86_64 | **ACTIVE** |

---

## 2. Kiểm Tra An Toàn API: Loại Bỏ Hoàn Toàn Secret (`Zero Secret Leak`)

Kiểm tra endpoint `GET /api/v1/client-config` đảm bảo Web Frontend nhận được cờ cấu hình hoạt động mà **không bao giờ để lộ API Key hay Database/Redis Connection String**:

- **HTTP Status:** `200 OK`
- **Payload trả về:**
  ```json
  {
    "default_corpus": "uit_thesis",
    "embedding_model": "BAAI/bge-m3",
    "reranker_model": "BAAI/bge-reranker-v2-m3",
    "classifier_enabled": false,
    "max_upload_mb": 20,
    "inference_mode": "remote",
    "storage_backend": "s3",
    "remote_gpu_enabled": true
  }
  ```
- **Kiểm định bảo mật:** Không chứa `api_key`, `token`, `secret`, `database_url`, `redis_url`. Đạt tiêu chí bảo mật của hệ thống.

---

## 3. Thực Nghiệm Luồng Xuyên Suốt End-to-End (`Pipeline Run`)

### 3.1. Quá trình Nạp Tài Liệu Nguồn (Corpus Indexing)
- **Tệp nguồn:** `app/static/samples/source_doc.docx`
- **Mã định danh:** `source_id: kltn_nguon_8c1455`, `corpus: uit_thesis`
- **Lưu trữ R2:** Tệp tự động tải lên Cloudflare R2 tại đường dẫn:
  `s3://ugtm-ai/uploads/31e47d3d4357e88e_source_doc.docx`
- **Suy luận Vector Embedding:** Remote GPU mã hóa 4 đoạn văn bản thành 4 vector 1024 chiều (`batch_size=32`).
- **Lưu trữ Neon:** 4 chunks được ghi vào `integrity.chunks` với embedding kiểu `vector(1024)`.
- **Trạng thái Job:** `completed` (báo cáo ghi nhận `indexed_chunks = 4`).

### 3.2. Quá trình Quét Bài Nộp So Khớp Đạo Văn (Suspect Document Scanning)
- **Tệp quét:** `app/static/samples/suspect_doc.docx`
- **Mã định danh:** `submission_id: kltn_nop_8c1455`, `corpus: uit_thesis`
- **Lưu trữ R2:** Tệp tự động tải lên Cloudflare R2 tại đường dẫn:
  `s3://ugtm-ai/uploads/888dd8c3208de9e9_suspect_doc.docx`
- **Suy luận Vector Embedding:** Remote GPU mã hóa 3 query chunks thành vector 1024 chiều.
- **Truy xuất Lai (RRF Fusion):** So khớp đồng thời bằng Cosine Distance trên `pgvector` và Trigram Similarity trên `pg_trgm`, hợp nhất top ứng viên bằng Reciprocal Rank Fusion (`k=60`).
- **Suy luận Reranker Cross-Encoder:** Remote GPU tính điểm Cross-Encoder cho 10 cặp ứng viên tiềm năng (`batch_size=16`).
- **Phát hiện Trùng lặp (Similarity Evidence):**
  - **Số cặp bị gắn cờ:** 1 cặp
  - **Điểm Reranker Score:** `0.9691` (xác suất trùng ngữ nghĩa rất cao)
  - **Điểm Dense Cosine:** `0.8267`
  - **Điểm Lexical Trigram:** `0.5871`
  - **Phân nhóm đoạn:** 1 passage group
  - **Báo cáo chuẩn hóa:** Schema version 2, lưu trữ an toàn trong Neon `integrity.jobs`.

---

## 4. Kiểm Chứng Độ Bền Vững Khi GPU Mất Kết Nối (`Offline GPU Resilience`)

Để ngăn chặn lỗi treo tác vụ vô hạn (hanging worker) khi mạng chập chờn hoặc Colab GPU bị ngắt kết nối:
1. Thử nghiệm cấu hình URL GPU không tồn tại (`http://127.0.0.1:9999/unreachable_gpu`) với timeout 2.0 giây.
2. Worker tiếp nhận tác vụ quét tài liệu và phát hiện mất kết nối tới GPU.
3. **Kết quả:**
   - Bắt chính xác ngoại lệ `InferenceUnavailableError` (`GPU_UNAVAILABLE`).
   - Cập nhật trạng thái `failed` và ghi nhận `error_code = "GPU_UNAVAILABLE"` vào Neon `integrity.jobs` trong vòng **10.76s**.
   - Worker giải phóng tài nguyên sạch sẽ, không bị treo (non-hanging).

---

## 5. Tổng Kết Đạt Chuẩn CP-01 (Exit Criteria Review)

| Tiêu chí CP-01 | Kết quả kiểm chứng | Bằng chứng |
|---|---|---|
| **EC-01.1** Native dev runtime sạch, không bypass check | **ĐẠT** | 64 packages khóa trong lockfile, không bypass torch |
| **EC-01.2** Neon PostgreSQL + pgvector hoạt động | **ĐẠT** | PostgreSQL 18.6, pgvector 0.8.6, pg_trgm 1.6, SSL require |
| **EC-01.3** Redis message broker & Celery decoupled | **ĐẠT** | Redis Cloud v8.6.2, Celery `backend=None`, Neon quản lý job |
| **EC-01.4** Object Storage R2 tích hợp an toàn | **ĐẠT** | Bucket `ugtm-ai`, tự động tải/dọn dẹp temp file bằng context manager |
| **EC-01.5** Google Colab GPU Worker chuẩn hóa | **ĐẠT** | Tesla T4, BGE-M3 + Reranker-v2-M3, có token auth |
| **EC-01.6** Smoke test toàn luồng & loại bỏ secret public | **ĐẠT** | Toàn bộ các bước nạp, quét, trích xuất báo cáo và xử lý lỗi GPU đều thành công |

