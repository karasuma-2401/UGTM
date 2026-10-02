# ĐẶC TẢ YÊU CẦU HỆ THỐNG

## Quản lý Khóa luận Tốt nghiệp (UIT)

---

## 1. Giới thiệu

### 1.1 Mục tiêu

Xây dựng **ứng dụng Web** hỗ trợ toàn bộ quy trình 8 bước quản lý khóa luận tốt nghiệp (KLTN): từ đăng ký/duyệt đề tài → duyệt đề cương → thực hiện & báo cáo giữa kỳ (kèm đổi tên đề tài nếu có) → báo cáo cuối kỳ, chấm điểm GVHD/GVPB → hội đồng chấm KLTN → xác nhận & công bố kết quả.

### 1.2 Nền tảng triển khai

- Hệ thống được xây dựng dưới dạng **website**, truy cập qua trình duyệt, không yêu cầu cài đặt phần mềm phía client.
- Giao diện responsive tối thiểu cho desktop/laptop (độ phân giải ≥ 1280px); ưu tiên hỗ trợ thêm tablet nếu thời gian cho phép.
- Tương thích các trình duyệt phổ biến, bản cập nhật gần nhất: Chrome, Edge, Firefox.
- Định hướng công nghệ (theo yêu cầu môn học Công nghệ .NET): ASP.NET Core (Web API/MVC), CSDL quan hệ (SQL Server), xác thực qua ASP.NET Core Identity + JWT.

### 1.3 Đối tượng sử dụng

Sinh viên, Giảng viên (GVHD/GVPB/Thành viên hội đồng), Khoa (giáo vụ/lãnh đạo), Phòng Đào Tạo, Admin hệ thống.

---

## 2. Các tác nhân (Actors / User Roles)

| Vai trò                                               | Mô tả theo quy trình gốc                                                                                                                                                                         |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Sinh viên**                                         | Đăng ký đề tài, nộp đề cương, thực hiện KLTN, nộp báo cáo tiến độ định kỳ theo mốc, nộp báo cáo giữa kỳ/cuối kỳ, xin đổi tên đề tài (nếu có), nộp lưu chiểu & chỉnh sửa theo góp ý GVPB/Hội đồng |
| **GVHD (Giảng viên hướng dẫn)**                       | Soạn/đề xuất đề tài, hỗ trợ xây dựng đề cương, tạo các mốc tiến độ (`ProgressMilestone`), nhận xét báo cáo tiến độ (`ProgressReport`), duyệt đơn ĐTĐT, đánh giá & chấm điểm GVHD                 |
| **GVPB (Giảng viên phản biện)**                       | Do Khoa phân công sau khi nhận báo cáo cuối kỳ; nhận xét, đánh giá, chấm điểm phản biện & kết luận điều kiện ra bảo vệ                                                                           |
| **Khoa** (bao gồm thư ký/trợ lý khoa & lãnh đạo khoa) | Duyệt đề tài, thẩm định đề cương (Cổng 1), thẩm định báo cáo giữa kỳ (Cổng 2), duyệt đơn ĐTĐT, phân công GVPB, sắp xếp hội đồng, xác nhận lưu chiểu, tổng hợp & công bố kết quả                  |
| **Phòng Đào Tạo**                                     | Phê duyệt chốt đơn ĐTĐT; ban hành quyết định thành lập hội đồng chấm KLTN; trích xuất báo cáo xét tốt nghiệp toàn trường                                                                         |
| **Hội đồng chấm KLTN**                                | Chấm điểm thành phần, nhận xét trực tiếp trong buổi bảo vệ và khóa biên bản điểm                                                                                                                 |
| **Admin hệ thống**                                    | Quản lý tài khoản, danh mục quyền (`Permission`), phân quyền vai trò (`RolePermission`), cấu hình đợt/học kỳ, mốc deadline 7 giai đoạn                                                           |

---

## 3. Thuật ngữ (Glossary)

| Viết tắt | Ý nghĩa                                                                            |
| -------- | ---------------------------------------------------------------------------------- |
| KLTN     | Khóa luận tốt nghiệp                                                               |
| GVHD     | Giảng viên hướng dẫn                                                               |
| GVPB     | Giảng viên phản biện                                                               |
| ĐTĐT     | Đơn xin đổi tên đề tài                                                             |
| PĐT      | Phòng Đào Tạo                                                                      |
| RBAC     | Role-Based Access Control — phân quyền theo vai trò & danh mục quyền (Permissions) |

