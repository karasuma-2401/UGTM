# UGTM-Plagiarism-AI — Báo cáo Kiểm toán Baseline B0 (`CP-00.1`)

- **Ngày chụp snapshot:** `2026-10-30` (`2026-10-30T02:52:09Z`)
- **Task ID:** `CP-00.1`
- **Cấu hình B0 đóng băng:** [`configs/baseline_b0.json`](../configs/baseline_b0.json)
- **Nơi lưu dữ liệu & dự đoán B0 (Git-ignored):** `data/baseline_b0/`
- **Nguyên tắc kiểm định:** Không chỉnh sửa bất kỳ mã nguồn thuật toán nào trước khi hoàn tất snapshot này; không đọc tệp `.env`; phân biệt tuyệt đối giữa **kết quả đo được**, **hành vi chưa chạy được do thiếu dịch vụ nền**, và **giới hạn dữ liệu hiện tại**.

---

## 1. Định danh phiên bản mã nguồn (Code Provenance)

| Thuộc tính | Giá trị ghi nhận |
|---|---|
| Nhánh Git (`branch`) | `dev` |
| Commit gốc (`HEAD`) | `78e4a94f173a32f178bab70568d397625227533d` |
| Thông điệp commit gốc | `feat(fe): config FE for new source BE` |
| Commit liền trước | `e85ca41` (`feat: final plagiarism AI for check`), `4f3d83b` (`fix: config docker and server for AI`) |
| Thay đổi working tree trước `CP-00` | Chỉ có bổ sung mục liên kết kế hoạch nâng cấp ở cuối `README.md` và 2 tài liệu kế hoạch trong `docs/`; **0 dòng code trong `app/`, `training/`, `scripts/`, `sql/`, `tests/` bị sửa đổi** |

---

## 2. Kiểm định môi trường thực thi hiện tại (Measured Environment Status)

### 2.1. Những phần chạy thành công trên máy phát triển
- **Hệ điều hành & Python:** `Windows-11-10.0.26300-SP0`, virtualenv `.venv` chạy `Python 3.13.15` (`MSC v.1944 64 bit (AMD64)`). *(Lưu ý: lỗi `Access is denied` từng gặp trong phiên review tĩnh trước đó là do giới hạn sandbox lúc lập kế hoạch; khi chạy trực tiếp trên host PowerShell, `.venv\Scripts\python.exe` thực thi bình thường).*
- **Unit Tests hiện có (`pytest -v tests`):** **24/24 test PASSED** trong `27.52s` (`test_alignment.py`: 2, `test_annotation_workflow.py`: 7, `test_classifier.py`: 5, `test_documents.py`: 8, `test_features.py`: 2).
- **7/11 module Python cốt lõi import thành công:** `app.config`, `app.db`, `app.documents`, `app.features`, `app.alignment`, `app.classifier`, `app.search`.

### 2.2. Các lỗi môi trường phát hiện được & hướng khắc phục (Không tự ghi "passed" toàn luồng)

