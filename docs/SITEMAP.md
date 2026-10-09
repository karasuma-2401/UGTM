# Sitemap và danh mục màn hình UGTM

Nguồn nghiệp vụ: [Requirements.md](Requirements.md). Chương 5 Word chưa có danh sách màn hình cụ thể; sitemap dưới đây là đề xuất triển khai từ FR/ma trận quyền, không phải route đã được code.

## 1. Route đang có

Nguồn: [router.tsx](../frontend/src/app/router.tsx), [modules.ts](../frontend/src/shared/config/modules.ts).

| Route thực tế | Nội dung |
| --- | --- |
| `/` | Dashboard nền tảng |
| `/modules/:modulePath` | Trang gọi ping cho 6 module; các path identity, academic-periods, topic-catalog, thesis-management, defense-council, notifications |

Chưa có login/403 hoặc các portal nghiệp vụ; AuthProvider/ProtectedRoute chưa được router sử dụng.

## 2. Màn hình dự kiến theo vai trò

Tên route là đề xuất, có thể đổi khi xây UI. Các nhóm nghiệp vụ dùng chung một SPA; người nhiều vai trò chỉ thấy chức năng được cấp trong đúng phạm vi phân công. GVPB xem/chấm cuối kỳ, không thẩm định đề cương. Sinh viên chọn đề tài GVHD đề xuất, không thêm chức năng đổi GVHD/đề xuất đề tài hoặc kiểm tra đạo văn ngoài phạm vi final.

[Sơ đồ sitemap](PlantUML/Sitemap_UGTM.puml) chứa cả route hiện tại và các màn hình dự kiến, có ghi trạng thái.

### Chung

| Route đề xuất | Màn hình/đặc quyền | FR |
| --- | --- | --- |
| `/login` | Đăng nhập email trường/mật khẩu nội bộ | FR-SYS-01 |
| `/profile` | Hồ sơ cá nhân và đổi mật khẩu | FR-SYS-01 |
| `/notifications` | Thông báo trong ứng dụng | FR-NOTI-01 |
| `/periods` | Tra cứu đợt/giai đoạn/thời hạn theo quyền | FR-SYS-13 |
| `/403` | Không có quyền truy cập | FR-SYS-11 |

### Sinh viên

| Route đề xuất | Màn hình/đặc quyền | FR |
| --- | --- | --- |
| `/student/dashboard` | Tiến độ 7 giai đoạn, milestones và cảnh báo | FR-STU-01 |
| `/student/topics` | Tra cứu đề tài công bố và đăng ký | FR-TOPIC-02, 05–07 |
| `/student/group` | Nhóm tối đa 2 người và trạng thái GVHD tiếp nhận | FR-TOPIC-04, 08 |
| `/student/syllabus` | Nộp đề cương, kết luận của Khoa, bản sửa | FR-REG-01–04 |
| `/student/progress` | Nộp sản phẩm milestone, theo dõi phản hồi | FR-MILE-02–05 |
| `/student/midterm` | Nộp giữa kỳ, xem quyết định GVHD | FR-MID-01, 04–05 |
| `/student/rename-requests` | Đổi tên tuần 3–10, trạng thái duyệt ba cấp | FR-MID-02, 06–09 |
| `/student/final-report` | Báo cáo cuối kỳ và bản sửa theo góp ý GVPB | FR-FIN-01, 08 |
| `/student/defense` | Lịch đã được duyệt, góp ý sau bảo vệ | FR-COU-05, 11 |
| `/student/archive` | PDF/DOCX, tóm tắt/từ khóa Việt/Anh và kết quả lưu chiểu | FR-COU-03, 12–14 |
| `/student/results` | Điểm sau Khoa công bố và xác nhận lưu chiểu | FR-RES-07 |
| `/student/documents` | Lịch sử phiên bản hồ sơ thuộc nhóm | FR-DOC-01 |

### Giảng viên — GVHD/GVPB theo phân công

| Route đề xuất | Màn hình/đặc quyền | FR |
| --- | --- | --- |
| `/lecturer/topics` | GVHD soạn và đề xuất đề tài | FR-TOPIC-01 |
| `/lecturer/registrations` | GVHD xác nhận tiếp nhận nhóm | FR-TOPIC-08 |
| `/lecturer/supervising` | GVHD xem đề cương, milestones, dashboard và lịch sử | FR-REG-01, FR-MILE-01, 03, FR-STU-01 |
| `/lecturer/midterm` | GVHD quyết định Tiếp tục/Dừng tại Cổng 2 | FR-MID-01, 04 |
| `/lecturer/rename-requests` | GVHD xét đơn đổi tên cấp 1 | FR-MID-06, 07 |
| `/lecturer/advisor-evaluations` | GVHD chấm cuối kỳ và quyết định chuyển phản biện | FR-FIN-01, 03 |
| `/lecturer/reviews` | GVPB nhận hồ sơ, chấm/kết luận bảo vệ và góp ý riêng | FR-FIN-02, 05, 08 |
| `/lecturer/archive-reviews` | GVHD kiểm tra/xác nhận báo cáo sửa sau bảo vệ | FR-COU-12 |

### Hội đồng — theo phân công cá nhân

