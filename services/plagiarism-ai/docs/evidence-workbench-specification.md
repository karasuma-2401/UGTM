# Evidence Workbench Specification (CP-05.3)

Tài liệu này đặc tả kiến trúc, nguyên lý thiết kế và hướng dẫn vận hành của **Evidence Workbench** trong hệ thống **UGTM Plagiarism AI**.

---

## 1. Mục đích & Phạm vi

Evidence Workbench là giao diện thẩm định bằng chứng dành cho giảng viên, hội đồng học thuật và nhà phát triển AI nhằm:
- Phân tích trực quan các kết quả đối soát học thuật dựa trên **Report Schema v3**.
- Hiển thị song song văn bản bài nộp (*Submission*) và văn bản tài liệu nguồn (*Source Material*) với mức độ chi tiết đến từng từ/ký tự thông qua `aligned_spans`.
- Phân loại rõ ràng ngữ cảnh trích dẫn (`citation_context`) và phát hiện hiện tượng trích dẫn sai lệch / nguồn thứ cấp (`potential_misattribution`).
- Tuân thủ nghiêm ngặt chuẩn mực liêm chính học thuật: kết quả AI chỉ mang tính đối soát tham chiếu theo kho tài liệu hiện hữu, không phải chứng nhận bài nguyên bản tuyệt đối.
- Cung cấp ngăn thẩm định chuyên gia (**Reviewer Action Box**) và lưu trữ lịch sử đánh giá (**Review Audit Trail**) phục vụ quy trình ra quyết định kỷ luật/chấm điểm.

---

## 2. Chuẩn hóa Báo cáo Report Schema v3

### 2.1. Phân biệt `similarity_coverage` và `max_reranker_score`

Khắc phục triệt để nhầm lẫn giữa tỷ lệ độ phủ và điểm tương quan ngữ nghĩa:
- **`similarity_coverage` (Độ phủ tương đồng)**:
  $$\text{Coverage} = \frac{\text{matched\_characters}}{\text{scanned\_characters}} \times 100\%$$
  Đại diện cho tỷ lệ chiều dài ký tự trong bài nộp được xác định trùng lặp không chồng lấn với các nguồn tài liệu đối soát.
- **`max_reranker_score` (Điểm tương đồng ngữ nghĩa cực đại)**:
  Điểm số từ mô hình Cross-Encoder (ví dụ: BGE Reranker) phản ánh mức độ trùng khớp ngữ nghĩa sâu của cặp đoạn nghi vấn cao nhất (thang điểm 0.0 - 1.0).

### 2.2. Khắc phục lỗi đọc dữ liệu từ Schema v2 sang Schema v3

Trên Frontend Workbench (`frontend/src/components/ReportTab.tsx`), cơ chế đọc dữ liệu an toàn hai tầng được áp dụng:
```typescript
const summary = report.summary || {};
const coverage = summary.similarity_coverage ?? report.similarity_coverage ?? 0;
const maxScore = summary.max_reranker_score ?? report.max_rerank_score ?? report.max_score ?? 0;
const scannedChars = summary.scanned_characters ?? report.scanned_characters ?? 0;
const matchedChars = summary.matched_characters ?? report.matched_characters ?? 0;
const citedCount = summary.cited_findings ?? report.cited_findings ?? 0;
const uncitedCount = summary.uncited_findings ?? report.uncited_findings ?? 0;
```
Nhờ đó, giao diện hiển thị chính xác mọi số liệu thống kê, không bị tình trạng hiển thị `0` do không khớp key.

---

## 3. Kiến trúc Giao diện Thẩm định

### 3.1. Các Thẻ Chỉ số Cốt lõi (Metrics Cards)
1. **Độ phủ tương đồng (`similarity_coverage`)**: Kèm thanh đo tỷ lệ phần trăm trực quan và số ký tự trùng/tổng ký tự.
2. **Điểm Reranker cực đại (`max_reranker_score`)**: Kèm phân loại mức độ rủi ro (Nghiêm trọng $\ge 0.85$, Đáng ngờ $\ge 0.70$, Thấp $< 0.70$).
3. **Nhóm đoạn & Cặp đối chiếu**: Thể hiện tổng số `grouped_findings` và `raw_findings`.
4. **Minh bạch trích dẫn**: Thống kê số phát hiện có trích dẫn hợp lệ so với phát hiện không có trích dẫn/nguồn không rõ.

### 3.2. Đối chiếu Song song Side-by-Side với Highlight `aligned_spans`
- **Highlight chính xác từng đoạn**:
  Hàm `renderHighlightedText(text, spans)` chia chuỗi văn bản thành các phân đoạn `<span>` bình thường và `<mark>` có màu nền vàng nhạt/cam để làm nổi bật chính xác các vị trí trùng khớp.