| Thành phần kiểm tra | Trạng thái thực tế | Bằng chứng / Mã lỗi cụ thể | Hướng khắc phục ở `CP-01` |
|---|---|---|---|
| Import `app.models` trên native `.venv` | **FAILED** | `ModuleNotFoundError: No module named 'requests'` — `app/models.py` gọi `import requests`, nhưng `requests` **chưa được khai báo trong `requirements.txt`** (trước đây chạy được trong Docker chỉ vì base image `pytorch/pytorch` có sẵn `requests`). Ngoài ra `.venv` chưa cài `torch`, `transformers`, `FlagEmbedding`. | Ở `CP-01.1`: khai báo tường minh HTTP client (`requests`/`httpx`), tách remote client khỏi local GPU loader để API/CPU worker không import `torch`/`FlagEmbedding`. |
| Import `app.celery_app`, `app.tasks`, `app.main` trên `.venv` | **FAILED** | `ModuleNotFoundError: No module named 'celery'` (`.venv` mới cài 33 gói phục vụ test parser/classifier, thiếu `celery`, `uvicorn`, `python-multipart`). | Ở `CP-01.1`: tách và cài đầy đủ `requirements-api.txt` / `requirements-worker.txt` kèm lockfile trên `.venv`. |
| Unpickle `suspicious_passage_classifier.joblib` | **WARNING** | `InconsistentVersionWarning`: artifact được serialize bằng `scikit-learn==1.6.1` (Kaggle), nhưng `.venv` đang cài `scikit-learn==1.9.1` do range mở `scikit-learn>=1.5,<2`. | Ở `CP-01.1` & `CP-04.4`: pin chặt phiên bản `scikit-learn` giữa môi trường train và runtime; lưu `sklearn_version` vào manifest. |
| Docker / PostgreSQL / Redis / Remote GPU | **OFFLINE** | `docker ps` trả về `0` container đang chạy; chưa kết nối Neon và Colab GPU. Do đó **chưa chạy được end-to-end dense vector (`BAAI/bge-m3`) + cross-encoder (`BAAI/bge-reranker-v2-m3`) + `pgvector`/`pg_trgm`** tại thời điểm `CP-00.1`. | Ở `CP-01.2`–`CP-01.6`: dựng Neon + Broker + Colab GPU và chạy lại đúng cấu hình `configs/baseline_b0.json` để ghi nhận điểm dense/reranker B0 đầy đủ. |

---

## 3. Cấu hình thuật toán Baseline B0 đã đóng băng

Toàn bộ thông số dưới đây được trích xuất trực tiếp từ mã nguồn tại commit `78e4a94f173a32f178bab70568d397625227533d` và khóa trong [`configs/baseline_b0.json`](../configs/baseline_b0.json):

1. **Parser (`app/documents.py`, `app/main.py`):**
   - Hỗ trợ `.pdf` (kiểm tra magic bytes `%PDF-`, tối đa `250` trang, trích xuất từng trang qua `page.get_text("text", sort=True)`) và `.docx` (kiểm tra ZIP chứa `word/document.xml`, dung lượng giải nén `<= 100 MiB`, tổng độ dài đoạn `<= 2,000,000` ký tự, upload tối đa `20 MB`).
   - Chuẩn hóa văn bản: `re.sub(r"\s+", " ", text).strip()`.
   - **Hạn chế B0 trên DOCX:** `extract_pages()` nối toàn bộ `doc.paragraphs` thành **1 `TextPage(page_number=None)` duy nhất** và gọi `classify_text_section(norm_text, page_number=1)`, bỏ qua bảng (`doc.tables`) và cấu trúc block.
2. **Chunker & Lọc cấu trúc (`app/documents.py`, `app/tasks.py`, `scripts/create_annotation_candidates.py`):**
   - Cửa sổ trượt theo từ trắng (`\S+`): `size = 90`, `overlap = 20` (bước nhảy `70` từ), `min_words = 8`.
   - **Bất nhất offline vs. online ở B0:** `scripts/create_annotation_candidates.py` truyền `exclude_sections={"cover", "table_of_contents", "abbreviations", "references"}`, nhưng `app/tasks.py:parse_job()` gọi `make_chunks(extract_pages(...))` với `exclude_sections=None` (không lọc gì khi index và scan).
3. **Embedding, Retrieval & Fusion (`app/models.py`, `app/search.py`, `app/tasks.py`):**
   - Embedding: `BAAI/bge-m3` (`1024` chiều, `max_length=512`, chỉ lấy `dense_vecs`, chưa pin model revision).
   - Truy xuất từng query chunk: lấy `top_k = 6` theo exact cosine `pgvector` (`1 - (embedding <=> query)`) và `top_k = 6` theo `pg_trgm.similarity(content, query)`, hợp nhất bằng RRF (`k = 60`), giữ `fused_k = 5` ứng viên, sau đó truy vấn lại Postgres để điền đủ cả `dense_score` và `lexical_score`.
   - Lọc thô trước Reranker (`app/tasks.py:146`): chỉ giữ cặp thỏa `dense_score >= 0.48 OR lexical_score >= 0.25`.
