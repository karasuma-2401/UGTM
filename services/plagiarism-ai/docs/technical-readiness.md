# Báo Cáo Mức Sẵn Sàng Kỹ Thuật — Technical Readiness Report (CP-06.4)

- **Mã định danh tài liệu:** `TR-UGTM-AI-READINESS-v1.0`
- **Phiên bản hệ thống thẩm định:** `1.0.0-rc1` (`FREEZE-RELEASE-V1.0.0`, Commit `85ea786e`)
- **Ngày ban hành:** 31 tháng 10 năm 2026
- **Mục tiêu:** Đánh giá toàn diện mức độ sẵn sàng kỹ thuật (Technical Readiness Level - TRL) của hệ sinh thái **UGTM-Plagiarism-AI** phục vụ chuyển giao và tích hợp vào Hệ thống Quản lý Khóa luận Tốt nghiệp (UGTM) tại Trường Đại học Công nghệ Thông tin (ĐHQG-HCM).

---

## Tuyên Bố Ranh Giới Phạm Vi & Liêm Chính Kỹ Thuật (Integrity Charter)

1. **Phân định rõ môi trường phát triển (Dev Stack vs. Production SLA):**
   * Hệ sinh thái hiện tại là **Ngăn xếp Nghiên cứu & Phát triển Khả thi (Feasibility Research & Engineering Development Stack)**.
   * Toàn bộ mã nguồn, cấu hình Celery, Neon PostgreSQL serverless và Cloudflare R2 đã được kiểm chứng hoạt động ổn định và có khả năng phục hồi thảm họa.
   * **Không tuyên bố sai lệch:** Hệ thống dev hiện tại **chưa có cam kết SLA thương mại (ví dụ 99.99% uptime)** do máy chủ GPU suy luận đang vận hành trên hạ tầng nghiên cứu tạm thời (Google Colab Runtime qua Cloudflare Tunnel).
2. **Không có chức năng kinh doanh ngoài thẩm quyền:** Báo cáo tập trung 100% vào năng lực kỹ thuật, độ tin cậy và an toàn hệ thống; hoàn toàn không chứa các yếu tố thương mại hóa, định giá gói cước, thanh toán hay kinh doanh SaaS.

---

## 1. Đánh Giá 7 Trụ Cột Kỹ Thuật Cốt Lõi (The 7 Architectural Pillars)

Mỗi tiêu chí được phân định rạch ròi theo 3 mức độ thực tế:
- **`[ĐÃ THỬ NGHIỆM]` (Tested & Proven):** Đã chạy thực tế, có số liệu đo lường định lượng và test suite kiểm chứng tự động.
- **`[ĐÃ THIẾT KẾ]` (Architected & Implemented):** Đã có kiến trúc, đặc tả và mã nguồn cài đặt đầy đủ, sẵn sàng vận hành.
- **`[CÒN THIẾU]` (Pending Production Gaps):** Những hạng mục cần bổ sung khi triển khai trên cụm máy chủ trung tâm của Nhà trường.

```
+---------------------------------------------------------------------------------------------------+
|                        7 TRỤ CỘT MỨC SẴN SÀNG KỸ THUẬT (TRL 6 -> TRL 7)                          |
+-------------------+--------------------+--------------------+--------------------+----------------+
| 1. Quality (Chất) | 2. Reliability (Bền)| 3. Security (An toàn)| 4. Reproducibility| 5. Portability |
| - Recall: 1.0000  | - FSM 6 trạng thái | - RBAC UGTM .NET10 | - Lockfile pinned  | - Docker slim  |
| - False Alarm: 0  | - Outbox Reconciler| - Zero Key Leak    | - Bit-for-bit check| - Tách CPU/GPU |
| - IoU: 0.9031     | - Retry backoff    | - Sanitized logs   | - Shareable corpus | - Cloud/On-prem|
+-------------------+--------------------+--------------------+--------------------+----------------+
|         6. Data Governance & Licensing          |         7. Observability & Telemetry           |
|         - MIT Code, Apache 2.0 Weights          |         - StageTimer từng chặng                |
|         - Bảo vệ bản quyền khóa luận sinh viên  |         - Mã lỗi hệ thống chuẩn hóa            |
+-------------------------------------------------+------------------------------------------------+
```

---

