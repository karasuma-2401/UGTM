# ĐẶC TẢ YÊU CẦU HỆ THỐNG (SRS)
## Hệ Thống Quản Lý Khóa Luận Tốt Nghiệp

---

## 1. Module 1: Quản trị Hệ thống & Cấu hình Đợt KLTN (System & Period Management)

Quản lý thông tin dùng chung, tài khoản người dùng, phân quyền RBAC và cấu hình mốc thời gian (deadlines) cho từng bước trong quy trình.

### Functional Requirements (FR)
* **FR-SYS-01 (Quản lý đợt KLTN):** Cho phép Admin thiết lập đợt KLTN theo năm học, học kỳ, ngày bắt đầu và ngày kết thúc.
* **FR-SYS-02 (Cấu hình mốc thời gian):** Admin tùy chỉnh thời hạn nộp/duyệt độc lập cho từng giai đoạn (Nộp đề tài, duyệt đề cương, nộp báo cáo giữa kỳ, nộp báo cáo cuối kỳ, nộp bản chỉnh sửa sau hội đồng).
* **FR-SYS-03 (Quản lý danh mục & phân quyền):** Quản lý tài khoản và gán vai trò theo mô hình RBAC: Admin, Khoa (Giáo vụ/Lãnh đạo), Phòng Đào Tạo, Giảng viên (GVHD/GVPB), Sinh viên, Thành viên Hội đồng.
* **FR-SYS-04 (Import dữ liệu ban đầu):** Cho phép Khoa/Admin import danh sách sinh viên đủ điều kiện làm KLTN và danh sách giảng viên từ file Excel.

### Non-functional Requirements (NFR)
* **NFR-SYS-01 (Bảo mật - Authentication & Authorization):** Tích hợp ASP.NET Core Identity kết hợp JWT; phân quyền chặt chẽ đến từng endpoint API dựa trên Role/Policy.
* **NFR-SYS-02 (Toàn vẹn - Audit Logging):** Mọi hành động cập nhật cấu hình đợt và thay đổi phân quyền phải được ghi nhận vào bảng `AuditLogs` kèm timestamp và UserID.

---

## 2. Module 2: Đề xuất & Duyệt Danh mục Đề tài (Topic Proposal & Approval)

Xử lý khởi tạo và kiểm duyệt ngân hàng đề tài trước khi mở đợt đăng ký chính thức cho sinh viên.

### Functional Requirements (FR)
* **FR-TOPIC-01 (Soạn thảo đề tài):** GVHD soạn thảo đề tài KLTN gồm: Tên đề tài (Việt/Anh), mục tiêu, yêu cầu công nghệ, số lượng sinh viên tối đa, gửi lên Khoa duyệt.
* **FR-TOPIC-02 (Kiểm duyệt đề tài):** Khoa xem xét danh sách đề tài do GVHD gửi lên, đánh dấu trạng thái: *Đạt*, *Yêu cầu chỉnh sửa*, hoặc *Từ chối*.
* **FR-TOPIC-03 (Công bố đề tài):** Khoa kích hoạt xuất bản danh sách đề tài hợp lệ để sinh viên tra cứu.
* **FR-TOPIC-04 (Tra cứu đề tài):** Sinh viên tra cứu, tìm kiếm và lọc danh mục đề tài đã duyệt theo tên, lĩnh vực hoặc tên GVHD.

### Non-functional Requirements (NFR)
* **NFR-TOPIC-01 (Hiệu năng tra cứu):** Thời gian phản hồi cho tác vụ tìm kiếm danh mục đề tài không quá 1 giây khi có tải trọng 500 truy vấn đồng thời.

---

## 3. Module 3: Đăng ký & Thẩm định Đề cương (Registration & Outline Review)

Quản lý việc sinh viên chọn đề tài và thẩm định đề cương – đóng vai trò là "cổng kiểm soát" (Gate) quyết định quyền tiếp tục thực hiện KLTN.

### Functional Requirements (FR)
* **FR-REG-01 (Đăng ký đề tài):** Sinh viên gửi đăng ký vào đề tài đã duyệt (sau khi tự liên hệ và được GVHD chấp thuận).
* **FR-REG-02 (Nộp đề cương chi tiết):** Nhóm sinh viên cùng GVHD upload file đề cương chi tiết (PDF/Docx) lên hệ thống trước hạn chót Bước 2.
* **FR-REG-03 (Duyệt đề cương):** Khoa đánh giá đề cương và cập nhật kết quả: *Đạt* hoặc *Không đạt*.
* **FR-REG-04 (Xử lý kết quả duyệt đề cương):**
  * Nếu *Không đạt*: Hệ thống mở lại quyền chỉnh sửa đề cương cho nhóm sinh viên/GVHD để **nộp lại** cho Khoa duyệt lại (lặp lại cho đến khi Đạt hoặc hết hạn Bước 2/3); **không** chuyển sang trạng thái dừng ở bước này.
  * Nếu *Đạt*: Chuyển trạng thái sang *Đang thực hiện KLTN*.

