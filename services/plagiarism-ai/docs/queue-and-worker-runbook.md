# Sổ tay Vận hành Hàng đợi & CPU Worker (`CP-01.3`)

**Cập nhật:** 30/10/2026  
**Checkpoint:** `CP-01.3 — Chốt queue và nơi chạy CPU worker`

Tài liệu này quy định kiến trúc hàng đợi, phân tách trách nhiệm giữa **Broker** và **Cơ sở dữ liệu kết quả**, cùng các kịch bản vận hành CPU Worker cho dịch vụ `UGTM-Plagiarism-AI`.

---

## 1. Phân định Trách nhiệm: Broker vs Database Kết quả

Một trong những nguyên tắc cốt lõi của `CP-01.3` là **tuyệt đối không dùng chung một hệ thống cho cả điều phối hàng đợi và lưu kết quả nặng**:

| Thành phần | Công nghệ | Trách nhiệm duy nhất | Dung lượng / Vòng đời |
|---|---|---|---|
| **Message Broker** | **Redis Cloud** (`rediss://` / `redis://`) | Chỉ tiếp nhận và chuyển tiếp tác vụ (`task_id`, `job_id`, `args`), giữ hàng đợi nhẹ (`celery`). Tắt hoàn toàn Celery Result Backend (`backend=None`). | Lưu trữ tạm thời (RAM 30 MB là dư dả vì không lưu report JSON nặng). |
| **Persistent Result Store** | **Neon PostgreSQL** (`integrity.jobs`) | Lưu trữ trạng thái bền vững (`status`), lỗi (`error_code`), dấu vết thời gian (`created_at`, `updated_at`), và toàn bộ Báo cáo Đối chứng chi tiết (`report` JSONB). | Bền vững lâu dài, không mất dữ liệu khi API hay Worker khởi động lại. |

### Cơ chế chống mất Job khi API khởi động lại (Zero Job Loss)
1. Khi có request nộp bài (`POST /api/v1/scans` hoặc `POST /api/v1/corpus/documents`), hàm `insert_job()` thực thi câu lệnh SQL:
   ```sql
   INSERT INTO integrity.jobs (id, job_type, corpus_id, document_key, version, filename, file_path, file_sha256, status)
   VALUES (...) ON CONFLICT DO NOTHING RETURNING id;
   ```
2. **Chỉ sau khi** bản ghi đã được `COMMIT` chắc chắn vào Neon PostgreSQL, API mới phát lệnh `scan_document.delay(job_id)` vào Redis broker.
3. Nếu tiến trình API bị restart/crash ngay sau đó:
   - Bản ghi job với trạng thái `queued` và mã băm `file_sha256` vẫn nằm an toàn trên Neon.
   - CPU worker lấy message từ Redis và đọc thông tin job trực tiếp từ Neon để tiếp tục xử lý.

---

## 2. Các Mô hình Vận hành (Deployment Topologies)

### Mô hình A: Hybrid Dev (Khuyên dùng nhất — Chi phí 0 VNĐ)
Đây là mô hình tối ưu nhất cho suốt quá trình phát triển và hoàn thiện khóa luận tốt nghiệp:
- **FastAPI API Gateway:** Chạy native trên máy cá nhân (`127.0.0.1:8000`) hoặc triển khai lên **Render Free Web Service** (định dạng Python Native Runtime).
- **CPU Orchestration Worker:** Chạy native trên máy tính của lập trình viên (tận dụng CPU đa nhân cục bộ để parse PDF/DOCX, tính cosine/trigram và điều phối mà không lo giới hạn RAM đám mây).
- **Message Broker:** **Redis Cloud Free** (AWS Singapore, 30 MB, không giới hạn số lượng lệnh).
- **Vector & Metadata Database:** **Neon PostgreSQL Free** (`pgvector` 0.8.6 + `pg_trgm` 1.6).
- **Remote GPU Inference:** **Google Colab T4 Worker** (bật qua tunnel khi cần quét tài liệu thực tế).

> [!WARNING]
> **Tại sao không chạy Celery Worker ngầm bên trong Render Free Web Service?**  
> Dịch vụ *Render Free Web Service* sẽ tự động rơi vào trạng thái ngủ đông (**Sleep/Spindown**) sau 15 phút nếu không có HTTP request gửi đến. Nếu đặt Celery worker chạy ngầm trong cùng tiến trình web miễn phí, worker sẽ bị hệ thống dừng đột ngột giữa chừng khi đang parse hoặc quét luận văn dài, gây hỏng file tạm và treo job. Do đó, worker phải chạy độc lập trên máy local hoặc trên dịch vụ Background Worker riêng.

### Mô hình B: Full Cloud Render (Khi nghiệm thu / Demo trực tuyến)
- **API Web Service:** Render Web Service (Free hoặc Starter).
- **Background Worker:** Render Background Worker (gói Starter \$7/tháng; đã cấu hình sẵn trong tệp [render.yaml](../render.yaml)).
- Sử dụng chung biến môi trường từ Web Service qua tính năng `fromService`.

---

## 3. Lệnh Khởi động Vận hành Native (Start Commands)

### 3.1. Khởi động FastAPI API Server

**Trên Windows (PowerShell):**
```powershell
.\.venv\Scripts\uvicorn.exe app.main:app --host 127.0.0.1 --port 8000 --reload
```

**Trên Linux / Render Native Runtime:**
```bash
uvicorn app.main:app --host 0.0.0.0 --port $PORT
```

### 3.2. Khởi động Celery CPU Worker

> [!IMPORTANT]
> **Các cờ bắt buộc khi chạy Celery Worker:**
> - Trên Windows: Bắt buộc cờ `--pool=solo` vì Windows không hỗ trợ POSIX fork.
> - Cờ `--without-gossip --without-mingle --without-heartbeat`: Tắt các gói tin bắt tay liên worker không cần thiết, giúp giữ kết nối Redis Cloud luôn êm và không làm tốn tài nguyên mạng.
> - Cờ `--concurrency=1` (Windows solo) hoặc `--concurrency=2` (Linux prefork).

**Trên Windows (PowerShell):**
```powershell
.\.venv\Scripts\celery.exe -A app.celery_app.celery_app worker --loglevel=INFO --concurrency=1 --pool=solo --without-gossip --without-mingle --without-heartbeat
```

**Trên Linux / Render Background Worker:**
```bash
celery -A app.celery_app.celery_app worker --loglevel=INFO --concurrency=2 --without-gossip --without-mingle --without-heartbeat
```

---

## 4. Kiểm tra Hàng đợi & Xử lý Sự cố (Troubleshooting)

### Kiểm tra nhanh số lượng tác vụ đang chờ trong Redis:
```powershell
.\.venv\Scripts\python.exe -c "import redis; from app.config import settings; r = redis.from_url(settings.redis_url); print('Celery queue depth:', r.llen('celery'))"
```

### Xóa sạch hàng đợi tác vụ tồn đọng (Purge):
```powershell
.\.venv\Scripts\celery.exe -A app.celery_app.celery_app purge -f
```

### Kiểm tra trạng thái Job trực tiếp trong Neon:
```powershell
.\.venv\Scripts\python.exe -c "from app.db import connect; con = connect(); print(con.execute('SELECT id, job_type, status, error_code, created_at FROM integrity.jobs ORDER BY created_at DESC LIMIT 5').fetchall())"
```