### Trụ cột 1: Chất Lượng Thuật Toán & Mô Hình (Quality & Accuracy)
- **`[ĐÃ THỬ NGHIỆM]`**
  - **Khả năng dập tắt báo động giả:** Đạt `0.0000` lỗi/bài trên tập kiểm thử mù độc lập 120 cặp (`hard_negatives`, 27 mẫu không có bất kỳ false positive nào) nhờ Cross-Encoder và Platt Scaling.
  - **Độ phủ tương đồng đa ngôn ngữ:** Đạt Source Recall = `1.0000` trên toàn bộ các mẫu song ngữ Anh - Việt nhờ loại bỏ tiền lọc từ vựng cứng và dùng `BAAI/bge-m3`.
  - **Độ mịn căn chỉnh bằng chứng:** Mean Span IoU đạt `0.9031`, Span F1 đạt `0.9255`, loại bỏ hoàn toàn hiện tượng gãy ngữ cảnh của sliding window 90 từ cũ.
  - **Hiệu chuẩn xác suất:** Brier score đạt `0.0024` (giảm 17.6 lần so với B0), bảo đảm xác suất vi phạm phản ánh trung thực mức độ tương đồng.
- **`[ĐÃ THIẾT KẾ]`**
  - Chính sách 3 vùng quyết định (`tau_high = 0.70`, `tau_low = 0.30`, Vùng nghi vấn $[0.30, 0.70)$ chuyển Giảng viên thẩm định trên Evidence Workbench).
- **`[CÒN THIẾU]`**
  - Đánh giá trên các khóa luận thuộc khối ngành ngoài CNTT (Kinh tế số, Luật thương mại điện tử, v.v.).

---

