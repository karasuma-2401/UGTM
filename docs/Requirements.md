# Đặc tả yêu cầu UGTM

Nguồn nghiệp vụ: Báo cáo SE310 (Bản Final).docx (tài liệu nghiệp vụ nhóm quản lý ngoài repo), chốt tháng 10/2026. Đây là yêu cầu cần triển khai; trạng thái source xem [Architecture.md](../backend/docs/Architecture/Architecture.md) và [DataModel.md](DataModel.md).

## 1. Phạm vi và quy trình 21 tuần

Hệ thống quản lý KLTN tại UIT, gồm 7 giai đoạn và 2 cổng kiểm soát. Không tự ghép đề tài theo nguyện vọng; sinh viên chủ động chọn đề tài. Nhóm có 1–2 sinh viên.

| STT | Thời gian | Nội dung chính |
| --- | --- | --- |
| 1 | Tuần 0–1 | GVHD đề xuất đề tài; Văn phòng Khoa thẩm định, phê duyệt và công bố danh mục đề tài. |
| 2 | Tuần 2 | Sinh viên đăng ký đề tài, ghép nhóm và nộp đề cương chi tiết; Văn phòng Khoa thẩm định đề cương tại Cổng kiểm soát 1. |
| 3 | Tuần 3–10 | Sinh viên thực hiện đề tài theo các Milestones do GVHD thiết lập, nộp sản phẩm tiến độ, báo cáo giữa kỳ và thực hiện thủ tục đổi tên đề tài khi có nhu cầu. |
| 4 | Tuần 10 | Văn phòng Khoa thẩm định báo cáo giữa kỳ tại Cổng kiểm soát 2; kết quả `Approved`, `NeedRevision` hoặc `Rejected` quyết định nhóm tiếp tục, nộp lại hay dừng KLTN. |
| 5 | Tuần 11–18 | Sinh viên nộp báo cáo cuối kỳ; GVHD nhận xét, chấm điểm và xác nhận chuyển phản biện; GVPB đánh giá, chấm điểm và kết luận điều kiện bảo vệ. |
| 6 | Tuần 19–21 | Văn phòng Khoa lập Hội đồng và xếp lịch bảo vệ; Phòng Đào tạo phê duyệt quyết định; Hội đồng tổ chức bảo vệ, chấm điểm và khóa biên bản. Sau bảo vệ, sinh viên hoàn thiện hồ sơ lưu chiểu, GVHD kiểm tra bản chỉnh sửa và Khoa xác nhận hoàn thành lưu chiểu. |
| 7 | Tuần 21 | Văn phòng Khoa tổng hợp điểm theo cấu hình động của đợt KLTN, rà soát bảng điểm dự thảo và công bố kết quả chính thức; Khoa và Phòng Đào tạo xuất báo cáo phục vụ xét tốt nghiệp. |

## 2. Tác nhân và phân quyền

Có 7 nhóm tác nhân nghiệp vụ: sinh viên, GVHD, GVPB, Hội đồng, Văn phòng Khoa, PĐT, Admin. Source có 6 vai trò hệ thống: `Student`, `Lecturer`, `CouncilMember`, `Faculty`, `AcademicAffairs`, `Admin`. GVHD/GVPB là hai trách nhiệm theo phân công của giảng viên; Chủ tịch/Thư ký/Ủy viên là vai trò trong Hội đồng. Quyền còn phụ thuộc nhóm/đề tài được phân công, thời hạn và trạng thái hồ sơ.

Quy ước: TH = Thực hiện; D = Duyệt/Xác nhận; N = Nộp; X = Xem; CH = Cấu hình; — = Không có quyền; * = Có điều kiện theo Policy-based Authorization.

| Nhóm chức năng | SV | GVHD | GVPB | Khoa | PĐT | HĐ | Admin |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1. Quản trị hệ thống và cấu hình đợt KLTN |  |  |  |  |  |  |  |
| Đăng nhập và quản lý tài khoản cá nhân | TH | TH | TH | TH | TH | TH | TH |
| Quản lý tài khoản, gán vai trò RBAC | — | — | — | X | — | — | TH |
| Khởi tạo đợt KLTN theo học kỳ/năm học | — | — | — | — | TH | — | TH |
| Cấu hình giai đoạn và deadline đợt KLTN | — | — | — | — | CH | — | CH |
| Tra cứu tiến độ, thời hạn của đợt KLTN | X | X | X | X | X | X | X |
| Import danh sách SV và GV từ Excel | — | — | — | X | TH | — | TH |
| Cấu hình phương thức tính, đầu điểm và trọng số/hệ số | — | — | — | CH | — | — | CH |
| Cấu hình điều kiện tiên quyết và điểm sàn | — | — | — | CH | — | — | CH |
| Tra cứu AuditLog | — | — | — | — | — | — | X |
| 2. Đăng ký đề tài, ghép nhóm và thẩm định đề cương — Cổng 1 |  |  |  |  |  |  |  |
| Tạo, chỉnh sửa đề tài | — | TH | — | X | — | — | — |
| Thẩm định, duyệt và công bố đề tài | X | X | — | D | — | — | — |
| Tạo nhóm, đăng ký đề tài | TH | — | — | X | — | — | — |
| Xác nhận tiếp nhận nhóm đăng ký | X | D | — | X | — | — | — |
| Nộp đề cương chi tiết | N | X | — | X | — | — | — |
| Thẩm định đề cương — Cổng 1 | X | X | — | D | X | — | — |
| Chỉnh sửa, nộp lại đề cương khi Khoa yêu cầu | N* | X | — | X | — | — | — |
| 3. Quản lý tiến độ theo Milestones |  |  |  |  |  |  |  |
| Thiết lập và quản lý các Milestones | X | TH | — | — | — | — | — |
| Nộp báo cáo, mã nguồn, sản phẩm theo mốc | N | X | — | — | — | — | — |
| Đánh giá và phản hồi kết quả Milestones | X | TH | — | — | — | — | — |
| Nộp lại sản phẩm theo yêu cầu chỉnh sửa | N* | X | — | — | — | — | — |
| Theo dõi trạng thái, hạn nộp và cảnh báo trễ mốc | X | X | — | — | — | — | — |
| 4. Báo cáo giữa kỳ và thẩm định điều kiện tiếp tục — Cổng 2 |  |  |  |  |  |  |  |
| Nộp báo cáo giữa kỳ | N | X | — | X | — | — | — |
| Thẩm định báo cáo giữa kỳ — Cổng 2 | X | X | — | D | — | — | D |
| Tổng hợp kết quả giữa kỳ và chuyển PĐT | X | X | — | TH | X | — | — |
| 5. Đổi tên đề tài liên thông ba cấp |  |  |  |  |  |  |  |
| Tạo đơn xin đổi tên đề tài | TH | X | — | X | X | — | — |
| Xác nhận đơn đổi tên — Cấp 1 | X | D | — | X | X | — | — |
| Thẩm định đơn đổi tên — Cấp 2 | X | X | — | D | X | — | — |
| Phê duyệt chính thức — Cấp 3 | X | X | — | X | D | — | — |
| Nộp lại đề cương sau khi đổi tên được duyệt | N* | X | — | X | — | — | — |
| 6. Báo cáo cuối kỳ, chấm điểm GVHD và phản biện |  |  |  |  |  |  |  |
| Nộp báo cáo cuối kỳ | N | X | X* | X | — | — | — |
| Nhận xét và chấm điểm hướng dẫn | — | TH | — | X | — | X* | — |
| Xác nhận chuyển đề tài sang phản biện | X | D | — | X | — | — | — |
| Phân công giảng viên phản biện | — | X | X* | TH | — | — | — |
| Nhận xét và chấm điểm phản biện | — | — | TH | X | — | X* | — |
| Kết luận Đồng ý/Không đồng ý cho bảo vệ | X* | X | D | X | — | — | — |
| Xử lý bất đồng trong đánh giá phản biện | — | TH* | TH* | TH* | — | — | — |
| Xem góp ý chỉnh sửa của GVPB | X* | X* | TH | X | — | — | — |
| Nộp bản báo cáo chỉnh sửa sau phản biện | N* | X | X | X | — | X* | — |
| 7. Thành lập Hội đồng, tổ chức bảo vệ và khóa biên bản |  |  |  |  |  |  |  |
| Lập dự thảo Hội đồng và xếp lịch bảo vệ | — | — | — | TH | X | — | — |
| Phê duyệt quyết định thành lập Hội đồng | — | — | — | X | D | — | — |
| Tra cứu lịch bảo vệ, phòng và thành viên Hội đồng | X | X | X | X | X | X* | — |
| Nhập điểm thành viên Hội đồng | — | — | — | X | — | TH* | — |
| Xử lý và ghi nhận kết luận điểm sai biệt | — | X* | X* | X | — | TH* | — |
| Ghi nhận biên bản bảo vệ điện tử | — | — | — | X | — | TH* | — |
| Khóa biên bản điểm Hội đồng | — | — | — | X | — | TH* | — |
| Cấp quyền mở khóa biên bản đặc biệt | — | — | — | TH* | — | — | — |
| Chỉnh sửa điểm cá nhân sau khi được mở khóa | — | — | — | — | — | TH* | — |
| Xem phiếu điểm GVHD/GVPB để lập hồ sơ bảo vệ | — | — | — | X | — | X* | — |
| Xem cảnh báo và xử lý thảo luận điểm lệch | — | — | — | X | — | TH* | — |
| 8. Nộp lưu chiểu và hậu bảo vệ |  |  |  |  |  |  |  |
| Xem biên bản góp ý sau bảo vệ | X | X | — | X | — | TH* | — |
| Nộp hồ sơ lưu chiểu hoàn thiện | N | X | — | X | — | — | — |
| Kiểm tra và xác nhận bản chỉnh sửa | X | D | — | X | — | — | — |
| Kiểm tra và xác nhận hoàn thành lưu chiểu | X | X | — | D | X | — | — |
| Yêu cầu chỉnh sửa, nộp lại hồ sơ lưu chiểu | N* | X | — | TH | — | — | — |
| 9. Tổng hợp điểm và công bố kết quả KLTN |  |  |  |  |  |  |  |
| Kích hoạt tổng hợp điểm theo cấu hình động | — | — | — | TH | X | — | — |
| Rà soát bảng điểm tổng kết dự thảo | — | — | — | TH | X | — | — |
| Công bố kết quả KLTN chính thức | — | — | — | TH | X | — | — |
| Tra cứu điểm chi tiết sau công bố | X* | X* | X* | X | X | X* | — |
| Xuất bảng điểm tổng hợp Excel/PDF | — | — | — | TH | TH | — | — |

