# Báo Cáo Kiểm Toán Phân Loại Dữ Liệu Gán Nhãn & Nguồn Gốc Nhãn Thô (CP-03.1)

> **Mã mốc:** `CP-03.1`  
> **Thời điểm thực hiện:** 2026-10-30  
> **Bộ dữ liệu kiểm toán:** `training_data/triage/` (600 cặp ứng viên legacy) & Tập mẫu ứng viên ban đầu  
> **Báo cáo dữ liệu chi tiết:** [`data/initial_annotation_sample_audit.json`](../data/initial_annotation_sample_audit.json)  
> **Trạng thái:** Hoàn tất thành công (Exit 0)

---

## 1. Bối Cảnh & Động Lực Kiểm Toán

Trước khi triển khai mốc `CP-02`, hệ thống đã sinh ra một tập ứng viên gán nhãn gồm 600 cặp câu (`training_data/annotation_candidates.csv`). Tuy nhiên, do lúc đó parser chưa có cơ chế bóc tách cấu trúc block (`CP-02.1`), chưa phân loại section (`CP-02.2`), và chưa phát hiện trùng lặp tài liệu (`CP-02.3`), việc gán nhãn trực tiếp trên tập dữ liệu này sẽ dẫn đến các hệ lụy học thuật nghiêm trọng:
1. **Lãng phí công sức con người vào nhiễu cấu trúc:** Hầu hết các cặp có điểm cosine cao nhất lại là trang bìa ("ĐẠI HỌC QUỐC GIA TP.HCM..."), mục lục, hoặc danh mục tài liệu tham khảo.
2. **Nguy cơ nhồi nhãn giả tạo (Label Fabrication):** Do workflow cũ yêu cầu cứng cả hai trường `reviewer_1_label` và `reviewer_2_label` phải đồng thuận với `final_label`, các dự án solo annotator buộc phải sao chép nhãn của mình sang cột reviewer thứ hai, tạo ra ấn tượng sai lệch rằng hệ thống có 2 người gán nhãn độc lập.
3. **Mất nguồn gốc nhãn khi trọng tài (Broken Provenance):** Khi có bất đồng, workflow cũ ép sửa đè nhãn của reviewer cho khớp với nhãn sau trọng tài, làm mất đi bằng chứng về sự bất đồng ban đầu.

---

## 2. Kết Quả Kiểm Toán Tập Dữ Liệu Triage Cũ (600 Cặp)

Phân tích toàn diện 600 cặp ứng viên ban đầu cho thấy:

| Danh mục phân loại | Số cặp | Tỷ lệ | Ý nghĩa & Hướng xử lý |
| :--- | :---: | :---: | :--- |
| **Mẫu bìa, trường, khoa, bộ môn** | **467** | 77.83% | Nhiễu biểu mẫu văn bản hành chính; loại bỏ hoàn toàn |
| **Mục lục, từ viết tắt, tài liệu tham khảo** | **62** | 10.33% | Nhiễu cấu trúc; loại bỏ hoàn toàn |
| **Tổng lượng nhiễu biểu mẫu bị loại** | **529** | **88.17%** | **88.17% ứng viên sinh ra từ code cũ là rác biểu mẫu!** |
| **Nội dung cấu trúc / bảng biểu** | **24** | 4.00% | Bảng đặc tả hoặc dữ liệu kỹ thuật lặp lại |
| **Cặp thuộc khóa luận trùng lặp** | **4** | 0.67% | Nằm trong 3 cặp khóa luận trùng đã xử lý tại CP-02.3 |
| **Nội dung thực chất có thể xem xét** | **43** | **7.17%** | Các đoạn văn thân bài thực sự cần đánh giá |
| **Tổng cộng** | **600** | **100.0%** | |

### Phân Bố Độ Tương Đồng Từ Vựng (`lexical_cosine`):
- `[0.00, 0.20)`: 23 cặp
- `[0.20, 0.30)`: 16 cặp
- `[0.30, 0.50)`: 4 cặp
- `[0.50, 1.00]`: 0 cặp (không có cặp nội dung thân bài nào đạt mức trùng lặp cao ngẫu nhiên)

---

## 3. Kiểm Tra Mẫu Nhãn Đầu Tiên & Quy Chuẩn Provenance

Kiểm tra chi tiết tập mẫu ưu tiên gồm 30 cặp nội dung thân bài trải trên 22 nhóm tài liệu độc lập:
1. **Kiểm tra nhãn giả tạo (Zero Fake Labels):**
   - Xác nhận 100% các dòng trong tập triage cũ đều giữ trạng thái `status="pending"`, không có ai tự ý điền nhãn giả `1` hay `0` hay giả mạo chữ ký của reviewer thứ hai.
2. **Phân tích ngữ nghĩa sơ bộ trên 30 cặp mẫu:**
   - **4 cặp (13.3%):** Có dấu hiệu tương đồng ngữ cảnh hoặc diễn đạt lại (paraphrase) về kiến trúc hệ thống và quy trình kỹ thuật.
   - **26 cặp (86.7%):** Là các đoạn văn độc lập hoàn toàn, chỉ trùng lặp một vài từ vựng chuyên ngành chung của ngành phần mềm (như *"cơ sở dữ liệu"*, *"giao diện người dùng"*, *"kiến trúc client-server"*).
3. **Tác động của bộ lọc mới (`CP-02.1`, `CP-02.2`, `CP-02.3`):**
   - Khi chạy bộ sinh ứng viên mới với cấu hình `production_v1`, **100% các cặp bìa (467) và references (62) đều bị loại bỏ ngay từ tầng parser**, không bao giờ bị đưa vào danh sách candidate làm phiền người gán nhãn.
   - Toàn bộ thời gian của annotator sẽ được tập trung vào các đoạn thân bài thực chất.

---

## 4. Quy Trình Gán Nhãn Đã Cải Tiến (`scripts/finalize_annotation_pairs.py`)

Workflow chốt dữ liệu gán nhãn đã được nâng cấp với 3 nguyên tắc bảo vệ tính liêm chính khoa học:

1. **Hỗ trợ chế độ Solo Annotator minh bạch:**
   - Nếu dự án chỉ có một người gán nhãn, hệ thống ghi nhận `provenance_mode="solo_annotator"` và `annotator_id="solo_annotator_author"`. Không yêu cầu điền reviewer giả.
2. **Hỗ trợ chế độ Trọng tài Bảo toàn Nhãn thô (Never Overwrite Raw Labels):**
   - Khi hai reviewer có ý kiến khác biệt (`reviewer_1_label="1"`, `reviewer_2_label="0"`):
   - Trọng tài gán `adjudicated_label="1"` và bắt buộc nhập `adjudication_reason`.
   - Cả hai nhãn thô ban đầu `1` và `0` đều được giữ nguyên vẹn trong tệp đầu ra để phục vụ đo lường độ đồng thuận liên đánh giá (Inter-Annotator Agreement - Cohen's Kappa).
3. **Bổ sung các trường phong phú:**
   - `reuse_type` (`verbatim`, `paraphrase`, `translated`, `summary`, `independent`).
   - `citation_status` (`cited`, `uncited`, `incorrect_citation`, `not_applicable`).
   - `adjudication_reason` cho mọi ca phân xử.