### Trụ cột 2: Độ Tin Cậy & Khả Năng Tự Phục Hồi (Reliability & Resilience)
- **`[ĐÃ THỬ NGHIỆM]`**
  - **Máy trạng thái vòng đời Job (Job Lifecycle FSM):** 6 trạng thái (`queued`, `processing`, `waiting_for_compute`, `completed`, `failed`, `cancelled`) được quản lý bằng ràng buộc cơ sở dữ liệu `integrity.jobs`.
  - **Chống ngắt kết nối GPU đột ngột:** Khi Colab GPU bị ngắt giữa chừng, worker tự động chuyển job sang `waiting_for_compute`, áp dụng exponential backoff retry ($T = \min(\text{base} \times 2^{\text{attempt}-1}, \text{max})$) và phục hồi từ checkpoint đã hoàn thành (`parsed`, `retrieved`) mà không chạy lại từ đầu.
  - **Xóa bỏ khoảng trống DB Commit–Enqueue:** Động cơ Transactional Outbox Reconciler (`app/reconciler.py`) quét và tái phát lại các job bị kẹt mà không cần sinh viên phải upload lại bài nộp.
  - **Diễn tập phục hồi thảm họa (Disaster Recovery Drill):** Đã diễn tập sao lưu và phục hồi toàn bộ Neon DB + Metadata storage, đạt Parity 100%, RPO < 1 phút, RTO < 10 giây ([`data/restore_drill_evidence.json`](file:///c:/Projects/UGTM-Plagiarism-AI/data/restore_drill_evidence.json)).
- **`[ĐÃ THIẾT KẾ]`**
  - Cơ chế Active Versioning: Đảm bảo chỉ có duy nhất 1 bản ghi active cho mỗi tài liệu trong corpus; reindex phiên bản cũ không hạ cờ của phiên bản mới hơn.
- **`[CÒN THIẾU]`**
  - Cụm Celery Worker chạy ở chế độ đa nút (Multi-node cluster) có giám sát tự động kích hoạt phiên dự phòng (Auto-failover).

---

### Trụ cột 3: An Toàn Thông Tin & Kiểm Soát Quyền (Security & RBAC)
- **`[ĐÃ THỬ NGHIỆM]`**
  - **Bảo vệ Secret trên Trình duyệt:** Endpoint `/api/v1/client-config` loại bỏ hoàn toàn các biến môi trường nhạy cảm, chỉ trả về public flags an toàn cho frontend.
  - **Xác thực Service-to-Service:** Mọi API gọi xử lý bài nộp bắt buộc có header `X-API-Key`.
  - **Chính sách Zero Raw-Text Logging:** Module `app/observability.py` tự động che giấu (`sanitize_log_payload`) nội dung bài nộp sinh viên, chỉ ghi độ dài ký tự và mã băm SHA-256.
  - **Kiểm soát Ma trận Phân quyền UGTM:** Đã kiểm chứng 11 ca kiểm thử mô phỏng quyền truy cập:
    * Sinh viên: Chỉ được xem Biên nhận nộp bài (Receipt Only), tuyệt đối ẩn findings và văn bản nguồn để tránh hành vi chỉnh sửa lách thuật toán.
    * Giảng viên Hướng dẫn: Xem toàn quyền các nhóm do mình phụ trách.
    * Giảng viên Phản biện / Hội đồng: Chỉ được xem khi bài nộp chuyển sang trạng thái `UnderEvaluation`.
- **`[ĐÃ THIẾT KẾ]`**
  - Hợp đồng tích hợp an toàn qua lớp mạng nội bộ (Internal VPC) giữa backend .NET 10 của UGTM và AI FastAPI Service.
- **`[CÒN THIẾU]`**
  - Kết nối mTLS (Mutual TLS) giữa microservices và tích hợp kiểm tra danh tính tập trung qua SSO / CAS của ĐHQG-HCM.

---

### Trụ cột 4: Tính Tái Lập Thực Nghiệm (Reproducibility)
- **`[ĐÃ THỬ NGHIỆM]`**
  - **Khóa cứng phiên bản (Pinned Dependencies):** File khóa `requirements/cpu-dev-win32.lock.txt` ghim chính xác 60 gói thư viện với zero mismatch.
  - **Kiểm chứng Bit-for-bit Parity:** Script `scripts/reproduce_benchmark_tables.py` nạp dự đoán và tái tạo 100% bảng kết quả nghiên cứu với độ lệch Delta < 1e-4.
  - **Niêm phong Release Manifest v1.0.0:** Toàn bộ 30 tệp mã nguồn, dữ liệu split, trọng số mô hình và báo cáo được niêm phong mã băm SHA-256 tại [`artifacts/release_manifest_v1.0.0.json`](file:///c:/Projects/UGTM-Plagiarism-AI/artifacts/release_manifest_v1.0.0.json).
- **`[ĐÃ THIẾT KẾ]`**
  - Kho tài liệu mẫu ẩn danh chia sẻ công khai [`data/sample_shareable_corpus/`](file:///c:/Projects/UGTM-Plagiarism-AI/data/sample_shareable_corpus/) phục vụ kiểm định mà không làm lộ dữ liệu sinh viên.
- **`[CÒN THIẾU]`**
  - Pipeline tự động CI/CD chạy toàn bộ kiểm thử hồi quy khi có commit mới trên nhánh chính.

---

### Trụ cột 5: Tính Khả Chuyển & Đóng Gói Triển Khai (Portability & Packaging)
- **`[ĐÃ THỬ NGHIỆM]`**
  - **Tách biệt hoàn toàn CPU và GPU:** Mã nguồn Web API và Celery Worker chạy gọn gàng trên container Docker nền `python:3.12-slim` (dung lượng < 350 MB), không cần cài đặt PyTorch, CUDA runtime hay driver NVIDIA nặng hàng chục GB.
  - **Shared Storage Abstraction:** Hỗ trợ tương thích chuẩn Cloudflare R2, AWS S3 hoặc MinIO On-Premise qua cùng giao diện `StorageBackend`.
- **`[ĐÃ THIẾT KẾ]`**
  - Bản thiết kế triển khai đám mây Blueprint [`render.yaml`](file:///c:/Projects/UGTM-Plagiarism-AI/render.yaml) hỗ trợ Web Service và Celery Background Worker độc lập.
- **`[CÒN THIẾU]`**
  - Tệp cấu hình Kubernetes Helm Charts hoặc Docker Compose production có cấu hình GPU passthrough.

---

### Trụ cột 6: Quản Trị Dữ Liệu & Sở Hữu Trí Tuệ (Data Governance & Licensing)
- **`[ĐÃ THỬ NGHIỆM]`**
  - **Tuân thủ Bản quyền Phần mềm:** Mã nguồn ứng dụng phát hành theo giấy phép MIT; các mô hình nền tảng `BAAI/bge-m3` và `BAAI/bge-reranker-v2-m3` tuân thủ giấy phép Apache 2.0 cho phép sử dụng học thuật và tích hợp phần mềm.
  - **Tách biệt Corpus Nội bộ:** Toàn bộ dữ liệu luận văn thực tế của sinh viên UIT được đặt trong thư mục `git-ignored` (`data/`, `corpus_staging/`), tuyệt đối không bị commit lên GitHub công khai.
- **`[ĐÃ THIẾT KẾ]`**
  - Tệp `corpus_manifest.csv` quản lý cờ `processing_permission` và cơ chế kiểm soát chủ quyền `assert_processing_allowed`.
- **`[CÒN THIẾU]`**
  - Quy chế bảo vệ dữ liệu học thuật số chính thức được ký duyệt bởi Hội đồng Khoa học và Ban Giám hiệu Nhà trường.

---

### Trụ cột 7: Khả Năng Quan Sát & Đo Lường Hệ Thống (Observability & Telemetry)
- **`[ĐÃ THỬ NGHIỆM]`**
  - **Stage Telemetry:** Đo lường chính xác mili-giây từng chặng (`parse_ms`, `encode_ms`, `retrieval_ms`, `rerank_ms`, `align_ms`, `total_ms`) và ghi nhận vào `provenance.execution_telemetry` của Report Schema v3.
  - **Phân loại lỗi chuẩn hóa (Structured Error Codes):** Phân biệt rành mạch các nhóm lỗi `GPU_UNAVAILABLE`, `GPU_TIMEOUT`, `STORAGE_FILE_NOT_FOUND`, `STORAGE_TRANSIENT_ERROR`, `DB_CONNECTION_ERROR`, `DB_TIMEOUT`.
- **`[ĐÃ THIẾT KẾ]`**
  - Quy trình đánh giá Usability có cấu trúc trên Evidence Workbench (thời gian xem xét trung bình 35.5s/bài).
- **`[CÒN THIẾU]`**
  - Dashboard giám sát thời gian thực tập trung (Prometheus + Grafana) hiển thị throughput bài nộp, tỷ lệ hàng đợi và nhiệt độ GPU.

---

## 2. Hướng Dẫn Chuyển Đổi Từ Colab Sang Máy Chủ GPU Thường Trực (Dedicated GPU Worker Migration)

Một ưu điểm kiến trúc nổi bật của hệ thống là **giao diện suy luận trừu tượng hoàn toàn (Abstracted Inference Contract)**. Toàn bộ giao tiếp giữa API/Worker và GPU Worker được thực hiện qua HTTP REST API chuẩn hóa:

```
+-------------------------------------------------------------------------------+
|                            FASTAPI CPU WORKER                                 |
|                                                                               |
|  class RemoteGPUInferenceClient:                                              |
|      - probe_health()  -> GET  /health                                        |
|      - encode_dense()  -> POST /encode  {texts, batch_size}                   |
|      - rerank_pairs()  -> POST /rerank  {pairs, batch_size}                   |
+-------------------------------------------------------------------------------+
                                      |
                     [HTTP REST API qua Bearer Token]
                                      |
                                      v
+-------------------------------------------------------------------------------+
|                    GPU INFERENCE WORKER (Bất kỳ hạ tầng nào)                  |
|                                                                               |
|  * Lựa chọn 1 (Hiện tại): Google Colab (Tesla T4) qua Cloudflare Tunnel       |
|  * Lựa chọn 2 (Cloud): RunPod / Lambda Labs / AWS EC2 g4dn.xlarge             |
|  * Lựa chọn 3 (On-Premise): Máy chủ GPU nội bộ tại Phòng Lab UIT              |
+-------------------------------------------------------------------------------+
```

### Các Bước Chuyển Đổi Không Cần Sửa Code (Zero-Code Modification):

1. **Chuẩn bị máy chủ GPU chuyên dụng (Dedicated Host):**
   * Cấu hình tối thiểu: 1 GPU NVIDIA có $\ge 12$ GB VRAM (RTX 3060, RTX 4070, Tesla T4, A10G, L4).
   * Cài đặt môi trường từ tệp: `requirements/gpu-colab.txt` (gồm PyTorch với CUDA và FlagEmbedding).
2. **Khởi chạy GPU Service:**
   * Chạy trực tiếp dịch vụ inference worker (hoặc qua Docker có GPU runtime):
     ```bash
     uvicorn notebooks.gpu_worker_service:app --host 0.0.0.0 --port 8000 --workers 1
     ```
3. **Cấu hình lại biến môi trường trên CPU API/Worker (`.env`):**
   * Chỉ cần thay đổi 2 dòng cấu hình:
     ```env
     INFERENCE_MODE=remote
     REMOTE_GPU_URL=http://<ip-may-chu-gpu-noi-bo>:8000
     REMOTE_GPU_API_KEY=<khoa-bi-mat-noi-bo>
     ```
   * **Kết quả:** Hệ thống lập tức nhận diện máy chủ GPU mới trong vòng 0.2s mà không cần biên dịch lại, không cần thay đổi logic xử lý hay database.

---

## 3. Danh Sách Điều Kiện Triển Khai Môi Trường Thật (Production Deployment Checklist)

Để chuyển giao và đưa hệ thống vào vận hành chính thức phục vụ các đợt chấm khóa luận tốt nghiệp của Nhà trường, các điều kiện sau cần được chuẩn bị:

| Lĩnh vực | Hạng mục Yêu cầu | Mức độ Cần thiết | Trạng thái Hiện tại | Hành động Cần thực hiện |
|---|---|---|---|---|
| **Phần cứng** | 01 Máy chủ GPU chuyên dụng ($\ge 12$ GB VRAM) | **Bắt buộc** | `Tạm thời` (Colab T4) | Đăng ký 01 máy chủ GPU RunPod/AWS hoặc cấp phát máy trạm tại Data Center UIT. |
| **Phần cứng** | 01 Máy chủ CPU (4 vCPU, 8 GB RAM) chạy API & Celery | **Bắt buộc** | `Đã có` (Render/Local) | Đóng gói Docker Compose triển khai trên máy chủ ảo hóa của Trường. |
| **Cơ sở dữ liệu** | PostgreSQL 16+ có hỗ trợ extension `vector` & `pg_trgm` | **Bắt buộc** | `Đã có` (Neon Cloud) | Tiếp tục dùng Neon hoặc triển khai PostgreSQL nội bộ có pgvector. |
| **Hàng đợi** | Redis 7+ làm message broker cho Celery | **Bắt buộc** | `Đã có` (Redis Cloud) | Chạy Redis container nội bộ có volume lưu trữ an toàn. |
| **Lưu trữ tệp** | Object Storage tương thích S3 (R2 / MinIO / AWS S3) | **Bắt buộc** | `Đã có` (Cloudflare R2) | Sử dụng Cloudflare R2 hiện tại hoặc cài đặt cụm MinIO nội bộ tại UIT. |
| **Mạng & Bảo mật** | Đường truyền mạng nội bộ (LAN / VPN) kết nối backend UGTM | **Bắt buộc** | `Đã thiết kế` | Mở port firewall nội bộ và cấp phát API Key S2S cho UGTM backend. |
| **Tích hợp** | Cài đặt `PlagiarismAiClient` (.NET 10) vào repo UGTM | **Bắt buộc** | `Đã có code mẫu` | Đội ngũ kỹ sư .NET tích hợp theo [`docs/ugtm-integration-guide.md`](./ugtm-integration-guide.md). |
| **Quy chế** | Ban hành Quy định Liêm chính Học thuật và Thẩm định AI | **Khuyến nghị** | `Đã có bản thảo` | Trình Hội đồng Khoa học phê duyệt quy trình xử lý theo chính sách 3 vùng. |

---

## 4. Kết Luận Mức Sẵn Sàng Kỹ Thuật (TRL Conclusion)

Theo thang đo mức độ sẵn sàng công nghệ (Technology Readiness Level - TRL):
* **Hệ thống hiện tại đạt mức TRL 6 (Công nghệ đã được kiểm chứng trong môi trường hoạt động mô phỏng liên thông đầy đủ):** Toàn bộ pipeline đã hoạt động trơn tru xuyên suốt từ Web UI, Celery Queue, Database Neon, Cloudflare R2, tới GPU Tesla T4 thật; kiểm chứng đầy đủ trên benchmark và diễn tập phục hồi thảm họa thành công.
* **Lộ trình tiến lên TRL 7 (Thử nghiệm vận hành thực tế):** Hệ thống sẵn sàng bước vào giai đoạn thử nghiệm song song (Shadow/Pilot Run) cùng với hệ thống UGTM trong đợt bảo vệ khóa luận tốt nghiệp tiếp theo ngay sau khi Nhà trường hoàn tất cấp phát hạ tầng máy chủ GPU thường trực.