4. **Reranker, Features & Decision (`app/models.py`, `app/features.py`, `app/classifier.py`, `app/tasks.py`):**
   - Reranker: `BAAI/bge-reranker-v2-m3` (`normalize=True`, `max_length=512`, `batch_size=16` ở `tasks.py`, `32` khi gọi remote).
   - Feature schema `candidate-features-v1` (5 đặc trưng): `semantic_cosine`, `reranker_score`, `char_trigram_dice`, `token_jaccard`, `length_ratio`.
   - Mặc định `CLASSIFIER_ENABLED = false` → dùng rule heuristic: `lexical_score >= 0.65 OR (dense_score >= 0.68 AND reranker_score >= 0.67)`.
   - Khi `CLASSIFIER_ENABLED = true` → gắn cờ `review_required` nếu `classifier_score >= threshold` (`0.25`) hoặc `lexical_score >= 0.92` (`near_exact_override`).
5. **Passage Grouping & Report Schema (`app/alignment.py`, `app/tasks.py`, `frontend/src/components/ReportTab.tsx`):**
   - Cắt `all_findings[:200]` theo `reranker_score` giảm dần **trước khi** gọi `group_adjacent_findings()` (nhóm cùng `(source_key, source_version)` có `0 <= delta_submission_chunk_no <= 1` và `0 <= delta_source_chunk_no <= 1`).
   - Worker ghi `schema_version: 2` (`model`, `summary`, mảng phẳng `findings` với khóa `reranker_score`), nhưng `ReportTab.tsx` đang đọc `report.passage_groups`, `report.max_rerank_score`, `report.total_chunks`, `finding.rerank_score` không qua adapter.

---

## 4. Kiểm toán Manifest Classifier & Dữ liệu hiện có

### 4.1. Hiện trạng Artifact Classifier (`candidate-lr-v1`)
Trong thư mục `artifacts/` hiện tồn tại **hai cặp tệp giống hệt nhau từng byte** (khác dấu gạch dưới `_` và gạch ngang `-`):
- `artifacts/suspicious_passage_classifier.joblib` & `artifacts/suspicious-passage-classifier.joblib`
  - **SHA-256:** `7a5267f6fa6abd37cd3d6344dffa839a55e61a522d811d1020f495c5283d7a7a` (`1,505` bytes)
- `artifacts/suspicious_passage_classifier.manifest.json` & `artifacts/suspicious-passage-classifier.manifest.json`
  - **SHA-256:** `57c97806e81d313b90f4177cab8a181ed9b2a27c29a53069bea763f46444fb32` (`created_at: 2026-10-29T13:05:32.572539+00:00`)

**Giới hạn nghiêm trọng của manifest hiện tại (bắt buộc ghi nhận):**
- `dataset_size = 24` cặp câu ngắn hoàn toàn synthetic (tương ứng tệp `examples/pairs.csv`, 12 nhóm `source-family-01..12`, mỗi nhóm chỉ gồm 1 câu dương tính ~12 từ và 1 câu âm tính khác chủ đề).
- Chia theo `GroupShuffleSplit`: **Train = 6 nhóm (12 cặp)**, **Validation = 3 nhóm (6 cặp)**, **Test = 3 nhóm (6 cặp: 3 dương, 3 âm)**.
- Trọng số Logistic Regression học được: `coef = [+1.2660 (semantic_cosine), +0.7961 (reranker_score), +0.1375 (char_trigram_dice), +0.2833 (token_jaccard), -0.2737 (length_ratio)]`, `intercept = +0.0686`, `threshold = 0.25`.
- **Kết luận kiểm toán:** Chỉ số `Precision = 1.0, Recall = 1.0, F1 = 1.0` trên tập test chỉ phản ánh **6 dự đoán synthetic dễ**, thậm chí `length_ratio` mang hệ số âm nhiễu mẫu nhỏ. Con số này **không có giá trị chứng minh chất lượng phát hiện tái sử dụng trên luận văn thật** và tuyệt đối không được dùng làm headline độ chính xác. Giữ `CLASSIFIER_ENABLED = false` cho tới `CP-04.4`.

