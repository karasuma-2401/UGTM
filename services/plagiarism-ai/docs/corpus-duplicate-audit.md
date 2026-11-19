# Báo Cáo Kiểm Toán Danh Mục Nguồn & Phân Cụm Tài Liệu (CP-02.3)

> **Mã mốc:** `CP-02.3`  
> **Thời điểm thực hiện:** 2026-10-30  
> **Bộ dữ liệu kiểm toán:** `data/corpus_manifest.csv` (76 tài liệu) & `corpus_staging/` (72 tệp khả dụng)  
> **Trạng thái:** Hoàn tất thành công (Exit 0)

---

## 1. Mục Tiêu & Cơ Chế Kiểm Toán

Để đảm bảo tính liêm chính của kho ngữ liệu số (corpus) và chống rò rỉ dữ liệu (data leakage) giữa các tập huấn luyện/kiểm thử, kiểm toán CP-02.3 áp dụng quy trình kiểm tra đa tầng (multi-tier duplicate audit):

1. **Tầng 1 (Exact Binary Hash - SHA-256):** Băm toàn bộ nội dung byte tệp. Phát hiện các tệp sao chép nguyên bản hoặc tệp bị nhân bản vật lý.
2. **Tầng 2 (Exact Normalized Text Hash - SHA-256):** Băm phần văn bản thân bài (Body text) sau khi đã loại bỏ trang bìa, mục lục, danh mục từ viết tắt và tài liệu tham khảo (theo chuẩn `CP-02.2`). Phát hiện tài liệu giống hệt nhau về nội dung nhưng khác định dạng (ví dụ DOCX vs PDF hoặc đổi font chữ).
3. **Tầng 3 (Fuzzy Metadata & Prefix Matching):** Chuẩn hóa tiêu đề đề tài và tên tác giả/giảng viên hướng dẫn trích xuất từ trang bìa, sử dụng thuật toán Levenshtein và n-gram token overlap (ngưỡng tương đồng tiêu đề $\ge 0.90$, tên tác giả $\ge 0.90$).

---

## 2. Kết Quả Kiểm Toán Thực Tế

| Chỉ số | Số lượng | Tỷ lệ | Diễn giải |
| :--- | :---: | :---: | :--- |
| **Tổng số bản ghi manifest** | **76** | 100% | Toàn bộ danh mục nguồn trong hệ thống |
| **Bản ghi sẵn sàng chuẩn tắc (`canonical_ready`)** | **69** | 90.8% | Các luận văn độc lập, đầy đủ tiêu chuẩn đánh chỉ mục |
| **Bản thảo trùng lặp (`duplicates_flagged`)** | **3** | 3.9% | Các bản nháp hoặc bản nộp sớm của cùng một sinh viên |
| **Cần rà soát thủ công (`needs_review`)** | **4** | 5.3% | Luận văn vượt quá 250 trang (`TOO_MANY_PAGES`) |
| **Tổng số cụm gia đình (`document_families_count`)** | **73** | - | Số cụm gia đình tài liệu sau khi nhóm |
| **Tính bất biến của tệp gốc (`corpus_staging`)** | **100%** | - | Toàn bộ 72 tệp byte-identical trước và sau kiểm toán |

### 3 Cặp Bản Thảo Trùng Lặp Được Phát Hiện

Tất cả 3 cặp trùng lặp đều thuộc dạng: **Bản thảo DOCX (nộp sớm) vs Luận văn chính thức PDF (nộp cuối kỳ)** của cùng một tác giả, cùng tiêu đề:

```json
[
  {
    "duplicate_key": "kltn-e45578c28f8a9ad2",
    "canonical_key": "kltn-dd6e253c24129c7d",
    "family_id": "fam-f280bcc9a843",
    "reason": "FUZZY_METADATA:TITLE_AND_AUTHORS_MATCH (title=1.00, authors=1.00)"
  },
  {
    "duplicate_key": "kltn-9bb75d32e1218418",
    "canonical_key": "kltn-b2dfab9ef469d0b0",
    "family_id": "fam-be6c59ed2525",
    "reason": "FUZZY_METADATA:TITLE_AND_AUTHORS_MATCH (title=1.00, authors=1.00)"
  },
  {
    "duplicate_key": "kltn-c00909b39e5102ff",
    "canonical_key": "kltn-80094bee54805d0c",
    "family_id": "fam-08a4f19de2ef",
    "reason": "FUZZY_METADATA:TITLE_AND_AUTHORS_MATCH (title=1.00, authors=1.00)"
  }
]
```

---

## 3. Cấu Trúc Manifest Schema v2

Bảng dữ liệu `data/corpus_manifest.csv` đã được nâng cấp lên Schema v2 với 17 trường chuẩn hóa:

1. `source_key`: Mã định danh duy nhất của tài liệu.
2. `document_family`: Mã nhóm gia đình tài liệu (ví dụ `fam-f280bcc9a843`).
3. `corpus_id`: Mã kho ngữ liệu (mặc định `uit-library-v1`).
4. `version`: Phiên bản sửa đổi của tài liệu.
5. `staged_filename`: Tên file vật lý trong thư mục staging.
6. `file_sha256`: Mã băm SHA-256 nhị phân của tệp.
7. `normalized_text_sha256`: Mã băm SHA-256 của văn bản thân bài đã chuẩn hóa.
8. `bytes`: Dung lượng tệp tính bằng byte.
9. `page_count`: Số trang thực tế.
10. `extracted_characters`: Số ký tự thân bài trích xuất được.
11. `title`: Tiêu đề đề tài nghiên cứu.
12. `authors`: Tên sinh viên thực hiện.
13. `status`: Trạng thái xử lý (`ready`, `duplicate`, `needs_review`, `needs_ocr`).
14. `duplicate_of`: `source_key` của tài liệu chuẩn tắc nếu là bản trùng lặp.
15. `duplicate_reason`: Lý do và điểm số trùng lặp.
16. `processing_permission`: Quyền hạn xử lý dữ liệu (`all`, `local_only`).
17. `notes`: Ghi chú bổ sung hoặc lý do ngoại lệ.

Bản gốc trước khi nâng cấp đã được lưu trữ an toàn tại `data/corpus_manifest.b0.csv`.