### Non-functional Requirements (NFR)
* **NFR-REG-01 (Độ tin cậy giao dịch):** Sử dụng Database Transaction khi xử lý đăng ký và cập nhật trạng thái đề cương để tránh xung đột dữ liệu (race condition) tại thời điểm cao điểm.
* **NFR-REG-02 (Giới hạn định dạng tệp):** Giới hạn dung lượng file đề cương tối đa 20MB, chỉ chấp nhận định dạng `.pdf`, `.doc`, `.docx`.

---

## 4. Module 4: Quản lý Tiến độ & Thủ tục Giữa kỳ (Mid-term Progress & Topic Modification)

Quản lý tiến độ báo cáo giữa kỳ và luồng nghiệp vụ Đổi tên đề tài (ĐTĐT) liên kết giữa Khoa và Phòng Đào Tạo.

### Functional Requirements (FR)
* **FR-MID-01 (Nộp báo cáo giữa kỳ):** Sinh viên upload file báo cáo KLTN giữa kỳ trước hạn chót quy định.
* **FR-MID-02 (Tạo đơn xin Đổi tên đề tài - ĐTĐT):** Nếu cần điều chỉnh hướng nghiên cứu, sinh viên nộp đơn xin ĐTĐT (nêu rõ tên cũ, tên mới, lý do có xác nhận của GVHD) kèm theo báo cáo giữa kỳ.
* **FR-MID-03 (Xác nhận của GVHD — cổng dừng/tiếp tục chính thức):** GVHD đánh giá tiến độ giữa kỳ và xác nhận *Cho phép tiếp tục* hoặc *Không cho phép tiếp tục*.
  * Nếu *Không cho phép tiếp tục*: Hệ thống chuyển trạng thái sinh viên sang *Dừng thực hiện KLTN*, khóa quyền thực hiện các bước sau (Bước 5 trở đi).
  * Nếu *Cho phép tiếp tục*: Sinh viên nộp báo cáo giữa kỳ (+ đơn ĐTĐT nếu có) cho Khoa (FR-MID-04).
* **FR-MID-04 (Khoa xét duyệt & chuyển tiếp):** Khoa tổng hợp danh sách đơn ĐTĐT và báo cáo giữa kỳ, phê duyệt quyết định ĐTĐT và chuyển hồ sơ cho Phòng Đào Tạo.
* **FR-MID-05 (Cập nhật đề cương sửa đổi):** Đối với các đề tài được duyệt ĐTĐT, hệ thống mở quyền cho sinh viên upload lại bản đề cương mới.

### Non-functional Requirements (NFR)
* **NFR-MID-01 (Quản lý phiên bản tệp - Versioning):** Hệ thống lưu trữ lịch sử các lần nộp và thay đổi tên đề tài (lưu trữ cả tên đề tài cũ và mới kèm mốc thời gian cập nhật).

---

## 5. Module 5: Báo cáo Cuối kỳ, Chấm điểm GVHD & GVPB (Final Report & Review Evaluation)

Xử lý nộp báo cáo hoàn thiện, GVHD chấm điểm trước, Khoa phân công GVPB và thu thập điểm phản biện.

### Functional Requirements (FR)
* **FR-FIN-01 (Nộp báo cáo cuối kỳ đợt 1):** Sinh viên nộp báo cáo toàn văn cho GVHD duyệt và nộp lên Khoa.
* **FR-FIN-02 (GVHD chấm điểm & nhận xét):** GVHD nhập phiếu đánh giá, nhận xét và chấm điểm thành phần của GVHD lên hệ thống.
* **FR-FIN-03 (Phân công GVPB):** Khoa phân công ngẫu nhiên hoặc chỉ định một Giảng viên phản biện (GVPB) cho từng đề tài.
* **FR-FIN-04 (GVPB chấm điểm):** GVPB tiếp nhận báo cáo, nhập phiếu nhận xét phản biện và nhập điểm phản biện, đưa ra kết luận *Đồng ý cho bảo vệ* hoặc *Không đồng ý*.
* **FR-FIN-05 (Chỉnh sửa sau phản biện):** Sinh viên nộp bản báo cáo cập nhật sau khi tiếp thu góp ý của GVPB để chuẩn bị ra Hội đồng.