## 3. Quy trình nghiệp vụ


### Quy trình Cấu hình đợt, Quản trị hệ thống và Thiết lập hệ số điểm động


**Luồng lý tưởng:**

- Quản trị viên hoặc phòng Đào tạo khởi tạo đợt KLTN mới theo năm học và học kỳ (chu kỳ 21 tuần cố định), cấu hình hạn chót cho từng giai đoạn và nạp danh sách người dùng (import danh sách sinh viên đủ điều kiện, giảng viên từ Excel).
- PĐT hoặc Admin truy cập phân hệ cấu hình bảng điểm để thiết lập phương thức tính điểm tổng kết (tính theo phần trăm trọng số hoặc trung bình cộng hệ số nguyên):
- + Thiết lập danh mục các đầu điểm thành phần: Điểm Cán bộ hướng dẫn (ADVISOR), Điểm Cán bộ phản biện (REVIEWER), Điểm Hội đồng bảo vệ (COUNCIL), kèm các đầu điểm tùy chọn khác.
- + Gán tỷ lệ trọng số (Wi%) hoặc hệ số nguyên (Ci) tương ứng cho từng đầu điểm (ví dụ: GVHD hệ số 2, GVPB hệ số 2, Hội đồng hệ số 1).
- + Cấu hình điều kiện tiên quyết cho từng đầu điểm (ví dụ: GVPB bắt buộc có điểm sàn ≥5.0 mới đủ điều kiện ra bảo vệ).
- Hệ thống lưu cấu hình điểm của đợt và phân quyền RBAC kết hợp Policy-based Authorization cho các nhóm tác nhân trước khi mở cổng đợt KLTN.

**Luồng phát sinh:**

- Điều chỉnh công thức/trọng số điểm: PĐT/Admin tạo Draft từ version đang hoạt động, nhập lý do và công bố version mới mà không cần can thiệp chỉnh sửa mã nguồn.
- Ràng buộc cấu hình trọng số: Nếu cấu hình theo tỷ lệ phần trăm (%), tổng các Wi phải bằng 100%; nếu cấu hình theo hệ số nguyên (Ci), tổng hệ số phải lớn hơn 0. Nếu vi phạm, hệ thống từ chối lưu và đưa ra cảnh báo lỗi trực quan.

### Quy trình đăng ký đề tài, ghép nhóm và thẩm định đề cương (Cổng kiểm soát 1 — Tuần 0 đến tuần 2)


**Luồng lý tưởng:**

- GVHD soạn thảo đề tài mới (tên tiếng Việt/Anh, mục tiêu, yêu cầu công nghệ, chỉ tiêu tiếp nhận tối đa 1–2 sinh viên) gửi lên hệ thống.
- Khoa tiến hành thẩm định danh mục đề tài → Phê duyệt → Công bố công khai để sinh viên tra cứu.
- Sinh viên đủ điều kiện tra cứu đề tài, chọn đề tài mong muốn và tạo nhóm (tối đa 2 sinh viên). Từng thành viên hoặc đại diện nhóm đăng ký đề tài.
- Hệ thống kích hoạt giao tác CSDL và cơ chế khóa dữ liệu (Database Transaction + Locking/Updlock) để trừ chỉ tiêu trống của đề tài, ngăn chặn tình trạng nhiều nhóm cùng đăng ký một lúc và xuất phiếu tiếp nhận đăng ký dạng PDF.
- GVHD nhận thông báo, truy cập hệ thống và bấm xác nhận tiếp nhận nhóm sinh viên.
- Nhóm sinh viên cùng GVHD xây dựng đề cương chi tiết → Đại diện nhóm upload file đề cương (định dạng .pdf, .doc, .docx ≤20 MiB (20,971,520 byte)) lên hệ thống trước hạn chót Tuần 2.
- Văn phòng Khoa thẩm định đề cương (Cổng kiểm soát 1): Khoa đánh giá "Đạt" → Hệ thống tự động chuyển trạng thái của nhóm sang "Đang thực hiện KLTN" và mở khóa quyền truy cập các tính năng tiếp theo.

**Luồng phát sinh:**

- Khoa đánh giá "Yêu cầu chỉnh sửa": Khoa nhập ý kiến phản hồi → Hệ thống mở quyền cho phép sinh viên nộp lại phiên bản đề cương cập nhật trước hạn chót bổ sung.
- Khoa đánh giá "Không đạt": Hệ thống chuyển trạng thái nhóm sang "Dừng KLTN" và khóa toàn bộ quyền truy cập các tính năng thực hiện KLTN trong học kỳ.
- Xung đột chỉ tiêu đăng ký: Nhóm gửi yêu cầu sau khi chỉ tiêu đề tài đã hết sẽ bị Rollback giao tác và nhận thông báo lỗi đăng ký không thành công.

### Quy trình quản lý tiến độ theo mốc (Milestones, Nộp sản phẩm và Phản hồi)


**Luồng lý tưởng:**

- Sau khi đề cương được duyệt, GVHD truy cập phân hệ Quản lý tiến độ của nhóm để thiết lập các mốc công việc Milestones theo lộ trình thực hiện .
- Đến hạn chót của từng mốc, nhóm sinh viên tải lên báo cáo tiến độ, đường dẫn mã nguồn hoặc sản phẩm thử nghiệm kèm ghi chú tóm tắt kết quả.
- GVHD truy cập xem xét nội dung nộp, nhập nhận xét phản hồi trực tiếp trên hệ thống và chọn kết luận đánh giá (Đạt / Cần chỉnh sửa).
- Hệ thống ghi vết lịch sử trao đổi, đồng thời tự động gửi thông báo in-app và email thông báo kết quả đánh giá mốc cho sinh viên.

**Luồng phát sinh:**

- Yêu cầu nộp lại sau phản hồi: Nếu GVHD đánh giá "Cần chỉnh sửa", hệ thống mở cờ cho phép nhóm upload lại sản phẩm mốc (phiên bản v2, v3,…) kèm lưu vết các lần nộp.
- Nộp bài trễ hạn mốc: Hệ thống ghi nhận trạng thái nộp trễ và gắn cờ cảnh báo trên Dashboard để GVHD làm căn cứ trừ điểm chuyên cần/tiến độ.

### Quy trình báo cáo giữa kỳ và thẩm định điều kiện tiếp tục (Cổng kiểm soát 2 — Tuần 3 đến Tuần 10)


**Luồng lý tưởng:**

- Trong suốt giai đoạn Tuần 3 đến Tuần 10, nhóm sinh viên hoàn thiện nội dung theo các mốc và theo dõi tiến độ tổng thể trên Dashboard.
- Đến hạn chót Tuần 10, sinh viên upload tệp Báo cáo giữa kỳ lên hệ thống.
- Văn phòng Khoa thẩm định báo cáo giữa kỳ tại Cổng 2, nhập nhận xét và chọn `Approved`, `NeedRevision` hoặc `Rejected`.
- `Approved` giữ nhóm ở trạng thái thực hiện KLTN; `NeedRevision` mở quyền nộp phiên bản mới trước hạn chót cuối cùng; `Rejected` chuyển nhóm sang `Stopped`.
- Khi qua hạn chốt cuối cùng mà báo cáo chưa `Approved`, hệ thống tự động dừng nhóm và lưu audit trail.

**Luồng phát sinh:**

- Khoa chọn `NeedRevision`: Khoa nhập feedback → hệ thống mở quyền cho nhóm nộp phiên bản mới cho đến deadline chốt cuối cùng; lịch sử phiên bản và feedback cũ được giữ nguyên.
- Khoa chọn `Rejected`: Khoa nhập feedback → nhóm chuyển `Stopped` và các nghiệp vụ KLTN tiếp theo bị khóa.
- Quá deadline chốt cuối cùng: worker tự động dừng nhóm nếu báo cáo giữa kỳ chưa `Approved`; deadline lấy từ cấu hình đợt, không hard-code tuần hoặc ngày trong source.

### Luồng Đổi tên đề tài (ĐTĐT) liên thông 3 bên (Tuần 3 đến Tuần 10)


**Luồng lý tưởng:**

- Trong giai đoạn Tuần 3 đến Tuần 10, sinh viên có nhu cầu điều chỉnh tên đề tài tạo "Đơn xin Đổi tên đề tài" trực tuyến trên hệ thống (không cần nộp bản giấy).
- Sinh viên nhập tên đề tài mới đề xuất (tiếng Việt và tiếng Anh), nêu rõ lý do điều chỉnh và bấm gửi duyệt.
- Cấp 1 (GVHD): GVHD xem xét nội dung điều chỉnh → Bấm xác nhận "Đồng ý" → Đơn chuyển sang trạng thái "Chờ Khoa duyệt".
- Cấp 2 (Văn phòng Khoa): Khoa thẩm định tính hợp lệ của đề xuất → Bấm "Ký duyệt" → Đơn chuyển sang trạng thái "Chờ PĐT phê duyệt".
- Cấp 3 (Phòng Đào tạo): Phòng Đào tạo kiểm tra hồ sơ liên thông → Bấm "Phê duyệt chính thức" để ghi nhận quyết định hành chính trực tuyến.
- Sau khi Phòng Đào tạo duyệt: Hệ thống tự động cập nhật tên đề tài mới vào CSDL, ghi vết vào AuditLog, chuyển trạng thái đơn sang "Hoàn tất" và tự động kích hoạt quyền cho sinh viên nộp lại bản Đề cương chi tiết cập nhật.
- Hệ thống tự động gửi email và thông báo in-app kết quả đồng thời đến Sinh viên, GVHD và Khoa.

