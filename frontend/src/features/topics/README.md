# Đề xuất đề tài

- Route: `/topics/propose`, dùng `AppLayout` hiện tại.
- Sidebar và route cho tài khoản có vai trò `Lecturer` hoặc `Admin`. Quyền Admin được mở tạm để kiểm thử màn hình Đề xuất.
- Dùng `httpClient` chung để gửi Bearer token và xử lý refresh phiên.
- Danh sách luôn truyền `advisorId` của người đăng nhập, kể cả khi giảng viên đồng thời có vai trò Khoa/Admin.

## API

| Thao tác | Endpoint |
| --- | --- |
| Chọn đợt, đọc đủ các trang | `GET /api/academic-periods/batches` |
| Danh sách đề tài cá nhân, có phân trang | `GET /api/topics/my-topics` |
| Đọc nội dung và trạng thái mới nhất khi mở | `GET /api/topics/{id}/tracking` |
| Tạo nháp hoặc tạo và gửi duyệt | `POST /api/topics` |
| Lưu sửa hoặc lưu và gửi duyệt trong cùng request | `PUT /api/topics/{id}` |
| Gửi bản đã lưu không có thay đổi | `POST /api/topics/{id}/submit` |

Chỉ `Draft` và `NeedRevision` cho chỉnh sửa/gửi duyệt. Các trạng thái khác chỉ xem.
Khi lưu chỉnh sửa ở `NeedRevision`, trạng thái vẫn là `NeedRevision` cho đến khi gửi lại.
Backend đã hỗ trợ Admin tạo, chỉnh sửa và gửi duyệt; màn hình vẫn chỉ lấy đề tài của người đang đăng nhập.

## Chạy và kiểm tra

Từ thư mục `frontend`:

```powershell
npm run dev
npm run test:topics
npm run lint
npm run build
```

Vite mặc định dùng `http://localhost:5173`, API mặc định `http://localhost:5287`.
Khi chạy API local bằng `dotnet run`, dùng Vite ở cổng 5173 theo CORS hiện có.
Frontend Docker ở cổng 3000 cần API cho phép origin `http://localhost:3000`;
Compose đã khai báo origin này cho backend Docker, nhưng cấu hình local có thể khác.
Nếu dùng frontend Docker tại `http://localhost:3000`, thay đổi source cần build lại từ thư mục gốc:

```powershell
docker compose up -d --build --no-deps frontend
```

Test tự động kiểm tra request/payload với HTTP giả lập, phân trang, thông báo lỗi, trạng thái có thể sửa,
và render `AppLayout` với các vai trò để kiểm tra mục sidebar. Việc lưu dữ liệu thật cần kiểm tra bằng
tài khoản Lecturer hoặc Admin và một đợt KLTN có sẵn trên backend.