---

## 4. Quy trình chuẩn (8 bước, đúng theo sơ đồ gốc)

### Bước 1 — Chuẩn bị & soạn đề tài (1 tuần trước khi bắt đầu học kỳ)

- FR1.1: Khoa gửi thông báo chuẩn bị đăng ký KLTN.
- FR1.2: GVHD soạn đề tài KLTN, gửi Khoa.

### Bước 2 — Duyệt đề tài, đăng ký & xây dựng đề cương (1 tuần)

- FR2.1: Khoa duyệt đề tài KLTN và thông báo để sinh viên đăng ký.
- FR2.2: Sinh viên đăng ký đề tài, **tự liên hệ GVHD** (không có cơ chế match tự động theo nguyện vọng).
- FR2.3: Sinh viên + GVHD cùng xây dựng đề cương chi tiết (GVHD hỗ trợ), nộp cho Khoa.
- _Output:_ Danh sách đăng ký KLTN.

### Bước 3 — Duyệt đề cương (1 tuần) — **cổng đánh giá 1, có thể lặp lại**

- FR3.1: Khoa duyệt đề cương chi tiết (`Syllabus`), kết quả **Đạt (Approved) / Cần sửa (NeedRevision) / Không đạt (Rejected)**:
  - **NeedRevision** → sinh viên **cập nhật lại đề cương và nộp lại cho Khoa** để thẩm định lại trước deadline.
  - **Approved** → sinh viên chuyển sang "Đang thực hiện KLTN", GVHD bắt đầu hướng dẫn.
- _Output:_ Danh sách sinh viên đăng ký KLTN và đề cương chi tiết KLTN đã duyệt.

### Bước 4 — Thực hiện & báo cáo giữa kỳ (7 tuần) — **cổng đánh giá 2 (Khoa duyệt 1 cổng duy nhất)**

- FR4.1: Sinh viên thực hiện KLTN dưới sự hướng dẫn của GVHD, nộp các Báo cáo tiến độ (`ProgressReport`) theo các mốc thời gian (`ProgressMilestone`) do GVHD/Khoa khởi tạo.
- FR4.2: Sinh viên làm báo cáo giữa kỳ (`MidtermReport`); **nếu cần đổi tên đề tài, nộp kèm đơn xin đổi tên đề tài (ĐTĐT)**, nộp cho Khoa.
- FR4.3: **Văn phòng Khoa thẩm định Báo cáo giữa kỳ (1 cổng duy nhất, hoàn toàn tương tự luồng duyệt Đề cương ở Bước 3):**
  - **Cần chỉnh sửa (NeedRevision)** → sinh viên nộp lại phiên bản báo cáo giữa kỳ mới cho Khoa.
  - **Không đạt (Rejected)** → hệ thống chuyển trạng thái sinh viên sang **"Dừng thực hiện KLTN"**, khóa quyền thực hiện các bước tiếp theo.
  - **Đạt (Approved)** → sinh viên tiếp tục thực hiện KLTN giai đoạn 2.
- FR4.4: Khoa thu gom các đơn ĐTĐT hợp lệ, **chuyển cho Phòng Đào Tạo phê duyệt chốt**.
- FR4.5: Hệ thống nhắc hạn nộp và theo dõi trạng thái nộp của từng sinh viên.
- _Output:_ Báo cáo KLTN giữa kỳ đã duyệt, đơn xin ĐTĐT, danh sách đổi tên đề tài.

### Bước 5 — Tiếp tục thực hiện & xử lý ĐTĐT (8 tuần)

- FR5.1: Khoa thông báo quyết định ĐTĐT cho sinh viên (nếu có đơn).
- FR5.2: Nếu ĐTĐT được PĐT duyệt → sinh viên cập nhật lại đề cương theo tên đề tài mới, nộp lại cho Khoa.
- FR5.3: Sinh viên tiếp tục thực hiện KLTN và nộp các báo cáo tiến độ định kỳ.

### Bước 6 — Báo cáo cuối kỳ, chấm điểm GVHD & phân công/chấm điểm GVPB (1 tuần)

