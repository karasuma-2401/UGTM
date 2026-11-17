# Usability Evaluation Protocol for Evidence Workbench (CP-05.5)

Tài liệu này quy định quy trình, kịch bản tiêu chuẩn và chỉ số đo lường trải nghiệm người dùng khi thao tác trên **Evidence Workbench**.

---

## 1. Tuyên bố Trung thực về Bằng chứng Đánh giá (Evidence Level Statement)

> [!IMPORTANT]
> **Phân loại Mức độ Bằng chứng:**
> - Nghiên cứu này được thực hiện dưới hình thức **Đánh giá Khám phá Nội bộ của Nhà phát triển (Self-Evaluation & Exploratory Developer Usability Study)** theo 5 kịch bản cố định.
> - **Giới hạn nghiên cứu:** Do đang trong giai đoạn phát triển và bảo vệ an toàn dữ liệu học vụ trước khi triển khai chính thức, thử nghiệm chưa bao gồm hội đồng thẩm định độc lập quy mô lớn (30–50 giảng viên). Báo cáo phản ánh trung thực thời gian thao tác và trải nghiệm tương tác thực tế của 1 người thực hiện có hướng dẫn, không tự nâng cấp thành kết quả nghiên cứu diện rộng.

---

## 2. Kịch bản Đánh giá Cố định (Fixed Evaluation Scenarios)

Nghiên cứu đánh giá 5 tác vụ cốt lõi mà một giảng viên hoặc hội đồng thẩm định học thuật phải thực hiện khi xử lý một bài nộp nghi vấn:

### Kịch bản 1 (S1): Khảo sát Tổng quan & Nhận diện Mức độ Tương đồng
- **Mục tiêu:** Mở báo cáo kiểm tra, quan sát các thẻ chỉ số chính (`similarity_coverage`, `max_reranker_score`, số đoạn vi phạm) để đánh giá nhanh mức độ rủi ro.
- **Thao tác:** Đọc tỷ lệ phần trăm độ phủ, xem thanh tiến độ, đối chiếu với ngưỡng quy chế khoa.
- **Tiêu chuẩn thành công:** Định vị và nắm bắt được tỷ lệ độ phủ trong vòng $< 10$ giây.

### Kịch bản 2 (S2): Đối chiếu Song song & Kiểm tra Căn chỉnh Span Highlight
- **Mục tiêu:** Chọn một phát hiện vi phạm (`finding`), kiểm tra đoạn văn bản bài nộp đối chiếu với đoạn tài liệu nguồn tương ứng.
- **Thao tác:** Quan sát các phân đoạn highlight vàng cam (`<mark>`), đọc câu văn để xác định hình thức sao chép (sao chép nguyên văn hay diễn giải paraphrase).
- **Tiêu chuẩn thành công:** Xác định được các cụm từ trùng khớp chính xác mà không cần đối chiếu văn bản thủ công bên ngoài.

### Kịch bản 3 (S3): Tra cứu Vị trí Vật lý (Trang PDF & Nguồn)
- **Mục tiêu:** Xác định vị trí chính xác của đoạn trích trong bản in luận văn để đối chiếu với tài liệu gốc.
- **Thao tác:** Kiểm tra trường `submission_page`, `source_page`, `source_filename`, và `document_family`.
- **Tiêu chuẩn thành công:** Xác định được trang vật lý trong vòng $< 5$ giây.

### Kịch bản 4 (S4): Đánh giá Ngữ cảnh Trích dẫn & Cảnh báo Nguồn Thứ cấp
- **Mục tiêu:** Kiểm tra xem đoạn trùng khớp có được trích dẫn hợp lệ hay không, và có hiện tượng trích dẫn sai lệch không.
- **Thao tác:** Đọc badge `citation_status`, xem giải trình `citation_context.explanation` và kiểm tra banner cảnh báo `potential_misattribution`.
- **Tiêu chuẩn thành công:** Phân biệt được trích dẫn hợp lệ với trích dẫn ngụy tạo/nguồn thứ cấp.

### Kịch bản 5 (S5): Ghi nhận Kết luận Thẩm định & Lưu vết Lịch sử
- **Mục tiêu:** Giảng viên đưa ra kết luận nghiệp vụ (*Chấp nhận*, *Cần giải trình*, *Vi phạm quy chế*, *Miễn trừ*), nhập ghi chú chuyên môn và lưu vào hệ thống.
- **Thao tác:** Nhập tên thẩm định viên, chọn kết luận, viết ghi chú, bấm "Lưu đánh giá", kiểm tra bảng **Review Audit Trail**.
- **Tiêu chuẩn thành công:** Quyết định được lưu bền vững vào `localStorage` kèm mốc thời gian ISO và hiển thị ngay trên bảng lịch sử.

---

## 3. Chỉ số Định lượng Đo lường (Quantitative Metrics)

1. **Thời gian Hoàn thành Tác vụ (Time on Task - ToT):** Đo bằng giây từ lúc bắt đầu đọc kịch bản đến khi hoàn tất thao tác.
2. **Tỷ lệ Hoàn thành Tác vụ (Task Completion Rate - TCR):** Tỷ lệ % tác vụ đạt mục tiêu mà không gặp lỗi cản trở.
3. **Số lần Thao tác Thừa / Click Lạc hướng (Misclicks / Errant Actions):** Số lần click chuột nhầm chỗ.
4. **Độ Trực quan Bằng chứng (Clarity Rating):** Đánh giá thang điểm 1–5 (1: Rất khó hiểu, 5: Rất trực quan, minh bạch).

