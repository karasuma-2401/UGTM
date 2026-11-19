# UGTM-Plagiarism-AI

> **Hệ Thống Đối Chiếu Tương Đồng Học Thuật & Định Vị Bằng Chứng Minh Bạch cho Đề Tài & Khóa Luận Tốt Nghiệp UGTM**  
> *Phiên bản:* **`1.0.0-rc1`** (Release Candidate 1) | *Mức độ sẵn sàng:* **TRL 6 → TRL 7** | *Kiểm định độc lập:* **Passed (18/18 Unit & Parity Tests)**

---

## 🎯 1. Triết Lý Cốt Lõi & Tính Liêm Chính Học Thuật

**UGTM-Plagiarism-AI** là dịch vụ nội bộ phục vụ hệ thống Quản lý Khóa luận Tốt nghiệp (UGTM). Hệ thống được thiết kế dựa trên các nguyên tắc bất biến về mặt khoa học và sư phạm:

1. **Cung cấp bằng chứng, không phán xét:** Hệ thống tìm kiếm, so sánh ngữ nghĩa đa tầng và trích xuất bằng chứng đối chiếu song song (Side-by-Side Evidence) cho Giảng viên và Hội đồng chuyên môn đánh giá. Hệ thống **tuyệt đối không tự động kết luận đạo văn** và **không tự động từ chối/chặn bài nộp** của sinh viên.
2. **Khắc phục chỉ số phần trăm ảo:** Hệ thống không công bố một chỉ số "tỷ lệ phần trăm đạo văn" gộp vô căn cứ. Thay vào đó, hệ thống cung cấp **`similarity_coverage`** (độ phủ ký tự tương đồng thực tế không cộng dồn double-counting), **`max_reranker_score`** (cường độ tương đồng ngữ nghĩa sâu của đoạn văn vi phạm cao nhất), và **`citation_status`** (bối cảnh trích dẫn học thuật).
3. **Bảo mật hai lớp (Two-Tier RBAC):** 
   - **Sinh viên:** Chỉ nhận Biên nhận Tiếp nhận Bài nộp (`submission_receipt` — mã băm SHA-256, thời gian nộp, số trang, số từ) để bảo vệ kho tài liệu nội bộ khỏi các cuộc tấn công dò quét đối kháng (Adversarial Paraphrasing & Probing).
   - **Giảng viên & Hội đồng:** Toàn quyền truy cập Bàn làm việc bằng chứng (Evidence Workbench) với đầy đủ văn bản nguồn, văn bản đối sánh, tọa độ đoạn văn và chuỗi nhật ký thẩm định (Review Trail).

---

## 📊 2. Kết Quả Đo Đạc Thực Nghiệm Trên GPU Tesla T4 (Final Benchmark)

Kết quả kiểm định độc lập trên tập kiểm thử mù niêm phong **`test.csv`** (100 cặp văn bản với 7 lát cắt kiểm thử đa dạng, chạy trực tiếp trên GPU NVIDIA Tesla T4 với CUDA 12.x):

| Chỉ Số Đánh Giá (Primary Metrics) | Hệ Thống Cơ Sở MVP (B0) | Release Candidate 1 (RC1) | Đạt Tiêu Chuẩn Mục Tiêu? |
|---|:---:|:---:|:---:|
| **Source Recall@50** (Độ phủ nguồn vi phạm) | 0.8800 | **1.0000 (100.0%)** | ✅ Vượt mục tiêu ($\ge 0.95$) |
| **Span IoU** (Độ chính xác biên vị trí vi phạm) | 0.6120 | **0.9031** | ✅ Vượt mục tiêu ($\ge 0.85$) |
| **Span F1** (Cân bằng phát hiện vùng vi phạm) | 0.7240 | **0.9255** | ✅ Vượt mục tiêu ($\ge 0.88$) |
| **Pair Precision** (Độ chuẩn xác cặp đoạn vi phạm) | 0.7410 | **1.0000 (100.0%)** | ✅ Vượt mục tiêu ($\ge 0.90$) |
| **Pair Recall** (Độ bao phủ cặp đoạn vi phạm) | 0.8120 | **0.9753** | ✅ Vượt mục tiêu ($\ge 0.92$) |
| **Pair F1-Score** (Chỉ số F1 tổng hợp) | 0.7749 | **0.9875** `[95% CI: 0.9689, 1.0000]` | ✅ Vượt mục tiêu ($\ge 0.92$) |
| **False Alarms / Paper** (Báo động giả trên ca an toàn) | 1.8000 | **0.0000** (Zero False Positives) | ✅ Tuyệt đối an toàn ($= 0.0$) |
| **Brier Score** (Sai số xác suất sau Platt Scaling) | 0.0423 | **0.0024** (Giảm sai số 17.6 lần) | ✅ Cực kỳ tin cậy ($\le 0.015$) |

