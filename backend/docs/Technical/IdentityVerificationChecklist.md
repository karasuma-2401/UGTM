# Backend Identity verification checklist

**Trạng thái:** đạt kiểm tra tự động · **Cập nhật:** 18/10/2026

Checklist này là tiêu chí nghiệm thu cuối cho module Identity phía Backend. Các mục có nhãn
`Automated` được khóa bằng test hoặc CI; các mục `Deployment` phải được xác nhận lại trên
mỗi môi trường thật vì phụ thuộc DNS, reverse proxy, SMTP và secret của môi trường đó.

## 1. Authentication lifecycle

- [x] `Automated` Tài khoản `PendingActivation` hoàn tất magic link và đặt mật khẩu lần đầu.
- [x] `Automated` Đăng nhập được bằng email và mã người dùng sau khi kích hoạt.
- [x] `Automated` Login tạo session, access token và refresh token; refresh thực hiện rotation.
- [x] `Automated` Phiên do người dùng thu hồi không thể refresh lại.
- [x] `Automated` Recovery tạo OTP, xác minh OTP, cấp reset grant dùng một lần và đổi mật khẩu.
- [x] `Automated` Mật khẩu cũ bị từ chối, mật khẩu mới đăng nhập được sau reset.
- [x] `Automated` Refresh đồng thời chỉ thành công một lần; reuse làm revoke toàn bộ session.
- [x] `Automated` Activation token đồng thời chỉ được consume một lần.

Test chấp nhận chính: `IdentityHttpLifecycleIntegrationTests`,
`IdentityLifecycleIntegrationTests` và `AuthenticationConcurrencyTests`. Nhóm HTTP đi qua
route, JSON contract, CSRF header/cookie, refresh cookie, authorization và response header;
các nhóm đều dùng PostgreSQL thật qua Testcontainers.

## 2. Permission Admin và Phòng Đào tạo

| Khả năng Identity | Admin | PĐT (`AcademicAffairs`) |
| --- | :---: | :---: |
| Xem danh sách người dùng | Có | Có |
| Import sinh viên/giảng viên | Có | Có |
| Tạo, sửa, đổi trạng thái người dùng | Có | Không mặc định |
| Xem danh mục permission | Có | Không mặc định |
| Cập nhật permission của role | Có | Không mặc định |
| Xem/thu hồi session của người khác | Có | Không mặc định |

- [x] `Automated` Migration cấp đủ sáu permission Identity cho Admin.
- [x] `Automated` Migration chỉ cấp `identity.users.read` và `identity.users.import` cho PĐT.
- [x] `Automated` Authorization handler cho phép Admin và chặn PĐT ở các permission không được cấp.
- [x] `Automated` Không thể suspend/disable đồng thời toàn bộ Admin đang active.

Các permission ngoài mặc định có thể được Admin gán có chủ đích qua API role-permission.

## 3. SMTP và transactional outbox

- [x] `Automated` Payload email được Data Protection bảo vệ khi nằm trong database.
- [x] `Automated` Lỗi gửi SMTP làm tăng `attempt_count`, lưu loại lỗi và đặt `next_attempt_at`.
- [x] `Automated` Message chưa tới thời điểm retry không được gửi lại sớm.
- [x] `Automated` Retry thành công đánh dấu `processed_at`, xóa lỗi và xóa protected payload.
- [x] `Automated` Các worker song song lấy message bằng `FOR UPDATE SKIP LOCKED`.
- [x] `Deployment` SMTP thật đã được xác nhận gửi thành công trong môi trường development.
- [ ] `Deployment` Gửi thử activation và recovery email sau mỗi lần đổi SMTP provider/credential.

Test retry chính: `IdentityOutboxRetryIntegrationTests`. Không dùng SMTP thật trong CI; test
dùng sender xác định lỗi ở lần đầu và thành công ở lần sau để kết quả ổn định.

## 4. Docker production và secret

- [x] `Automated` Production Compose bắt buộc `DATABASE_CONNECTION_STRING`,
  `JWT_PRIVATE_KEY_FILE`, `IDENTITY_IP_HASH_KEY`, `SMTP_HOST` và `SMTP_FROM_ADDRESS`.
- [x] `Automated` CI chạy `docker compose ... config --quiet` với giá trị giả, không in secret.
- [x] `Automated` Workflow deploy từ chối chạy nếu IP hash key thiếu hoặc ngắn hơn 32 ký tự.
- [x] `Automated` Workflow và runtime cùng dùng `Notifications:Smtp:Security`; cấu hình cũ
  `UseStartTls` đã được loại bỏ khỏi deployment.
- [x] JWT private key được tạo thành file với `umask 077` và chỉ mount read-only.
- [x] Data Protection key ring dùng volume bền vững.
- [x] `.env`, private key và credential thật không được commit; CI chạy Gitleaks.
- [ ] `Deployment` Đặt HTTPS ở reverse proxy và khai báo đúng `Proxy:KnownProxies`.
- [ ] `Deployment` Sao lưu database và volume Data Protection key trước khi release.
- [ ] `Deployment` Xác nhận secret production không dùng chung với development.

Không xoay `IDENTITY_IP_HASH_KEY` tùy tiện: việc xoay key làm thay đổi fingerprint IP trong
Audit Log. Mọi replica phải dùng cùng key. Các secret truyền bằng environment của Docker có
thể được người có quyền quản trị Docker xem qua container metadata; môi trường yêu cầu mức
bảo vệ cao hơn nên chuyển sang secret manager của nền tảng triển khai.

## 5. Lệnh nghiệm thu

```powershell
dotnet format UGTM.sln --verify-no-changes --no-restore
dotnet build UGTM.sln --no-restore
dotnet test UGTM.sln --no-build --no-restore
docker compose --env-file .env -f docker/docker-compose.prod.yml config --quiet
```

Sau khi deploy:

1. chạy migration bằng quy trình được kiểm soát;
2. khởi động backend và kiểm tra startup không có lỗi option/JWT/Data Protection;
3. gọi `/api/modules/Identity/ping`;
4. smoke test CSRF → login → refresh → logout bằng tài khoản thử nghiệm;
5. gửi một activation email và một recovery email;
6. kiểm tra Audit Log không chứa email, OTP, token, mật khẩu hoặc IP thô.

## Kết luận phạm vi

Backend Identity đạt phạm vi hiện tại cho mô hình một API replica. Google/SSO, giao diện
frontend và rate limit phân tán giữa nhiều replica không thuộc tiêu chí hoàn tất này; nếu scale
ngang API, rate limiter cần được chuyển sang kho quota dùng chung.