- FR6.1: Sinh viên nộp báo cáo cuối kỳ cho **GVHD trước**, sau đó nộp cho Khoa.
- FR6.2: GVHD đánh giá và chấm điểm KLTN độc lập **trước buổi bảo vệ**, gửi kết quả lên hệ thống.
- FR6.3: Khoa thu báo cáo cuối kỳ và **phân công GVPB** (Khoa chọn, sinh viên không tự chọn).
- FR6.4: Sinh viên nộp báo cáo cuối kỳ cho GVPB được phân công.
- FR6.5: GVPB nhận xét, đánh giá, chấm điểm phản biện độc lập **trước buổi bảo vệ** & kết luận điều kiện ra bảo vệ (`Approved` / `Rejected`).
- _Output:_ Báo cáo cuối kỳ, bảng điểm GVHD & GVPB, danh sách sinh viên đủ điều kiện ra Hội đồng bảo vệ.

### Bước 7 — Chỉnh sửa theo GVPB & tổ chức hội đồng (1 tuần)

- FR7.1: Sinh viên sửa báo cáo cuối kỳ theo góp ý của GVPB, nộp cho hội đồng chấm KLTN.
- FR7.2: Khoa sắp xếp thành phần hội đồng chấm KLTN, **gửi Phòng Đào Tạo ra quyết định thành lập**.
- FR7.3: Khoa tổ chức hội đồng chấm KLTN (lịch, phòng, thời gian).
- FR7.4: Trong buổi bảo vệ trực tiếp, các thành viên Hội đồng tiến hành chấm điểm. Nếu thành viên Hội đồng **đồng thời là GVPB của nhóm KLTN đó (TH1)**, thành viên này **không chấm điểm Hội đồng lần 2** (bỏ qua form chấm điểm HĐ). Thư ký ghi nhận biên bản nhận xét buổi bảo vệ.
- _Output:_ Báo cáo cuối kỳ (đã sửa), quyết định thành lập hội đồng, hồ sơ hội đồng, nhận xét của hội đồng và phản biện.

### Bước 8 — Bảo vệ, xác nhận & công bố kết quả (1 tuần)

- FR8.1: Sinh viên sửa báo cáo cuối kỳ theo góp ý của hội đồng, nộp bản đã sửa **lên hệ thống của trường**.
- FR8.2: Khoa xác nhận KLTN đã nộp trên hệ thống trường (`ArchiveConfirmed`).
- FR8.3: Hệ thống tự động tính toán và tổng hợp điểm từ 3 nguồn (GVHD, GVPB, Hội đồng) theo trọng số cấu hình trong `ScoreWeightConfig` và áp dụng quy tắc BR-07 (xử lý trường hợp GVPB kiêm Hội đồng TH1 vs TH2).
- FR8.4: Khoa công bố kết quả bảo vệ KLTN.
- _Output:_ Bảng điểm KLTN chính thức.

---

### 4.1. Quy tắc Nghiệp vụ Chấm điểm & Xử lý Vai trò Kép (BR-07)

1. **Trình tự chấm điểm phân định rõ ràng**:
   - **Giai đoạn trước bảo vệ (Bước 6)**: GVHD và GVPB tiến hành chấm điểm độc lập trước. GVPB đồng thời ra kết luận cho phép/không cho phép ra Hội đồng bảo vệ.
   - **Giai đoạn buổi bảo vệ (Bước 7 - 8)**: Hội đồng tiến hành chấm điểm trực tiếp tại buổi bảo vệ sau khi GVHD và GVPB đã hoàn tất điểm số.
2. **Xử lý vai trò kép (GVPB kiêm Thành viên Hội đồng)**:
   - **TH1 (GVPB được phân công làm thành viên Hội đồng của cùng nhóm KLTN đó)**: Trong buổi bảo vệ, thành viên Hội đồng này (vốn đã chấm GVPB trước đó) **không tiến hành chấm điểm lại ở vai trò Hội đồng**. Form chấm điểm Hội đồng của thành viên này bị vô hiệu hóa/loại trừ. Điểm trung bình Hội đồng `Score_Council_Avg` được tính bằng **trung bình cộng điểm số của các thành viên Hội đồng còn lại** (không bao gồm GVPB).
   - **TH2 (GVPB không thuộc Hội đồng của nhóm KLTN đó)**: Tất cả thành viên Hội đồng tiến hành chấm điểm như bình thường. Điểm trung bình Hội đồng `Score_Council_Avg` tính bằng trung bình cộng của tất cả thành viên trong Hội đồng.