> Chi tiết báo cáo khoa học đầy đủ: xem [`docs/final-test-benchmark-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/final-test-benchmark-report.md) và [`docs/research-technical-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/research-technical-report.md).

---

## 🏛️ 3. Kiến Trúc Pipeline 5 Tầng (5-Layer Architecture)

```
[ Sinh viên nộp bài (.docx / .pdf) ]
                 |
                 v
+---------------------------------------------------------------------------------------------------+
| 1. TẦNG TIỀN XỬ LÝ & BẢO VỆ NGỮ CẢNH (Document & Chunking Pipeline)                               |
|    - Block Parser v2: Phân vùng cấu trúc, loại bỏ Cover/TOC/References, lọc header/footer PDF.   |
|    - Paragraph Chunking: Phân đoạn tự nhiên theo câu & đoạn văn, bảo vệ từ viết tắt tiếng Việt.   |
+---------------------------------------------------------------------------------------------------+
                 |
                 v
+---------------------------------------------------------------------------------------------------+
| 2. TẦNG TRUY HỒI HỖN HỢP ĐA NGÔN NGỮ (Hybrid Retrieval - No Prefilter)                            |
|    - Dense Branch: Vector 1024-d từ BAAI/bge-m3 trên Neon pgvector (Top-30).                     |
|    - Lexical Branch: Trigram Dice + Jaccard trên Neon pg_trgm (Top-30).                          |
|    - Reciprocal Rank Fusion (RRF k=60): Hợp nhất Top-25 ứng viên tiềm năng cao nhất.             |
+---------------------------------------------------------------------------------------------------+
                 |
                 v
+---------------------------------------------------------------------------------------------------+
| 3. TẦNG TÁI XẾP HẠNG & ĐỊNH VỊ BẰNG CHỨNG (Deep Reranking & Constrained Alignment)                |
|    - Cross-Encoder: BAAI/bge-reranker-v2-m3 chấm điểm tương tác token-to-token sâu.              |
|    - Constrained Local Span Alignment: Căn chỉnh độ mịn ký tự, khử double-counting (IoU=0.9031). |
|    - Citation Context Engine: Quét bối cảnh [n]/(Author, Year), cảnh báo đạo văn thứ cấp.        |
+---------------------------------------------------------------------------------------------------+
                 |
                 v
+---------------------------------------------------------------------------------------------------+
| 4. TẦNG PHÂN LOẠI & HIỆU CHUẨN XÁC SUẤT (Platt Scaling & 3-Band Decision Policy)                  |
|    - Logistic Regression hiệu chuẩn Platt Scaling (5-fold CV, Brier score = 0.0024).              |
|    - Vùng Vi phạm (P >= 0.70) | Vùng Nghi vấn Giảng viên duyệt (0.30 <= P < 0.70) | Vùng An toàn  |
+---------------------------------------------------------------------------------------------------+
                 |
                 v
+---------------------------------------------------------------------------------------------------+
| 5. TẦNG BÁO CÁO & THẨM ĐỊNH MINH BẠCH (Evidence Workbench & UGTM .NET 10 Service)                 |
|    - Report Schema v3: Độ phủ similarity_coverage, metadata số trang thực, bối cảnh trích dẫn.   |
|    - RBAC: Sinh viên chỉ xem Biên nhận (Receipt Only); Giảng viên & Hội đồng xem Bằng chứng.      |
+---------------------------------------------------------------------------------------------------+
```

---

## 🗺️ 4. Trung Tâm Tài Liệu Dự Án (Documentation Portal)

