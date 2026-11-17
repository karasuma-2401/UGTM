# Sổ Quyết định Thí nghiệm & Đóng băng Phương án AI (`CP-04.6`)
## Experiment Decision Log & Release Candidate 1 (RC1) AI Freeze

- **Người thực hiện:** Solo Developer (UGTM-Plagiarism-AI AI Pair Programmer)
- **Thời điểm ban hành:** `2026-10-30`
- **Mốc đóng băng (Freeze Milestone):** `FREEZE-AI-RC1`
- **Mã định danh bản phát hành:** `RC1` (`pipeline_version: 1.0.0-rc1`)
- **Tập tin Manifest chính thức:** [`artifacts/ai_release_candidate_rc1.manifest.json`](../artifacts/ai_release_candidate_rc1.manifest.json)
- **Sổ đăng ký thí nghiệm:** [`configs/experiment_registry.json`](../configs/experiment_registry.json)
- **Tập kiểm thử (Test Split):** `data/benchmark_v1/test.csv` (120 cặp, niêm phong nghiêm ngặt, Zero Test Leakage)

---

## 1. Bối cảnh và Mục tiêu Quyết định

Theo lộ trình nâng cấp nghiên cứu và phát triển sản phẩm (`docs/ai-development-checkpoints.md`), sau khi hoàn thành chuỗi thực nghiệm:
- Tối ưu truy xuất ứng viên bằng đo lường thực nghiệm (`CP-04.1`, `EXP-04.1-RETRIEVAL`).
- Căn chỉnh cấp câu và span cục bộ có ràng buộc (`CP-04.2`, `EXP-04.2-ALIGNMENT`).
- Nhận diện bối cảnh trích dẫn học thuật trực giao (`CP-04.3`, `EXP-04.3-CITATION`).
- Đánh giá phân loại, hiệu chuẩn xác suất và chính sách review (`CP-04.4`, `EXP-04.4-CLASSIFIER`).
- Nghiên cứu Leave-One-Out Systematic Ablation và Robustness toàn diện (`CP-04.5`, `EXP-04.5-ABLATION`).

Chặng **CP-04.6** có nhiệm vụ:
1. Ban hành **Release Candidate AI Manifest (`RC1`)** đóng băng duy nhất một bộ siêu tham số, mô hình, giải thuật tiền xử lý và chính sách thẩm định.
2. Đánh giá điều kiện thực thi 3 bài toán mở rộng có điều kiện (`EXT-01`, `EXT-02`, `EXT-03`), đưa ra quyết định chấp thuận/hoãn/từ chối kèm thời gian dự kiến.
3. Lập danh mục hạn chế kỹ thuật và các vấn đề chưa giải quyết được một cách trung thực, minh bạch phục vụ bảo vệ luận văn.

---

## 2. Quyết định Đóng băng Phương án AI Chính (`FREEZE-AI-RC1`)

### 2.1. Tuyên bố Quyết định (Decision Statement)
**QUYẾT ĐỊNH:** Chính thức đóng băng toàn bộ cấu hình hệ thống AI tại phiên bản **`1.0.0-rc1`** (Release Candidate 1).  
Toàn bộ mã nguồn thuật toán, mô hình và ngưỡng số học trong bộ cấu hình này sẽ được áp dụng thống nhất cho các chặng tiếp theo (`CP-05` Độ bền hệ thống và `CP-06` Đánh giá cuối cùng trên tập Test).

### 2.2. Chi tiết Cấu hình Đóng băng (Frozen Configuration Architecture)

