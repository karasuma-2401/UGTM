# Topic Advising — tích hợp và API còn thiếu

Cập nhật 27/10/2026, đối chiếu mã nguồn backend trong repository. Chưa xác minh phiên bản trên môi trường triển khai. Thời hạn xác nhận đã chốt: **24 giờ**.

## Đã nối frontend

| API đã triển khai | Dữ liệu sử dụng |
| --- | --- |
| `GET /api/topics/my-topics?advisorId=...&batchId=...&pageNumber=...&pageSize=100` | Đề tài của GV hiện tại, nhóm, sinh viên, trạng thái khóa luận, chỉ tiêu. Truyền `advisorId` cả khi tài khoản có thêm vai trò Admin/Faculty để không lấy nhóm của GV khác. |
| `GET /api/topics/{id}/tracking` | Tải lại chi tiết khi mở hồ sơ; hiển thị tên đề tài, nội dung, nhóm, liên hệ sinh viên. Có loading, lỗi và thử lại riêng. |
| `GET /api/academic-periods/batches` | Danh sách đợt KLTN thật qua hook dùng chung; không còn ID đợt mẫu `2026-1`. Cần quyền `academic.batches.read`; thiếu quyền thì vẫn đọc đề tài, hiển thị mã đợt. |
| `POST /api/topics`, `PUT /api/topics/{id}` | Form đề xuất đã hỗ trợ chỉ định bằng `studentCodes`. Chỉ cập nhật danh sách khi người dùng sửa danh sách; bỏ toàn bộ chỉ định gửi `clearAssignedStudents: true` khi lưu. |

- Hai tab nhóm dùng dữ liệu thật, không còn fallback dữ liệu mẫu khi API lỗi.
- Vì chưa có API nhóm, frontend đọc đủ trang đề tài trong phạm vi GV/đợt, rồi lọc nhóm, tìm tên/MSSV/email/ID và phân trang tại frontend. Không lấy tổng đề tài làm tổng nhóm, không tính thống kê chỉ trên trang đang hiển thị.
- Tab Nhóm đăng ký hiển thị các nhóm hiện tại backend trả về; tab Đang hướng dẫn loại `Completed` và `Stopped`. Các trạng thái khóa luận khác giữ nguyên và dịch theo enum backend. Nhóm `Registered` không đồng nghĩa mọi thành viên đã xác nhận.
- Tổng đề tài và nhóm hướng dẫn tính từ toàn bộ dữ liệu đã tải của GV/đợt. Hai endpoint tracking đã bổ sung `confirmedStudentsCount` (thành viên Confirmed) và `pendingConfirmationsCount` (Pending chưa hết hạn). Frontend cộng hai trường trên các nhóm đang hướng dẫn, loại Completed/Stopped; không phụ thuộc tìm kiếm hoặc phân trang. Danh sách rỗng hiện 0; API cũ thiếu trường hoặc tải lỗi vẫn hiện `—` để tránh số liệu giả.
- `joinedAt` chỉ là thời điểm ghi nhận; không suy ra thời hạn bằng `joinedAt + 24h`, không suy ra người mời/người được mời theo thứ tự mảng. Mã nhóm/đề tài dạng nghiệp vụ chưa có thì dùng ID thật, không tạo mã giả.
- Liên hệ Khoa chỉ soạn/sao chép nội dung; chưa gửi yêu cầu hoặc hủy nhóm.
- Danh sách tự tải lại mỗi 60 giây và khi quay lại cửa sổ. Đọc đủ trang có thể tốn nhiều request khi phạm vi lớn; API nhóm/thống kê riêng sẽ thay thế cách tổng hợp này.

## Cần backend bổ sung để hoàn thiện trang GV

Đường dẫn dưới đây là **đề xuất, chưa triển khai**; backend có thể mở rộng endpoint hiện có nếu trả đủ contract tương đương.