**Luồng phát sinh:**

- Đơn bị từ chối ở bất kỳ cấp nào (GVHD, Khoa, hoặc PĐT): Người thẩm định nhập lý do từ chối → Đơn dừng luồng duyệt, giữ nguyên tên đề tài ban đầu và thông báo lý do về cho sinh viên.
- Khởi tạo đơn ngoài thời gian quy định: Sau Tuần 10, hệ thống tự động khóa tính năng tạo đơn ĐTĐT.

### Quy trình Báo cáo cuối kỳ, Chấm điểm GVHD & GVPB và Xét duyệt điều kiện ra Hội đồng (Tuần 11 đến Tuần 18)


**Luồng lý tưởng:**

- Student đã Confirmed trong nhóm đang hoạt động, được Khoa/Admin Approved giữa kỳ, upload báo cáo cuối kỳ toàn văn PDF/DOC/DOCX ≤20 MiB (20,971,520 byte). Đợt phải Open và thời điểm nhận nằm trong FINAL_EVALUATION theo lịch/múi giờ cấu hình của đợt (mặc định Tuần 11–18), bao gồm đúng giờ mở và deadline.
- Bất kỳ thành viên đã Confirmed nào cũng được nộp lại trong hạn khi chưa bắt đầu chấm. Giữ nguyên mã/loại InitialFinal, tăng số phiên bản, giữ toàn bộ file/người nộp/thời điểm và audit cũ. GVHD hiện tại hoặc Admin bấm “Bắt đầu chấm” với số phiên bản đang xem; hệ thống chuyển Submitted → UnderEvaluation, khóa đúng bản và không cho nộp lại. Bắt đầu chấm có thể thực hiện sau deadline nộp.
- GVHD hiện được phân công hoặc Admin truy cập đọc đúng bản báo cáo đã khóa, nhập/sửa phiếu nhận xét và điểm hướng dẫn (ADVISOR) từ 0.0 đến 10.0. Điểm được làm tròn hai chữ số thập phân; trước khi duyệt, người có quyền được sửa phiếu theo phiên bản concurrency. Khi đồng ý chuyển phản biện, hệ thống khóa điểm, nhận xét và quyết định, chuyển nhóm sang sẵn sàng phân công GVPB; không có thu hồi trong phạm vi workflow này.
- Văn phòng Khoa phân công Cán bộ phản biện (GVPB) cho từng đề tài (đảm bảo GVPB không trùng GVHD).
- GVPB tải báo cáo cuối kỳ, nhập phiếu nhận xét phản biện, chấm điểm phản biện (REVIEWER) trực tiếp trên hệ thống và tích chọn kết luận: "Đồng ý cho bảo vệ".
- Cổng kiểm soát bảo vệ: Khi đề tài thỏa mãn đồng thời hai điều kiện (Điểm GVPB ≥5.0 VÀ GVPB chọn "Đồng ý cho bảo vệ"), hệ thống tự động gắn trạng thái đủ điều kiện ra Hội đồng bảo vệ.
- Bảo mật điểm số: Điểm và nhận xét của cả GVHD và GVPB được ẩn bảo mật đối với sinh viên cho đến khi khoa công bố kết quả chính thức; Thư ký Hội đồng được mở quyền xem phiếu điểm để lập hồ sơ bảo vệ.
- Sinh viên được mở quyền xem ý kiến góp ý chỉnh sửa của GVPB để cập nhật báo cáo nộp cho Hội đồng.

**Luồng phát sinh:**

- GVHD/Admin không đồng ý cho phản biện: phải ghi lý do; hệ thống giữ nguyên bản báo cáo đã khóa và chặn phân công GVPB. GVHD/Admin có thể sửa phiếu rồi xét lại. Hệ thống không tự dừng nhóm, không tự mở nộp lại, không tự gán điểm 0; gia hạn, mở khóa hoặc chuyển đợt do Khoa xử lý ở quy trình riêng.
- GVPB đánh giá điểm phản biện <5.0 hoặc chọn "Không đồng ý cho bảo vệ": Hệ thống chặn không cho đưa đề tài vào danh sách xếp lịch Hội đồng → Khoa kích hoạt quy trình xử lý không đồng thuận: tổ chức trao đổi giữa GVHD và GVPB, mời GVPB thứ 2 hoặc trình Trưởng Khoa ra quyết định cuối cùng theo quy chế.

### Quy trình thành lập hội đồng, tổ chức bảo vệ và khóa biên bản điểm (Tuần 19 đến Tuần 20)


**Luồng lý tưởng:**

- Văn phòng Khoa lập dự thảo danh sách các Hội đồng bảo vệ (Chủ tịch, Thư ký, Ủy viên, phòng thi, ca thi, gán các đề tài đủ điều kiện). Hệ thống tự động kiểm tra quy tắc: GVHD không được thuộc Hội đồng chấm đề tài mình hướng dẫn.
- Khoa chuyển dữ liệu dự thảo sang Phòng Đào tạo. Phòng Đào tạo thẩm định và tải lên Quyết định thành lập Hội đồng phê duyệt online.
- Hệ thống công bố lịch bảo vệ, phòng thi và thành viên Hội đồng cho sinh viên và GVHD tra cứu.
- Tại buổi bảo vệ (Tuần 20), từng thành viên Hội đồng đăng nhập tài khoản cá nhân, nhập điểm thành phần (thang điểm 0.0 – 10.0) dựa trên bảng tiêu chí chấm điểm và ý kiến đánh giá trực tiếp.
- Hệ thống tự động tính điểm trung bình Hội đồng bằng trung bình cộng điểm các thành viên Hội đồng hợp lệ.
- Thư ký Hội đồng ghi nhận biên bản bảo vệ điện tử (các yêu cầu chỉnh sửa) và nhấn nút "Khóa biên bản điểm" (có xác thực mật khẩu/OTP).
- Hệ thống chuyển toàn bộ điểm Hội đồng của đề tài sang trạng thái chỉ đọc, ngăn chặn mọi thao tác chỉnh sửa trái phép.

**Luồng phát sinh:**

- Mở khóa biên bản điểm khi có khiếu nại: Thành viên Hội đồng không thể tự sửa điểm đã khóa. Chỉ Lãnh đạo Khoa mới có quyền sử dụng chức năng "Cấp quyền Mở khóa đặc biệt" kèm lý do giải trình → Hệ thống ghi vết toàn bộ thao tác mở khóa và sửa điểm vào AuditLog.
- Điểm các thành viên lệch ≥2.0 điểm: Hệ thống kích hoạt cảnh báo trực tiếp trên màn hình chấm điểm để Chủ tịch Hội đồng tổ chức thảo luận thống nhất trước khi Thư ký bấm khóa biên bản.

### Quy trình Nghiệm thu nộp lưu chiểu và Hậu bảo vệ (Tuần 20 đến Tuần 21)


**Luồng lý tưởng:**

- Sau buổi bảo vệ, sinh viên nhận biên bản góp ý chỉnh sửa từ Thư ký Hội đồng.
- Sinh viên tiến hành chỉnh sửa báo cáo toàn văn → Upload hồ sơ lưu chiểu hoàn thiện lên hệ thống trước hạn chót quy định (gồm file .pdf, file .docx, đoạn tóm tắt nội dung và từ khóa tiếng Việt/Anh).
- GVHD truy cập kiểm tra bản sửa và xác nhận đạt yêu cầu chỉnh sửa trên hệ thống.
- Văn phòng Khoa kiểm tra tính hợp lệ của hồ sơ lưu chiểu → Bấm nút "Xác nhận hoàn thành nộp lưu chiểu".
- Hệ thống cập nhật trạng thái lưu chiểu thành công, kích hoạt điều kiện tiên quyết để chuyển đề tài sang bước trả điểm chính thức.

**Luồng phát sinh:**

- Chưa hoàn thành nộp lưu chiểu: Nếu chưa có xác nhận hoàn thành lưu chiểu từ Khoa, hệ thống khóa quyền tra cứu điểm chính thức của nhóm sinh viên đó dù tất cả các đầu điểm đã được chấm xong.
- Hồ sơ lưu chiểu chưa đạt: Khoa từ chối xác nhận và gửi thông báo yêu cầu sinh viên tiếp tục chỉnh sửa nộp lại trước hạn chót tổng hợp.

### Quy trình Tổng hợp điểm theo Trọng số động và Công bố kết quả (Tuần 21)


**Luồng lý tưởng:**

