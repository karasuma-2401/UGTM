# TÀI LIỆU BIỂU ĐỒ USE CASE HỆ THỐNG UGTM
## Môn học: Công nghệ .NET (SE310.R11) — Nhóm 1

> Tài liệu này tổng hợp toàn bộ các biểu đồ Use Case của hệ thống **UGTM (Undergraduate Graduation Thesis Management)**, bám sát 100% nội dung phân tích nghiệp vụ tại **Chương 2 (Mục 2.3: Danh mục Use Case toàn hệ thống - Bảng 2.1 đến 2.7)** trong Báo cáo phân tích và thiết kế.

---

## 📂 Danh mục File Mã nguồn PlantUML (`docs/PlantUML/UseCases/`)

| STT | Phân hệ / Tác nhân | File PlantUML | Tham chiếu Báo cáo | Số lượng UC |
|:---:|:---|:---|:---:|:---:|
| **0** | **Tổng quan Toàn Hệ thống** | [`UC_Overview_System.puml`](PlantUML/UseCases/UC_Overview_System.puml) | Mục 2.3 & 1.3 | 7 Tác nhân |
| **1** | **Phân hệ Sinh viên** | [`UC_Student.puml`](PlantUML/UseCases/UC_Student.puml) | Bảng 2.1 | 11 Use Cases |
| **2** | **Phân hệ Giảng viên hướng dẫn (GVHD)** | [`UC_Supervisor.puml`](PlantUML/UseCases/UC_Supervisor.puml) | Bảng 2.2 | 5 Use Cases |
| **3** | **Phân hệ Giảng viên phản biện (GVPB)** | [`UC_Reviewer.puml`](PlantUML/UseCases/UC_Reviewer.puml) | Bảng 2.3 | 3 Use Cases |
| **4** | **Phân hệ Hội đồng chấm KLTN** | [`UC_Council.puml`](PlantUML/UseCases/UC_Council.puml) | Bảng 2.4 | 3 Use Cases |
| **5** | **Phân hệ Văn phòng Khoa** | [`UC_Faculty.puml`](PlantUML/UseCases/UC_Faculty.puml) | Bảng 2.5 | 11 Use Cases |
| **6** | **Phân hệ Phòng Đào Tạo (PĐT)** | [`UC_AcademicAffairs.puml`](PlantUML/UseCases/UC_AcademicAffairs.puml) | Bảng 2.6 | 3 Use Cases |
| **7** | **Phân hệ Quản trị viên hệ thống (Admin)** | [`UC_Admin.puml`](PlantUML/UseCases/UC_Admin.puml) | Bảng 2.7 | 4 Use Cases |

---

## 1. Biểu đồ Use Case Tổng quan Toàn Hệ thống

- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_Overview_System.puml`](PlantUML/UseCases/UC_Overview_System.puml)
- **Mô tả:** Mô hình hóa ranh giới hệ thống UGTM với 7 nhóm tác nhân người dùng, bao quát toàn bộ 6 khối nghiệp vụ qua tiến trình 21 tuần và 2 cổng kiểm soát (Gatekeeper Tuần 2 và Tuần 10).

---

## 2. Chi tiết Use Case các Phân hệ Nghiệp vụ

### 2.1. Phân hệ Sinh viên (Bảng 2.1)
- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_Student.puml`](PlantUML/UseCases/UC_Student.puml)
- **Danh mục Use Case:**
  - `UC-STU01`: Tra cứu & Tìm kiếm đề tài
  - `UC-STU02`: Đăng ký đề tài & Ghép nhóm
  - `UC-STU03`: Nộp & Chỉnh sửa Đề cương chi tiết (Cổng kiểm soát 1 - Tuần 2)
  - `UC-STU04`: Theo dõi tiến độ cả quá trình (Dashboard 7 giai đoạn)
  - `UC-STU05`: Nộp Báo cáo giữa kỳ (Cổng kiểm soát 2 - Tuần 10)
  - `UC-STU06`: Tạo Đơn xin Đổi tên đề tài (ĐTĐT - Tuần 3 đến 10)
  - `UC-STU07`: Cập nhật Đề cương mới (sau khi duyệt ĐTĐT)
  - `UC-STU08`: Nộp Báo cáo cuối kỳ (Tuần 11 đến 18)
  - `UC-STU09`: Tra cứu Lịch bảo vệ Hội đồng
  - `UC-STU10`: Nộp Báo cáo Lưu chiểu
  - `UC-STU11`: Tra cứu Điểm số KLTN
- **Quan hệ kỹ thuật:**
  - `<<include>>`: *Đăng nhập hệ thống*, *Kiểm tra ràng buộc tệp đính kèm (PDF/Docx <= 20MB)*.
  - `<<extend>>`: *Nộp lại phiên bản Đề cương mới (khi Khoa yêu cầu sửa)*, *Nộp bản Báo cáo sửa đổi (theo nhận xét GVPB)*.

