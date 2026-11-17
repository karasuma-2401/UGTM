# Đặc Tả Thống Nhất Lọc Section & Phân Đoạn Văn Bản (Chunking) (`CP-02.2`)

- **Ngày ban hành:** `2026-10-30`
- **Mã checkpoint:** `CP-02.2`
- **Tệp cấu hình chính:** [`configs/preprocessing_config.json`](../configs/preprocessing_config.json)
- **Mã nguồn xử lý:** [`app/preprocessing.py`](../app/preprocessing.py), [`app/documents.py`](../app/documents.py)
- **Kiểm thử tự động:** [`tests/test_preprocessing_and_chunking.py`](../tests/test_preprocessing_and_chunking.py)

---

## 1. Bối Cảnh & Vấn Đề Lệch Pha Giữa Offline và Online (Baseline B0)

Trong giai đoạn Baseline B0:
- **Offline (Gán nhãn & sinh task):** [`scripts/create_annotation_candidates.py`](../scripts/create_annotation_candidates.py) gọi `make_chunks(pages, exclude_sections=DEFAULT_EXCLUDED_SECTIONS)` loại bỏ bìa, mục lục, từ viết tắt, tài liệu tham khảo.
- **Online (Celery Worker index & scan):** `app/tasks.py:parse_job()` gọi `make_chunks(extract_pages(...))` với `exclude_sections=None` (không lọc bất kỳ phân vùng nào).
- **Hệ quả tiêu cực:** 
  - Kho vector `integrity.chunks` bị phình to do lưu trữ cả trang bìa, mục lục, bảng tra cứu.
  - Tỉ lệ cảnh báo giả (false alarms) cao do sinh viên cùng trích xuất chung danh mục biểu mẫu trường.
  - Phân phối độ dài và đặc trưng của chunk huấn luyện offline lệch pha với chunk thực tế khi người dùng quét bài trực tuyến.

---

## 2. Cấu Hình Tiền Xử Lý Thống Nhất (`configs/preprocessing_config.json`)

Toàn bộ tham số tiền xử lý được quản lý tập trung và chia thành các profile hoạt động rõ ràng:

```json
{
  "version": "preprocessing-v1",
  "default_profile": "production_v1",
  "profiles": {
    "production_v1": {
      "strategy": "sliding_window",
      "exclude_sections": [
        "cover",
        "table_of_contents",
        "abbreviations",
        "references",
        "appendix",
        "header",
        "footer",
        "empty"
      ],
      "size": 90,
      "overlap": 20,
      "min_words": 8
    },
    "baseline_b0": {
      "strategy": "sliding_window",
      "exclude_sections": [],
      "size": 90,
      "overlap": 20,
      "min_words": 8
    },
    "sentence_aware": {
      "strategy": "sentence_window",
      "exclude_sections": [
        "cover", "table_of_contents", "abbreviations", "references", "appendix", "header", "footer", "empty"
      ],
      "target_words": 90,
      "overlap_sentences": 1,
      "min_words": 8,
      "max_words": 140
    },
    "paragraph_aware": {
      "strategy": "paragraph_boundary",
      "exclude_sections": [
        "cover", "table_of_contents", "abbreviations", "references", "appendix", "header", "footer", "empty"
      ],
      "target_words": 90,
      "min_words": 8,
      "max_words": 150
    }
  }
}
```

---

## 3. Hệ Thống Mã Lý Do Loại Bỏ Khối (Discard Reasons)

Mỗi khối/phân vùng khi bị loại bỏ trong quá trình tiền xử lý đều được ghi nhận vào `PreprocessingStats`:

| Phân vùng bị loại | Mã lý do theo dõi (`discard_reason`) | Ý nghĩa nghiệp vụ |
|---|---|---|
| `cover` | `COVER_BOILERPLATE` | Biểu mẫu bìa trường, tên sinh viên, giảng viên, niên khóa |
| `table_of_contents` | `TOC_BOILERPLATE` | Mục lục, danh mục hình ảnh, danh mục bảng biểu |
| `abbreviations` | `ABBREVIATIONS_BOILERPLATE` | Danh mục thuật ngữ và từ viết tắt |
| `references` | `REFERENCES_BIBLIOGRAPHY` | Tài liệu tham khảo, danh mục trích dẫn sách báo |
| `appendix` | `APPENDIX_EXCLUDED` | Phụ lục đính kèm sau bài viết |
| `header` | `HEADER_REPETITION` | Dòng tiêu đề đầu trang lặp lại qua các trang PDF |
| `footer` | `FOOTER_REPETITION` | Dòng chân trang / đánh số trang lặp lại |
| `empty` | `EMPTY_BLOCK_OR_PAGE` | Trang trắng hoặc khối không chứa text layer |