3. **Công thức tính điểm tổng kết**:
   $$\text{FinalScore} = \text{Score\_GVHD} \times W_{\text{GVHD}} + \text{Score\_GVPB} \times W_{\text{GVPB}} + \text{Score\_Council\_Avg} \times W_{\text{Council}}$$
   *(Trọng số $W_{\text{GVHD}}, W_{\text{GVPB}}, W_{\text{Council}}$ lấy từ `ScoreWeightConfig` của đợt KLTN, tổng trọng số = 1.0)*

---

## 5. Yêu cầu chức năng bổ sung (không thuộc luồng chính nhưng cần thiết)

- FR9.1: Cấu hình mốc thời gian (deadline) cho từng bước theo học kỳ/đợt (Admin có thể chỉnh theo từng đợt thực tế).
- FR9.2: Thông báo/email tự động tại các mốc quan trọng: mở đăng ký, hạn nộp đề cương, hạn báo cáo giữa kỳ, hạn báo cáo cuối kỳ, lịch bảo vệ, công bố điểm.
- FR9.3: Theo dõi trạng thái từng sinh viên qua từng bước (dashboard tiến độ: Đang đăng ký / Chờ duyệt đề cương / Đang thực hiện / Dừng KLTN / Chờ chấm GVPB / Chờ bảo vệ / Đã có kết quả).
- FR9.4: Lưu trữ lịch sử các phiên bản báo cáo và đề cương (mỗi lần nộp lại đề cương ở Bước 3, báo cáo tiến độ, giữa kỳ, cuối kỳ, sau góp ý GVPB, sau góp ý hội đồng).
- FR9.5: Quản lý hồ sơ hội đồng (thành phần, quyết định thành lập từ Phòng Đào Tạo, biên bản).
- FR9.6: Sinh viên tra cứu trạng thái & kết quả cuối cùng.
- FR9.7: Quản lý danh mục quyền (`Permissions`) và phân quyền chi tiết cho vai trò (`RolePermissions`) thuộc hệ thống RBAC Granular.
- FR9.8: Quản lý các mốc tiến độ thực hiện KLTN (`ProgressMilestone`) và theo dõi/đánh giá Báo cáo tiến độ định kỳ (`ProgressReport`).

---

## 6. Yêu cầu phi chức năng

| Nhóm             | Yêu cầu                                                                                                                                                               |
| ---------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Nền tảng         | Ứng dụng Web, responsive tối thiểu cho desktop/laptop; tương thích Chrome, Edge, Firefox bản mới                                                                      |
| Bảo mật          | Phân quyền theo vai trò và danh mục quyền (RBAC Granular — `Permission` & `RolePermission`); xác thực tài khoản; mã hóa mật khẩu                                      |
| Hiệu năng        | Chịu tải cao điểm ở Bước 2 (đăng ký), Bước 3 (nộp/duyệt đề cương) và Bước 6 (nộp báo cáo cuối kỳ)                                                                     |
| Toàn vẹn dữ liệu | Lưu log các quyết định duyệt/không duyệt (đề cương, giữa kỳ, ĐTĐT, hội đồng, điểm) để tra soát khiếu nại; dùng transaction cho các thao tác đổi trạng thái quan trọng |
| Khả năng mở rộng | Dễ cấu hình lại mốc thời gian, công thức tính điểm, số vòng chỉnh sửa đề cương cho mỗi đợt khóa luận                                                                  |
| Khả dụng         | Ổn định trong các tuần cao điểm nộp bài/bảo vệ                                                                                                                        |
| Khả năng sử dụng | Giao diện rõ ràng theo từng vai trò, tối thiểu số bước thao tác cho các tác vụ lặp lại (nộp file, chấm điểm)                                                          |
| Sao lưu dữ liệu  | Backup định kỳ dữ liệu báo cáo, điểm số, hồ sơ hội đồng                                                                                                               |

---

## 7. Ma trận Actor – Chức năng chính (tóm tắt, dùng để vẽ Use Case Diagram)

