# Gửi email thật qua SMTP

**Trạng thái:** đã triển khai · **Cập nhật:** 18/10/2026

UGTM gửi email kích hoạt tài khoản và OTP khôi phục mật khẩu qua `IEmailSender` của module Notifications.
Identity không gọi SMTP trong request chính: handler ghi message vào transactional outbox, sau đó
`IdentityOutboxProcessor` giải mã payload và gửi bất đồng bộ. Vì vậy lỗi SMTP không làm rollback thao tác nghiệp vụ;
message thất bại được retry theo exponential backoff, tối đa 60 phút giữa hai lần thử.

Mỗi runtime phải có một `Identity:Email:OutboxPartition` ổn định. Partition được đưa vào Data Protection
purpose của payload và worker chỉ lấy message thuộc partition của mình. Repository cấu hình sẵn
`local-direct` cho Rider/`dotnet run`, `local-docker` cho Docker Compose và `azure-production` cho deployment.
Nhờ đó local và Azure có thể dùng chung database mà không lấy nhầm message được mã hóa bằng key ring khác.

## Giao diện email

Identity có template HTML riêng cho email kích hoạt và OTP khôi phục mật khẩu. Template dùng
table layout cùng inline CSS để tương thích các email client phổ biến, màu thương hiệu chuyển
từ xanh `#2054A5` sang cam `#FA7921`, CTA/link dự phòng, cảnh báo bảo mật và bản text fallback.
OTP chỉ xuất hiện trong nội dung, không được đưa vào subject để tránh lộ qua notification.

Logo chữ UGTM được dựng trực tiếp bằng HTML email-safe và màu thương hiệu. Email không tải ảnh từ
URL bên ngoài, không dùng Content-ID và không đính kèm file, tránh trường hợp Gmail không ghép được
inline MIME resource hoặc hiển thị logo thành attachment.

## Cấu hình

Không lưu tài khoản hoặc mật khẩu SMTP thật trong Git. Sao chép `.env.example` thành `.env` và điền credential
do nhà cung cấp email cấp:

```dotenv
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_SECURITY=StartTls
SMTP_FROM_ADDRESS=your-sender@ugtm.edu.vn
SMTP_FROM_NAME=UGTM
SMTP_USERNAME=your-sender@ugtm.edu.vn
SMTP_PASSWORD=<provider-issued-smtp-app-password>
SMTP_TIMEOUT_SECONDS=30
```

`SMTP_PASSWORD` phải là App Password hoặc SMTP credential chuyên dụng của nhà cung cấp, không dùng mật khẩu
đăng nhập cá nhân. Địa chỉ `SMTP_FROM_ADDRESS` phải là sender được nhà cung cấp cho phép gửi.

| Biến | Ý nghĩa |
| --- | --- |
| `SMTP_HOST` | Máy chủ SMTP |
| `SMTP_PORT` | Cổng kết nối; thường là 587 cho STARTTLS hoặc 465 cho SSL-on-connect |
| `SMTP_SECURITY` | `StartTls`, `SslOnConnect` hoặc `None` |
| `SMTP_FROM_ADDRESS` | Địa chỉ hiển thị ở trường From |
| `SMTP_FROM_NAME` | Tên người gửi |
| `SMTP_USERNAME` / `SMTP_PASSWORD` | Cặp credential SMTP; phải cùng có hoặc cùng không có |
| `SMTP_TIMEOUT_SECONDS` | Timeout kết nối/gửi, từ 5 đến 120 giây |

Không đặt cùng một outbox partition cho hai môi trường có Data Protection key ring khác nhau. Các replica
thật sự của cùng một môi trường chỉ được dùng chung partition khi chúng đồng thời dùng chung key ring.

Production từ chối `SMTP_SECURITY=None` ngay khi startup. Không tắt kiểm tra chứng thư TLS.
`None` chỉ dành cho SMTP giả lập như Mailpit trong môi trường Development.

## Chạy bằng Docker Compose

Sau khi điền SMTP thật vào `.env`, chạy:

```powershell
docker compose up -d --build backend
```

Compose truyền toàn bộ biến SMTP vào `Notifications:Smtp:*`. Dịch vụ Mailpit có thể vẫn chạy để phục vụ kiểm thử,
nhưng backend sẽ gửi tới `SMTP_HOST` đã cấu hình thay vì Mailpit.

## Chạy trực tiếp hoặc bằng Rider

Chạy lại script sau mỗi lần thay đổi `.env`:

```powershell
./scripts/ConfigureDevelopment.ps1
```

Script ghi cấu hình database, JWT và SMTP vào `appsettings.Development.local.json`. File này bị Git ignore.
Nếu không đặt `SMTP_HOST`, ứng dụng Development tiếp tục dùng Mailpit theo `appsettings.Development.json`.

## Kiểm tra end-to-end

1. Khởi động backend và xác nhận không có lỗi `OptionsValidationException`.
2. Dùng `POST /api/identity/auth/activation/resend` với tài khoản `PendingActivation`, hoặc
   `POST /api/identity/auth/recovery/start` với tài khoản `Active`.
3. API trả 200 trước khi email được gửi vì outbox xử lý bất đồng bộ.
4. Kiểm tra hộp thư đến và thư mục spam; worker quét outbox mỗi 5 giây.
5. Nếu email chưa đến, kiểm tra log `IdentityOutboxProcessor` và các cột `attempt_count`, `next_attempt_at`,
   `last_error` trong `identity.outbox_message`.

Message tạo trước khi cơ chế partition được triển khai có protection purpose phiên bản cũ và không được worker
mới nhận. Hãy gọi resend một lần sau khi backend mới được triển khai; Magic Link mới sẽ vô hiệu hóa link cũ.

Không dùng endpoint resend liên tục để thử SMTP vì nhóm activation có rate limit 5 request/10 phút/IP
và mỗi lần resend sẽ vô hiệu Magic Link trước đó.

## Chế độ bảo mật

- `StartTls`: kết nối thường trên cổng SMTP rồi bắt buộc nâng cấp TLS; phù hợp với cổng 587.
- `SslOnConnect`: thiết lập TLS ngay khi mở kết nối; phù hợp với cổng 465.
- `None`: kết nối không mã hóa, chỉ được chấp nhận trong Development.

Sender giới hạn thời gian kết nối/gửi, bật kiểm tra thu hồi chứng thư và không có cơ chế bỏ qua certificate.
Payload token/OTP trong outbox được bảo vệ bằng ASP.NET Core Data Protection và bị xóa sau khi gửi thành công.