### Non-functional Requirements (NFR)
* **NFR-FIN-01 (Bảo mật chấm thi - Data Privacy):** Điểm số và nhận xét chi tiết của GVPB chỉ được công khai cho sinh viên và hội đồng tại thời điểm Khoa cho phép (tránh lộ thông tin trước hạn).

---

## 6. Module 6: Tổ chức Hội đồng & Bảo vệ KLTN (Defense Council & Protection)

Quản lý thành lập hội đồng phối hợp cùng Phòng Đào Tạo, xếp lịch và chấm điểm bảo vệ trực tiếp.

### Functional Requirements (FR)
* **FR-COU-01 (Dự thảo danh sách Hội đồng):** Khoa thiết lập danh sách các hội đồng (Chủ tịch, Thư ký, Ủy viên), phòng bảo vệ, ca bảo vệ và gán đề tài vào từng hội đồng.
* **FR-COU-02 (Phê duyệt từ Phòng Đào Tạo):** Phòng Đào Tạo tiếp nhận danh sách từ Khoa, ban hành/upload quyết định chính thức thành lập Hội đồng chấm KLTN.
* **FR-COU-03 (Nhập điểm Hội đồng):** Trong buổi bảo vệ, Thư ký hoặc các thành viên Hội đồng nhập điểm thành phần và ghi nhận biên bản nhận xét bảo vệ vào hệ thống.
* **FR-COU-04 (Nộp bản lưu chiểu cuối cùng):** Sinh viên nộp bản báo cáo đã chỉnh sửa theo kết luận của Hội đồng lên hệ thống (mô phỏng cổng lưu chiểu của trường).
* **FR-COU-05 (Khoa xác nhận nộp hoàn tất):** Khoa kiểm tra tính đầy đủ của hồ sơ và bấm xác nhận hoàn tất nộp KLTN trên hệ thống.

### Non-functional Requirements (NFR)
* **NFR-COU-01 (Tính sẵn sàng cao - High Availability):** Đảm bảo hệ thống hoạt động ổn định $\ge 99.9\%$ trong các ngày diễn ra bảo vệ hội đồng.
* **NFR-COU-02 (Toàn vẹn dữ liệu điểm):** Điểm hội đồng sau khi thư ký bấm "Khóa biên bản" sẽ không thể chỉnh sửa trừ khi có cấp quyền override từ Lãnh đạo Khoa.

---

## 7. Module 7: Tổng hợp Điểm, Nghiệm thu & Tra cứu (Grading Consolidation & Archival)

Tổng hợp toàn bộ quá trình đánh giá, xuất báo cáo và lưu trữ số hóa hồ sơ.

### Functional Requirements (FR)
* **FR-RES-01 (Bảng tổng hợp điểm đa nguồn):** Hiển thị trực quan 3 cột điểm thành phần độc lập: Điểm GVHD, Điểm GVPB, Điểm Hội đồng.
* **FR-RES-02 (Cấu hình & Nhập điểm tổng kết):** Cho phép Khoa nhập điểm tổng kết thủ công hoặc áp dụng công thức trọng số cấu hình động để tính điểm trung bình KLTN.
* **FR-RES-03 (Công bố kết quả):** Khoa chuyển trạng thái sang *Đã công bố*; sinh viên nhận thông báo và tra cứu bảng điểm chi tiết.
* **FR-RES-04 (Xuất báo cáo & Biên bản):** Cho phép xuất toàn bộ danh sách điểm, biên bản bảo vệ và quyết định ra file định dạng Excel/PDF chuẩn mẫu quy định.
* **FR-RES-05 (Kho lưu trữ số KLTN):** Lưu trữ các đề tài đã hoàn thành phục vụ việc tra cứu, tham khảo cho các khóa sinh viên kế tiếp.

### Non-functional Requirements (NFR)
* **NFR-RES-01 (Khả năng mở rộng công thức - Extensibility):** Logic tính điểm thiết kế theo dạng Strategy Pattern để linh hoạt thay đổi tỷ lệ trọng số giữa các năm học mà không cần sửa code.
* **NFR-RES-02 (Tối ưu hóa xuất file):** Tác vụ xuất danh sách điểm toàn khóa ra Excel/PDF sử dụng thư viện xử lý streaming (ClosedXML/EPPlus) nhằm tối ưu bộ nhớ máy chủ.