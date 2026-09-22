# TÀI LIỆU BIỂU ĐỒ TUẦN TỰ (SEQUENCE DIAGRAMS) HỆ THỐNG UGTM
## Môn học: Công nghệ .NET (SE310.R11) — Nhóm 1

> Tài liệu này tổng hợp toàn bộ các biểu đồ Tuần tự (Sequence Diagram) mô hình hóa chi tiết các luồng tương tác nghiệp vụ trọng yếu của hệ thống **UGTM**, bám sát 100% các sơ đồ luồng dữ liệu và thuật toán xử lý tại **Chương 2 (Mục 2.4: SĐ1 đến SĐ5)** trong Báo cáo phân tích và thiết kế.

---

## 📂 Danh mục File Mã nguồn PlantUML (`docs/PlantUML/Sequences/`)

| STT | Luồng Nghiệp vụ | Tệp PlantUML | Tham chiếu Báo cáo | Đặc tả Kỹ thuật nổi bật |
|:---:|:---|:---|:---:|:---|
| **1** | **Đăng ký Đề tài & Ghép nhóm** | [`SD1_DangKyDeTai.puml`](PlantUML/Sequences/SD1_DangKyDeTai.puml) | Mục 2.4.1 (SĐ1) | Giao tác CSDL (Transaction & `UPDLOCK`), chống race-condition khi nhiều nhóm cùng đăng ký 1 đề tài |
| **2** | **Thẩm định Đề cương chi tiết (Cổng 1)** | [`SD2_ThamDinhDeCuong.puml`](PlantUML/Sequences/SD2_ThamDinhDeCuong.puml) | Mục 2.4.2 (SĐ2) | Xử lý 3 nhánh kết luận (Đạt / Yêu cầu sửa / Không đạt), tích hợp phân hệ Notification gửi mail tự động |
| **3** | **Đổi tên đề tài (ĐTĐT) liên thông 3 bên** | [`SD3_DoiTenDeTai_3Ben.puml`](PlantUML/Sequences/SD3_DoiTenDeTai_3Ben.puml) | Mục 2.4.3 (SĐ3) | Luồng bản mềm 100% online qua 3 cấp tuần tự (GVHD $\rightarrow$ Khoa $\rightarrow$ PĐT), ghi vết toàn diện vào `AuditLogs` |
| **4** | **Chấm điểm Hội đồng & Khóa biên bản** | [`SD4_ChamDiemHoiDong_KhoaBienBan.puml`](PlantUML/Sequences/SD4_ChamDiemHoiDong_KhoaBienBan.puml) | Mục 2.4.4 (SĐ4) | Tự động tính điểm trung bình HĐ, khóa điểm Read-only có xác thực và cơ chế Override đặc biệt khi có khiếu nại |
| **5** | **Tổng hợp điểm 3 nguồn & Công bố kết quả** | [`SD5_TongHopDiem_CongBo.puml`](PlantUML/Sequences/SD5_TongHopDiem_CongBo.puml) | Mục 2.4.5 (SĐ5) | Kiểm tra điều kiện nộp lưu chiểu, tính điểm linh hoạt theo Strategy Pattern, xuất báo cáo Excel/PDF |

---

## 1. SD1: Đăng ký Đề tài & Ghép nhóm KLTN (Mục 2.4.1 - SĐ1)
- **Tác nhân tham gia:** Sinh viên (Nhóm trưởng), Web SPA, Host API Gateway, `RegisterThesisHandler`, `ThesisRegistration` (Aggregate Root), SQL Server (`thesis` schema).
- **Quy tắc kiểm tra:**
  1. Hạn chót đăng ký trong Tuần 0 – 2 (BR-01).
  2. Đề tài ở trạng thái "Đã công bố" và còn chỉ tiêu trống (QĐ1.3).
  3. Tất cả thành viên trong nhóm chưa đăng ký đề tài nào khác trong đợt (QĐ1.2).
- **Tính năng nổi bật:** Khởi tạo `BEGIN TRANSACTION` và sử dụng cơ chế khóa dữ liệu (Locking) nhằm ngăn chặn việc nhiều nhóm cùng lúc ghi đè chỉ tiêu của một đề tài.