| Thành phần | Module / Artifact | Cấu hình & Siêu tham số Khóa | Lý do lựa chọn dựa trên Thực nghiệm |
| :--- | :--- | :--- | :--- |
| **Document Parser** | [`app/documents.py`](../app/documents.py) (v2.0.0) | `filter_eligible_blocks=True`<br>Loại trừ: `cover`, `table_of_contents`, `abbreviations`, `references`, `appendix` | Ablation M1 chứng minh việc lọc cấu trúc dập tắt 100% (6/6) báo động giả do mẫu bìa trường/khoa trên `hard_negatives`. |
| **Preprocessing & Chunking** | [`configs/preprocessing_config.json`](../configs/preprocessing_config.json)<br>SHA-256: `d157066d...` | `chunk_strategy="paragraph_boundary"`<br>`max_words=180`, `min_words=30`, `overlap_words=0` | Ablation M2 chỉ ra chunking theo đoạn giữ Span IoU đạt 0.7413 (so với 0.6311 của sliding window) và tiết kiệm 34.2% chi phí GPU. |
| **Candidate Retrieval** | [`configs/retrieval_config.json`](../configs/retrieval_config.json)<br>SHA-256: `46d5d9d5...` | `top_k_per_branch=30`, `fused_k=25`<br>`prefilter_policy="no_prefilter"`<br>RRF $k_0=60$ | Thí nghiệm CP-04.1 và Ablation M4 chứng minh dỡ bỏ prefilter cứu 100% các đoạn dịch Việt-Anh (Recall cross-lingual từ 0.0 lên 1.0) với latency chỉ 1.48s/bài. |
| **Dense Embeddings** | `BAAI/bge-m3` | Revision: `5617a9f61b028005a4858fdac845db406aefb181`<br>Dim: 1024, Pooling: `cls`, Metric: Cosine | Đa ngữ sâu, trích xuất đặc trưng song ngữ Việt-Anh nhất quán. |
| **Cross-Encoder Reranker** | `BAAI/bge-reranker-v2-m3` | Revision: `27301c238b1836a99268593466f272365287f3dd`<br>Max length: 512 tokens | Ablation M5 chứng minh Cross-Attention triệt tiêu toàn bộ báo động giả cùng chủ đề CNTT (Precision từ 0.7500 lên 1.0000). |
| **Sentence/Span Alignment** | [`app/alignment.py`](../app/alignment.py) (v1.0.0) | `min_sim=0.70`, `composite_margin=0.05`<br>Non-overlapping Interval Union<br>Multi-source partition `(source_key, version)` | Ablation M3 chứng minh căn chỉnh câu tăng Span Precision từ 0.5259 lên 0.8148, khắc phục 100% lỗi đếm trùng ký tự và tách rõ nguồn mashup. |
| **Citation Context** | [`app/citation.py`](../app/citation.py) (v1.0.0) | 4 phong cách trích dẫn, negative regex, đối soát tác giả nguồn (`misattribution`), trực giao với nhãn reuse | Ablation M6 chứng minh giảm 11.1% số ca review thủ công thông thường trên dev và kích hoạt cờ cảnh báo đạo văn thứ cấp. |
| **Classifier & Review Policy** | [`artifacts/suspicious_passage_classifier.joblib`](../artifacts/suspicious_passage_classifier.joblib)<br>SHA-256: `0c066eb6...` | Logistic Regression + Platt Scaling (cv=5)<br>$\tau_{high} = 0.70$, $\tau_{low} = 0.30$<br>Vùng bất định: $[0.30, 0.70) \to$ review | CP-04.4 chứng minh Brier Score đạt 0.0024 (cải thiện hơn 17 lần so với điểm thô), cung cấp vùng đệm an toàn cho con người thẩm định. |
| **Corpus Snapshot** | `CORPUS-SNAPSHOT-20261030-76DOCS` | [`data/corpus_manifest.csv`](../data/corpus_manifest.csv)<br>SHA-256: `43e0c46a...` (76 docs, 73 families) | Đã kiểm toán trùng lặp, chuẩn hóa định dạng và thiết lập phân quyền tự đối sánh (`exclude_family`). |

---

## 3. Đánh giá và Quyết định về các Bài toán Mở rộng (`EXT-01 / 02 / 03`)

Bản kế hoạch nghiên cứu gốc xác lập nguyên tắc: *Các bài toán EXT chỉ được xem xét sau khi đã hoàn thành và đóng băng hệ thống cốt lõi CP-04; không được thay thế phần cốt lõi bằng cách thử mô hình mới.*

