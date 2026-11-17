# UGTM-Plagiarism-AI — Báo cáo xử lý `annotation_candidates.csv`

**Ngày xử lý:** 24/10/2026  
**Dữ liệu đầu vào:** 600 candidate pairs, 17 cột, 64 `group_id`.  
**Phương pháp:** Bộ quy tắc kiểm tra bìa/tiêu đề, mục lục, danh mục từ viết tắt, tài liệu tham khảo, định dạng bảng, và dấu hiệu một khóa luận bị nhập nhiều lần. **Không gọi AI bên ngoài và không gửi tài liệu lên cloud.**

## Kết quả

| Nhóm | Số cặp | Ý nghĩa | Hướng xử lý |
|---|---:|---|---|
| Bìa, tiêu đề, thông tin trường/khoa | 467 | Phần lớn giống nhau do mẫu trình bày | Gợi ý bỏ khỏi tập ứng viên huấn luyện, giữ để audit |
| Mục lục, danh mục, tài liệu tham khảo | 62 | Không phù hợp làm tín hiệu đạo văn trong bản MVP | Gợi ý bỏ, kiểm tra mẫu trước khi áp dụng toàn corpus |
| Dạng bảng/đặc tả cần cân nhắc | 24 | Có thể là dữ liệu kỹ thuật, nhưng cũng nhiều mẫu lặp lại | Để sau, không gán `1/0` tự động |
| Cặp thuộc nguồn nghi bị nhập trùng | 4 | Nằm trong 3 cặp source IDs nghi cùng khóa luận | Kiểm tra hash/tên tác giả/nhan đề; không dùng như hai nguồn độc lập |
| Nội dung có thể tiếp tục xem xét | **43** | Có thể dùng để chọn mẫu đánh giá; **chưa có nhãn thật** | Ưu tiên từ `03_substantive_review.csv` |
| **Tổng** | **600** | | |

**529/600** cặp được *gợi ý* loại khỏi tập ứng viên huấn luyện (467 + 62). Đây là **lọc chất lượng dữ liệu**, **không phải xác định 529 cặp có nhãn `0`**. 28 cặp khác được hoãn để đánh giá nguồn hoặc ngữ cảnh. 43 cặp còn lại cần bằng chứng về nguồn gốc nếu muốn gán nhãn nội dung tái sử dụng.

Trong 43 cặp nội dung, chỉ **4** có `lexical_cosine >= 0.30`, 16 cặp nằm trong `[0.20, 0.30)` và 23 cặp dưới `0.20`. Điều này cho thấy batch ứng viên hiện tại **không phải bộ dữ liệu cân đối positive/negative để train classifier**. Phần lớn kết quả xuất phát từ so khớp bìa hoặc nội dung cấu trúc.

## Dấu hiệu nhập trùng tài liệu

Các hàng `TASK-00001`, `TASK-00002`, `TASK-00004` có điểm cosine ở trang bìa rất cao, đồng thời nội dung đầu trang (bao gồm nhan đề/tác giả) gần như giống nhau. Bộ lọc phát hiện **3 cặp ID có khả năng là hai bản của cùng một khóa luận**. Hãy đối chiếu SHA-256 file, tiêu đề, tác giả và phiên bản; chưa thể khẳng định hai ID trùng tài liệu chỉ từ đoạn bìa.

Trong đó `TASK-00003`, `TASK-00009`, `TASK-00509`, `TASK-00588` thuộc các cặp ID này và đã được chuyển sang nhóm cần kiểm tra nhập trùng nguồn thay vì ưu tiên dùng làm ví dụ tái sử dụng.

## Bộ tệp bàn giao

