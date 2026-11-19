# UGTM-Plagiarism-AI — Trung Tâm Tài Liệu Dự Án (Documentation Portal)

Chào mừng bạn đến với Cổng tài liệu chính thức của hệ thống **UGTM-Plagiarism-AI** (Phiên bản: **`1.0.0-rc1`**, Release Candidate 1).

Kho tài liệu này được hệ thống hóa chuyên nghiệp nhằm giúp mọi đối tượng độc giả — từ **Hội đồng Thẩm định Khoa học**, **Giảng viên**, **Kỹ sư Tích hợp UGTM (.NET 10)**, đến **Kỹ sư Vận hành (DevOps)** và **Nhà nghiên cứu Tái lập Thí nghiệm** — có thể nhanh chóng nắm bắt, kiểm chứng và kế thừa toàn bộ dự án.

---

## 🗺️ 1. Lộ Trình Đọc Tài Liệu Theo Vai Trò (Reading Roadmaps by Role)

Tùy theo nhu cầu và vai trò của bạn, hãy chọn lộ trình đọc tối ưu dưới đây để tiết kiệm thời gian:

### 🎓 Lộ trình A: Dành cho Hội đồng Khoa học & Nhà Nghiên cứu
*Mục tiêu: Đánh giá phương pháp khoa học, tính mới, độ liêm chính học thuật và kết quả đo đạc thực tế.*

1. **Bắt đầu tại:** [`docs/research-technical-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/research-technical-report.md) — Báo cáo nghiên cứu khoa học tổng kết toàn diện (11 chương mục, bài toán, RQ1–3, baselines, kết quả Tesla T4).
2. **Đối soát tuyên bố:** [`docs/claim-to-experiment-traceability-matrix.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/claim-to-experiment-traceability-matrix.md) — Ma trận truy vết 1-1 cho 7 tuyên bố cải tiến với số liệu đo lường `[MEASURED]`.
3. **Xem kết quả kiểm thử mù:** [`docs/final-test-benchmark-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/final-test-benchmark-report.md) — Báo cáo chi tiết trên tập test niêm phong (F1 = 0.9875, False Alarms = 0.0000).
4. **Hiểu về nhãn và dữ liệu:** [`docs/benchmark-data-card.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/benchmark-data-card.md) & [`docs/annotation-guidelines.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/annotation-guidelines.md) — Thẻ dữ liệu và quy chuẩn gán nhãn khoa học.
5. **Nghiên cứu cắt giảm:** [`docs/ablation-and-robustness-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ablation-and-robustness-report.md) — Đánh giá định lượng 8 cấu hình Leave-One-Out (M0 $\to$ M7).

---

### 💻 Lộ trình B: Dành cho Kỹ sư Phần mềm & Tích hợp UGTM (.NET 10)
*Mục tiêu: Tích hợp API quét đạo văn vào backend UGTM, cấu hình phân quyền RBAC và hiển thị báo cáo v3.*

