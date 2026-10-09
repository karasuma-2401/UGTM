# Biểu đồ tuần tự UGTM

Năm sơ đồ theo mục 2.4.1–2.4.5 của [báo cáo final](Requirements.md), đối chiếu các quy định và thuật toán [DataFlows.md](DataFlows.md). Đây là thiết kế tương tác nghiệp vụ cần triển khai, không mô tả endpoint/handler đã tồn tại.

Participants API, tính điểm, thông báo/kho tệp là vai trò xử lý dự kiến; không tự chọn MediatR, CQRS, aggregate hoặc event bus. Dữ liệu dùng tên schema PostgreSQL hiện tại.

| Luồng | Sơ đồ | Điểm kiểm soát |
| --- | --- | --- |
| Đăng ký/ghép nhóm | [SD1](PlantUML/Sequences/SD1_DangKyDeTai.puml) | Transaction/khóa, điều kiện SV, nhóm ≤2, chỉ tiêu, PDF sau commit, GVHD xác nhận tiếp nhận sau đăng ký |
| Thẩm định đề cương | [SD2](PlantUML/Sequences/SD2_ThamDinhDeCuong.puml) | Khoa quyết định Cổng 1; Đạt/Cần sửa/Không đạt; deadline bổ sung và lịch sử phiên bản |
| Đổi tên ba cấp | [SD3](PlantUML/Sequences/SD3_DoiTenDeTai_3Ben.puml) | SV→GVHD→Khoa→PĐT, từ chối giữ tên; chỉ duyệt cuối mới cập nhật, mở nộp lại đề cương và ghi identity.audit_log |
| Hội đồng/biên bản | [SD4](PlantUML/Sequences/SD4_ChamDiemHoiDong_KhoaBienBan.puml) | Chấm cá nhân, thảo luận sai biệt; Chủ tịch kết luận/Thư ký ghi; Thư ký xác thực khóa, Lãnh đạo Khoa cấp mở khóa; chỉ sửa điểm mình |
| Tổng hợp/công bố | [SD5](PlantUML/Sequences/SD5_TongHopDiem_CongBo.puml) | Đủ đầu điểm và lưu chiểu; cấu hình %/hệ số/trường hợp GVPB; dự thảo→Khoa công bố; SV tra cứu sau đủ điều kiện; Khoa/PĐT export |

Cổng 2 không nằm trong năm sơ đồ DFD này; xem [Gatekeeper_Workflow](PlantUML/Gatekeeper_Workflow.puml) và Use Case 2.3.2.6 tại [UseCases.md](UseCases.md). GVHD quyết định tiếp tục/dừng; Khoa tổng hợp và chuyển PĐT theo dõi. Quá hạn giữa kỳ không tự gán điểm 0.

Quy tắc công thức được quản lý tại [Requirements.md](Requirements.md). GVPB kiêm ủy viên không nhập điểm Hội đồng; điểm phản biện trước bảo vệ tính hệ số 2. Chỉ loại điểm theo kết luận không công nhận và loại cả hệ số khỏi mẫu số. Không tự loại điểm khi có cảnh báo. Điểm bảo mật được lọc ở API; góp ý chỉnh sửa phải cung cấp riêng trước công bố.