- Văn phòng Khoa truy cập phân hệ Điểm và bấm lệnh "Tổng hợp điểm đợt KLTN".
- Hệ thống kiểm tra điều kiện tiên quyết: Đề tài phải có đầy đủ các đầu điểm thành phần theo cấu hình động của đợt đã hoàn thành và nộp lưu chiếu, thông thường sẽ tuân theo các tiêu chí:
- Ý nghĩa khoa học, giá trị thực tiễn:
- + Tính mới và độ phức tạp
- + Tính thực tiễn và đóng góp
- Vận dụng kiến thức nền tảng và chuyên sâu ngành
- + Khả năng tìm hiểu, phân tích yêu cầu và thiết kế hệ thống
- + Khả năng hiện thức hóa, kiểm thử và vận hành hệ thống, đánh giá giải pháp
- + Học hỏi và cập nhật kiến thức, công nghệ mới, khả năng học tập suốt đời
- Trình bày kết quả:
- + Nội dung trình bày cuốn báo và sản phẩm (viết)
- + Kỹ năng trình bày (nói)
- + Trả lời các câu hỏi
- Kỹ năng mềm
- + Lập kế hoạch và quản lý thời gian, công việc của cá nhân hoặc đội nhóm
- + Kỹ năng tìm hiểu, đọc tài liệu ngoại ngữ và khả năng tư duy, giải quyết vấn đề
- Hệ thống áp dụng lấy bộ tham số cấu hình trọng số/hệ số động của đợt để tự động tính Điểm tổng kết:

`FinalScore = sum(score_i * C_i) / sum(C_i)`.

- Nếu Giáo viên phản biện là ủy viên trong hội đồng thì hệ số của của điểm tồng kết:

`FinalScore = (2 * GVHD + 2 * GVPB + Chủ_tịch + Thư_ký) / 6`.

- Nếu giáo viên phản biện không phải là ủy viên trong hội đồng thì hệ số của điểm tồng kết:

`FinalScore = (2 * GVHD + 2 * GVPB + Chủ_tịch + Thư_ký + Ủy_viên) / 7`.

- Bảng điểm tổng kết hiển thị ở trạng thái “Dự thảo" để Văn phòng Khoa rà soát.
- Khoa nhấn nút "Công bố kết quả": Hệ thống chuyển trạng thái sang phát hành, giải mã và hiển thị công khai bảng điểm chi tiết (từng đầu điểm thành phần và điểm tổng kết) trên Dashboard của sinh viên.
- Hệ thống tự động gửi email thông báo kết quả điểm số cho sinh viên.
- Khoa và Phòng Đào tạo thực hiện chức năng kết xuất Bảng điểm tổng hợp KLTN toàn trường ra file định dạng Excel (.xlsx) hoặc PDF phục vụ công tác xét tốt nghiệp chính thức.

**Luồng phát sinh:**

- Loại trừ điểm sai biệt ≥2.0 điểm: Nếu có thành viên chấm điểm lệch mà Hội đồng không công nhận, hệ thống hỗ trợ bật cờ IsIgnoredInCalculation = True để tự động loại trừ điểm đó ra khỏi công thức tính điểm trung bình theo quy chế.

## 4. Danh mục yêu cầu chức năng

Mã FR được giữ theo báo cáo để truy vết với Use Case và kiểm thử.

### Phân hệ Quản trị hệ thống, Tài khoản và Cấu hình


| Mã FR | Tên nghiệp vụ | Chi tiết yêu cầu chức năng | Vai trò tác động |
| --- | --- | --- | --- |
| FR-SYS-01 | Xác thực và quản lý tài khoản cá nhân | Hệ thống cho phép người dùng đăng nhập bằng email trường và mật khẩu được quản lý nội bộ. Mật khẩu được lưu trữ dưới dạng băm bảo mật (Password Hashing). Sau khi xác thực thành công, hệ thống cấp JWT để xác định danh tính và phục vụ kiểm soát quyền truy cập. Cho phép người dùng thay đổi mật khẩu bằng cách xác thực mật khẩu hiện tại và nhập mật khẩu mới. | Tất cả vai trò |
| FR-SYS-02 | Khởi tạo và cấu hình đợt KLTN | Cho phép tạo đợt KLTN theo năm học và học kỳ với chu kỳ cố định 21 tuần; thiết lập các giai đoạn và cấu hình thời hạn nộp, xét duyệt tương ứng trong từng giai đoạn. | Admin, PĐT |
| FR-SYS-03 | Thiết lập phương thức tính điểm động | Cho phép lựa chọn phương thức tính điểm tổng kết của từng đợt KLTN theo tỷ lệ trọng số phần trăm hoặc trung bình cộng có hệ số nguyên. Các tham số được thiết lập thông qua giao diện cấu hình mà không cần thay đổi mã nguồn. | PĐT, Admin |
| FR-SYS-04 | Tra cứu nhật ký hệ thống | Cho phép tìm kiếm, xem và lọc AuditLog theo tài khoản người dùng, loại thao tác và khoảng thời gian. Chỉ Admin được phép truy cập chức năng này. | Admin |
| FR-SYS-05 | Quản lý tài khoản và phân quyền RBAC | Cho phép Admin tạo mới, cập nhật, vô hiệu hóa tài khoản và gán vai trò người dùng theo 7 nhóm: Sinh viên, GVHD, GVPB, Khoa, PĐT, Hội đồng và Admin. Hệ thống áp dụng RBAC kết hợp Policy-based Authorization để kiểm soát quyền thao tác theo chức danh và ngữ cảnh nghiệp vụ. | Admin |
| FR-SYS-06 | Import danh sách sinh viên và giảng viên | Cho phép tải lên tệp Excel chứa danh sách sinh viên đủ điều kiện thực hiện KLTN và danh sách giảng viên để phục vụ quản lý, phân công và đăng ký trong từng đợt. | Admin, PĐT |
| FR-SYS-07 | Quản lý danh mục đầu điểm thành phần | Cho phép thiết lập danh mục đầu điểm của từng đợt KLTN, bao gồm điểm GVHD (ADVISOR), điểm GVPB (REVIEWER), điểm Hội đồng (COUNCIL) và các đầu điểm tùy chọn khác theo cấu hình. | PĐT, Admin |
| FR-SYS-08 | Cấu hình trọng số và hệ số điểm | Cho phép gán, cập nhật tỷ lệ phần trăm hoặc hệ số nguyên cho từng đầu điểm thành phần. Hỗ trợ điều chỉnh tham số tính điểm trong quá trình thực hiện đợt KLTN theo quyền được cấp. | PĐT, Admin |
| FR-SYS-09 | Cấu hình điều kiện tiên quyết của đầu điểm | Cho phép thiết lập các điều kiện bắt buộc đối với từng đầu điểm, chẳng hạn ngưỡng điểm phản biện tối thiểu để xét điều kiện ra Hội đồng bảo vệ. Các điều kiện này được lưu theo cấu hình của từng đợt KLTN. | PĐT, Admin |
| FR-SYS-10 | Kiểm tra tính hợp lệ của cấu hình điểm | Hệ thống tự động kiểm tra các tham số trước khi lưu. Với phương thức tỷ lệ phần trăm, tổng trọng số phải bằng 100%; với phương thức hệ số nguyên, tổng hệ số phải lớn hơn 0. Nếu không hợp lệ, hệ thống từ chối lưu và hiển thị thông báo lỗi. | Hệ thống (tự động) |
| FR-SYS-11 | Kiểm soát quyền theo chính sách nghiệp vụ | Hệ thống kiểm tra quyền thao tác dựa trên vai trò RBAC kết hợp các chính sách đã xác định tại mục 1.3.2, bao gồm chức danh được phân công, phạm vi đề tài, giai đoạn, trạng thái hồ sơ và các quyền đặc biệt. Từ chối thao tác khi không thỏa mãn điều kiện tương ứng. | Hệ thống (tự động) |
| FR-SYS-12 | Tự động ghi nhận AuditLog | Hệ thống ghi nhận nhật ký đối với các thao tác quan trọng như thay đổi phân quyền, cập nhật cấu hình, phê duyệt hồ sơ, mở khóa biên bản, điều chỉnh và loại trừ điểm. Nhật ký lưu thông tin người thực hiện, thời điểm và nội dung thao tác để phục vụ kiểm tra. | Hệ thống (tự động) |
| FR-SYS-13 | Tra cứu thông tin đợt KLTN | Cho phép người dùng tra cứu thông tin đợt KLTN, các giai đoạn, mốc thời gian và thời hạn thực hiện phù hợp với phạm vi quyền truy cập của mình. | Tất cả vai trò |

### 2.1.2. Phân hệ Đề tài và Đăng ký