---

## 2. SD2: Thẩm định Đề cương chi tiết — Cổng Kiểm soát 1 (Mục 2.4.2 - SĐ2)
- **Tác nhân tham gia:** Văn phòng Khoa, Nhóm Sinh viên & GVHD, Web SPA, API, `ReviewOutlineHandler`, SQL Server, Notification Module.
- **Phân nhánh kết quả thẩm định:**
  - **Đạt (Approved):** Chuyển trạng thái sang `Đang thực hiện KLTN`.
  - **Cần chỉnh sửa (NeedRevision):** Mở cờ cho phép nộp lại phiên bản đề cương mới trước thời hạn bổ sung.
  - **Không đạt (Rejected):** Chuyển trạng thái sang `Dừng KLTN` và khóa quyền truy cập các tính năng thực hiện KLTN trong học kỳ.

---

## 3. SD3: Xử lý Đơn Đổi Tên Đề Tài Online Liên Thông 3 Bên (Mục 2.4.3 - SĐ3)
- **Tác nhân tham gia:** Sinh viên, GVHD, Văn phòng Khoa, Phòng Đào Tạo (PĐT), `TopicChangeHandler`, `identity.AuditLogs`.
- **Luồng phê duyệt 3 cấp tuần tự (Tuần 3 – 10):**
  - Cấp 1: GVHD xác nhận $\rightarrow$ Chuyển trạng thái sang *Chờ Khoa duyệt*.
  - Cấp 2: Văn phòng Khoa ký duyệt $\rightarrow$ Chuyển trạng thái sang *Chờ PĐT phê duyệt*.
  - Cấp 3: Phòng Đào Tạo phê duyệt (bước cuối) $\rightarrow$ Cập nhật tên đề tài chính thức vào hệ thống, mở quyền cho sinh viên nộp lại bản đề cương mới và ghi vết vào `AuditLogs`.

---

## 4. SD4: Chấm Điểm Hội Đồng & Khóa Biên Bản Điểm (Mục 2.4.4 - SĐ4)
- **Tác nhân tham gia:** Thành viên Hội đồng (Chủ tịch, UV), Thư ký Hội đồng, Lãnh đạo Khoa, `CouncilGradingHandler`, SQL Server, `AuditLogs`.
- **Cơ chế an toàn điểm số:**
  - Điểm thành phần từ 0.0 đến 10.0, tự động tính điểm trung bình Hội đồng.
  - Thao tác "Khóa biên bản điểm" yêu cầu xác thực và chuyển toàn bộ dữ liệu điểm sang chế độ chỉ đọc (**Read-only**).
  - Nghiêm cấm mọi hành vi sửa điểm trực tiếp; trường hợp có khiếu nại hợp lệ cần Lãnh đạo Khoa cấp quyền **Override** đặc biệt kèm lý do và ghi vết kiểm toán.

---

## 5. SD5: Tổng Hợp Điểm 3 Nguồn & Công Bố Kết Quả (Mục 2.4.5 - SĐ5)
- **Tác nhân tham gia:** Văn phòng Khoa, Sinh viên, `ConsolidateGradesHandler`, `GradingStrategy`, SQL Server, Export Service.
- **Quy trình tổng kết:**
  - Kiểm tra điều kiện ràng buộc: Đề tài bắt buộc phải có xác nhận **"Đã hoàn thành nộp lưu chiểu"** từ Khoa mới được trả điểm chính thức (BR-05 / QĐ5.1).
  - Áp dụng công thức tham số hóa theo mẫu thiết kế Strategy:
    $$\text{Điểm Tổng kết} = (\text{Điểm}_{GVHD} \times W_{HD}) + (\text{Điểm}_{GVPB} \times W_{PB}) + (\text{Điểm}_{HĐ} \times W_{HĐ})$$
  - Khi Khoa duyệt "Công bố", hệ thống kích hoạt quyền tra cứu trên Dashboard cho sinh viên và xuất tệp báo cáo tổng hợp toàn trường dạng Excel/PDF gửi Phòng Đào Tạo.
