# Hướng dẫn Chạy Native Python và Tách Môi trường CPU/API – GPU (`CP-01.1`)

**Cập nhật:** 30/10/2026  
**Checkpoint:** `CP-01.1 — Tách môi trường CPU/API khỏi GPU`

Tài liệu này hướng dẫn cách thiết lập và vận hành `UGTM-Plagiarism-AI` trực tiếp bằng **Python Native Runtime** (trên Windows dev hoặc Render Linux) mà **không cần Docker Compose hằng ngày**, đồng thời tách biệt hoàn toàn môi trường điều phối CPU/API khỏi môi trường suy luận GPU (`BAAI/bge-m3` & `BAAI/bge-reranker-v2-m3`).

---

## 1. Phân tầng Dependency (`requirements/`)

Hệ thống được chia thành 4 hồ sơ phụ thuộc độc lập để API và CPU Worker không phải kéo `torch`, `CUDA`, `transformers` hay `FlagEmbedding` (~4,5 GB):

| Hồ sơ | Tệp | Mục đích sử dụng | Bao gồm PyTorch / BGE? |
|---|---|---|---|
| **API Minimal** | `requirements/api.txt` | Tiến trình FastAPI (`app.main:app`) nhận request, kiểm tra định dạng upload, ghi Neon PostgreSQL và đẩy `job_id` vào Redis broker. | **Không** |
| **CPU Orchestration Worker** *(Mặc định)* | `requirements/cpu-worker.txt` *(được trỏ từ `requirements.txt` gốc)* | Tiến trình Celery CPU Worker (`app.celery_app.celery_app`): parse PDF/DOCX (`PyMuPDF`, `python-docx`), chunking, gọi HTTP tới Remote GPU, truy vấn `pgvector`/`pg_trgm`, căn chỉnh đoạn và chấm điểm classifier bảng (`scikit-learn`). | **Không** |
| **Local Dev & Test** | `requirements/dev.txt` | Kế thừa `cpu-worker.txt` và bổ sung `pytest`, `httpx` (`TestClient`). Kèm bản khóa phiên bản thực tế tại `requirements/cpu-dev-win32.lock.txt`. | **Không** |
| **GPU Worker (Colab / Workstation)** | `requirements/gpu-colab.txt` | Dành riêng cho Google Colab T4/L4 (`notebooks/colab_gpu_worker.ipynb`) hoặc máy trạm có GPU khi bật tường minh `INFERENCE_MODE=local`. | **Có** (`torch>=2.6.0`, `safetensors>=0.4.5`, `FlagEmbedding>=1.3.4`) |

---

## 2. Kiến trúc Inference Interface & Chính sách Không Fallback Âm thầm

Toàn bộ tầng suy luận được tách vào gói `app/inference/` và lộ diện qua facade `app/models.py`:

- `app/inference/base.py`: Định nghĩa giao thức `InferenceBackend`, cây ngoại lệ có mã lỗi chuẩn (`InferenceError.error_code`) và các hàm kiểm định đầu ra (`validate_embeddings`, `validate_rerank_scores`, `validate_model_identity`) để phát hiện sai kích thước vector (`!= 1024`), lệch số lượng phần tử, giá trị `NaN`/`Inf`, hoặc lệch tên model/revision.
- `app/inference/remote.py` (`RemoteInferenceClient`):
  - Chỉ dùng `requests` + `numpy`, **tuyệt đối không import `torch` hay `FlagEmbedding`**.
  - Tự động chia batch tối đa `128` phần tử/request để khớp giới hạn bộ nhớ GPU từ xa.
  - Gửi header xác thực `Authorization: Bearer <REMOTE_GPU_API_KEY>` và `X-API-Key` (tách biệt với `API_KEY` của dịch vụ chính).
  - **Không bao giờ fallback âm thầm sang CPU local** khi Colab tắt, hết hạn URL, hoặc lỗi mạng — tránh làm treo worker hoặc tràn RAM trên Render.
- `app/inference/local.py` (`LocalInferenceBackend`):
  - Chỉ được khởi tạo khi cấu hình tường minh `INFERENCE_MODE=local`.
  - Lazy-import `torch`, `safetensors`, `FlagEmbedding` tại thời điểm gọi thực sự.

### Mã lỗi suy luận (`error_code` ghi vào `integrity.jobs`)