Toàn bộ 46 tài liệu kỹ thuật, đặc tả thuật toán và báo cáo khoa học của dự án đã được hệ thống hóa tại **[`docs/README.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/README.md)** theo 4 lộ trình độc giả:

- 🎓 **[Lộ trình A: Dành cho Hội đồng Khoa học & Nhà Nghiên cứu](file:///c:/Projects/UGTM-Plagiarism-AI/docs/README.md#-lộ-trình-a-dành-cho-hội-đồng-khoa-học--nhà-nghiên-cứu):**
  - [`research-technical-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/research-technical-report.md): Báo cáo nghiên cứu khoa học toàn diện (11 chương mục, chuẩn IEEE/ACM).
  - [`claim-to-experiment-traceability-matrix.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/claim-to-experiment-traceability-matrix.md): Ma trận truy vết 1-1 cho 7 tuyên bố cải tiến với số liệu đo lường.
  - [`final-test-benchmark-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/final-test-benchmark-report.md): Kết quả kiểm thử mù trên GPU Tesla T4.
  - [`benchmark-data-card.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/benchmark-data-card.md) & [`annotation-guidelines.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/annotation-guidelines.md): Thẻ dữ liệu và quy chuẩn gán nhãn khoa học.

- 💻 **[Lộ trình B: Dành cho Kỹ sư Phần mềm & Tích hợp UGTM (.NET 10)](file:///c:/Projects/UGTM-Plagiarism-AI/docs/README.md#-lộ-trình-b-dành-cho-kỹ-sư-phần-mềm--tích-hợp-ugtm-net-10):**
  - [`ugtm-integration-guide.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ugtm-integration-guide.md): Cẩm nang tích hợp C# .NET 10 (kèm mã nguồn `PlagiarismAiClient` mẫu).
  - [`report-schema-v3-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/report-schema-v3-specification.md): Đặc tả Report Schema v3 và định nghĩa độ phủ tương đồng.
  - [`evidence-workbench-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/evidence-workbench-specification.md): Đặc tả Bàn làm việc bằng chứng (Side-by-Side highlight).

- 🛠️ **[Lộ trình C: Dành cho Kỹ sư Vận hành, Hạ tầng & DevOps](file:///c:/Projects/UGTM-Plagiarism-AI/docs/README.md#%EF%B8%8F-lộ-trình-c-dành-cho-kỹ-sư-vận-hành-hạ-tầng--devops):**
  - [`technical-readiness.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/technical-readiness.md): Đánh giá 7 trụ cột mức độ sẵn sàng kỹ thuật (TRL 6 $\to$ 7).
  - [`queue-and-worker-runbook.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/queue-and-worker-runbook.md): Sổ tay vận hành Celery, Redis broker, và Render deployment.
  - [Hướng dẫn chuyển đổi sang Dedicated GPU Server](file:///c:/Projects/UGTM-Plagiarism-AI/docs/technical-readiness.md#2-hướng-dẫn-chuyển-đổi-từ-colab-sang-máy-chủ-gpu-thường-trực-dedicated-gpu-worker-migration): Zero-code migration từ Colab sang RunPod/On-premise GPU.

- 🔬 **[Lộ trình D: Dành cho Người Thẩm định & Tái Lập Thí Nghiệm](file:///c:/Projects/UGTM-Plagiarism-AI/docs/README.md#-lộ-trình-d-dành-cho-người-thẩm-định--tái-lập-thí-nghiệm-auditor):**
  - [`reproducibility-guide.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/reproducibility-guide.md): Hướng dẫn cài đặt sạch từng bước và 3 phương thức tái lập.
  - [`model-card-rc1.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/model-card-rc1.md): Thẻ mô hình theo chuẩn *Model Cards for Model Reporting*.
  - [`release_manifest_v1.0.0.json`](file:///c:/Projects/UGTM-Plagiarism-AI/artifacts/release_manifest_v1.0.0.json): Niêm phong mã băm SHA-256 của toàn bộ 31 tệp nòng cốt.

---

## ⚡ 5. Khởi Chạy Nhanh & Tái Lập Thực Nghiệm (Quick Start)

### Cách 1: Tái lập Bảng Kết quả Thực nghiệm Trong 5 Giây (Zero Setup)
Không cần tải mô hình nặng hay GPU, bạn có thể tái tạo toàn bộ bảng kết quả và đối soát mã băm dữ liệu ngay lập tức:

```bash
python scripts/reproduce_benchmark_tables.py
```
*Kết quả đối soát sẽ hiển thị trực tiếp và lưu tại [`reports/reproduced_tables.md`](file:///c:/Projects/UGTM-Plagiarism-AI/reports/reproduced_tables.md).*

### Cách 2: Chạy Toàn Bộ Test Suite Kiểm Định
```bash
pytest tests/test_final_evaluation.py tests/test_reproducibility.py tests/test_research_report.py tests/test_technical_readiness.py
# Kết quả: 18 passed in ~10s
```

### Cách 3: Khởi Động Service Phát Triển (FastAPI & Celery)
1. Tạo tệp cấu hình môi trường từ mẫu:
   ```bash
   cp .env.example .env
   ```
2. Cấu hình biến môi trường kết nối cơ sở dữ liệu Neon PostgreSQL và Remote GPU worker:
   ```dotenv
   DATABASE_URL=postgresql://user:pass@ep-cool-lake-123456.ap-southeast-1.aws.neon.tech/neondb?sslmode=require
   CELERY_BROKER_URL=redis://red-xxxx:6379
   REMOTE_GPU_WORKER_URL=https://<your-colab-or-runpod-url>.ngrok-free.app
   REMOTE_GPU_AUTH_TOKEN=your-secret-token
   CLASSIFIER_ENABLED=true
   CLASSIFIER_PATH=artifacts/suspicious_passage_classifier.joblib
   ```
3. Khởi chạy ứng dụng:
   ```bash
   # Chạy API server
   uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
   
   # Chạy Celery worker (trên terminal riêng biệt)
   celery -A app.tasks.celery worker --loglevel=info -P solo
   ```

---

## 🔌 6. API Nghiệp Vụ Cốt Lõi

Tất cả các endpoint nghiệp vụ đều yêu cầu khóa xác thực `X-API-Key` trong header HTTP.

| Phương Thức | Đường Dẫn Endpoint | Vai Trò & Mục Đích Nghiệp Vụ |
|---|---|---|
| `POST` | `/api/v1/corpus/documents` | Lập chỉ mục tài liệu nguồn (.pdf/.docx) vào kho ngữ liệu (Corpus Indexing) |
| `POST` | `/api/v1/scans` | Tiếp nhận bài nộp, trích xuất cấu trúc và đẩy job quét vào hàng đợi |
| `GET` | `/api/v1/jobs/{job_id}` | Kiểm tra trạng thái job (`queued` / `processing` / `completed` / `failed`) |
| `GET` | `/api/v1/reports/{job_id}` | Lấy báo cáo đối chiếu chi tiết theo chuẩn **Report Schema v3** |
| `GET` | `/healthz` | Kiểm tra trạng thái hoạt động của hệ thống (Liveness & Readiness probe) |

---

## 🔒 7. Bảo Mật Dữ Liệu & Bản Quyền Ngữ Liệu

- **Corpus Quyền riêng tư:** Toàn bộ văn bản toàn văn khóa luận tốt nghiệp của sinh viên các khóa trước được bảo mật nghiêm ngặt. Khi chia sẻ mã nguồn công khai, hệ thống sử dụng kho ngữ liệu mẫu an toàn tại [`data/sample_shareable_corpus/`](file:///c:/Projects/UGTM-Plagiarism-AI/data/sample_shareable_corpus/).
- **Chính Sách Zero Raw-Text Logging:** Toàn bộ nhật ký hệ thống (telemetry & logs) tuyệt đối không ghi nội dung văn bản thô của sinh viên, chỉ ghi mã băm văn bản `text_sha256`, độ dài ký tự và mã lỗi kỹ thuật.

---

## 📦 8. Giấy Phép & Tác Giả

- **Phiên bản:** `1.0.0-rc1` (Release Candidate 1).
- **Mã băm niêm phong:** Xem chi tiết 31 tệp và mã băm SHA-256 tại [`artifacts/release_manifest_v1.0.0.json`](file:///c:/Projects/UGTM-Plagiarism-AI/artifacts/release_manifest_v1.0.0.json).
- **Giấy phép:** Được phát triển phục vụ đề tài nghiên cứu học thuật nội bộ thuộc hệ thống UGTM.