| Ưu tiên | API đề xuất / phần cần mở rộng | Dữ liệu và hành vi cần có |
| --- | --- | --- |
| P1 | Mở rộng `GET /api/topics/my-topics` và `GET /api/topics/{id}/tracking` | Tách thành viên có hiệu lực và lời mời; trả `confirmationStatus`, `confirmedAtUtc`, `confirmationExpiresAtUtc`, người mời/nguồn chỉ định; lịch sử Pending/Accepted/Expired/Declined. Không trả token cho GV. |
| P1 | `GET /api/thesis/registrations` | Hồ sơ theo GV/đợt/đề tài, phạm vi `all` hoặc `advising`, lọc trạng thái nhóm và lời mời độc lập; tìm tên/MSSV/email/mã; phân trang server; gồm lịch sử hết hạn/từ chối/hủy. GV chỉ xem nhóm mình phụ trách. |
| P1 | `GET /api/thesis/registrations/{id}` | Chi tiết hồ sơ, mã nhóm/hồ sơ, sinh viên, lời mời với hạn 24h, tiến độ, lý do/người/thời điểm hủy. Có thể thay bằng mở rộng tracking tương đương. |
| P2 | `GET /api/thesis/advising/statistics` | Tối ưu thay việc tải tất cả các trang để cộng thống kê. Các ô thống kê đã hoạt động bằng DTO tracking hiện có; endpoint riêng không còn là điều kiện để hiển thị số liệu. |
| P1 | Mở rộng chỉ tiêu trong DTO tracking của GV | Đã có `confirmedStudentsCount`, `pendingConfirmationsCount`, loại Pending hết hạn khỏi thống kê chờ. Còn cần `reservedSlots`, `availableSlots` để thay các chỉ tiêu cũ. `registeredStudentsCount` vẫn gộp bản ghi Pending và Confirmed. API danh mục công khai `/api/topics/all-topics` đã có ba số liệu chỗ. |
| P2 | `POST /api/thesis/registrations/{id}/cancel` | Khoa/Admin xử lý ngoại lệ, lý do bắt buộc, audit và giải phóng chỉ tiêu. GV không có quyền gọi; không dùng API bỏ chỉ định chuẩn bị đề tài thay cho nghiệp vụ này. |
| P2 | API gửi yêu cầu xử lý tới Khoa | Chỉ cần nếu muốn nút Liên hệ Khoa thực sự gửi hồ sơ; hiện chưa có endpoint tương ứng, UI chỉ sao chép nội dung. Cần chốt path, người nhận và trạng thái xử lý. |

## Luồng SV và chỉ định — phân biệt đã có với còn thiếu

| Trạng thái | API / chức năng |
| --- | --- |
| Đã có backend, thuộc màn hình SV | `POST /api/thesis/topic-registrations/`, `POST /api/thesis/topic-registrations/confirm`, `GET /api/thesis/topic-registrations/my-status`. `my-status` chỉ trả dữ liệu SV đang đăng nhập, không dùng cho danh sách GV. |
| Đã nối form đề xuất qua tạo/sửa đề tài | `studentCodes` trong `POST /api/topics` / `PUT /api/topics/{id}`. Backend cũng có `PUT /api/thesis/topic-registrations/advisor-assign` và `DELETE /api/thesis/topic-registrations/advisor-assign/{topicId}` nhưng form dùng chung request tạo/sửa, không gọi thêm các API này. Giới hạn giai đoạn `TOPIC_PREPARATION`; không phải quyền GV từ chối nhóm SV đăng ký. |
| Chưa có backend | Chủ động từ chối lời mời (`POST /api/thesis/registrations/decline-participation`, path đề xuất). |
| Chưa có backend | Gửi lại lời mời (`POST /api/thesis/registrations/{id}/resend-invitation`, path đề xuất), không gia hạn 24h ban đầu. |
| Chưa có API chuyên biệt | Tìm SV đủ điều kiện cho GV chỉ định (`GET /api/topics/assignment-candidates`, path đề xuất), tránh mở quyền đọc toàn bộ tài khoản. |

Nguồn đối chiếu: `TopicEndpoints.cs`, `TopicDtos.cs`, `TopicQueryReader.cs`, `ThesisRegistrationContract.cs`, `TopicRegistrationEndpoints.cs`, `RegisterTopicHandler.cs` và `TopicRegistrationStore.cs`.

## Chỉ định sinh viên trong form đề xuất

- Nhập tối đa 1–2 MSSV theo chỉ tiêu, tùy chọn. Backend tra cứu SV tồn tại và đủ điều kiện; frontend không giả lập kết quả tìm SV. MSSV được trim/uppercase; kiểm tra trùng, số lượng và chiều dài tối đa 20 ký tự theo schema Identity.
- Xem trước, lưu tạm, khôi phục và cảnh báo rời trang giữ cả danh sách MSSV. Bản lưu tạm trên thiết bị không thực hiện chỉ định.
- Khi mở lại đề tài, đọc `group.students[].userCode` từ tracking. Nếu thiếu MSSV của thành viên hiện tại thì khóa sửa chỉ định, vẫn cho sửa nội dung khác để tránh mất danh sách.
- Sửa nội dung/gửi lại đề tài mà không đổi sinh viên không gửi lại `studentCodes`, không bỏ chỉ định và không gửi lại email chỉ định. Nhân bản đề tài chỉ sao chép nội dung, không sao chép sinh viên.
- Khi giảm chỉ tiêu, giữ nguyên MSSV đã nhập và báo lỗi nếu vượt chỉ tiêu; không tự bỏ sinh viên. Xóa danh sách chỉ có hiệu lực sau khi lưu với `clearAssignedStudents: true` trong giai đoạn chuẩn bị.
- **Giới hạn backend hiện tại:** `UpdateTopicHandler` gán SV trước khi lưu chỉ tiêu mới, còn store kiểm tra theo chỉ tiêu đang lưu. Với đề tài cũ chỉ tiêu 1, phải lưu chỉ tiêu 2 trước khi gán 2 SV. Frontend báo rõ trước khi gửi request kết hợp này; backend cần hỗ trợ cập nhật chỉ tiêu và chỉ định trong cùng giao tác nếu muốn thực hiện một lần lưu.