| Route đề xuất | Màn hình/đặc quyền | FR |
| --- | --- | --- |
| `/council/assignments` | Hồ sơ và lịch được phân công | FR-COU-01, 05 |
| `/council/grading` | Điểm/nhận xét cá nhân theo tiêu chí | FR-COU-02 |
| `/council/discrepancies` | Chủ tịch điều phối, xác nhận kết luận sai biệt | FR-COU-07, 08 |
| `/council/minutes` | Thư ký ghi biên bản/góp ý và khóa sau xác thực | FR-COU-02, 08, 11 |
| `/council/score-corrections` | Người chấm sửa điểm mình sau cấp mở khóa | FR-COU-10 |

### Văn phòng Khoa

| Route đề xuất | Màn hình/đặc quyền | FR |
| --- | --- | --- |
| `/faculty/topics` | Thẩm định và công bố đề tài | FR-TOPIC-03 |
| `/faculty/syllabuses` | Thẩm định đề cương Cổng 1 | FR-REG-02, 04 |
| `/faculty/midterm-summary` | Tổng hợp quyết định GVHD, chuyển PĐT | FR-MID-03 |
| `/faculty/rename-requests` | Thẩm định đổi tên cấp 2 | FR-MID-06, 07 |
| `/faculty/grading-config` | Cách tính, đầu điểm, trọng số/hệ số, điều kiện tiên quyết | FR-SYS-03, 07–10 |
| `/faculty/reviewer-assignments` | Phân công GVPB khác GVHD | FR-FIN-02, 04 |
| `/faculty/review-disagreements` | Xử lý không đồng thuận phản biện | FR-FIN-06 |
| `/faculty/councils` | Dự thảo Hội đồng, phòng/ca và chuyển PĐT | FR-COU-01, 04 |
| `/faculty/score-unlocks` | Lãnh đạo Khoa cấp mở khóa đặc biệt có lý do | FR-COU-09 |
| `/faculty/archive` | Nghiệm thu lưu chiểu sau GVHD xác nhận | FR-COU-13 |
| `/faculty/results` | Tổng hợp, rà soát Dự thảo, công bố | FR-RES-01–06 |
| `/faculty/reports` | Xuất báo cáo Excel/PDF | FR-AA-01 |

### Phòng Đào tạo

| Route đề xuất | Màn hình/đặc quyền | FR |
| --- | --- | --- |
| `/academic/periods` | Khởi tạo đợt và cấu hình 7 Stage/deadline | FR-SYS-02 |
| `/academic/imports` | Import SV đủ điều kiện và giảng viên từ Excel | FR-SYS-06 |
| `/academic/midterm-summary` | Theo dõi kết quả Khoa tổng hợp | FR-MID-03 |
| `/academic/rename-requests` | Phê duyệt đổi tên cấp 3 | FR-MID-06, 07 |
| `/academic/council-decisions` | Thẩm định/phê duyệt và upload quyết định Hội đồng | FR-COU-01 |
| `/academic/reports` | Xuất kết quả Excel/PDF phục vụ xét tốt nghiệp | FR-AA-01 |

### Admin

| Route đề xuất | Màn hình/đặc quyền | FR |
| --- | --- | --- |
| `/admin/users` | Quản lý tài khoản và vai trò | FR-SYS-05 |
| `/admin/permissions` | Danh mục quyền và gán quyền vai trò | FR-SYS-11 |
| `/admin/periods` | Khởi tạo đợt và cấu hình deadline | FR-SYS-02 |
| `/admin/imports` | Import SV và giảng viên Excel | FR-SYS-06 |
| `/admin/grading-config` | Cấu hình cách tính và điều kiện điểm | FR-SYS-03, 07–10 |
| `/admin/audit-logs` | Tra cứu AuditLog; chỉ Admin | FR-SYS-04 |

## 3. Điều kiện hiển thị và quyền

- Cổng 1: Khoa thẩm định. Cổng 2: GVHD quyết định; màn hình Khoa chỉ tổng hợp, PĐT theo dõi.
- Điểm/phiếu GVHD, GVPB bảo mật trước công bố; góp ý sửa GVPB/Hội đồng được cung cấp riêng. Lịch và điểm là hai nội dung có điều kiện truy cập khác nhau.
- GVPB kiêm ủy viên chỉ tham dự, không thấy form nhập điểm Hội đồng của chính đề tài đã phản biện; điểm GVPB trước bảo vệ vẫn tính hệ số 2.
- Thư ký xem phiếu điểm cần thiết, ghi biên bản và khóa; Chủ tịch xác nhận kết luận sai biệt; Lãnh đạo Khoa cấp quyền mở khóa. Thành viên chỉ sửa điểm mình sau khi có quyền hợp lệ.
- Lưu chiểu: SV nộp → GVHD xác nhận bản sửa → Khoa nghiệm thu. Điểm chính thức cần cả công bố và hoàn tất lưu chiểu.
- Khoa/Admin cấu hình điểm; PĐT/Admin khởi tạo đợt/import; Khoa công bố; Khoa/PĐT xuất báo cáo; chỉ Admin xem AuditLog.

Ẩn nút/route ở UI phải đi cùng kiểm tra permission, tài nguyên, deadline và trạng thái phía API.