| Mã FR | Tên nghiệp vụ | Chi tiết yêu cầu chức năng | Vai trò tác động |
| --- | --- | --- | --- |
| FR-TOPIC-01 | Soạn thảo và đề xuất đề tài | Cho phép GVHD tạo mới, chỉnh sửa đề tài trước khi công bố, bao gồm tên đề tài tiếng Việt/Anh, mục tiêu, yêu cầu công nghệ và số lượng sinh viên tiếp nhận tối đa từ 1 đến 2 sinh viên. GVHD có thể gửi đề tài đến Văn phòng Khoa để thẩm định. | GVHD |
| FR-TOPIC-02 | Tra cứu và đăng ký đề tài | Cho phép sinh viên đủ điều kiện thực hiện KLTN tìm kiếm, lọc và xem danh sách đề tài đã được Khoa công bố theo tên đề tài, GVHD và các thông tin liên quan. Sinh viên có thể lựa chọn, đăng ký đề tài trực tuyến theo hình thức cá nhân hoặc nhóm trong thời gian đăng ký hợp lệ. | Sinh viên |
| FR-TOPIC-03 | Thẩm định và công bố đề tài | Cho phép Văn phòng Khoa tiếp nhận danh sách đề tài do GVHD đề xuất, xem xét nội dung và đưa ra kết luận Đạt, Yêu cầu chỉnh sửa hoặc Không đạt. Các đề tài được phê duyệt sẽ được công bố để sinh viên tra cứu và đăng ký. | Khoa |
| FR-TOPIC-04 | Tạo và quản lý nhóm sinh viên | Cho phép sinh viên tạo nhóm thực hiện KLTN với số lượng tối đa 2 thành viên. Hệ thống kiểm tra số lượng thành viên và ràng buộc nhóm phù hợp với chỉ tiêu tiếp nhận của đề tài trước khi đăng ký. | Sinh viên, Hệ thống |
| FR-TOPIC-05 | Kiểm tra điều kiện đăng ký | Hệ thống kiểm tra điều kiện tham gia KLTN của sinh viên, trạng thái công bố đề tài, thời hạn đăng ký và số lượng chỉ tiêu còn trống. Những yêu cầu không đáp ứng điều kiện sẽ bị từ chối và hiển thị thông báo tương ứng. | Hệ thống |
| FR-TOPIC-06 | Kiểm soát chỉ tiêu và xử lý đăng ký đồng thời | Hệ thống sử dụng Database Transaction kết hợp cơ chế khóa dữ liệu để bảo đảm không vượt quá chỉ tiêu tiếp nhận của đề tài khi nhiều nhóm đăng ký đồng thời. Nếu chỉ tiêu đã hết, hệ thống thực hiện Rollback giao tác và thông báo đăng ký không thành công. | Hệ thống |
| FR-TOPIC-07 | Tạo phiếu tiếp nhận đăng ký đề tài | Sau khi đăng ký được ghi nhận thành công, hệ thống tự động tạo phiếu tiếp nhận đăng ký đề tài dưới định dạng PDF, thể hiện thông tin đề tài, sinh viên và nhóm đăng ký. | Sinh viên, Hệ thống |
| FR-TOPIC-08 | Xác nhận tiếp nhận nhóm sinh viên | Sau khi sinh viên đăng ký đề tài thành công, hệ thống thông báo đến GVHD phụ trách. GVHD truy cập danh sách đăng ký và thực hiện xác nhận tiếp nhận nhóm sinh viên theo đề tài tương ứng. | GVHD |
| FR-TOPIC-09 | Thông báo kết quả đăng ký | Hệ thống tự động thông báo kết quả đăng ký đề tài cho sinh viên, đồng thời gửi thông báo đến GVHD khi có yêu cầu đăng ký cần xác nhận. | Hệ thống |

### 2.1.3. Phân hệ Đề cương và Tiến độ giữa kỳ


| Mã FR | Tên nghiệp vụ | Chi tiết yêu cầu chức năng | Vai trò tác động |
| --- | --- | --- | --- |
| FR-REG-01 | Nộp đề cương chi tiết | Cho phép đại diện nhóm sinh viên tải lên tệp đề cương chi tiết định dạng PDF, DOC hoặc DOCX với dung lượng tối đa 20 MiB (20,971,520 byte) trước hạn chót Tuần 2. GVHD được phép truy cập và xem đề cương của nhóm mình hướng dẫn. | Sinh viên, GVHD |
| FR-REG-02 | Thẩm định đề cương – Cổng kiểm soát 1 | Cho phép Văn phòng Khoa xem xét đề cương đã nộp, nhập nhận xét và đưa ra một trong ba kết luận: Đạt, Yêu cầu chỉnh sửa hoặc Không đạt. Hệ thống ghi nhận kết quả thẩm định và thông báo đến nhóm sinh viên. | Khoa |
| FR-REG-03 | Chỉnh sửa và nộp lại đề cương | Khi Khoa đánh giá "Yêu cầu chỉnh sửa", hệ thống cho phép nhóm sinh viên tải lên phiên bản đề cương cập nhật trước thời hạn bổ sung. Các phiên bản và kết quả thẩm định được lưu lại để phục vụ tra cứu. | Sinh viên, Hệ thống |
| FR-REG-04 | Cập nhật trạng thái sau thẩm định đề cương | Hệ thống tự động chuyển trạng thái nhóm sang "Đang thực hiện KLTN" và mở quyền truy cập các nghiệp vụ tiếp theo khi đề cương đạt yêu cầu. Nếu kết quả "Không đạt", hệ thống chuyển trạng thái sang "Dừng KLTN" và khóa quyền thực hiện các nghiệp vụ KLTN tiếp theo trong học kỳ. | Hệ thống (tự động) |
| FR-MILE-01 | Thiết lập và quản lý Milestones | Sau khi đề cương được phê duyệt, cho phép GVHD thiết lập các mốc công việc (Milestones) theo lộ trình thực hiện KLTN của từng nhóm, bao gồm nội dung công việc và thời hạn hoàn thành tương ứng. | GVHD |
| FR-MILE-02 | Nộp sản phẩm theo Milestones | Cho phép nhóm sinh viên tải lên báo cáo tiến độ, cung cấp đường dẫn mã nguồn hoặc sản phẩm thử nghiệm và bổ sung ghi chú tóm tắt kết quả thực hiện cho từng Milestone. Hệ thống ghi nhận thời điểm và nội dung của mỗi lần nộp. | Sinh viên |
| FR-MILE-03 | Đánh giá và phản hồi Milestones | Cho phép GVHD truy cập sản phẩm đã nộp theo từng Milestone, nhập nhận xét và đưa ra kết luận "Đạt" hoặc "Cần chỉnh sửa". Kết quả đánh giá được lưu trên hệ thống để nhóm sinh viên theo dõi. | GVHD |
| FR-MILE-04 | Chỉnh sửa và nộp lại sản phẩm Milestones | Khi GVHD đánh giá "Cần chỉnh sửa", hệ thống mở quyền cho phép nhóm sinh viên nộp lại sản phẩm của Milestone tương ứng. Các phiên bản nộp lại (v2, v3,...) và lịch sử phản hồi được lưu trữ để theo dõi tiến trình thực hiện. | Sinh viên, Hệ thống |
| FR-MILE-05 | Theo dõi và cảnh báo tiến độ Milestones | Hệ thống theo dõi thời hạn hoàn thành của từng Milestone, tự động ghi nhận trạng thái nộp trễ và hiển thị cảnh báo trên Dashboard. GVHD có thể sử dụng thông tin nộp trễ làm căn cứ đánh giá chuyên cần và tiến độ thực hiện của nhóm. | Sinh viên, GVHD, Hệ thống |
| FR-MILE-06 | Thông báo kết quả đánh giá Milestones | Hệ thống tự động gửi thông báo trong ứng dụng (in-app) và email đến sinh viên khi GVHD hoàn thành đánh giá Milestone. Đồng thời lưu vết lịch sử trao đổi và phản hồi giữa GVHD với nhóm sinh viên. | Hệ thống (tự động) |
| FR-MID-01 | Nộp và thẩm định báo cáo giữa kỳ | Cho phép mọi thành viên nhóm tải lên báo cáo giữa kỳ trước deadline cấu hình. Văn phòng Khoa hoặc Admin thẩm định từng phiên bản với `Approved`, `NeedRevision` hoặc `Rejected`; feedback bắt buộc cho hai kết quả không đạt. Phiên bản nộp và kết quả thẩm định được lưu lịch sử. | Sinh viên, Khoa, Admin |
| FR-MID-02 | Tạo đơn xin đổi tên đề tài | Cho phép sinh viên tạo và gửi đơn xin đổi tên đề tài trực tuyến trong giai đoạn Tuần 3 đến Tuần 10. Đơn phải có tên đề tài mới bằng tiếng Việt, tiếng Anh và lý do điều chỉnh. Hệ thống không cho phép tạo đơn sau Tuần 10. | Sinh viên |
| FR-MID-03 | Tổng hợp kết quả giữa kỳ | Cho phép Khoa tổng hợp các nhóm đã `Approved`, đang `NeedRevision` hoặc bị `Rejected` tại Cổng 2 và chuyển dữ liệu cần thiết sang PĐT theo dõi. | Khoa, PĐT |
| FR-MID-04 | Xử lý kết quả không đạt giữa kỳ | Khi Khoa chọn `Rejected`, hệ thống yêu cầu feedback, tự động chuyển nhóm sang `Stopped` và khóa quyền truy cập các giai đoạn tiếp theo. Khi Khoa chọn `NeedRevision`, hệ thống chỉ mở quyền nộp phiên bản mới đến deadline chốt cuối cùng. | Khoa, Hệ thống |
| FR-MID-05 | Kiểm soát deadline Cổng 2 | Hệ thống kiểm tra ba deadline cấu hình của Cổng 2. Qua hạn chốt cuối cùng, worker dừng nhóm chưa được `Approved`; không hard-code tuần hoặc ngày. | Hệ thống (tự động) |
| FR-MID-06 | Phê duyệt đơn đổi tên đề tài liên thông ba cấp | Cho phép xử lý đơn đổi tên đề tài lần lượt qua ba cấp: GVHD xác nhận cấp 1, Văn phòng Khoa thẩm định và ký duyệt cấp 2, Phòng Đào tạo phê duyệt chính thức cấp 3. Hệ thống quản lý trạng thái đơn và chỉ chuyển sang cấp tiếp theo khi cấp trước đã đồng ý. | GVHD, Khoa, PĐT |
| FR-MID-07 | Từ chối đơn đổi tên đề tài | Cho phép người thẩm định tại bất kỳ cấp nào từ chối đơn đổi tên đề tài và nhập lý do từ chối. Hệ thống dừng quy trình phê duyệt, giữ nguyên tên đề tài hiện tại và thông báo kết quả cùng lý do cho sinh viên. | GVHD, Khoa, PĐT |
| FR-MID-08 | Cập nhật đề tài sau phê duyệt đổi tên | Sau khi Phòng Đào tạo phê duyệt chính thức, hệ thống tự động cập nhật tên đề tài mới trong cơ sở dữ liệu, chuyển trạng thái đơn sang "Hoàn tất", ghi nhận lịch sử thay đổi vào AuditLog và mở quyền cho sinh viên nộp lại bản đề cương chi tiết cập nhật. | Hệ thống (tự động), Sinh viên |
| FR-MID-09 | Thông báo kết quả đổi tên đề tài | Hệ thống tự động gửi email và thông báo in-app về kết quả xử lý đơn đổi tên đề tài đến sinh viên, GVHD và Văn phòng Khoa sau khi hoàn tất phê duyệt. Trường hợp đơn bị từ chối, hệ thống thông báo kết quả và lý do cho sinh viên. | Hệ thống (tự động) |