### 4.2. Hiện trạng Corpus Staging & Triage Batch cũ (Chỉ thống kê metadata an toàn)
- `data/corpus_manifest.csv` (Git-ignored): gồm `76` bản ghi (`72` file `.pdf` ở trạng thái `ready`, `4` file `.docx` ở trạng thái `needs_review`); thư mục `corpus_staging/` hiện lưu `72` tệp PDF đã khử tên.
- `training_data/triage/triage_summary.json` (SHA-256 nguồn: `4b89d025eb8eb9bda5e58a2d246ad5a31cdf0dcce6df9ec501a22c5d2c91f87c`): trên `600` cặp ứng viên sinh tự động từ `64` `group_id`, có tới **529/600 cặp (88.17%)** là nhiễu biểu mẫu bìa (`467`) và mục lục/tài liệu tham khảo (`62`), `24` cặp bảng cấu trúc, `4` cặp nghi thuộc `3` khóa luận bị nhập trùng, chỉ còn **43 cặp nội dung** để xem xét và **0 cặp có nhãn ground-truth được duyệt**.

---

## 5. Kết quả chạy thử DOCX & PDF được phép và Truy vết Dữ liệu B0

Đã chạy thực nghiệm các module CPU deterministic của B0 trên cặp tài liệu mẫu hợp lệ trong repo (`app/static/samples/source_doc.docx`, `app/static/samples/suspect_doc.docx`) cùng cặp PDF đối ứng sinh từ chính văn bản mẫu đó (`source_doc_b0.pdf`, `suspect_doc_b0.pdf`) và cặp kiểm chứng trang bìa (`cover_plus_body_b0.docx`, `cover_plus_body_b0.pdf`).

### 5.1. Thống kê trích xuất & chia đoạn B0

| Tài liệu thử nghiệm | Định dạng | SHA-256 tệp đầu vào | Số `TextPage` | Nhãn `section_type` | Chunks (không lọc) | Chunks (khi bật `exclude_sections`) |
|---|---|---|---:|---|---:|---:|
| `app/static/samples/source_doc.docx` | DOCX | `31e47d3d4357e88e...` | 1 (`page=None`) | `['body']` | 4 | 4 |
| `app/static/samples/suspect_doc.docx` | DOCX | `888dd8c3208de9e9...` | 1 (`page=None`) | `['body']` | 3 | 3 |
| `data/baseline_b0/samples/source_doc_b0.pdf` | PDF | `ac0834c8c4d89dd9...` | 2 (`page=1, 2`) | `['body', 'body']` | 4 | 4 |
| `data/baseline_b0/samples/suspect_doc_b0.pdf` | PDF | `ff5958895da69320...` | 2 (`page=1, 2`) | `['body', 'body']` | 4 | 4 |
| `data/baseline_b0/samples/cover_plus_body_b0.docx` | DOCX | *(sinh cục bộ)* | 1 (`page=None`) | `['cover']` | 4 | **0 (mất 100% phần thân)** |
| `data/baseline_b0/samples/cover_plus_body_b0.pdf` | PDF | *(sinh cục bộ)* | 2 (`page=1, 2`) | `['cover', 'body']` | 5 | **4 (giữ đúng phần thân)** |