1. `annotation_candidates_triaged.csv`: **đủ 600 cặp**, bảo toàn toàn bộ 17 cột nguồn, bổ sung 6 cột `triage_*`, không thay đổi `reviewer_1_label`, `reviewer_2_label`, `final_label`, `status`.
2. `01_cover_and_format_noise.csv`: 529 cặp gợi ý loại (không xóa khỏi tập gốc).
3. `02_structured_content_defer.csv`: 28 cặp cần xem xét sau.
4. `03_substantive_review.csv`: 43 cặp nội dung được giữ lại để đối chiếu.
5. `04_priority_review_shortlist.csv`: 30 cặp ưu tiên, trải trên 22 nhóm nguồn, dùng khi chỉ có ít thời gian.
6. `05_possible_duplicate_documents.csv`: 3 hàng đầu mối nghi nhập trùng nguồn (các hàng khác của cùng cặp nguồn được gắn flag trong tệp triage tổng).
7. `triage_summary.json`: thống kê, SHA-256 tệp đầu vào.
8. `UGTM_triage_annotations.py`: script Python standard library để tái chạy ở local.

## Việc nhóm cần Codex làm tiếp (không cần tự gán nhãn 600 cặp)

**Bước A — Sửa nguồn sinh candidate:**

- Trong `scripts.create_annotation_candidates`, bỏ qua các chunk trang bìa, mục lục, tài liệu tham khảo, danh sách từ viết tắt trước khi TF-IDF chọn top candidates; nên dùng metadata `section_type` ngay từ lúc trích xuất.
- Lọc tài liệu trùng trước khi indexing: SHA-256 toàn file + hash văn bản sau normalize + so sánh tên đề tài/tác giả (trường hợp PDF khác định dạng có thể cùng nội dung).
- Không chỉ tìm theo `lexical_cosine`: kết hợp hard negative cùng chủ đề và positive được tạo có kiểm soát từ nội dung nhóm sở hữu/được phép dùng. Không suy ra nhãn của hai khóa luận thật từ cosine.
- Giữ các biến thể của cùng source trong cùng `group_id`, và chia train/test theo group hoặc document để tránh leakage.

**Bước B — Tiếp tục phát triển mô hình trong hạn 5–6 tuần:**

- Dùng bộ synthetic 96 cặp trước đó để *smoke-test* training/evaluation/export, **không dùng kết quả test synthetic làm thước đo thực tế**.
- Chưa có nhãn chất lượng đủ tốt thì duy trì `CLASSIFIER_ENABLED=false`. Pipeline BGE-M3 + Hybrid Search + Reranking + Passage Alignment vẫn chạy bình thường.
- Nếu thu thập được positive từ nguồn kiểm soát và các negative có bằng chứng, đánh giá độc lập theo nguồn trước khi bật classifier.
- Không đăng dữ liệu khóa luận lên repo public hoặc cloud nếu quyền sử dụng chưa được cấp rõ ràng.

## Prompt cho Codex (copy sử dụng trực tiếp)

> Hãy dùng các file triage đi kèm để cải thiện pipeline dữ liệu của repository UGTM-Plagiarism-AI. Đọc và so sánh `scripts.create_annotation_candidates`, parser/chunking hiện tại, `training.train_classifier` và `scripts.finalize_annotation_pairs`. Bổ sung lọc bìa/mục lục/danh mục/tài liệu tham khảo **trước** bước lấy lexical candidates; kiểm tra dedup theo SHA-256, nhan đề và tác giả. Không xóa file gốc, không tự điền reviewer labels, final_label hoặc `approved`. Duy trì group-safe split, tạo test dữ liệu bìa/trùng văn bản và test dòng trả về giữ nguyên schema. Cho phép chạy smoke-test bằng bộ synthetic có nguồn gốc xác định, nhưng giữ classifier disabled khi chưa có đánh giá độc lập. Viết báo cáo chính xác số candidate bị lọc và lỗi phát hiện trong corpus hiện tại.

## Giới hạn của kết quả

- Không truy cập được PDF gốc từ tệp CSV; phân loại được thực hiện bằng dấu hiệu văn bản và metadata trích xuất, có thể có false positive/false negative.
- `triage_category` là **nhãn về chất lượng dữ liệu**, không phải nhãn đạo văn.
- Không có cơ sở điền nhãn `1`, `0`, hai reviewer hay `approved` cho các cặp thật. Để có model đủ tin cậy, cần nguồn positive có kiểm soát và negative độc lập đã kiểm chứng.
- Mọi file xuất đều có thể chứa các đoạn khóa luận do người dùng cung cấp: chỉ lưu nội bộ và sử dụng theo quyền được cấp.
