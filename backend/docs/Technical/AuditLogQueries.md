# Tra cứu nhật ký kiểm toán

Các endpoint dưới đây yêu cầu policy `AdminOnly`. Danh sách, thống kê và xuất dùng cùng điều kiện lọc; `fromUtc` là cận dưới bao gồm và `toUtc` là cận trên không bao gồm.

- `GET /api/audit-logs`: kết quả phân trang, sắp xếp theo `timestamp DESC, id DESC`. Các tham số gồm `actorUserId`, `actorSessionId`, `action`, `targetType`, `targetId`, `requestId`, `outcome`, `fromUtc`, `toUtc`, `searchTerm`, `pageNumber`, `pageSize`. ID đối tượng, request và session là điều kiện khớp chính xác. Từ khóa tối đa 200 ký tự, khớp chuỗi con không phân biệt hoa thường trên tên/email tác nhân hiện tại, loại/mã đối tượng, action, reason code, request ID và metadata JSON.
- `GET /api/audit-logs/statistics`: chấp nhận cùng bộ lọc trừ outcome và phân trang, trả `total`, `success`, `denied`, `failed`, `other`. `other` là các outcome lịch sử không thuộc danh mục, không tự quy về thất bại.
- `GET /api/audit-logs/{id}`: đọc một sự kiện độc lập với trang danh sách.
- `GET /api/audit-logs/actions`: các action đã ghi nhận; action chưa có nhãn được hiển thị bằng mã gốc.
- `GET /api/audit-logs/export`: xuất CSV theo cùng bộ lọc, tối đa 100.000 bản ghi. Số lượng và nội dung xuất được đọc trong cùng snapshot PostgreSQL. CSV có UTF-8 BOM và bảo vệ các ô có thể bị chương trình bảng tính diễn giải thành công thức; giá trị gốc vẫn nằm trong audit log. Client cần xử lý hủy kết nối và lỗi tải file.
- `GET /api/audit-logs/outbox-health`: số sự kiện chưa xử lý, số đang retry và thời điểm cũ nhất theo từng nguồn. Bộ xử lý cũng ghi warning mỗi phút khi có retry hoặc sự kiện chờ trên năm phút. Endpoint này không thay thế giám sát và cảnh báo của môi trường triển khai.

Giao diện dùng ngày lịch tại `Asia/Ho_Chi_Minh`: một ngày được chuyển sang khoảng `[00:00 ngày chọn, 00:00 ngày kế tiếp)` rồi đổi UTC. Các preset 7 và 30 ngày bao gồm hôm nay. Tên/email tác nhân được join từ hồ sơ hiện tại, không phải snapshot tại thời điểm sự kiện; tác nhân thiếu thông tin không mặc định là hệ thống tự động. `Succeeded` trong các sự kiện cũ được đọc là `Success`; outbox mới chuẩn hóa outcome trước khi lưu.

## Đánh giá index

Đã thử PostgreSQL 18 với 100.000 dòng giả lập: truy vấn `outcome = 'Failed' ORDER BY timestamp DESC, id DESC LIMIT 20` dùng index `timestamp` mất khoảng 0,243 ms, trong khi index `(outcome, timestamp DESC, id DESC)` mất khoảng 0,088 ms. Đây chỉ là dữ liệu giả lập, không đại diện phân phối hoặc tải vận hành. Chưa thêm migration index. Trước khi thêm, chạy `EXPLAIN (ANALYZE, BUFFERS)` trên môi trường có dữ liệu đại diện cho các truy vấn mặc định, action, outcome, search và thống kê; cân nhắc chi phí ghi, dung lượng và kế hoạch rollback. Tìm kiếm metadata cast `jsonb` sang text và có thể cần chiến lược index riêng nếu dữ liệu lớn.