- **Thông tin định vị vị trí vật lý**:
  - Bài nộp: Trang PDF vật lý (`submission_page`), Số thứ tự đoạn (`chunk_no`), Tọa độ ký tự (`char_start` - `char_end`).
  - Nguồn đối chiếu: Tên file nguồn (`source_filename`), Họ tài liệu (`document_family`), Trang PDF gốc (`source_page`), Số thứ tự đoạn nguồn (`source_chunk_no`).

### 3.3. Đánh giá Trích dẫn & Cảnh báo Dẫn nguồn Thứ cấp
- **Badge Trạng thái Trích dẫn**:
  - `present` / `cited`: Màu xanh lá cây — Có tín hiệu trích dẫn học thuật hợp lệ.
  - `absent` / `uncited`: Màu đỏ — Không phát hiện dấu vết trích dẫn trong phạm vi lân cận.
  - `uncertain`: Màu vàng cam — Tín hiệu trích dẫn chưa rõ ràng hoặc định dạng không chuẩn.
- **Giải trình trích dẫn (`explanation`)**: Hiển thị chuỗi phân tích ngữ cảnh trích dẫn từ pipeline phát hiện.
- **Banner Cảnh báo Nguồn thứ cấp (`potential_misattribution`)**:
  Khi tác giả trích dẫn một tác giả A nhưng văn bản lại sao chép nguyên văn từ tác giả B, hệ thống hiển thị banner màu vàng cảnh báo nguy cơ trích dẫn sai lệch để thẩm định viên đặc biệt lưu ý.

### 3.4. Tuyên bố Liêm chính Học thuật khi Không có Vi phạm (No-Match Disclaimer)
Khi `similarity_coverage == 0` hoặc không có phát hiện vi phạm:
- Hệ thống hiển thị biểu tượng xác nhận màu xanh dịu cùng thông điệp:
  > **"Không phát hiện dấu hiệu trùng lặp trong phạm vi đối soát"**
- Đồng thời bắt buộc kèm theo **Tuyên bố Giới hạn Phạm vi**:
  > *"Lưu ý: Kết quả này xác nhận bài nộp không trùng khớp với các tài liệu hiện có trong kho dữ liệu tham chiếu tại thời điểm quét. Đây không phải là chứng nhận bài nguyên bản tuyệt đối đối với các nguồn tài liệu ngoài hệ thống."*
Điều này ngăn chặn triệt để hành vi cấp chứng chỉ giả mạo tính nguyên bản tuyệt đối.

### 3.5. Ngăn Thẩm định Độc lập & Nhật ký Thẩm định (Reviewer Box & Audit Trail)
- Giảng viên/chuyên gia thẩm định có thể chọn một trong các kết luận:
  - `Chấp nhận (Hợp lệ)`: Trích dẫn đúng quy định học thuật.
  - `Cần giải trình`: Tỷ lệ trùng khớp ở mức nghi vấn, yêu cầu sinh viên làm rõ.
  - `Vi phạm quy chế`: Sao chép không trích dẫn hoặc vi phạm nghiêm trọng liêm chính.
  - `Miễn trừ`: Đoạn trùng nằm trong danh mục công thức, thuật ngữ cố định hoặc phụ lục cho phép.
- Ghi chú thẩm định (`reviewerNotes`) và tên người thẩm định (`reviewerName`).
- Tất cả quyết định được lưu vết vào `localStorage` theo từng Job ID và hiển thị trong bảng **Nhật ký Thẩm định (Review Audit Trail)** với mốc thời gian ISO rõ ràng.

### 3.6. Trạng thái Job Toàn diện
Bổ sung hỗ trợ đầy đủ các trạng thái hệ thống:
- `waiting_for_compute`: Trạng thái chờ GPU kết nối lại hoặc đang backoff retry.
- `cancelled`: Trạng thái job đã bị người dùng huỷ bỏ.

---

## 4. Kiểm thử Tích hợp & Fixtures

Hai bộ fixture chuẩn Report Schema v3 được lưu trữ tại `tests/fixtures/`:
1. `sample_report_v3_violations.json`: Trường hợp có vi phạm trùng lặp, đa dạng trạng thái trích dẫn, có cảnh báo nguồn thứ cấp và danh sách `aligned_spans`.
2. `sample_report_v3_nomatch.json`: Trường hợp không có vi phạm (`coverage = 0`), dùng để kiểm chứng hiển thị tuyên bố liêm chính học thuật.

Bộ test kiểm thử giao diện và API: `tests/test_evidence_workbench.py` (6/6 passed).
Toàn bộ frontend đã được build thành công thành static assets trong thư mục `frontend/dist/` và được FastAPI phục vụ tại root.