### 2.1.4. Phân hệ Báo cáo cuối kỳ, Phản biện và Hội đồng


| Mã FR | Tên nghiệp vụ | Chi tiết yêu cầu chức năng | Vai trò tác động |
| --- | --- | --- | --- |
| FR-FIN-01 | Nộp báo cáo cuối kỳ và chấm điểm GVHD | Student đã Confirmed trong nhóm được duyệt giữa kỳ được nộp PDF/DOC/DOCX ≤20 MiB trong FINAL_EVALUATION của đợt Open (mặc định Tuần 11–18); nộp lại trước khi bắt đầu chấm, giữ lịch sử. GVHD hiện tại/Admin bắt đầu chấm để khóa đúng phiên bản. GVHD/Admin lưu phiếu nhận xét và điểm ADVISOR 0–10, làm tròn hai chữ số thập phân; được sửa đến trước khi đồng ý chuyển phản biện. SCRUM-102 triển khai phần nộp/lịch sử/tải/khóa phiên bản; SCRUM-103 triển khai phiếu điểm/duyệt. | Sinh viên, GVHD, Admin |
| FR-FIN-02 | Phân công và chấm điểm phản biện | Cho phép Văn phòng Khoa phân công GVPB cho từng đề tài. GVPB được phân công có quyền truy cập báo cáo cuối kỳ, nhập phiếu nhận xét, chấm điểm phản biện (REVIEWER) và đưa ra kết luận "Đồng ý cho bảo vệ" hoặc "Không đồng ý cho bảo vệ". | Khoa, GVPB |
| FR-FIN-03 | Xác nhận chuyển sang phản biện | GVHD hiện tại hoặc Admin quyết định sau khi đã có phiếu cho đúng bản báo cáo bị khóa. Không đồng ý phải có lý do, giữ nhóm ở `FinalReportSubmitted`, chặn phân công GVPB và cho phép xét lại. Đồng ý là quyết định cuối: khóa phiếu và chuyển nhóm sang `ReadyForReviewerAssignment`; mọi thao tác sau đó bị chặn. Audit ghi actor thực tế, kể cả Admin thao tác thay GVHD. | GVHD, Admin, Hệ thống |
| FR-FIN-04 | Kiểm tra điều kiện phân công GVPB | Hệ thống cung cấp contract kiểm tra lại ngay trước phân công: chỉ trả điều kiện khi phiếu GVHD đã đồng ý và khóa, nhóm `ReadyForReviewerAssignment`, báo cáo `InitialFinal`/phiên bản/file/checksum vẫn khớp bản đã khóa. SCRUM-104 tiếp tục kiểm tra GVPB không trùng GVHD hiện tại và quyền phân công. | Hệ thống (tự động) |
| FR-FIN-05 | Xét duyệt điều kiện ra Hội đồng bảo vệ | Hệ thống tự động kiểm tra điều kiện ra Hội đồng sau khi hoàn tất phản biện, bao gồm: GVHD đã đồng ý chuyển sang phản biện, điểm GVPB đáp ứng ngưỡng tối thiểu theo cấu hình của đợt và GVPB chọn "Đồng ý cho bảo vệ". Chỉ những đề tài thỏa mãn đầy đủ điều kiện mới được chuyển sang trạng thái đủ điều kiện bảo vệ. | Hệ thống (tự động) |
| FR-FIN-06 | Xử lý kết quả phản biện không đồng thuận | Khi điểm GVPB không đạt ngưỡng quy định hoặc GVPB không đồng ý cho bảo vệ, hệ thống chặn đề tài khỏi danh sách xếp lịch Hội đồng. Văn phòng Khoa được phép ghi nhận quá trình xử lý không đồng thuận thông qua việc trao đổi giữa GVHD và GVPB, phân công GVPB thứ hai hoặc trình Trưởng Khoa xem xét quyết định cuối cùng theo quy chế. | Khoa, GVHD, GVPB |
| FR-FIN-07 | Bảo mật điểm số và phiếu nhận xét | Phiếu GVHD của SCRUM-103 chỉ cho GVHD hiện được phân công hoặc Admin đọc/ghi. Student, Faculty chỉ có quyền xem báo cáo theo contract SCRUM-102, GVHD cũ và người khác không được suy ra quyền xem phiếu; audit quyết định không chứa nội dung nhận xét/lý do từ chối. Công bố điểm và quyền xem phiếu của Hội đồng thực hiện theo workflow riêng. | Hệ thống (tự động), Hội đồng |
| FR-FIN-08 | Xem góp ý và nộp báo cáo chỉnh sửa sau phản biện | Cho phép sinh viên truy cập các ý kiến góp ý chỉnh sửa của GVPB trước khi công bố điểm và tải lên phiên bản báo cáo đã chỉnh sửa để phục vụ Hội đồng bảo vệ. Hệ thống lưu lại các phiên bản báo cáo được nộp. | Sinh viên, GVPB |
| FR-COU-01 | Thành lập và phê duyệt Hội đồng bảo vệ | Cho phép Văn phòng Khoa lập dự thảo Hội đồng bảo vệ, phân công Chủ tịch, Thư ký, Ủy viên, phòng thi, ca thi và danh sách đề tài đủ điều kiện bảo vệ. Phòng Đào tạo xem xét, phê duyệt và tải lên Quyết định thành lập Hội đồng chính thức. | Khoa, PĐT |
| FR-COU-02 | Chấm điểm Hội đồng và khóa biên bản | Cho phép từng thành viên Hội đồng được phân công nhập điểm thành phần theo thang điểm từ 0.0 đến 10.0 và ý kiến đánh giá trực tiếp trên hệ thống. Thư ký Hội đồng ghi nhận biên bản bảo vệ điện tử và thực hiện thao tác "Khóa biên bản điểm" sau khi xác thực bằng mật khẩu hoặc OTP. Khi biên bản được khóa, toàn bộ điểm Hội đồng chuyển sang trạng thái chỉ đọc. | Hội đồng |
| FR-COU-03 | Nộp hồ sơ lưu chiểu sau bảo vệ | Cho phép sinh viên tải lên hồ sơ lưu chiểu hoàn thiện sau khi chỉnh sửa theo góp ý của Hội đồng, bao gồm tệp PDF, DOCX, đoạn tóm tắt nội dung và từ khóa bằng tiếng Việt và tiếng Anh. Hệ thống ghi nhận thời điểm nộp và thông tin hồ sơ tương ứng. | Sinh viên |
| FR-COU-04 | Kiểm tra ràng buộc thành viên Hội đồng | Hệ thống tự động kiểm tra danh sách thành viên khi Khoa thành lập Hội đồng và phân công đề tài, bảo đảm GVHD không được tham gia Hội đồng chấm đề tài do chính mình hướng dẫn. Nếu vi phạm, hệ thống từ chối phân công và hiển thị cảnh báo. | Hệ thống (tự động) |
| FR-COU-05 | Công bố và tra cứu lịch bảo vệ | Sau khi Hội đồng được Phòng Đào tạo phê duyệt, hệ thống công bố lịch bảo vệ, bao gồm thời gian, phòng thi, danh sách đề tài và thành viên Hội đồng. Cho phép sinh viên và GVHD tra cứu thông tin lịch bảo vệ tương ứng. | Sinh viên, GVHD, Hệ thống |
| FR-COU-06 | Tính điểm trung bình Hội đồng | Hệ thống tự động tính điểm Hội đồng bằng trung bình cộng các điểm thành viên hợp lệ. Chỉ các điểm được Hội đồng công nhận mới được đưa vào phép tính, đồng thời bảo đảm kết quả được cập nhật khi có thay đổi điểm hợp lệ. | Hệ thống (tự động) |
| FR-COU-07 | Cảnh báo điểm chấm lệch giữa các thành viên | Hệ thống hỗ trợ phát hiện và hiển thị cảnh báo khi điểm đánh giá của các thành viên Hội đồng có sự chênh lệch cần xem xét. Chủ tịch Hội đồng tiếp nhận cảnh báo và tổ chức thảo luận để thống nhất kết quả trước khi Thư ký khóa biên bản điểm. | Hội đồng, Hệ thống |
| FR-COU-08 | Ghi nhận và xử lý điểm sai biệt không được công nhận | Khi điểm của thành viên Hội đồng, CBHD hoặc CBPB chênh lệch từ 2 điểm trở lên so với điểm tổng kết, hệ thống cảnh báo để Hội đồng tổ chức thảo luận. Nếu sau thảo luận điểm sai biệt không thay đổi và Hội đồng quyết định không công nhận, Chủ tịch xác nhận kết luận và Thư ký ghi nhận vào biên bản, đánh dấu điểm tương ứng không được công nhận. Hệ thống loại trừ những điểm này khỏi phép tính điểm tổng kết theo quy định. | Hội đồng, Hệ thống |
| FR-COU-09 | Cấp quyền mở khóa biên bản đặc biệt | Cho phép Lãnh đạo Khoa cấp quyền mở khóa biên bản điểm khi có khiếu nại hợp lệ. Việc cấp quyền yêu cầu ghi rõ lý do giải trình và được hệ thống tự động lưu vào AuditLog. Các thành viên Hội đồng không được tự ý mở khóa biên bản. | Khoa (Lãnh đạo Khoa) |
| FR-COU-10 | Chỉnh sửa điểm cá nhân sau khi mở khóa | Sau khi Lãnh đạo Khoa cấp quyền mở khóa hợp lệ, hệ thống chỉ cho phép giảng viên Hội đồng đã nhập điểm được chỉnh sửa điểm do chính mình nhập. Không cho phép thay đổi điểm của thành viên khác. Mọi thao tác chỉnh sửa được tự động ghi nhận vào AuditLog. | Hội đồng, Hệ thống |
| FR-COU-11 | Ghi nhận và cung cấp biên bản góp ý sau bảo vệ | Cho phép Thư ký Hội đồng ghi nhận các yêu cầu chỉnh sửa báo cáo trong biên bản bảo vệ điện tử. Sau buổi bảo vệ, sinh viên được phép truy cập nội dung góp ý để hoàn thiện báo cáo trước khi nộp lưu chiểu. | Hội đồng, Sinh viên |
| FR-COU-12 | Xác nhận bản chỉnh sửa sau bảo vệ | Cho phép GVHD truy cập hồ sơ lưu chiểu do sinh viên nộp, kiểm tra nội dung báo cáo đã chỉnh sửa theo góp ý của Hội đồng và xác nhận đạt yêu cầu chỉnh sửa trước khi Văn phòng Khoa nghiệm thu lưu chiểu. | GVHD |
| FR-COU-13 | Kiểm tra và xác nhận hoàn thành lưu chiểu | Cho phép Văn phòng Khoa kiểm tra tính hợp lệ của hồ sơ lưu chiểu sau khi GVHD xác nhận bản chỉnh sửa, sau đó lựa chọn "Xác nhận hoàn thành nộp lưu chiểu" hoặc từ chối xác nhận kèm yêu cầu chỉnh sửa. Nếu được xác nhận, hệ thống cập nhật trạng thái hoàn thành lưu chiểu; nếu chưa đạt, sinh viên được yêu cầu tiếp tục chỉnh sửa và nộp lại trước hạn chót. | Khoa, Hệ thống |
| FR-COU-14 | Kiểm soát điều kiện tra cứu điểm sau lưu chiểu | Hệ thống kiểm tra trạng thái lưu chiểu trước khi cho phép sinh viên tra cứu điểm chính thức. Nếu hồ sơ chưa được Văn phòng Khoa xác nhận hoàn thành, hệ thống khóa quyền tra cứu điểm chính thức dù các đầu điểm thành phần đã được chấm đầy đủ. | Hệ thống (tự động) |