### 2.2. Phân hệ Giảng viên hướng dẫn (Bảng 2.2)
- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_Supervisor.puml`](PlantUML/UseCases/UC_Supervisor.puml)
- **Danh mục Use Case:**
  - `UC-ADV01`: Soạn thảo & Đề xuất đề tài (Tên Việt/Anh, số SV tối đa, mục tiêu, yêu cầu)
  - `UC-ADV02`: Xác nhận nhóm sinh viên đăng ký
  - `UC-ADV03`: Đánh giá Tiến độ giữa kỳ (Cổng kiểm soát 2: Dừng / Tiếp tục KLTN)
  - `UC-ADV04`: Xác nhận Đơn xin ĐTĐT (Bước 1 trong luồng liên thông 3 bên)
  - `UC-ADV05`: Nhập nhận xét & Chấm điểm GVHD (Đánh giá Báo cáo cuối kỳ)

### 2.3. Phân hệ Giảng viên phản biện (Bảng 2.3)
- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_Reviewer.puml`](PlantUML/UseCases/UC_Reviewer.puml)
- **Danh mục Use Case:**
  - `UC-REV01`: Tiếp nhận đề tài phản biện (Truy cập danh sách Khoa phân công, tải hồ sơ báo cáo)
  - `UC-REV02`: Nhập nhận xét & Chấm điểm GVPB (Bảo mật điểm cho đến khi Khoa công bố)
  - `UC-REV03`: Đánh giá điều kiện ra Hội đồng ("Đồng ý cho bảo vệ" / "Không đồng ý")

### 2.4. Phân hệ Hội đồng chấm KLTN (Bảng 2.4)
- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_Council.puml`](PlantUML/UseCases/UC_Council.puml)
- **Danh mục Use Case:**
  - `UC-COU01`: Xem hồ sơ & Báo cáo KLTN của nhóm sinh viên
  - `UC-COU02`: Nhập điểm thành phần & Nhận xét trực tiếp tại buổi bảo vệ (Thang điểm 0.0 - 10.0)
  - `UC-COU03`: Khóa biên bản điểm Hội đồng (Chuyển chế độ Read-only)
- **Phân vai:** Thành viên Hội đồng (Chủ tịch, Thư ký, Ủy viên) và Thư ký/Chủ tịch Hội đồng thực hiện thao tác Khóa biên bản.

### 2.5. Phân hệ Văn phòng Khoa (Bảng 2.5)
- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_Faculty.puml`](PlantUML/UseCases/UC_Faculty.puml)
- **Danh mục Use Case:**
  - `UC-FAC01`: Thẩm định & Duyệt đề tài đề xuất
  - `UC-FAC02`: Công bố danh mục đề tài ra toàn hệ thống
  - `UC-FAC03`: Thẩm định Đề cương chi tiết (Cổng kiểm soát 1 - Tuần 2)
  - `UC-FAC04`: Phê duyệt Đơn xin ĐTĐT (Bước 2: Ký duyệt chuyển PĐT)
  - `UC-FAC05`: Xử lý & Tiếp nhận kết quả Báo cáo giữa kỳ (Tổng hợp danh sách Tiếp tục / Dừng)
  - `UC-FAC06`: Phân công Giảng viên phản biện
  - `UC-FAC07`: Lập dự thảo Hội đồng & Xếp lịch bảo vệ
  - `UC-FAC08`: Xác nhận Hoàn thành Lưu chiểu (Mở điều kiện trả điểm chính thức)
  - `UC-FAC09`: Cấp quyền đặc biệt Mở khóa điểm Hội đồng (Xử lý khiếu nại)
  - `UC-FAC10`: Tổng hợp điểm 3 nguồn (Công thức trọng số linh hoạt Strategy)
  - `UC-FAC11`: Công bố kết quả & Xuất báo cáo (Excel/PDF)

### 2.6. Phân hệ Phòng Đào Tạo (Bảng 2.6)
- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_AcademicAffairs.puml`](PlantUML/UseCases/UC_AcademicAffairs.puml)
- **Danh mục Use Case:**
  - `UC-AA01`: Phê duyệt Đơn xin ĐTĐT (Bước 3 cuối cùng trong luồng liên thông 3 bên)
  - `UC-AA02`: Ban hành Quyết định thành lập Hội đồng (Upload file QĐ online)
  - `UC-AA03`: Trích xuất Báo cáo & Công nhận tốt nghiệp toàn trường

### 2.7. Phân hệ Quản trị viên hệ thống (Bảng 2.7)
- **Mã nguồn:** [`docs/PlantUML/UseCases/UC_Admin.puml`](PlantUML/UseCases/UC_Admin.puml)
- **Danh mục Use Case:**
  - `UC-SYS01`: Quản trị Người dùng & Phân quyền (RBAC) (Tạo mới, khóa, import Excel)
  - `UC-SYS02`: Cấu hình Đợt KLTN & Mốc thời gian (Chuẩn 21 tuần, deadline 7 giai đoạn)
  - `UC-SYS03`: Cấu hình Tham số & Trọng số tính điểm (Thay đổi tỷ lệ % linh hoạt)
  - `UC-SYS04`: Theo dõi Nhật ký hệ thống (Audit Log)