1. **Bắt đầu tại:** [`docs/ugtm-integration-guide.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ugtm-integration-guide.md) — Hướng dẫn tích hợp toàn trình cho kỹ sư C# .NET 10 (kèm mã nguồn `PlagiarismAiClient` mẫu).
2. **Hiểu cấu trúc báo cáo:** [`docs/report-schema-v3-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/report-schema-v3-specification.md) — Đặc tả Report Schema v3, định nghĩa độ phủ tương đồng `similarity_coverage` và bối cảnh trích dẫn.
3. **Xem mẫu JSON hợp đồng:** [`docs/contracts/`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/contracts/) (các file mẫu `report_v3.sample.json`, `job_status.sample.json`).
4. **Kiểm tra giao diện thẩm định:** [`docs/evidence-workbench-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/evidence-workbench-specification.md) — Đặc tả bàn làm việc bằng chứng (Side-by-Side highlight, review trail).

---

### 🛠️ Lộ trình C: Dành cho Kỹ sư Vận hành, Hạ tầng & DevOps
*Mục tiêu: Triển khai container, quản lý hàng đợi Celery, cấu hình Database Neon/Storage R2 và chuyển đổi GPU.*

1. **Bắt đầu tại:** [`docs/technical-readiness.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/technical-readiness.md) — Đánh giá 7 trụ cột sẵn sàng kỹ thuật, danh sách điều kiện TRL 6 $\to$ TRL 7, và ranh giới dev stack vs production.
2. **Quy trình chuyển đổi GPU:** [`docs/technical-readiness.md#2-hướng-dẫn-chuyển-đổi-từ-colab-sang-máy-chủ-gpu-thường-trực-dedicated-gpu-worker-migration`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/technical-readiness.md) — Chuyển từ Colab sang GPU server chuyên dụng (RunPod/On-premise UIT) mà không sửa code.
3. **Vận hành hàng đợi & Worker:** [`docs/queue-and-worker-runbook.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/queue-and-worker-runbook.md) — Sổ tay vận hành Celery, Redis broker, và Render deployment.
4. **Chống treo job và mất task:** [`docs/job-lifecycle-and-resilience-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/job-lifecycle-and-resilience-specification.md) & [`docs/db-commit-enqueue-and-reconciler-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/db-commit-enqueue-and-reconciler-specification.md).
5. **Quan sát & Phục hồi thảm họa:** [`docs/observability-and-telemetry-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/observability-and-telemetry-specification.md) & [`docs/usability-evaluation-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/usability-evaluation-report.md).

---

### 🔬 Lộ trình D: Dành cho Người Thẩm định & Tái Lập Thí Nghiệm (Auditor)
*Mục tiêu: Cài đặt từ môi trường sạch, chạy lại toàn bộ kiểm thử và kiểm chứng số liệu bit-for-bit parity.*

1. **Bắt đầu tại:** [`docs/reproducibility-guide.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/reproducibility-guide.md) — Hướng dẫn cài đặt sạch từng bước và 3 phương thức tái lập thực nghiệm.
2. **Chạy script tái tạo kết quả:** Chạy `python scripts/reproduce_benchmark_tables.py` để kiểm chứng 100% số liệu với [`reports/reproduced_tables.md`](file:///c:/Projects/UGTM-Plagiarism-AI/reports/reproduced_tables.md).
3. **Thẻ mô hình chuẩn mực:** [`docs/model-card-rc1.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/model-card-rc1.md) — Model Card theo tiêu chuẩn Mitchell et al. (2019).
4. **Đối soát mã băm niêm phong:** [`artifacts/release_manifest_v1.0.0.json`](file:///c:/Projects/UGTM-Plagiarism-AI/artifacts/release_manifest_v1.0.0.json) — Mã băm SHA-256 của toàn bộ 31 tệp nòng cốt.

---

## 🏛️ 2. Sơ Đồ Kiến Trúc Hệ Thống Toàn Trình (Architecture Overview)

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

## 📚 3. Danh Mục Phân Loại Toàn Bộ 46 Tài Liệu Kỹ Thuật

Tất cả các tài liệu trong thư mục `docs/` được nhóm thành 6 nhóm chuyên đề logic:

### Nhóm 1: Báo Cáo Nghiên Cứu & Đánh Giá Độc Lập
| Tên Tài Liệu | Mô Tả Tóm Tắt & Mục Đích Sử Dụng |
|---|---|
| [`research-technical-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/research-technical-report.md) | **Báo cáo nghiên cứu khoa học tổng kết toàn diện** (11 chương mục, chuẩn IEEE/ACM format). |
| [`claim-to-experiment-traceability-matrix.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/claim-to-experiment-traceability-matrix.md) | **Ma trận đối soát 1-1** giữa 7 tuyên bố cải tiến kỹ thuật với thực nghiệm và số liệu `[MEASURED]`. |
| [`final-test-benchmark-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/final-test-benchmark-report.md) | Báo cáo chi tiết kết quả chạy trên tập kiểm thử niêm phong `test.csv` đo trực tiếp từ GPU Tesla T4. |
| [`baseline-b0-b4-benchmark-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/baseline-b0-b4-benchmark-report.md) | Báo cáo đánh giá so sánh 5 hệ thống đối chứng (B0, B1, B2, B3, B4) trên tập phát triển `val.csv`. |
| [`ablation-and-robustness-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ablation-and-robustness-report.md) | Nghiên cứu cắt giảm Leave-One-Out (M0 $\to$ M7) chứng minh vai trò từng module. |
| [`baseline-audit.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/baseline-audit.md) | Kiểm toán phiên bản MVP v2 gốc, phát hiện lỗi nuốt thân bài DOCX và lệch pha 90 từ. |
| [`experiment-decision-log.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/experiment-decision-log.md) | Sổ quyết định kỹ thuật giải trình lý do chấp nhận RC1 và hoãn/từ chối các nhánh EXT-01..03. |

### Nhóm 2: Đặc Tả Thuật Toán & Động Cơ AI Cốt Lõi
| Tên Tài Liệu | Mô Tả Tóm Tắt & Mục Đích Sử Dụng |
|---|---|
| [`parser-block-mapping-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/parser-block-mapping-specification.md) | Đặc tả máy trạng thái phân vùng cấu trúc Block Parser v2 cho DOCX và PDF. |
| [`preprocessing-and-chunking-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/preprocessing-and-chunking-specification.md) | Đặc tả phân đoạn văn bản tự nhiên theo đoạn văn (Paragraph chunking) và tách câu tiếng Việt. |
| [`candidate-retrieval-optimization-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/candidate-retrieval-optimization-report.md) | Nghiên cứu quét siêu tham số truy xuất $K$ và khám phá hiện tượng Prefilter Blindness. |
| [`sentence-span-alignment-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/sentence-span-alignment-specification.md) | Thuật toán căn chỉnh câu cục bộ có ràng buộc xử lý 4 ca biên và khử double-counting. |
| [`citation-aware-context-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/citation-aware-context-specification.md) | Động cơ phân tích bối cảnh trích dẫn học thuật và phát hiện đạo văn thứ cấp (Misattribution). |
| [`classifier-calibration-and-review-policy.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/classifier-calibration-and-review-policy.md) | Hiệu chuẩn xác suất Platt Scaling và chính sách phân loại 3 vùng quyết định. |
| [`evaluator-specification-and-metrics-schema.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/evaluator-specification-and-metrics-schema.md) | Đặc tả bộ đánh giá 4 tầng `UGTM-Evaluator-v1` và thuật toán Cluster Bootstrap 95% CI. |

### Nhóm 3: Dữ Liệu, Gán Nhãn & Quản Trị Corpus
| Tên Tài Liệu | Mô Tả Tóm Tắt & Mục Đích Sử Dụng |
|---|---|
| [`benchmark-data-card.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/benchmark-data-card.md) | Thẻ dữ liệu chuẩn hóa của bộ benchmark `UGTM-Bench-v1` (800 cặp, 7 lát cắt kiểm thử). |
| [`annotation-guidelines.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/annotation-guidelines.md) | Hướng dẫn gán nhãn khoa học đa chiều (loại vi phạm, mức độ tương đồng, bối cảnh trích dẫn). |
| [`annotation-triage-audit.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/annotation-triage-audit.md) | Báo cáo kiểm toán mẫu gán nhãn ban đầu, loại bỏ rác bìa/references khỏi tập huấn luyện. |
| [`test-access-protocol.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/test-access-protocol.md) | Quy chế bảo vệ tập kiểm thử mù `test.csv`, ném ngoại lệ khi truy cập trái phép. |
| [`benchmark-split-leakage-audit.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/benchmark-split-leakage-audit.md) | Báo cáo kiểm toán phân ly họ tài liệu (Disjoint Split), xác nhận 0% rò rỉ chéo giữa các tập. |
| [`corpus-duplicate-audit.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/corpus-duplicate-audit.md) | Kiểm toán phát hiện 3 cặp trùng lặp trong corpus, quy hoạch 73 document families. |
| [`self-match-exclusion-rules.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/self-match-exclusion-rules.md) | Quy tắc loại trừ tự đối chiếu bài nộp với chính nó hoặc các bản thảo cùng nhóm tác giả. |
| [`evidence-traceability-audit.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/evidence-traceability-audit.md) | Kiểm toán truy ngược bằng chứng toàn trình đạt tỷ lệ 100% về vị trí block và tệp nguồn. |

### Nhóm 4: Kiến Trúc Hệ Thống, Độ Bền & Vòng Đời Job
| Tên Tài Liệu | Mô Tả Tóm Tắt & Mục Đích Sử Dụng |
|---|---|
| [`job-lifecycle-and-resilience-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/job-lifecycle-and-resilience-specification.md) | Máy trạng thái FSM 6 trạng thái xử lý ngắt kết nối GPU Colab, retry backoff và checkpoint resume. |
| [`db-commit-enqueue-and-reconciler-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/db-commit-enqueue-and-reconciler-specification.md) | Động cơ Transactional Outbox Reconciler xóa bỏ khoảng trống DB-Queue và active versioning. |
| [`colab-gpu-inference-contract.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/colab-gpu-inference-contract.md) | Hợp đồng giao tiếp suy luận HTTP REST API giữa CPU Worker và Remote GPU Worker. |
| [`native-runtime-guide.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/native-runtime-guide.md) | Cẩm nang thiết lập môi trường tách biệt CPU và GPU, loại trừ phụ thuộc CUDA cục bộ. |
| [`queue-and-worker-runbook.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/queue-and-worker-runbook.md) | Sổ tay vận hành hàng đợi Celery, Redis message broker và cấu hình Render blueprint. |
| [`dev-environment-smoke-test.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/dev-environment-smoke-test.md) | Báo cáo smoke test tích hợp toàn diện 4 thành phần đám mây (Neon, Redis, R2, Colab GPU). |
| [`neon-dev-smoke-test.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/neon-dev-smoke-test.md) | Báo cáo kiểm thử kết nối Neon PostgreSQL 18.6 với pgvector 0.8.6 và pg_trgm 1.6. |

### Nhóm 5: Hướng Dẫn Tích Hợp UGTM & An Toàn Phân Quyền
| Tên Tài Liệu | Mô Tả Tóm Tắt & Mục Đích Sử Dụng |
|---|---|
| [`ugtm-integration-guide.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ugtm-integration-guide.md) | **Cẩm nang tích hợp chính thức cho kỹ sư .NET 10** (kèm code C# `PlagiarismAiClient` mẫu). |
| [`ai-integration-contract.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ai-integration-contract.md) | Hợp đồng tích hợp tổng quan giữa service AI và hệ sinh thái quản lý khóa luận UGTM. |
| [`report-schema-v3-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/report-schema-v3-specification.md) | Đặc tả kỹ thuật Report Schema v3, định nghĩa độ phủ tương đồng và bối cảnh trích dẫn. |
| [`evidence-workbench-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/evidence-workbench-specification.md) | Đặc tả giao diện Bàn làm việc bằng chứng phục vụ hội đồng chuyên môn thẩm định. |
| [`contracts/`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/contracts/) | Thư mục chứa các tệp JSON schema hợp đồng mẫu (`report_v3.sample.json`, `job_status.sample.json`, v.v.). |

### Nhóm 6: Tái Lập, Mức Sẵn Sàng Kỹ Thuật & Quản Trị Tiến Độ
| Tên Tài Liệu | Mô Tả Tóm Tắt & Mục Đích Sử Dụng |
|---|---|
| [`technical-readiness.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/technical-readiness.md) | **Báo cáo mức sẵn sàng kỹ thuật (TRL 6 $\to$ 7)**, đánh giá 7 trụ cột và quy trình chuyển đổi Dedicated GPU. |
| [`reproducibility-guide.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/reproducibility-guide.md) | **Hướng dẫn tái lập thực nghiệm từng bước từ môi trường sạch** (3 phương thức tái lập). |
| [`model-card-rc1.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/model-card-rc1.md) | Thẻ mô hình Release Candidate 1 theo tiêu chuẩn *Model Cards for Model Reporting*. |
| [`observability-and-telemetry-specification.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/observability-and-telemetry-specification.md) | Đặc tả hệ thống quan sát telemetry từng chặng và chính sách Zero Raw-Text Logging. |
| [`usability-evaluation-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/usability-evaluation-report.md) | Báo cáo đánh giá thao tác người dùng (thời gian thẩm định trung bình 35.5s/bài). |
| [`usability-evaluation-protocol.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/usability-evaluation-protocol.md) | Quy trình và 5 kịch bản đánh giá trải nghiệm xem bằng chứng của giảng viên. |
| [`ai-development-checkpoints.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ai-development-checkpoints.md) | **Checklist chính thức quản lý tiến độ dự án** qua các checkpoint từ CP-00 đến CP-06. |
| [`ai-execution-log.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/ai-execution-log.md) | **Sổ nhật ký thực thi chi tiết** ghi chép minh bạch mọi ngày làm việc, commit và artifact. |

---

## 📖 4. Bảng Thuật Ngữ Cốt Lõi (Glossary of Terms)

| Thuật Ngữ | Định Nghĩa Kỹ Thuật Trong Hệ Thống |
|---|---|
| **RC1 (Release Candidate 1)** | Phiên bản phương án AI chính thức được đóng băng tại CP-04.6 (`1.0.0-rc1`), kết hợp Block Parser v2 + Paragraph chunking + Hybrid RRF + Cross-Encoder + Constrained Alignment + Platt Calibration. |
| **Similarity Coverage** | Tỷ lệ độ phủ ký tự tương đồng thực tế trên tổng số ký tự đã quét ($\text{matched\_chars} / \text{scanned\_chars}$), tính bằng phép hợp các khoảng ký tự không chồng lấn; **tuyệt đối không cộng dồn double-counting**. |
| **Max Reranker Score** | Điểm số tương đồng ngữ nghĩa cao nhất giữa một cặp đoạn văn bản do Cross-Encoder tính toán $\in [0, 1]$; **mang ý nghĩa cường độ tương đồng đoạn, không phải phần trăm toàn bài**. |
| **Citation Status** | Trạng thái bối cảnh trích dẫn của đoạn vi phạm: `present` (có trích dẫn), `absent` (không trích dẫn), hoặc `uncertain` (nghi ngờ trích dẫn không đầy đủ hoặc sai nguồn). |
| **Platt Scaling** | Phương pháp hiệu chuẩn xác suất sử dụng hàm Sigmoid trên điểm phân loại để đưa điểm thô về xác suất thực nghiệm tin cậy, tối ưu hóa theo Brier score. |
| **Disjoint Family Partitioning** | Phương pháp phân chia tập dữ liệu huấn luyện, phát triển và kiểm thử ở cấp độ họ tài liệu (`document_family`), triệt tiêu 100% nguy cơ rò rỉ dữ liệu giữa các bài nộp cùng nhóm tác giả. |
| **Zero Test Snooping** | Nguyên tắc niêm phong tuyệt đối tập kiểm thử `test.csv`; không xem, không huấn luyện và không chỉnh sửa ngưỡng quyết định trên tập test cho đến khi chạy đánh giá cuối cùng. |
| **Dual-Write Gap & Reconciler** | Khoảng trống rủi ro khi ghi DB thành công nhưng enqueue vào Redis thất bại; được giải quyết triệt để bởi động cơ quét và phát lại Transactional Outbox Reconciler. |
| **Review Zone ($[0.30, 0.70)$)** | Vùng xác suất trung gian nơi các cặp tương đồng ngữ nghĩa tinh vi (như tóm tắt cô đọng) được hệ thống gắn cờ để Giảng viên đưa ra phán quyết chuyên môn trên Evidence Workbench. |