---

## 4. Các Chiến Lược Phân Đoạn (Chunking Strategies)

1. **`sliding_window` (Mặc định - Tương thích B0):**
   - Cửa sổ trượt theo từ: `size = 90`, `overlap = 20`, `min_words = 8`.
   - Bảo toàn 100% tính lặp lại của thí nghiệm B0.
2. **`sentence_window` (Bảo toàn ranh giới câu):**
   - Phân đoạn câu tiếng Việt thông minh qua regex và danh mục viết tắt (`ABBR_RE`: `PGS.`, `TS.`, `ThS.`, `TP.`, `GVHD.`, `et al.`, số thập phân `3.14`).
   - Gom các câu trọn vẹn cho tới khi đạt `target_words = 90` (tối đa `max_words = 140`), bước nhảy `step = 1..2` câu.
   - **Ưu điểm:** Tuyệt đối không cắt ngang giữa chừng một câu văn.
3. **`paragraph_boundary` (Bảo toàn ranh giới đoạn văn):**
   - Sử dụng các `DocumentBlock` đoạn văn trong DOCX hoặc khối văn bản PDF.
   - Gom các đoạn văn ngắn liền kề cho tới khi đạt `target_words = 90`.
   - Nếu một đoạn văn quá dài (> 150 từ), tự động chuyển sang phân đoạn theo câu bên trong đoạn đó.
   - **Ưu điểm:** Ý nghĩa học thuật mạch lạc, không trộn lẫn đoạn văn của các phân vùng khác nhau.

---

## 5. Thống Kê Thực Nghiệm Trước & Sau Lọc Trên Tài Liệu Thử Nghiệm

| Tài liệu mẫu | Cấu hình | Tổng khối | Khối giữ lại | Khối bị loại | Lý do loại bỏ chính | Số chunks sinh ra |
|---|---|---:|---:|---:|---|---:|
| `cover_plus_body_b0.docx` | `baseline_b0` (không lọc) | 8 | 8 | 0 | Không | 4 |
| `cover_plus_body_b0.docx` | `production_v1` (có lọc) | 8 | 3 | 5 | `COVER_BOILERPLATE`: 5 | 4 |
| `cover_body_refs.docx` | `production_v1` (có lọc) | 14 | 5 | 9 | `COVER_BOILERPLATE`: 5<br>`REFERENCES_BIBLIOGRAPHY`: 4 | 3 |
| `cover_body_refs.docx` | `sentence_aware` | 14 | 5 | 9 | `COVER_BOILERPLATE`: 5<br>`REFERENCES_BIBLIOGRAPHY`: 4 | 2 |
| `multi_page_with_header_footer.pdf` | `production_v1` | 15 | 8 | 7 | `HEADER_REPETITION`: 2<br>`FOOTER_REPETITION`: 2<br>`EMPTY_BLOCK_OR_PAGE`: 1 | 2 |

---

## 6. Xác Minh Độ Khớp Giữa Offline và Online (Parity Verification)

Hàm [`prepare_document_chunks()`](../app/preprocessing.py#L170) đóng vai trò là điểm tiếp nhận duy nhất cho cả:
1. **Online Celery Task:** [`app/tasks.py:parse_job()`](../app/tasks.py#L14)
2. **Offline Annotation Pipeline:** [`scripts/create_annotation_candidates.py:load_staged_corpus_chunks()`](../scripts/create_annotation_candidates.py#L125)

Xác minh qua kiểm thử tự động [`tests/test_preprocessing_and_chunking.py::test_offline_online_preprocessing_parity`](../tests/test_preprocessing_and_chunking.py#L110):
- **100% khớp từng byte** về `chunk_no`, `text`, `start_offset`, `end_offset`, `section_type`, `start_block_index`, `end_block_index`.
- Đảm bảo dữ liệu huấn luyện và dữ liệu vận hành thực tế hoàn toàn đồng nhất.

