# Màn hình đề tài và nhóm hướng dẫn

Đường dẫn: `/topics/advising`. Menu: **Khóa luận → Đề tài & nhóm hướng dẫn**. Quyền truy cập: Lecturer và Admin, theo cơ chế ProtectedRoute hiện có.

## Phạm vi đã thực hiện

| Hạng mục | Kết quả |
| --- | --- |
| Bố cục | Dùng AppLayout, Button, Input, Select, Badge, Card, StatisticsBar, Dialog và TablePagination hiện có. |
| Danh sách đề tài | Mã, tên, đợt, chỉ tiêu sinh viên đã xác nhận, trạng thái thẩm định/công bố và xem chi tiết. Có liên kết sang màn hình đề xuất hiện có. |
| Nhóm đăng ký | Xem nhóm chờ email, đã xác nhận, hết hạn hoặc Khoa đã hủy; giữ lịch sử nhóm theo đề tài. |
| Đang hướng dẫn | Chỉ hiển thị nhóm đã xác nhận và chưa hoàn thành; lọc theo tiến độ khóa luận riêng. |
| Thống kê | Đề tài phụ trách, nhóm chờ email, nhóm đang hướng dẫn, sinh viên hướng dẫn; ba chỉ số đầu có thể bấm lọc nhanh. Thống kê theo đợt đã áp dụng, không theo từ khóa/trạng thái. |
| Tìm kiếm | Tên tiếng Việt/Anh, mã đề tài, mã hồ sơ, mã nhóm, tên sinh viên, MSSV, email; hỗ trợ tìm tiếng Việt không dấu. |
| Bộ lọc | Chọn đợt và trạng thái, bấm Áp dụng, bỏ lọc; đổi tab giữ đợt đã áp dụng và xóa trạng thái/từ khóa cũ. |
| Phân trang | 5/10/20 dòng; điều hướng đầu, trước, sau, cuối bằng component hiện có. |
| Chi tiết | Nội dung/yêu cầu đề tài, giảng viên, đợt, nhóm và thành viên, email/điện thoại, thời điểm đăng ký, hạn và thời điểm xác nhận, tiến độ, lý do Khoa hủy. |
| Ngoại lệ | Hướng dẫn liên hệ Khoa và sao chép nội dung tham khảo. Không gửi yêu cầu hay thay đổi nhóm. |
| Trạng thái giao diện | Loading, lỗi/thử lại, danh sách rỗng, tải lại, phản hồi sao chép thành công/thất bại. |
| Luồng GV | Không có duyệt, tiếp nhận, từ chối hay hủy nhóm; không có tải phiếu. |

## Dữ liệu và giới hạn

Toàn bộ dữ liệu màn hình mới lấy từ adapter `frontend/src/features/topics/services/advisingDemo.ts`, tách riêng khỏi API thật và có nhãn **Dữ liệu mẫu**. Không tự suy luận xác nhận email từ trạng thái `Registered` hiện tại. Chức năng tải lại chỉ nạp lại bộ dữ liệu mẫu. Dữ liệu không thay đổi sau thao tác xem/lọc; không gửi email, không mô phỏng thao tác GV xác nhận thay sinh viên.

Các tình huống gồm hai đợt, tám đề tài, bảy hồ sơ và nhóm một/hai sinh viên. Mốc thời gian là tình huống cố định, không tự chuyển hết hạn theo đồng hồ thiết bị. Số chỗ chưa xác nhận chỉ là `chỉ tiêu - SV đã xác nhận`, **không phải số chỗ có thể đăng ký** khi đang có hồ sơ chờ giữ chỗ.

## Cần bổ sung để dùng dữ liệu thật

| Phần còn thiếu | Cần bổ sung |
| --- | --- |
| API danh sách | Danh sách đề tài và đăng ký đúng giảng viên; lọc theo đợt/trạng thái/tiến độ, tìm sinh viên, phân trang và số đếm từ server. API TopicCatalog hiện tại có thể dùng lại một phần. |
| Đăng ký và email | API tạo đăng ký, gửi và xác thực email; token hết hạn, chống dùng lại; tự ghi nhận nhóm sau xác nhận. |
| Ràng buộc đăng ký | Kiểm tra sinh viên đủ điều kiện, cùng đợt, số thành viên/chỉ tiêu; transaction và kiểm soát đăng ký đồng thời. |
| Trạng thái dữ liệu | Trạng thái xác nhận email tách khỏi tiến độ khóa luận; mã hồ sơ/nhóm, hạn xác nhận, người/thời điểm xác nhận và lý do Khoa hủy. Các kiểu trạng thái mới trong frontend hiện chỉ là đề xuất cho demo. |
| Ngoại lệ Khoa | API hủy nhóm đúng quyền, lưu lý do, audit và giải phóng chỉ tiêu; cập nhật danh sách GV sau khi hủy. |
| Tích hợp frontend | Thay adapter demo bằng API; kiểm tra quyền sở hữu ở server; phân trang/lọc/thống kê tại server; tải lại hoặc nhận cập nhật khi sinh viên xác nhận. |
| Kiểm chứng giao diện | Cần kiểm tra trực quan trên trình duyệt desktop/mobile và với phiên đăng nhập thật. Phiên công cụ hiện tại không có trình duyệt khả dụng. |

## Quy tắc cần chốt khi thiết kế API

- Ai cần xác nhận email: đại diện nhóm hay từng thành viên? Demo hiển thị trạng thái từng thành viên để bao phủ trường hợp xác nhận riêng; không thực thi quy tắc xác nhận thay backend.
- Hồ sơ chờ email có giữ chỗ không, giữ bao lâu và hoàn trả chỉ tiêu khi nào?
- Một đề tài cho phép một hay nhiều nhóm đồng thời? Giao diện chi tiết hỗ trợ danh sách nhóm và lịch sử, không làm mất nhóm cũ.
- Nhóm nào được tính đang hướng dẫn khi dừng thực hiện, bảo lưu hoặc hoàn thành? Demo loại nhóm hoàn thành, hết hạn và đã hủy; cần ánh xạ đầy đủ mã tiến độ thật.

## Kiểm tra

- `npm.cmd run build`: kiểm tra TypeScript và build production.
- `npm.cmd run lint`: kiểm tra lint toàn frontend.
- `npm.cmd run test:advising`: kiểm tra hành vi lọc/tìm, thống kê, danh sách hướng dẫn, phân trang, chi tiết, hướng dẫn liên hệ Khoa và các trạng thái tải/lỗi.

Các test giao diện sử dụng cây React và state harness theo cách kiểm tra hiện có trong repo; không thay thế kiểm thử end-to-end trên trình duyệt.
