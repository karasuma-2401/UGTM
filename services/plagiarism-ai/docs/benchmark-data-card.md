# Thẻ Dữ Liệu Chuẩn Đoán (Dataset Card) — UGTM-Bench-v1 (CP-03.2)

> **Tên tập dữ liệu:** `UGTM-Bench-v1`  
> **Phiên bản:** `1.0.0`  
> **Mã mốc phát triển:** `CP-03.2`  
> **Thời điểm ban hành:** 2026-10-30  
> **Người thực hiện:** Solo Annotator Author  
> **Quyền sử dụng:** Nghiên cứu khoa học nội bộ (Academic Research & Internal UIT Benchmark Only)  
> **Tệp dữ liệu:** `data/benchmark_v1/benchmark_pairs.csv` & `data/benchmark_v1/benchmark_metadata.json`

---

## 1. Tổng Quan & Mục Tiêu Nghiên Cứu

`UGTM-Bench-v1` là bộ dữ liệu chuẩn (benchmark) chuyên biệt đầu tiên dành cho việc đánh giá các mô hình và thuật toán phát hiện tái sử dụng văn bản học thuật (Academic Text Reuse & Plagiarism Detection) trong bối cảnh luận văn đại học/thạc sĩ bằng tiếng Việt và song ngữ Việt–Anh.

Bộ dữ liệu giải quyết 3 điểm nghẽn lớn của các nghiên cứu trước đây:
1. **Khắc phục hiện tượng "dễ ảo" của bộ dữ liệu đồ chơi:** Thay thế tập synthetic 24 cặp câu ngắn cũ (`examples/pairs.csv`) bằng **800 cặp đoạn văn bản học thuật hoàn chỉnh** (trung bình 40–120 từ mỗi đoạn).
2. **Bao phủ toàn diện 7 hình thái tái sử dụng thực tế (7 Evaluation Slices):** Không chỉ dừng lại ở sao chép nguyên văn (`verbatim`), mà kiểm tra sâu khả năng phát hiện diễn đạt lại (`paraphrase`), tóm tắt (`summarization`), dịch thuật song ngữ (`cross_lingual_vi_en`), ghép nối đa nguồn (`multi_source`), và khả năng phân biệt âm tính khó cùng chủ đề (`hard_negatives`).
3. **Bảo toàn nguồn gốc nhãn và kiểm soát rò rỉ (Zero Leakage):** Mọi mẫu đều có mã `document_family` để đảm bảo khi chia tập huấn luyện/kiểm thử ở `CP-03.3`, không có hiện tượng rò rỉ dữ liệu giữa các biến thể của cùng một đề tài.

---

## 2. Thống Kê Quy Mô & Cơ Cấu Phân Tầng (Slices Distribution)

| Slice | Tên phân tầng | Nhãn | Số lượng cặp | Tỷ lệ | Mô tả phương pháp tạo |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **`verbatim_copy`** | Sao chép nguyên văn | `1` | **120** | 15.0% | Đoạn văn thân bài sao chép nguyên vẹn hoặc cận nguyên văn ($\ge 85\%$ từ trùng lặp). |
| **`paraphrase`** | Diễn đạt lại | `1` | **140** | 17.5% | Biến đổi từ đồng nghĩa kỹ thuật, cấu trúc câu chủ/bị động, đảo trật tự mệnh đề tiếng Việt. |
| **`summarization`** | Tóm tắt luận điểm | `1` | **100** | 12.5% | Rút gọn đoạn văn phân tích 3–4 câu thành 1 câu kết luận luận điểm then chốt. |
| **`cross_lingual_vi_en`** | Song ngữ Việt–Anh | `1` | **100** | 12.5% | Dịch thuật các khái niệm/lý thuyết kinh điển từ nguồn tiếng Anh sang tiếng Việt. |
| **`multi_source`** | Tổng hợp đa nguồn | `1` | **80** | 10.0% | Đoạn văn ghép nối ý tưởng từ 2 document families khác nhau trong cùng ngữ cảnh. |
| **`hard_negatives`** | Âm tính cùng chủ đề | `0` | **180** | 22.5% | Các đoạn văn từ 2 document families khác nhau, cùng ngành CNTT nhưng viết hoàn toàn độc lập. |
| **`out_of_corpus`** | Ngoài kho ngữ liệu | `0` | **80** | 10.0% | Trích xuất từ tài liệu chuẩn mở bên ngoài (Wikipedia tiếng Việt, giáo trình CNTT mở). |
| **TỔNG CỘNG** | | | **800** | **100.0%** | **540 Dương tính (`1`) / 260 Âm tính (`0`)** |

- **Số cụm gia đình tài liệu bao phủ (`document_families_count`):** $\ge 50$ document families.
- **Số tài liệu truy vấn (`query_documents_count`):** $\ge 60$ query documents độc lập.

---

## 3. Cấu Trúc Bảng Dữ Liệu (`BENCHMARK_FIELDS`)

Tệp `data/benchmark_v1/benchmark_pairs.csv` gồm 17 trường chuẩn hóa:

1. `pair_id`: Mã định danh duy nhất của cặp (`BENCH-0001` đến `BENCH-0800`).
2. `query_doc_id`: Mã tài liệu truy vấn sinh ra đoạn query.
3. `query_family_id`: Mã cụm gia đình của tài liệu truy vấn (hoặc `fam-external-open`).
4. `source_doc_id`: Mã tài liệu nguồn.
5. `source_family_id`: Mã cụm gia đình của tài liệu nguồn.
6. `query_text`: Nội dung đoạn văn truy vấn.
7. `source_text`: Nội dung đoạn văn nguồn.
8. `label`: Nhãn nhị phân (`1` = Có tái sử dụng, `0` = Độc lập).
9. `slice`: Phân tầng đánh giá (1 trong 7 slices ở bảng trên).
10. `reuse_type`: Hình thức tái sử dụng (`verbatim`, `paraphrase`, `translated`, `summary`, `independent`).
11. `citation_status`: Trạng thái trích dẫn (`cited`, `uncited`, `not_applicable`).
12. `query_span`: Vị trí tương đối trong query document.
13. `source_span`: Vị trí tương đối trong source document.
14. `provenance_type`: Phân loại nguồn gốc tạo mẫu (`controlled_extraction`, `controlled_transformation`, `controlled_synthesis`, `cross_lingual_translation`, `multi_source_synthesis`, `cross_family_hard_negative`, `external_open_corpus`).
15. `transformation_details`: Chi tiết kỹ thuật về phép biến đổi hoặc căn cứ ghép cặp.
16. `annotator_id`: Ghi rõ `solo_annotator_author` (tuân thủ nguyên tắc trung thực học thuật).
17. `status`: Trạng thái phê duyệt (`approved`).

---

## 4. Quyền Hạn Dữ Liệu & Đạo Đức Nghiên Cứu (Data Sovereignty & Ethics)

1. **Khử danh tính cá nhân:** Toàn bộ họ tên sinh viên, mã số sinh viên, và thông tin nhạy cảm đã được loại bỏ ở tầng parser `CP-02.1` (bỏ trang bìa).
2. **Phạm vi sử dụng:** Dữ liệu chỉ được dùng cho mục đích nghiên cứu, huấn luyện nội bộ, và đo lường benchmark thuật toán trong khuôn khổ dự án UGTM. Không chia sẻ hay đăng tải công khai toàn văn các đoạn khóa luận lên mạng Internet.
3. **Tính tái lập:** Toàn bộ tập dữ liệu được sinh với seed ngẫu nhiên cố định (`SEED = 42`), đảm bảo có thể tái tạo chính xác từng ký tự từ đầu.