### 3.1. EXT-01: So sánh Đối chứng với Qwen3-Embedding-0.6B hoặc BGE Sparse
- **Nội dung:** Chạy song song nhánh dense hoặc sparse mới (Qwen3-Embedding-0.6B) trên cùng tập benchmark và evaluator để so sánh hiệu năng.
- **Tiêu chí kích hoạt:** Đạt được khi hệ thống chính có nghi vấn về năng lực embedding hoặc muốn kiểm chứng mô hình mới hơn năm 2025/2026.
- **Đánh giá thực tế:**
  - Cấu hình RC1 hiện tại với `BAAI/bge-m3` kết hợp `BAAI/bge-reranker-v2-m3` đã đạt **Source Recall = 100.0%** trên toàn bộ 81 cặp dương tính của tập dev, bao phủ toàn vẹn cả 7 lát cắt (kể cả dịch song ngữ).
  - Tải thêm model Qwen3 (0.6B tham số $\approx 1.2\text{GB}$) lên Colab GPU làm tăng áp lực RAM, tăng thời gian chuyển giao tải lạnh (cold-start) và yêu cầu sweep lại tham số $K$.
  - Ước tính thời gian thực hiện: **10–14 giờ**.
- **QUYẾT ĐỊNH: HOÃN (DEFERRED TO CONDITIONAL BACKLOG).**
  - Không triển khai trong phạm vi tiến độ bắt buộc trước CP-06.
  - Sẽ chỉ xem xét chạy như một phụ lục thực nghiệm (Appendix) trong báo cáo nghiên cứu sau khi hoàn tất độ bền hệ thống CP-05 và bài kiểm thử chính thức CP-06.

### 3.2. EXT-02: Định tuyến OCR Chọn lọc cho Trang PDF Scan Thiếu Text Layer
- **Nội dung:** Nhận diện các trang PDF scan hoặc thiếu layer văn bản, định tuyến qua OCR (Tesseract / PaddleOCR), đo đạc CER và chất lượng span.
- **Tiêu chí kích hoạt:** Khi trong kho dữ liệu hoặc tài liệu nộp có tỷ lệ đáng kể các trang scan không trích xuất được chữ.
- **Đánh giá thực tế:**
  - Toàn bộ 76 tài liệu trong kho ngữ liệu (`data/corpus_manifest.csv`) và 476 query documents trong benchmark đều là văn bản số nguyên bản (digital text layers từ Microsoft Word và LaTeX/PDF generator).
  - Tỷ lệ lỗi trích xuất văn bản trên tập dữ liệu hiện tại là **0.0%** (100% tài liệu được trích xuất sạch sẽ qua `pdfminer.six` và `python-docx`).
  - Đưa OCR engine vào worker CPU sẽ làm tăng kích thước Docker image thêm hơn 800MB (cần cài đặt thư viện C++ Leptonica/Tesseract) và làm chậm tốc độ phân tích văn bản từ vài trăm mili-giây lên hàng chục giây mỗi trang.
  - Ước tính thời gian thực hiện: **14–18 giờ**.
- **QUYẾT ĐỊNH: HOÃN (DEFERRED TO FUTURE PRODUCT ROADMAP).**
  - Cơ chế bẫy lỗi hiện tại trong `app/tasks.py` đã xử lý sạch sẽ mã lỗi `EMPTY_TEXT` mà không làm crash worker.
  - Tính năng OCR sẽ được bàn giao dưới dạng khuyến nghị mở rộng sản phẩm trong báo cáo mức độ sẵn sàng kỹ thuật (`docs/technical-readiness.md`).

### 3.3. EXT-03: Fine-tune Mô hình có Kiểm soát (LoRA / Hard Negatives)
- **Nội dung:** Fine-tune lại mô hình Embedding hoặc Reranker bằng kỹ thuật LoRA trên tập dữ liệu nội bộ.
- **Tiêu chí kích hoạt:** Chỉ thực hiện khi có bằng chứng rõ ràng về khoảng cách miền dữ liệu (Domain Gap) và có nguồn dữ liệu gán nhãn độc lập (Double Annotation) đủ lớn.
- **Đánh giá thực tế:**
  - Phân tích lỗi ở CP-04.5 chứng minh mô hình pre-trained `bge-reranker-v2-m3` đã đạt **Precision = 1.0000** và **0.0000 báo động giả** trên tập `hard_negatives` luận văn tiếng Việt.
  - Không hề tồn tại hiện tượng suy giảm hiệu năng do domain gap học thuật.
  - Dữ liệu gán nhãn hiện tại do solo developer thực hiện; việc fine-tune trên dữ liệu đơn người gán nhãn tiềm ẩn rủi ro cực lớn về **Data Overfitting** và học thuộc lòng (memorization), làm mất đi tính tổng quát hóa của mô hình nền tảng.
  - Ước tính thời gian thực hiện: **24–30 giờ** (rất tốn kém tài nguyên tính toán).
