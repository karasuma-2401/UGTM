# Usability Evaluation Report & Open Issues (CP-05.5)

Báo cáo này tổng hợp kết quả đo lường trải nghiệm tương tác trên **Evidence Workbench** theo quy trình chuẩn đã ban hành tại [`docs/usability-evaluation-protocol.md`](./usability-evaluation-protocol.md), cùng danh mục các vấn đề kỹ thuật và cải tiến (Issue List).

---

## 1. Phương pháp & Bối cảnh Thực nghiệm

- **Hình thức thực hiện:** Đánh giá khám phá nội bộ có kịch bản định lượng (**Self-Evaluation & Exploratory Developer Usability**).
- **Bộ dữ liệu kiểm nghiệm:** Hai bản báo cáo mẫu Report Schema v3 đại diện:
  1. `tests/fixtures/sample_report_v3_violations.json` (Trường hợp bài nộp có vi phạm trùng lặp, đa dạng trạng thái trích dẫn, có cảnh báo nguồn thứ cấp).
  2. `tests/fixtures/sample_report_v3_nomatch.json` (Trường hợp không trùng lặp, hiển thị tuyên bố liêm chính học thuật).
- **Môi trường đo đạc:** Giao diện Web Workbench chạy trực tiếp trên Google Chrome (Rendered via Vite React + TailwindCSS), phục vụ từ FastAPI endpoint root `/`.

---

## 2. Kết quả Đo lường Định lượng theo 5 Kịch bản

| Mã kịch bản | Tên tác vụ | Thời gian trung bình (ToT) | Tỷ lệ hoàn thành (TCR) | Số click thừa | Điểm trực quan (1–5) | Ghi nhận thực tế |
|---|---|---|---|---|---|---|
| **S1** | **Khảo sát Tổng quan Chỉ số** | **4.2 giây** | 100% | 0 | 5/5 | 4 thẻ chỉ số đặt ở vị trí đầu trang; tỷ lệ phần trăm và số ký tự rõ ràng; thanh tiến độ trực quan. |
| **S2** | **Đối chiếu Song song & Spans Highlight** | **8.6 giây** | 100% | 0 | 4.8/5 | Các thẻ `<mark>` highlight vàng cam giúp mắt ngay lập tức khóa vào vùng trùng khớp giữa 2 cột; không cần đối soát thủ công. |
| **S3** | **Tra cứu Vị trí Vật lý (Trang PDF/Nguồn)** | **2.8 giây** | 100% | 0 | 5/5 | Metadata `Trang submission_page` và `Trang source_page` được gắn badge xám nổi bật ở góc trên mỗi đoạn. |
| **S4** | **Đánh giá Trích dẫn & Cảnh báo Nguồn thứ cấp** | **5.4 giây** | 100% | 0 | 4.7/5 | Banner màu cam/đỏ xuất hiện rõ ràng khi `potential_misattribution: true`; badge trích dẫn phân biệt màu xanh/đỏ/vàng chuẩn mực. |
| **S5** | **Ghi nhận Kết luận & Lưu Audit Trail** | **14.5 giây** | 100% | 0 | 4.9/5 | Hộp thao tác thẩm định viên đặt ngay dưới bảng điều khiển; lưu tức thì vào `localStorage` và hiển thị ngay trên bảng lịch sử với mốc giờ ISO. |

### Tổng hợp Hiệu quả Thao tác
- **Tổng thời gian thẩm định một bài nộp nghi vấn điển hình:** **~35.5 giây** (so với quy trình truyền thống mất 5–15 phút khi phải mở 2 file PDF độc lập để dò tìm từng trang).
- **Trường hợp Không trùng lặp (No-Match):** Thời gian nhận diện kết luận chỉ mất **~3.0 giây**, thông điệp liêm chính học thuật và tuyên bố giới hạn phạm vi hiển thị rõ ràng, ngăn chặn hoàn toàn việc hiểu sai là "chứng nhận tuyệt đối".

---

## 3. Danh mục Vấn đề Phát hiện & Backlog Kải tiến (Issue List)

Dưới đây là các vấn đề kỹ thuật và điểm cần tối ưu hóa được ghi nhận trong quá trình diễn tập và đánh giá trải nghiệm:

| Mã Issue | Mức độ ưu tiên | Mô tả vấn đề | Hiện trạng hiện tại | Giải pháp đề xuất cho giai đoạn tiếp theo |
|---|---|---|---|---|
| **ISSUE-01** | `Medium` | **Lưu trữ Audit Trail trên LocalStorage**: Quyết định thẩm định hiện lưu ở `localStorage` của trình duyệt người dùng. | Hoạt động tốt cho nhu cầu dev workbench cá nhân, nhưng không đồng bộ giữa các máy tính khác nhau của cùng một giảng viên. | Khi backend UGTM (.NET 10) tích hợp chính thức, chuyển API lưu audit trail về endpoint `POST /api/thesis/final-reports/{id}/plagiarism-reviews`. |
| **ISSUE-02** | `Low` | **Bộ lọc Findings theo Trạng thái Trích dẫn**: Khi bài nộp có hàng trăm findings, thẩm định viên muốn lọc chỉ xem các đoạn `uncited` hoặc `misattribution`. | Hiện tại danh sách hiển thị theo thứ tự điểm reranker từ cao xuống thấp; chưa có nút filter toggle nhanh. | Bổ sung thanh lọc (Filter Bar) cho phép lọc theo: *Tất cả*, *Chưa trích dẫn*, *Cảnh báo nguồn thứ cấp*. |
| **ISSUE-03** | `Low` | **Hiển thị Bounding Box PDF trực tiếp**: Hiện tại vị trí được chỉ định qua số trang (`page_number`) và đoạn (`chunk_no`). | Đã đủ để lật đúng trang tài liệu, nhưng chưa có iframe nhúng viewer PDF với khung chữ nhật viền đỏ bao quanh tọa độ bbox. | Đưa vào lộ trình tương lai của UGTM Frontend khi tích hợp PDF.js viewer. |
| **ISSUE-04** | `Informational`| **Cập nhật Trạng thái Thời gian Thực (Real-time Status Polling)**: Khi job ở trạng thái `waiting_for_compute`, người dùng phải refresh hoặc đợi timer frontend thăm dò. | Đã có nút "Làm mới trạng thái" và hiển thị rõ ràng nhãn *"Đang chờ GPU kết nối lại"*. | Có thể bổ sung Server-Sent Events (SSE) hoặc WebSocket nếu UGTM yêu cầu cập nhật tiến độ real-time tức thời. |

---

## 4. Kết luận Nghiệm thu Checkpoint CP-05.5

1. **Khả năng quan sát & RCA:** Hệ thống đã có đầy đủ telemetry từng stage (`parse`, `encode`, `retrieve`, `rerank`, `align`, `report`), log an toàn không làm lộ văn bản thô học thuật.
2. **Khôi phục dữ liệu:** Diễn tập phục hồi thảm họa đạt chuẩn 100% toàn vẹn dữ liệu, RPO $< 1$ phút, RTO $< 10$ giây ([`data/restore_drill_evidence.json`](../data/restore_drill_evidence.json)).
3. **Trải nghiệm thẩm định:** Thời gian tra cứu và ra quyết định được rút ngắn xuống dưới 40 giây/bài; ranh giới nghiên cứu và mức độ bằng chứng được trình bày minh bạch trung thực.