### 2.1.5. Phân hệ Điểm, Báo cáo


| Mã FR | Tên nghiệp vụ | Chi tiết yêu cầu chức năng | Vai trò tác động |
| --- | --- | --- | --- |
| FR-RES-01 | Tổng hợp điểm KLTN | Cho phép Văn phòng Khoa kích hoạt chức năng "Tổng hợp điểm đợt KLTN". Hệ thống tự động thu thập các đầu điểm thành phần theo cấu hình của đợt, bao gồm điểm GVHD (ADVISOR), điểm GVPB (REVIEWER), điểm Hội đồng (COUNCIL) và các đầu điểm tùy chọn khác để phục vụ tính điểm tổng kết. | Khoa, Hệ thống |
| FR-RES-02 | Kiểm tra điều kiện tổng hợp điểm | Trước khi tính điểm tổng kết, hệ thống tự động kiểm tra đề tài đã có đầy đủ các đầu điểm bắt buộc theo cấu hình của đợt và hồ sơ lưu chiểu đã được Văn phòng Khoa xác nhận hoàn thành. Nếu chưa đáp ứng điều kiện, hệ thống không thực hiện tổng hợp điểm chính thức cho đề tài và hiển thị thông tin chưa hoàn thành. | Hệ thống (tự động) |
| FR-RES-03 | Tính điểm tổng kết theo cấu hình động | Hệ thống tự động tính điểm tổng kết dựa trên phương thức tính điểm và bộ trọng số/hệ số đã được cấu hình cho từng đợt KLTN. Hỗ trợ phương thức tính theo tỷ lệ phần trăm hoặc trung bình cộng có hệ số nguyên, đồng thời áp dụng quy tắc tương ứng đối với trường hợp GVPB là hoặc không là thành viên Hội đồng bảo vệ. | Hệ thống (tự động) |
| FR-RES-04 | Loại trừ và tính lại điểm sai biệt | Hệ thống tiếp nhận kết quả đánh dấu điểm không được công nhận từ quy trình xử lý điểm sai biệt của Hội đồng. Những điểm được xác nhận không công nhận sẽ được gắn cờ IsIgnoredInCalculation = True và loại khỏi phép tính điểm tổng kết theo quy chế. Hệ thống thực hiện tính lại kết quả dựa trên các điểm hợp lệ và cấu hình tính điểm tương ứng. | Hội đồng, Hệ thống |
| FR-RES-05 | Rà soát bảng điểm dự thảo | Sau khi tính toán, hệ thống lưu và hiển thị bảng điểm tổng kết ở trạng thái "Dự thảo". Văn phòng Khoa được phép truy cập bảng điểm, kiểm tra các đầu điểm thành phần, kết quả tính toán và tình trạng hoàn thành điều kiện nghiệp vụ trước khi thực hiện công bố. | Khoa |
| FR-RES-06 | Công bố kết quả KLTN | Cho phép Văn phòng Khoa thực hiện chức năng "Công bố kết quả" sau khi rà soát bảng điểm dự thảo. Hệ thống cập nhật trạng thái kết quả sang "Đã công bố" và mở quyền tra cứu bảng điểm chính thức cho những nhóm sinh viên đã hoàn thành điều kiện lưu chiểu theo quy định. | Khoa, Hệ thống |
| FR-RES-07 | Tra cứu bảng điểm chính thức | Cho phép sinh viên tra cứu bảng điểm chi tiết của nhóm, bao gồm các đầu điểm thành phần và điểm tổng kết sau khi Khoa công bố kết quả. Hệ thống chỉ cho phép truy cập khi hồ sơ lưu chiểu đã được xác nhận hoàn thành và kết quả đã được công bố chính thức. | Sinh viên |
| FR-RES-08 | Thông báo kết quả điểm số | Sau khi Văn phòng Khoa công bố kết quả, hệ thống tự động gửi email thông báo kết quả KLTN đến sinh viên. Quyền xem điểm chi tiết trên Dashboard vẫn phải đáp ứng điều kiện công bố và hoàn thành lưu chiểu. | Hệ thống (tự động) |
| FR-NOTI-01 | Thông báo và nhắc nhở thời hạn | Hệ thống tự động gửi thông báo qua email về các mốc thời gian quan trọng trong quy trình KLTN, bao gồm thời hạn nộp đề cương, báo cáo giữa kỳ, báo cáo cuối kỳ và lưu chiểu. Đồng thời hỗ trợ thông báo kết quả đánh giá, phê duyệt hồ sơ và các sự kiện nghiệp vụ tương ứng đến người dùng liên quan. | Hệ thống (tự động) |
| FR-STU-01 | Dashboard theo dõi tiến độ KLTN | Cho phép sinh viên và GVHD theo dõi trạng thái thực hiện KLTN theo 7 giai đoạn, các mốc thời gian, thời hạn còn lại, tiến độ Milestones và cảnh báo nộp trễ. Thông tin được hiển thị theo nhóm và đề tài thuộc phạm vi quản lý của người dùng. | Sinh viên, GVHD |
| FR-AA-01 | Xuất báo cáo kết quả KLTN toàn trường | Cho phép Văn phòng Khoa và Phòng Đào tạo kết xuất bảng điểm tổng hợp KLTN theo đợt, bao gồm thông tin sinh viên, đề tài, các đầu điểm thành phần và điểm tổng kết. Hệ thống hỗ trợ xuất báo cáo định dạng Excel (.xlsx) hoặc PDF để phục vụ công tác quản lý và xét tốt nghiệp chính thức. | Khoa, PĐT |
| FR-DOC-01 | Lưu trữ và tra cứu lịch sử phiên bản hồ sơ | Hệ thống lưu trữ lịch sử các phiên bản tài liệu đã nộp trong suốt quá trình thực hiện KLTN, bao gồm đề cương chi tiết, sản phẩm Milestones, báo cáo giữa kỳ, báo cáo cuối kỳ và hồ sơ lưu chiểu. Cho phép người dùng có quyền truy cập xem lại lịch sử nộp và các phiên bản tài liệu tương ứng. | Sinh viên, GVHD, Khoa, Hệ thống |


## 5. Yêu cầu phi chức năng


### 2.2.1. Hiệu năng và Tải trọng

- Đảm bảo thời gian phản hồi cho tác vụ tra cứu, tìm kiếm danh mục đề tài không vượt quá 1 giây dưới tải trọng 500 truy vấn đồng thời.

- Hệ thống phải duy trì vận hành ổn định, không bị tắc nghẽn tài nguyên vào các mốc chót nộp bài đồng loạt (Tuần 2 nộp đề cương, Tuần 18 nộp báo cáo cuối kỳ)

- Xử lý tải tệp bất đồng bộ; tệp đề cương, giữa kỳ, tiến độ và cuối kỳ tối đa 20 MiB (20 × 1024 × 1024 = 20,971,520 byte), chấp nhận đúng giới hạn.


### 2.2.2. An toàn và Bảo mật

- Xác thực bằng ASP.NET Core Identity + JWT (stateless); mật khẩu được mã hóa trước khi lưu

- Phân quyền RBAC kết hợp Policy-based Authorization cho 06 vai trò.

- Điểm và nhận xét của GVPB phải được ẩn ở tầng API/DB (không chỉ ẩn ở UI) cho đến khi Khoa công bố chính thức


### 2.2.3. Kiến trúc, Độ tin cậy và độ khả dụng

