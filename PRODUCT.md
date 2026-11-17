# UGTM product context

<!-- impeccable:product-schema 1 -->

## Platform

web

## Product Purpose

UGTM là hệ thống quản lý khóa luận tốt nghiệp của dự án môn SE310 tại UIT. Phạm vi nghiệp vụ gồm quản lý đợt, đề tài, đăng ký nhóm, nộp hồ sơ, phản biện, hội đồng bảo vệ và điểm. Đây là phạm vi sản phẩm; không có nghĩa mọi chức năng đã hoàn tất.

## Positioning

Bản ghi khởi tạo từ README, route và source hiện có. Chưa có xác nhận riêng về lợi thế cạnh tranh hoặc lời hứa thương hiệu; không tự thêm các claim này vào UI.

## Operating Context

Ứng dụng web cho quy trình khóa luận theo đợt, vai trò và trạng thái hồ sơ. Giao diện dùng API backend để đọc dữ liệu và thực hiện thao tác; runtime local và các dịch vụ tích hợp được mô tả trong README. Các tác vụ có dữ liệu thật cần môi trường và tài khoản được phép sử dụng.

## Người dùng và công việc chính

- Sinh viên: xem/đăng ký đề tài, theo dõi nhóm và trạng thái, nộp hồ sơ và đọc phản hồi.
- Giảng viên: đề xuất/hướng dẫn đề tài, quản lý mốc tiến độ và phản hồi bài nộp.
- Khoa và phòng đào tạo: quản lý đợt, thẩm định hồ sơ, phân công và phê duyệt theo quyền được cấp.
- Quản trị viên: quản lý tài khoản, phân quyền và cấu hình hệ thống.

Vai trò và quyền thực tế phải đọc từ source và API. Không coi danh sách trên là policy phân quyền.

## Ràng buộc khi thiết kế

- Giao diện chính bằng tiếng Việt, phục vụ công việc có bảng dữ liệu, form và trạng thái nghiệp vụ.
- Người dùng cần biết đang thao tác với đợt/nhóm/hồ sơ nào, phiên bản nào và hành động nào còn được phép.
- Hiển thị rõ loading, lỗi tải, không có dữ liệu và không có quyền. Không dùng số liệu mẫu để thay cho kết quả API thất bại.
- Các thao tác lưu/công bố/thẩm định cần phản ánh đúng kết quả server và bảo vệ nội dung đang chỉnh sửa.

## Nguồn sự thật

- `README.md`: phạm vi và cách chạy dự án.
- `docs/Requirements.md`: yêu cầu nghiệp vụ.
- `docs/ApiContract/`: API contract.
- `frontend/src/app/router.tsx` và `frontend/src/shared/layout/navigationConfig.ts`: route và điều hướng hiện tại.
- Backend và test liên quan: validation, quyền và chuyển trạng thái thực tế.

## Evidence on Hand

Source, API contract và test trong repository là bằng chứng cho phạm vi đã triển khai. Chưa có bằng chứng trong bản ghi này cho khách hàng, số liệu vận hành, testimonial hoặc chứng nhận accessibility; không tự tạo nội dung thay thế.

## Product Principles

- Bám đúng đợt, nhóm và phiên bản hồ sơ khi hiển thị dữ liệu hoặc thực hiện thao tác.
- Giữ rõ nguồn dữ liệu và trạng thái thật; không biến lỗi tải thành kết quả thành công.
- Giữ các ràng buộc nghiệp vụ và quyền từ API trong quá trình cải thiện UI.