| Ngoại lệ | `error_code` | Nguyên nhân |
|---|---|---|
| `InferenceConfigurationError` | `GPU_NOT_CONFIGURED` | `INFERENCE_MODE=remote` nhưng thiếu `REMOTE_GPU_URL`, hoặc giá trị `INFERENCE_MODE` không hợp lệ. |
| `InferenceUnavailableError` | `GPU_UNAVAILABLE` | Remote GPU offline, timeout, lỗi kết nối tunnel, hoặc trả HTTP `5xx`. |
| `InferenceAuthError` | `GPU_AUTH_ERROR` | Remote GPU từ chối `REMOTE_GPU_API_KEY` (HTTP `401`/`403`). |
| `InferenceContractError` | `GPU_CONTRACT_ERROR` | Sai tên model, sai revision đã ghim, sai chiều vector (`!= 1024`), thiếu phần tử, hoặc chứa `NaN`/`Inf`. |
| `InferenceDependencyError` | `LOCAL_INFERENCE_DEPENDENCY_MISSING` | Đặt `INFERENCE_MODE=local` trên môi trường chưa cài `requirements/gpu-colab.txt` hoặc `torch < 2.6.0`. |

---

## 3. Loại bỏ Model-Loading Safety Bypass & Kiểm chứng Artifact

1. **Xóa bỏ monkeypatch `check_torch_load_is_safe`:**
   - Phiên bản B0 từng gán `check_torch_load_is_safe = lambda: None` trong `app/models.py` do image Docker cũ dùng `torch==2.5.1` bị `transformers` chặn theo cảnh báo bảo mật `CVE-2025-32434`.
   - Từ `CP-01.1`, bypass này đã bị loại bỏ hoàn toàn. `requirements/gpu-colab.txt` và `LocalInferenceBackend` yêu cầu `torch>=2.6.0` cùng `safetensors>=0.4.5` để nạp trọng số chính thống của `BAAI/bge-m3` và `BAAI/bge-reranker-v2-m3` an toàn.
2. **Kiểm tra toàn vẹn SHA-256 cho Classifier `.joblib`:**
   - `training/train_classifier.py` tự động ghi `artifact_sha256` và `sklearn_version` vào tệp `.manifest.json`.
   - `app/classifier.py` kiểm tra mã băm `SHA-256` của tệp `.joblib` **trước khi** gọi `joblib.load()` (ném `CLASSIFIER_ARTIFACT_HASH_MISMATCH` nếu tệp bị sửa đổi).

---

## 4. Hướng dẫn Cài đặt và Chạy Native

### Bước 1: Tạo / Đồng bộ môi trường ảo `.venv`

**Trên Windows (PowerShell):**
```powershell
python -m venv .venv
.\.venv\Scripts\pip.exe install -r requirements\dev.txt
```

**Trên Linux / macOS / Render Native:**
```bash
python3 -m venv .venv
.venv/bin/pip install -r requirements/cpu-worker.txt
```

### Bước 2: Cấu hình `.env` (Không lưu secret vào Git)

Sao chép khuôn mẫu từ `.env.example` sang `.env` và điền thông tin kết nối dev (Neon PostgreSQL, Redis Cloud, khóa API nội bộ, và `REMOTE_GPU_URL` từ Colab khi chạy job):

```powershell
Copy-Item .env.example .env
```

Các biến quan trọng cho chế độ native:
- `INFERENCE_MODE=remote` *(mặc định; giữ nguyên cho API và CPU worker)*
- `POSTGRES_DSN=postgresql://...` *(chuỗi kết nối Neon dev có `sslmode=require`)*
- `REDIS_URL=rediss://...` *(chuỗi kết nối Redis Cloud hoặc broker tương thích)*
- `API_KEY=...` *(khóa xác thực dịch vụ AI)*
- `REMOTE_GPU_URL=https://...` *(endpoint của Colab GPU Worker)*
- `REMOTE_GPU_API_KEY=...` *(token riêng cho GPU Worker)*

### Bước 3: Khởi động FastAPI AI Server

**Windows (PowerShell):**
```powershell
.\.venv\Scripts\uvicorn.exe app.main:app --host 127.0.0.1 --port 8000 --reload
```

**Render Web Service (Start Command):**
```bash
uvicorn app.main:app --host 0.0.0.0 --port $PORT
```

Kiểm tra nhanh trạng thái API:
```powershell
Invoke-RestMethod http://127.0.0.1:8000/healthz
```

### Bước 4: Khởi động Celery CPU Orchestration Worker

> **Lưu ý trên Windows:** Celery trên Windows bắt buộc dùng `--pool=solo`. Nếu dùng serverless Redis giới hạn số lượng command (như Upstash), thêm `--without-gossip --without-mingle --without-heartbeat`.

**Windows (PowerShell):**
```powershell
.\.venv\Scripts\celery.exe -A app.celery_app.celery_app worker --loglevel=INFO --concurrency=1 --pool=solo --without-gossip --without-mingle --without-heartbeat
```

**Linux / Render Background Worker:**
```bash
celery -A app.celery_app.celery_app worker --loglevel=INFO --concurrency=2 --without-gossip --without-mingle --without-heartbeat
```

### Bước 5: Chạy Bộ Kiểm thử Tự động (`pytest`)

```powershell
.\.venv\Scripts\python.exe -m pytest -v tests
```