- Kiến trúc Modular Monolith + DDD; các tầng Domain / Application / Infrastructure của 1 module không được tham chiếu module khác

- Mỗi module có DbContext và schema CSDL riêng biệt (identity, academic, topics, thesis, councils...)

- Bắt buộc dùng Database Transaction/Locking khi đăng ký đề tài và cập nhật trạng thái thẩm định đề cương, tránh race condition tại thời điểm sát deadline.

- Điểm Hội đồng chuyển trạng thái read-only ngay sau khi khóa biên bản; chỉ sửa được qua cơ chế override có log.

- Hệ thống phải cho tham đổi các tham số quan trọng và ảnh hưởng đến các công thức tính và các báo cáo. Kiểm kê, không hardcode

- Hệ thống phải đảm bảo độ khả dụng > 99.9% liên tục trong các tuần trọng điểm, đặc biệt là tuần ra bảo vệ trước HĐ (Tuần 19 - tuần 20)

- Hệ thống có sao lưu tự động CSDL định kỳ và đảm bảo khôi phục nhanh khi xảy ra sự cố về dữ liệu


### Đối chiếu yêu cầu kỹ thuật

Các câu về SQL Server, DbContext riêng mỗi module và không có FK liên module là thiết kế trong báo cáo, chưa phản ánh code. Theo quyết định của nhóm ngày 10/10/2026, docs kiến trúc lấy source hiện tại làm chuẩn: PostgreSQL, một `UGTMDbContext`, 6 schema và FK liên schema. Chi tiết khác biệt ở [Architecture.md](../backend/docs/Architecture/Architecture.md).

## 6. Quy tắc diễn giải và giới hạn

- Cổng 1 và Cổng 2 đều do Khoa hoặc Admin thẩm định theo cùng state machine: `Submitted` → `NeedRevision` → `Submitted` (v2, v3, ...) → `Approved` hoặc `Rejected`. GVHD không thẩm định Cổng 2; GVHD chỉ đánh giá các Milestone của đề tài được phân công.
- Quá deadline chốt cuối cùng của Cổng 1 hoặc Cổng 2: worker dừng nhóm chưa `Approved` và lưu audit trail. Hệ thống không tự gán điểm 0; điểm theo quy chế là nghiệp vụ riêng.
- Ngưỡng phản biện lấy từ cấu hình đợt; `5.0` trong luồng mô tả là giá trị minh họa, không hard-code.
- Quy tắc file đã chốt cho validator hiện tại: PDF/DOC/DOCX ≤20 MiB (20,971,520 byte), bao gồm báo cáo cuối kỳ; kiểm tra extension và chữ ký nội dung, không tin MIME từ client. Đây là quy tắc thay thế cách ghi “20 MB”, “dưới 20 MB” hoặc “PDF/DOCX” cũ trong phần nộp báo cáo. Lưu chiểu vẫn là nghiệp vụ riêng, cần cả PDF, DOCX, tóm tắt và từ khóa Việt/Anh; SCRUM-102 không thay đổi yêu cầu lưu chiểu.
- Điểm và phiếu nhận xét bảo mật không trả cho sinh viên trước công bố. Góp ý chỉnh sửa của GVPB và Hội đồng phải được cung cấp riêng, không kèm dữ liệu điểm chưa công bố.
- Chỉ Thư ký khóa biên bản sau xác thực mật khẩu/OTP; chỉ Lãnh đạo Khoa cấp quyền mở khóa có lý do. Người chấm chỉ sửa điểm mình nhập sau cấp quyền. Chỉ Admin tra cứu AuditLog.
- Sai biệt từ 2 điểm trở lên so với điểm tổng kết cần thảo luận; không tự loại điểm. Chủ tịch xác nhận kết luận không công nhận, Thư ký ghi biên bản và đánh dấu `IsIgnoredInCalculation = True`. Phạm vi bao gồm điểm GVHD, GVPB và thành viên Hội đồng.
- Tổng hợp chính thức cần đủ đầu điểm bắt buộc theo cấu hình và Khoa xác nhận lưu chiểu. Sinh viên xem điểm khi đồng thời đã công bố và hoàn thành lưu chiểu.
- Phê duyệt online không mặc định yêu cầu chữ ký số. Lưu chiểu mô phỏng nội bộ, không gọi API cổng trường. Danh sách người dùng nhập Excel, không phụ thuộc LDAP/AD.
- Công thức hệ số 6/7 phía trên được lấy từ Equation của Word và nhóm xác nhận ngày 10/10/2026. GVPB kiêm ủy viên chỉ tham dự bảo vệ, không nhập điểm Hội đồng; điểm GVPB trước bảo vệ vẫn có hệ số 2. Điểm bị Hội đồng kết luận không công nhận được loại cùng hệ số; xem quy tắc tính dưới đây.
- Báo cáo còn để mở điểm tổng kết tham chiếu để phát hiện sai biệt, thời hạn quyền sửa và thủ tục khóa lại. Không tự đặt quy tắc cho các nội dung này.

Ngoài phạm vi: học phí/thanh toán, thời khóa biểu chung, phát hiện đạo văn tự động và tích hợp trực tiếp hệ thống đào tạo/thư viện trường.


### 6.1. Quy tắc chấm điểm đã được nhóm xác nhận

Ngày 10/10/2026, nhóm xác nhận hai công thức trong Equation Word và quy tắc loại điểm/hệ số. Các công thức dưới đây là bộ hệ số tương ứng với hai trường hợp đã chốt; nền tảng vẫn phải hỗ trợ cấu hình phương thức và đầu điểm theo đợt, không thay bằng một công thức cố định dùng trung bình Hội đồng với trọng số bất kỳ.

| Trường hợp | Điểm được đưa vào phép tính | Công thức theo bộ hệ số đã chốt |
| --- | --- | --- |
| GVPB là ủy viên trong Hội đồng của cùng đề tài | GVHD hệ số 2; GVPB hệ số 2; Chủ tịch hệ số 1; Thư ký hệ số 1 | `(2 × GVHD + 2 × GVPB + Chủ_tịch + Thư_ký) / 6` |
| GVPB không phải ủy viên Hội đồng của đề tài | GVHD hệ số 2; GVPB hệ số 2; Chủ tịch/Thư ký/Ủy viên mỗi điểm hệ số 1 | `(2 × GVHD + 2 × GVPB + Chủ_tịch + Thư_ký + Ủy_viên) / 7` |

**GVPB kiêm ủy viên:** GVPB đã chấm trước buổi bảo vệ nên khi lên Hội đồng chỉ cần có mặt, không thực hiện chấm điểm ở vai trò Hội đồng. Không tạo/cho nhập thêm điểm ủy viên cho cùng người/cùng đề tài; không coi điểm này là thiếu đầu điểm bắt buộc. Điểm phản biện giữ hệ số 2, không bị bỏ khỏi phép tính chỉ vì GVPB thuộc Hội đồng. GVHD không được thuộc Hội đồng chấm đề tài mình hướng dẫn.

**Điểm sai biệt:** cảnh báo từ 2 điểm trở lên so với điểm tổng kết chỉ kích hoạt thảo luận. Nếu Hội đồng kết luận không công nhận, Chủ tịch xác nhận và Thư ký ghi biên bản/đánh dấu `IsIgnoredInCalculation = True`. Loại điểm bị đánh dấu **và hệ số của điểm đó** khỏi cả tử số và mẫu số:

`FinalScore = sum(score_i × coefficient_i của các điểm được công nhận) / sum(coefficient_i của các điểm được công nhận)`.

Nếu tổng hệ số còn lại bằng 0 thì không có điểm tổng kết hợp lệ để chia/tổng hợp chính thức; cần xử lý nghiệp vụ, không mặc định điểm 0 hoặc chia cho mẫu số cũ. Việc loại điểm theo kết luận không đồng nghĩa mọi điểm bị loại trở thành “chưa chấm”; khi kiểm tra đủ đầu điểm phải phân biệt phiếu đã hoàn thành nhưng không được công nhận với phiếu chưa được nhập.

Ở phương thức phần trăm, đọc các trọng số đã cấu hình. Khi có điểm bị loại theo kết luận, loại cả điểm/trọng số và chuẩn hóa theo tổng trọng số hợp lệ còn lại; không giữ mẫu số 100 nếu đã loại một đầu điểm có trọng số. Không hard-code bộ hệ số 6/7 cho đợt lựa chọn bộ tham số khác.

Ví dụ kiểm chứng với GVHD=8, GVPB=7, Chủ tịch=9, Thư ký=8, Ủy viên=6:

| Tình huống | Phép tính | Kết quả trước làm tròn |
| --- | --- | --- |
| GVPB kiêm ủy viên | `(16 + 14 + 9 + 8) / 6` | `47/6 ≈ 7.833333` |
| GVPB ngoài Hội đồng | `(16 + 14 + 9 + 8 + 6) / 7` | `53/7 ≈ 7.571429` |
| Trường hợp chia 6, Hội đồng không công nhận điểm GVHD | `(14 + 9 + 8) / 4` | `7.75` |
| Trường hợp chia 7, Hội đồng không công nhận điểm ủy viên | `(16 + 14 + 9 + 8) / 6` | `47/6 ≈ 7.833333` |

Ví dụ không đặt quy tắc làm tròn. Điểm tham chiếu để phát hiện sai biệt, thời hạn quyền sửa và thủ tục khóa lại vẫn là nội dung Word chưa định nghĩa đầy đủ; không suy ra từ các công thức đã chốt.

## 7. Tài liệu liên quan

[Use Case](UseCases.md) · [Luồng dữ liệu](DataFlows.md) · [Tuần tự](../backend/docs/Architecture/Sequences.md) · [Kiến trúc](../backend/docs/Architecture/Architecture.md) · [Dữ liệu](DataModel.md).