| Chức năng                                | Sinh viên        | GVHD             | Khoa                  | GVPB     | Phòng Đào Tạo      | Hội đồng | Admin |
| ---------------------------------------- | ---------------- | ---------------- | --------------------- | -------- | ------------------ | -------- | ----- |
| Soạn/duyệt đề tài                        |                  | ✔ (soạn)         | ✔ (duyệt)             |          |                    |          |       |
| Đăng ký đề tài & nộp đề cương            | ✔                | ✔ (hỗ trợ)       | ✔ (duyệt)             |          |                    |          |       |
| Tạo mốc & Nộp/Đánh giá Báo cáo tiến độ   | ✔ (nộp)          | ✔ (tạo/nhận xét) |                       |          |                    |          |       |
| Nộp & Thẩm định Báo cáo giữa kỳ (1 cổng) | ✔ (nộp)          |                  | ✔ (thẩm định)         |          |                    |          |       |
| Tạo & Duyệt đơn ĐTĐT 3 bên               | ✔ (tạo đơn)      | ✔ (xác nhận)     | ✔ (duyệt)             |          | ✔ (phê duyệt chốt) |          |       |
| Nộp & chấm báo cáo cuối kỳ               | ✔                | ✔ (chấm)         | ✔ (phân công GVPB)    | ✔ (chấm) |                    |          |       |
| Tổ chức & chấm hội đồng                  | ✔                |                  | ✔ (sắp xếp)           |          | ✔ (ra quyết định)  | ✔ (chấm) |       |
| Xác nhận nộp & công bố kết quả           | ✔ (nộp bản cuối) |                  | ✔ (xác nhận, công bố) |          |                    |          |       |
| Quản lý tài khoản, vai trò & Permissions |                  |                  |                       |          |                    |          | ✔     |
| Tra cứu trạng thái/kết quả               | ✔                | ✔                | ✔                     | ✔        | ✔                  | ✔        | ✔     |

---

## 8. Các điểm còn MỞ — cần bạn xác nhận thêm sau khảo sát

1. **Bước nộp "hệ thống trường" (FR8.1/8.2):**
   Giả định tạm thời: hệ thống sẽ **có màn hình để sinh viên upload file KLTN cuối cùng**, và Khoa bấm "Xác nhận đã nộp" — coi như **mô phỏng nội bộ** bước nộp lên hệ thống trường, KHÔNG tích hợp API thật với hệ thống trường (vì thường không có quyền truy cập). Cần xác nhận: (a) ai là người bấm xác nhận — Khoa hay GVHD; (b) có tích hợp thật hay hoàn toàn mô phỏng.

2. **Công thức tính điểm cuối (FR8.3):**
   Giả định tạm thời: hệ thống hiển thị 3 điểm thành phần (GVHD, GVPB, hội đồng) để **Khoa xem và nhập điểm tổng kết thủ công**, đồng thời **để ngỏ khả năng cấu hình công thức trọng số tự động sau này** (không hard-code %). Cần xác nhận với Khoa/quy chế thực tế trước khi quyết định có tự động hóa hay không.

3. **Giới hạn số lần nộp lại đề cương (Bước 3):** Sơ đồ gốc không nêu rõ có giới hạn số lần "Không đạt → nộp lại" hay không. Tạm giả định không giới hạn số vòng, chỉ giới hạn bởi deadline chung của Bước 3.

---

## 9. Phạm vi ngoài hệ thống (Out of scope)

- Không tích hợp trực tiếp (API thật) với hệ thống nộp lưu chiểu / thư viện số của trường — chỉ mô phỏng qua upload + xác nhận thủ công (xem mục 8.1).
- Không xử lý thanh toán/học phí.
- Không quản lý xếp lịch học/thời khóa biểu chung của trường (chỉ xếp lịch riêng cho buổi bảo vệ hội đồng).
- Không tự động hóa việc phát hiện đạo văn (có thể là hướng mở rộng tương lai, không thuộc phạm vi hiện tại).

---

## 10. Gợi ý bước tiếp theo

1. Vẽ **Activity Diagram** theo đúng 8 bước ở mục 4, lưu ý cổng dừng/tiếp tục chính thức nằm ở Bước 4.
2. Vẽ **Use Case Diagram** dựa trên ma trận actor–chức năng ở mục 7 và bảng FR ở mục 4–5.
3. Thiết kế CSDL với các thực thể: SinhVien, GVHD, GVPB, DeTai, DeCuong (có version), BaoCaoGiuaKy, DonDoiTenDeTai, BaoCaoCuoiKy, PhanCongGVPB, HoiDong, QuyetDinhHoiDong, DiemSo, TrangThaiSinhVien.
4. Làm rõ 3 điểm còn mở ở mục 8 sau khi khảo sát thực tế với Khoa/Phòng Đào Tạo, rồi cập nhật lại tài liệu này trước khi chốt thiết kế DB/API chi tiết.
