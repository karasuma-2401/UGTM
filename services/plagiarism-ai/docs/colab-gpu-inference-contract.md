# Hợp đồng Giao tiếp & Sổ tay Vận hành Colab Remote GPU Inference Worker (`CP-01.5`)

**Cập nhật:** 30/10/2026  
**Checkpoint:** `CP-01.5 — Chuẩn hóa Colab Remote GPU Inference Worker`

Tài liệu này chuẩn hóa **Hợp đồng suy luận HTTP (Inference Contract)** giữa hệ thống AI (FastAPI / Celery CPU Worker trên Render/Local) và máy chủ suy luận GPU từ xa (**Google Colab T4/L4 Worker** qua Cloudflare Tunnel), đồng thời cung cấp quy trình tái kết nối khi Colab khởi động lại.

---

## 1. Nguyên tắc An toàn & Tách biệt Bí mật (Zero-Secret Isolation)

1. **Colab KHÔNG có quyền truy cập Database hay Storage:**
   - Máy chủ Colab **tuyệt đối không nhận** `POSTGRES_DSN`, `REDIS_URL`, hay khóa bí mật S3/R2.
   - Colab chỉ đóng vai trò bộ tính toán toán học thuần túy (nhận mảng chuỗi văn bản → trả mảng số thực vector/điểm số).
2. **Xác thực riêng biệt (`REMOTE_GPU_API_KEY`):**
   - Tách biệt hoàn toàn với `API_KEY` của hệ thống UGTM.
   - Được gửi qua header `Authorization: Bearer <token>` hoặc `X-API-Key: <token>`.
   - Nếu phía client gửi sai token, Colab từ chối ngay lập tức với mã `HTTP 401 Unauthorized`.
3. **Phát hiện lỗi suy luận tức thì:**
   - Bộ mã lỗi chuẩn hóa (`GPU_NOT_CONFIGURED`, `GPU_UNAVAILABLE`, `GPU_AUTH_ERROR`, `GPU_CONTRACT_ERROR`) giúp CPU worker phát hiện ngay lập tức nếu mô hình bị sai kích thước (`!= 1024`), chứa giá trị `NaN`/`Inf`, hoặc trả sai số lượng phần tử.

---

## 2. Đặc tả Kỹ thuật API Suy luận (Inference Contract)

### 2.1. Kiểm tra trạng thái máy chủ (`GET /health`)
- **Headers:** `Authorization: Bearer <token>` (tùy chọn nếu có cấu hình).
- **Phản hồi mẫu (`HTTP 200`):**
```json
{
  "status": "healthy",
  "device": "Tesla T4",
  "cuda": true,
  "vram_allocated_mb": 2180.45,
  "embedding_model": "BAAI/bge-m3",
  "embedding_revision": "main",
  "embedding_dim": 1024,
  "reranker_model": "BAAI/bge-reranker-v2-m3",
  "reranker_revision": "main"
}
```

### 2.2. Nhúng vector dày (`POST /encode`)
- **Mục đích:** Biến đổi danh sách đoạn văn bản thành các vector ngữ nghĩa 1024 chiều (`BAAI/bge-m3`).
- **Giới hạn an toàn (Batch Cap):** Tối đa **128** đoạn văn bản / request (để bảo vệ VRAM Colab T4 15 GB không bị tràn bộ nhớ). Phía CPU worker tự động chia nhỏ mảng nếu số lượng lớn hơn 128.
- **Yêu cầu (`POST /encode`):**
```json
{
  "texts": [
    "Đoạn văn bản thứ nhất cần tạo vector nhúng...",
    "Đoạn văn bản thứ hai..."
  ],
  "batch_size": 32
}
```
- **Phản hồi mẫu (`HTTP 200`):**
```json
{
  "model": "BAAI/bge-m3",
  "revision": "main",
  "dimension": 1024,
  "count": 2,
  "dense_vecs": [
    [-0.0124, 0.0451, ..., 0.0892],
    [0.0341, -0.0211, ..., -0.0054]
  ]
}
```

### 2.3. Chấm điểm tương đồng cặp Cross-Encoder (`POST /rerank`)
- **Mục đích:** Tính điểm tương đồng sâu giữa câu truy vấn và đoạn nguồn (`BAAI/bge-reranker-v2-m3`).
- **Giới hạn an toàn (Batch Cap):** Tối đa **128** cặp `[query_text, source_text]` / request.
- **Yêu cầu (`POST /rerank`):**
```json
{
  "pairs": [
    ["Đoạn văn trong bài nộp", "Đoạn văn trong kho luận văn nguồn"],
    ["Đoạn truy vấn thứ hai", "Đoạn nguồn thứ hai"]
  ],
  "batch_size": 32
}
```
- **Phản hồi mẫu (`HTTP 200`):**
```json
{
  "model": "BAAI/bge-reranker-v2-m3",
  "revision": "main",
  "count": 2,
  "scores": [0.8942, 0.2315]
}
```

---

## 3. Sổ tay Tái kết nối khi Colab Khởi động lại (Reconnection Runbook)

Do Google Colab Free có thể ngắt kết nối sau vài giờ idle hoặc đổi địa chỉ tunnel khi khởi động lại, quy trình cập nhật diễn ra trong **dưới 1 phút**:

1. **Khởi động lại Colab Worker:**
   - Mở tệp notebook [notebooks/colab_gpu_worker.ipynb](../notebooks/colab_gpu_worker.ipynb) trên Google Colab.
   - Nhấn **Runtime** → **Run all** (hoặc `Ctrl + F9`).
2. **Lấy địa chỉ công khai mới:**
   - Ở khối lệnh cuối cùng, sao chép URL xuất hiện:
     ```text
     ========================================================================
     UGTM REMOTE GPU INFERENCE WORKER — ONLINE
     ========================================================================
     Target Hardware   : Tesla T4 (15.0 GB VRAM)
     Public Endpoint   : https://example-subdomain.trycloudflare.com
     
     System Configuration:
     Add or update the following entry in your local .env configuration:
     REMOTE_GPU_URL=https://example-subdomain.trycloudflare.com
     ========================================================================
     ```
3. **Cập nhật cấu hình hệ thống:**
   - Trên máy cá nhân: Cập nhật dòng `REMOTE_GPU_URL=https://example-subdomain.trycloudflare.com` trong file `.env`.
   - Trên Render Web Service / Worker: Vào mục **Environment** trên dashboard Render và dán URL mới (Render sẽ tự động reload trong 10 giây).
4. **Kiểm tra thông tuyến nhanh (Verification):**
   Chạy lệnh kiểm thử sau trong terminal máy bạn để xác nhận kết nối sẵn sàng:
   ```powershell
   .\.venv\Scripts\python.exe scripts/smoke_test_colab_client.py
   ```

