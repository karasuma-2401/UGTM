# Báo Cáo Kiểm Toán Truy Ngược Bằng Chứng (Evidence Traceability Audit) & Bộ Kiểm Thử Hồi Quy (CP-02.5)

> **Mã mốc:** `CP-02.5`  
> **Thời điểm thực hiện:** 2026-10-30  
> **Áp dụng cho:** `app/evidence.py`, `scripts/audit_evidence_traceability.py`, `tests/test_evidence_traceability.py`  
> **Trạng thái:** Hoàn tất thành công (Exit 0, 100% Traceability)

---

## 1. Mục Tiêu & Cơ Chế Kiểm Toán Bằng Chứng

Hệ thống rà soát tính liêm chính học thuật đòi hỏi **tính minh bạch và giải trình tối đa (Full Explainability & Auditability)**:
Mọi đoạn văn bản xuất hiện trong báo cáo tương đồng (`ReportV3`) phải có khả năng truy vết ngược xuôi không đứt đoạn:
$$\text{Report Finding} \longrightarrow \text{Document Chunk} \longrightarrow \text{Block / Paragraph / Page} \longrightarrow \text{Tệp Vật Lý Gốc (.docx / .pdf)}$$

### Các Tiêu Chí Kiểm Tra:
1. **Độ Chuẩn Xác Ký Tự (Character-Level Fidelity):** Văn bản trích dẫn trong báo cáo phải trùng khớp nguyên gốc ($\ge 0.95$) với văn bản đọc trực tiếp từ tệp lưu trữ.
2. **Bảo Toàn Toàn Vẹn Tiếng Việt (Vietnamese Diacritics & Unicode Integrity):**
   - Đảm bảo 100% thanh dấu tiếng Việt (`ă, â, đ, ê, ô, ơ, ư` và các dấu hỏi, ngã, nặng, sắc, huyền) không bị lỗi phân rã NFD hoặc biến dạng thành mã Mojibake (`Ã¡`, `á»`, `\ufffd`).
3. **Tính Liên Tục & Chồng Lấn Đoạn (Passage Group Overlap):**
   - Trong cùng một nhóm đoạn văn (`passage_group_id`), phần giao nhau giữa hai chunk kế tiếp (`overlap_span`) phải khớp chính xác từng ký tự trên văn bản gốc.
4. **Bộ Kiểm Thử Hồi Quy Tình Huống Biên & Lỗi (Edge & Error Cases):**
   - Tài liệu không có trùng lặp trong kho (Clean Document).
   - Tệp rỗng 0-byte (`EMPTY_TEXT`).
   - Tệp hỏng / không đọc được (`CORRUPTED_DOCUMENT`).
   - Kho ngữ liệu rỗng (`CORPUS_EMPTY`).

---

## 2. Kết Quả Kiểm Toán Thực Tế (`scripts/audit_evidence_traceability.py`)

Báo cáo lưu trữ tại [`data/evidence_audit_report.json`](../data/evidence_audit_report.json):

| Chỉ số kiểm toán | Kết quả thực tế | Ngưỡng yêu cầu | Đánh giá |
| :--- | :---: | :---: | :---: |
| **Tổng số bằng chứng kiểm toán** | **3 findings** | - | Hoàn tất |
| **Tỷ lệ truy ngược tệp nguồn (`source_traceability_rate`)** | **100.0%** | $\ge 95\%$ | Đạt tuyệt đối |
| **Tỷ lệ truy ngược tệp bài nộp (`submission_traceability_rate`)** | **100.0%** | $\ge 95\%$ | Đạt tuyệt đối |
| **Tỷ lệ bảo toàn thanh dấu tiếng Việt (`vietnamese_integrity_rate`)** | **100.0%** | $100\%$ | Đạt tuyệt đối |
| **Số nhóm đoạn văn kiểm toán chồng lấn (`passage_groups`)** | **1 group** | - | 100% Khớp |
| **Tất cả bằng chứng truy vết thành công (`all_evidence_traceable`)** | **TRUE** | **TRUE** | **XUẤT SẮC** |

### Chi Tiết Kiểm Tra Khớp Đoạn Chồng Lấn (Overlap Span Verification)
- Khoảng giao giữa Chunk 0 và Chunk 1: `[335, 414]` (độ dài 79 ký tự).
- `overlap_match_ratio: 1.0` (khớp hoàn hảo 100%).

---

## 3. Bộ Kiểm Thử Hồi Quy Các Trường Hợp Biên & Lỗi

Triển khai tại [`tests/test_evidence_traceability.py`](../tests/test_evidence_traceability.py):

1. `test_vietnamese_diacritics_integrity_and_mojibake_detection`:
   - Xác nhận chuỗi tiếng Việt chuẩn NFC không có cảnh báo.
   - Phát hiện chính xác lỗi chuỗi Mojibake (`MOJIBAKE_DETECTED`).
2. `test_trace_evidence_to_physical_docx`:
   - Truy ngược từ `finding` đến tệp `source_doc.docx` vật lý: fidelity đạt `1.0`.
3. `test_trace_evidence_to_physical_pdf`:
   - Truy ngược từ `finding` đến tệp `multi_page_with_header_footer.pdf` vật lý: xác định chính xác số trang vật lý `page_number` và khối văn bản.
4. `test_passage_group_overlap_verification`:
   - Xác minh thuật toán tính overlap giữa các chunk kế cận.
5. `test_edge_case_clean_document_no_match`:
   - Tài liệu độc lập hoàn toàn sạch: báo cáo hoàn tất `status="completed"`, `similarity_coverage=0.0`, `total_findings=0`.
6. `test_edge_case_empty_and_corrupted_file_handling`:
   - Tệp rỗng 0-byte: bắt và gán mã lỗi chuẩn `EMPTY_TEXT`.
   - Tệp hỏng/mã độc giả mạo đuôi docx: bắt và gán mã lỗi chuẩn `CORRUPTED_DOCUMENT`.
7. `test_edge_case_empty_corpus_in_scan`:
   - Kho chưa có tài liệu nào active: báo lỗi chuẩn `CORPUS_EMPTY`.