### 5.2. Hai phát hiện định lượng quan trọng từ dữ liệu dự đoán B0
1. **Bằng chứng thực nghiệm lỗi mất toàn bộ thân bài DOCX (`b0_cover_bug_repro.json`):** Khi tài liệu có trang bìa chuẩn khóa luận UIT ở đầu, bản PDF tách trang giữ được `4/5` chunks phần thân sau khi lọc `cover`, trong khi bản DOCX cùng nội dung bị gán `section_type="cover"` trên `TextPage` duy nhất và trả về **`0` chunk** nếu bật `exclude_sections`. Điều này xác nhận bắt buộc phải hoàn thành `CP-02.1` (block-level DOCX parser) trước `CP-02.2`.
2. **Hiệu ứng lệch pha cửa sổ 90 từ làm loãng điểm lexical (`b0_audit_snapshot.json`):** `suspect_doc` chứa đoạn chép nguyên văn ~95 từ về kiến trúc Transformer từ `source_doc`, nhưng đặt sau ~134 từ mở đầu về Microservices. Do cửa sổ cố định 90 từ bị lệch pha giữa hai tài liệu:
   - Trên DOCX: cặp khớp cao nhất (`submission_chunk_no=2` vs `source_chunk_no=1`) đạt `char_trigram_dice = 0.7094`, `token_jaccard = 0.5567`, nhưng `pg_trgm` xấp xỉ chỉ đạt **`0.5987`** (qua được prefilter `0.25`, nhưng thấp hơn ngưỡng lexical heuristic `0.65`).
   - Trên PDF: cặp khớp cao nhất (`submission_chunk_no=3` vs `source_chunk_no=1`) đạt `char_trigram_dice = 0.7315`, `token_jaccard = 0.5862`, `pg_trgm` xấp xỉ đạt **`0.6392`** (vẫn thấp hơn `0.65`).
   - Hệ quả: Ở B0, ngay cả một đoạn chép nguyên văn ~3 câu dài cũng phụ thuộc hoàn toàn vào nhánh GPU (`dense >= 0.68 AND reranker >= 0.67`) để được gắn cờ nếu bị lệch pha chunk 90 từ — củng cố trực tiếp động lực chuyển sang sentence/paragraph chunking & span alignment ở `CP-02.2` và `CP-04.2`.

### 5.3. Bảng truy vết tệp đầu ra B0 (Reproducibility & Traceability)

Mọi report và tập dự đoán B0 dưới đây được tạo bởi commit `78e4a94f173a32f178bab70568d397625227533d` và cấu hình [`configs/baseline_b0.json`](../configs/baseline_b0.json):

| Tệp lưu trữ | Phạm vi Git | Nội dung truy vết |
|---|---|---|
| [`configs/baseline_b0.json`](../configs/baseline_b0.json) | Tracked | Toàn bộ siêu tham số parser, chunker, retrieval, prefilter, heuristic, classifier manifest và schema v2 của B0. |
| `data/baseline_b0/b0_audit_snapshot.json` | Git-ignored (`data/`) | Snapshot đầy đủ: probe imports, trọng số/scaler của `candidate-lr-v1`, chunks/offsets của DOCX & PDF mẫu, đặc trưng lexical của 24 cặp `examples/pairs.csv` và ma trận điểm từng cặp chunk. |
| `data/baseline_b0/b0_sample_report_docx_schema_v2.json` | Git-ignored (`data/`) | Báo cáo mẫu `schema_version: 2` (chế độ CPU offline, ghi rõ `dense_score: null`, `reranker_score: null`) cho cặp DOCX mẫu. |
| `data/baseline_b0/b0_sample_report_pdf_schema_v2.json` | Git-ignored (`data/`) | Báo cáo mẫu `schema_version: 2` (chế độ CPU offline) cho cặp PDF mẫu 2 trang. |
| `data/baseline_b0/b0_cover_bug_repro.json` | Git-ignored (`data/`) | Kết quả đối chứng DOCX (`0/4` chunks còn lại) vs PDF (`4/5` chunks còn lại) khi có trang bìa và bật `exclude_sections`. |