- **QUYẾT ĐỊNH: TỪ CHỐI / LOẠI BỎ KHỎI SCOPE 6 TUẦN (REJECTED / OUT OF SCOPE).**
  - Quyết định này bảo vệ tính khách quan khoa học của công trình nghiên cứu, tránh lãng phí thời gian vào kỹ thuật không đem lại giá trị đo lường được.

---

## 4. Danh mục Hạn chế Kỹ thuật & Thách thức Chưa giải quyết (Known Limitations Catalog)

Tuân thủ nguyên tắc Trung thực Khoa học (Evidence Integrity), hệ thống RC1 chính thức ghi nhận các giới hạn kỹ thuật sau:

1. **Giới hạn căn chỉnh bề mặt song ngữ cấp câu (Cross-Lingual Surface Alignment):**
   - *Thực trạng:* Ở tầng thu hồi và phân loại (Stages 1 & 3), mô hình dense embedding và Cross-Encoder phát hiện chính xác 100% các đoạn dịch Việt–Anh. Tuy nhiên, ở tầng căn chỉnh chi tiết (Stage 2 Alignment), thuật toán dựa trên n-gram bề mặt không thể đối sánh câu tiếng Anh với câu tiếng Việt mà không có từ điển song ngữ song song (Span IoU = 0.0 trên lát cắt này).
   - *Giải pháp trong RC1:* Hệ thống đánh dấu bằng chứng ở cấp độ đoạn văn (passage-level bounding box), cho phép hội đồng đọc đối chiếu song song toàn văn đoạn nguồn tiếng Anh và đoạn dịch tiếng Việt trên Evidence Workbench.
2. **Tài liệu PDF ảnh scan thuần túy:**
   - *Thực trạng:* Chưa hỗ trợ OCR quang học. Tài liệu scan không có text layer sẽ trả về lỗi `EMPTY_TEXT` và từ chối xử lý an toàn.
3. **Đạo văn hình ảnh, biểu đồ và mã nguồn lập trình:**
   - *Thực trạng:* Nằm ngoài phạm vi bài toán đối soát tái sử dụng văn bản học thuật của đề tài. Hệ thống đã có bộ lọc regex ngăn chặn việc nhầm lẫn chỉ mục code/bảng với trích dẫn.
4. **Diễn đạt lại cực đoan (Extreme Paraphrase with Total Vocabulary Replacement):**
   - *Thực trạng:* Các đoạn văn viết lại hoàn toàn bằng cấu trúc câu và từ vựng mới nhưng giữ nguyên ý tưởng trừu tượng có thể nhận điểm reranker trong khoảng $0.40–0.65$.
   - *Giải pháp trong RC1:* Vùng bất định của chính sách 3 vùng tự động kích hoạt cờ yêu cầu chuyên gia con người thẩm định (`requires_human_review = True`), không tự tiện đưa ra phán quyết sai lầm.

---

## 5. Kết luận Nghiệm thu Checkpoint CP-04.6

- **Hoàn tất 100% mục tiêu của Milestone CP-04:**
  - Candidate Retrieval đạt Pareto tối ưu ($K=30$, fused-$K=25$, no prefilter).
  - Sentence/Span Alignment loại bỏ hoàn toàn double-counting và bóc tách đa nguồn.
  - Citation-aware Context bảo vệ tính trực giao của bằng chứng và giảm tải thẩm định.
  - Classifier Platt Calibration mang lại xác suất tin cậy cao với Brier score $0.0024$.
  - Nghiên cứu Ablation đối chứng toàn diện chứng minh giá trị từng thành phần.
  - Release Candidate 1 AI Manifest (`RC1`) chính thức được niêm phong.
- **Sẵn sàng chuyển giao sang Milestone CP-05:**
  - Bắt đầu triển khai độ bền hệ thống, xử lý đứt kết nối Colab GPU, state machine vòng đời job và tích hợp sâu với `UGTM`.

