# Authentication backend

**Trạng thái:** đã triển khai · **Cập nhật:** 13/10/2026

Tài liệu này giải thích phần xác thực đang chạy trong module Identity: code nằm ở đâu, dữ liệu đi qua hệ thống thế nào và cần cấu hình gì khi vận hành. Request/response công khai được chốt riêng tại [Identity API](../../../docs/ApiContract/IdentityApiContract.md).

## Phạm vi hiện tại

Backend hỗ trợ:

- cấp tài khoản sinh viên qua application handler nội bộ;
- kích hoạt tài khoản bằng magic link và đặt mật khẩu lần đầu;
- đăng nhập bằng email hoặc mã sinh viên;
- JWT access token và phiên refresh token có rotation;
- đăng xuất, đổi mật khẩu, quên mật khẩu bằng OTP;
- roles, permissions và policy động;
- rate limiting theo IP;
- email qua transactional outbox và SMTP.

Google login và SSO chưa nằm trong hệ thống. `ProvisioningStudentAccountHandler` cũng chưa có endpoint HTTP; module nghiệp vụ hoặc luồng import sẽ gọi handler này khi được triển khai.

## Bố cục source

```text
backend/Modules/Identity/
  Domain/
    Entities/                 # User, session, refresh token, challenge, outbox
    Enums/                    # trạng thái tài khoản, mục đích token, lý do revoke
    Repositories/             # cổng persistence của aggregate
  Application/
    Activation/               # validate, complete, resend magic link
    Authentication/           # login, refresh, logout, me, change password
    Provisioning/             # cấp tài khoản sinh viên
    Recovery/                 # start, verify OTP, reset password
    Security/                 # contract token/password và policy thời hạn
  Contracts/Authorization/    # ICurrentUser và mã permission công khai
  Infrastructure/
    Authentication/           # JWT, cookie, password hasher, session validator
    Authorization/            # current user và policy Permission:<code>
    Endpoints/                # Minimal API
    Notifications/            # outbox writer và background processor
    Persistence/              # EF configuration, repository, unit of work
    RateLimiting/             # các policy giới hạn request
```

[`IdentityModule`](../../Modules/Identity/Infrastructure/IdentityModule.cs) là composition root của module. File này đăng ký DbContext, handler, authentication, authorization, Data Protection, rate limiter và outbox worker. [`Program.cs`](../../host/Api/Program.cs) đặt middleware theo thứ tự: forwarded headers, exception handler, HTTPS, CORS, rate limiter, authentication và authorization.

## Mô hình tài khoản

`User` bắt đầu ở trạng thái `PendingActivation` và chưa có password hash. Luồng cấp tài khoản tạo user, gán role `Student`, tạo activation challenge rồi ghi email vào outbox trong cùng transaction. Chỉ bước hoàn tất kích hoạt mới đặt mật khẩu và chuyển trạng thái sang `Active`.

| Trạng thái | Có thể đăng nhập | Ý nghĩa |
| --- | :---: | --- |
| `PendingActivation` | Không | Đã cấp tài khoản, đang chờ đặt mật khẩu |
| `Active` | Có | Tài khoản hoạt động |
| `Suspended` | Không | Tạm ngưng |
| `Disabled` | Không | Vô hiệu hóa |

`SecurityStamp` là phiên bản bảo mật của tài khoản. Kích hoạt, đổi/reset mật khẩu, suspend và disable đều đổi stamp. Mỗi session giữ một snapshot; stamp lệch làm JWT và refresh session mất hiệu lực ngay ở request kế tiếp.

Sau 5 lần nhập sai liên tiếp, tài khoản bị khóa 15 phút. Đăng nhập thành công xóa bộ đếm. Rate limit theo IP vẫn chạy độc lập để giảm brute force phân tán trên nhiều tài khoản.

## Token và phiên

### Access token

Access token là JWT ký bằng RSA SHA-256, mặc định sống 10 phút. Payload gồm:

| Claim | Giá trị |
| --- | --- |
| `sub` | User ID |
| `email` | Email |
| `jti` | ID riêng của JWT |
| `sid` | Authentication session ID |
| role claim | Một claim cho mỗi role |
| `permission` | Một claim cho mỗi permission |

JWT validator kiểm tra issuer, audience, chữ ký, thời hạn với `ClockSkew = 0`, sau đó `SessionValidator` đọc session từ database. Một token còn hạn vẫn bị từ chối nếu session đã revoke, tài khoản không còn active hoặc security stamp đã đổi.

Roles và permissions được chụp vào JWT khi login hoặc refresh. `/me` luôn đọc profile hiện tại từ database; quyền mới chỉ đi vào access token sau lần refresh kế tiếp.

### Refresh token

Refresh token là chuỗi ngẫu nhiên dạng `<selector>.<validator>`. Database giữ selector để tìm bản ghi và SHA-256 của validator để đối chiếu constant time; raw token chỉ tồn tại ở cookie trình duyệt.

Mỗi lần refresh:

1. khóa token hiện tại trong transaction PostgreSQL;
2. kiểm tra token, session, thời hạn nhàn rỗi, thời hạn tuyệt đối và security stamp;
3. đánh dấu token cũ đã dùng và liên kết token thay thế;
4. tạo access token cùng refresh token mới;
5. cập nhật refresh cookie.

Hai request đồng thời không thể cùng xoay một token. Dùng lại token đã tiêu thụ sẽ revoke toàn bộ session với lý do `RefreshTokenReuse`. Session mặc định hết hạn sau 7 ngày không hoạt động hoặc 30 ngày kể từ lúc tạo, tùy mốc nào đến trước.

### One-time challenge

Activation token, OTP khôi phục và reset grant dùng chung `OneTimeChallenge`. Secret được tạo bằng CSPRNG và database chỉ giữ hash. Challenge có purpose, hạn dùng, số lần thử, thời điểm consume và invalidate. Activation link sống 24 giờ; OTP và reset grant sống 10 phút; challenge cho phép tối đa 5 lần thử trừ reset grant chỉ cho phép một lần.

## Các luồng chính

### Cấp và kích hoạt tài khoản

1. `ProvisioningStudentAccountHandler` tạo user `PendingActivation` và gán role `Student`.
2. Handler tạo activation challenge, ghi email được bảo vệ vào outbox rồi commit một transaction.
3. Worker gửi link `${PublicWebBaseUrl}/activate#token=...`.
4. Frontend gọi `/activation/validate` để hiển thị tên và email đã che.
5. Frontend gọi `/activation/complete`; backend hash mật khẩu, active tài khoản và consume challenge.

`/activation/resend` vô hiệu các activation challenge còn hiệu lực trước khi tạo link mới. Response luôn thành công khi identifier không khớp để tránh account enumeration.

### Đăng nhập và khôi phục phiên

1. Frontend lấy CSRF token từ `/csrf`.
2. `/login` kiểm tra CSRF, trạng thái tài khoản, lockout và password hash.
3. Backend tạo session, refresh token và JWT; refresh token đi vào cookie `HttpOnly`.
4. Frontend giữ JWT trong bộ nhớ và gửi Bearer header cho API được bảo vệ.
5. Sau khi reload hoặc JWT hết hạn, frontend lấy CSRF token mới rồi gọi `/refresh` với cookie.

`/logout` revoke session hiện tại. Đổi hoặc reset mật khẩu revoke mọi session của người dùng và xóa refresh cookie trên trình duyệt đang gọi.

### Khôi phục mật khẩu

1. `/recovery/start` tạo OTP và outbox email cho tài khoản active.
2. `/recovery/verify` consume OTP và cấp reset grant dùng một lần.
3. `/recovery/reset` consume grant, đổi mật khẩu/security stamp và revoke mọi session.

Start trả response có cùng hình dạng cho email không tồn tại, rỗng hoặc tài khoản không active. Challenge ID giả không thể verify nhưng không tiết lộ tài khoản có tồn tại hay không.

## CSRF, CORS và proxy

Refresh token nằm trong cookie nên `login`, `refresh` và `logout` dùng synchronizer token của ASP.NET Core. `/csrf` đặt cookie chống giả mạo và trả request token; client gửi request token bằng `X-CSRF-TOKEN`. Cả refresh cookie và antiforgery cookie dùng `SameSite=Strict`, `Path=/`; production thêm prefix `__Host-` và cờ `Secure`.

CORS chỉ nhận origin trong `Cors:AllowedOrigins` và bật credentials. Production cần HTTPS từ browser đến reverse proxy. `Proxy:KnownProxies` phải chứa đúng địa chỉ proxy tin cậy để backend đọc `X-Forwarded-For` và `X-Forwarded-Proto`; rate limit dùng địa chỉ IP sau bước này.

## Authorization

`ICurrentUser` cung cấp user ID, session ID, roles và permissions từ claims cho Application layer. Endpoint chỉ cần đăng nhập dùng `.RequireAuthorization()`.

Policy theo permission có tên `Permission:<permission-code>`, ví dụ:

```csharp
endpoint.RequireAuthorization("Permission:identity.users.read");
```

`PermissionPolicyProvider` tạo policy động và `PermissionAuthorizationHandler` kiểm tra claim `permission`. Module hiện khai báo ba mã Identity:

- `identity.users.read`
- `identity.users.manage`
- `identity.sessions.revoke`

Các endpoint auth hiện chỉ dùng authenticated-user policy. Policy theo phân công, deadline, trạng thái đề tài hoặc quyền sửa điểm thuộc module sở hữu nghiệp vụ và chưa được xây trong Identity.

## Email outbox

Activation và recovery ghi `IdentityOutboxMessage` trong cùng transaction với thay đổi tài khoản/challenge. Payload chứa token hoặc OTP được mã hóa bằng ASP.NET Core Data Protection trước khi lưu.

`IdentityOutboxProcessor` chạy mỗi 5 giây, lấy tối đa 20 message bằng `FOR UPDATE SKIP LOCKED`, gửi qua `IEmailSender`, rồi đánh dấu processed. Lỗi được retry theo exponential backoff, tối đa 60 phút. Sau khi gửi thành công, worker xóa nội dung protected payload khỏi bản ghi.

Data Protection key ring phải được giữ bền vững và dùng chung giữa các replica. Nếu mất key ring, các outbox message chưa gửi sẽ không thể giải mã. Docker Compose mount volume `data_protection_keys` cho mục đích này.

## Dữ liệu

Migration [`CompleteIdentityAuthentication`](../../Database.Migrations/Migrations/20261012162113_CompleteIdentityAuthentication.cs) bổ sung bốn bảng:

| Bảng | Vai trò |
| --- | --- |
| `identity.authentication_session` | phiên, hạn dùng, security stamp snapshot, trạng thái revoke |
| `identity.refresh_token` | chuỗi rotation của refresh token |
| `identity.one_time_challenge` | activation token, OTP và reset grant |
| `identity.outbox_message` | email chờ gửi, payload được Data Protection bảo vệ |

`identity.user` có thêm normalized email, account status, thời điểm kích hoạt/đăng nhập/đổi mật khẩu, lockout, security stamp và concurrency stamp. `IdentityUnitOfWork` dùng execution strategy cùng transaction cho các handler cần cập nhật nhiều bản ghi. Repository dùng row lock cho refresh token và challenge để chống xử lý đồng thời.

`identity.audit_log` đã có model mới cho actor, session, request và metadata; các handler auth hiện chưa ghi audit event vào bảng này.

## Cấu hình

| Key | Mặc định | Ghi chú |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | rỗng | PostgreSQL runtime |
| `Identity:Jwt:Issuer` | `UGTM` | issuer phải khớp khi validate |
| `Identity:Jwt:Audience` | `UGTM.Web` | audience phải khớp khi validate |
| `Identity:Jwt:PrivateKeyPath` | `/run/secrets/ugtm-jwt-private.pem` | RSA private PEM; bắt buộc tồn tại lúc startup |
| `Identity:Jwt:KeyId` | `ugtm-auth-2026-01` | `kid` để nhận diện khóa |
| `Identity:Jwt:AccessTokenMinutes` | `10` | thời hạn JWT |
| `Identity:DataProtection:KeyRingPath` | chưa đặt | thư mục lưu key ring bền vững |
| `Identity:Email:PublicWebBaseUrl` | `http://localhost:5173` | gốc URL trong activation email |
| `Identity:Security:*` | xem `appsettings.json` | lockout, session và challenge lifetime |
| `Notifications:Smtp:*` | xem Development/Compose | SMTP host, port, sender, TLS và credentials |
| `Cors:AllowedOrigins` | `http://localhost:5173` | danh sách origin chính xác |
| `Proxy:KnownProxies` | rỗng | chỉ cấu hình proxy do hệ thống quản lý |

Các option bảo mật và JWT được validate lúc startup. Không lưu private key, SMTP password hoặc connection string thật trong Git.

## Chạy local

Cách ít sai khác nhất là chạy backend và Mailpit bằng Compose:

```powershell
Copy-Item .env.example .env
# Điền DATABASE_CONNECTION_STRING trong .env
./scripts/GenerateDevelopmentJwtKey.ps1
./scripts/Database.ps1 -Action Update
docker compose up -d backend mailpit
```

- API: `http://localhost:5287`
- Scalar API Reference: `http://localhost:5287/scalar`
- Mailpit: `http://localhost:8025`

Nếu chạy API trực tiếp bằng `dotnet run`, đặt đường dẫn key phù hợp với máy thay cho đường dẫn trong container:

```powershell
$env:ConnectionStrings__DefaultConnection = '<PostgreSQL connection string>'
$env:Identity__Jwt__PrivateKeyPath = (Resolve-Path ./secrets/ugtm-jwt-private.pem)
dotnet run --project backend/host/Api/UGTM.Api.csproj --launch-profile http
```

API không tự migrate khi startup. Dùng `Database.ps1` hoặc EF CLI và review migration trước khi update môi trường dùng chung.

## Kiểm thử và chẩn đoán

```powershell
dotnet test backend/tests/UGTM.Modules.Identity.Tests/UGTM.Modules.Identity.Tests.csproj
dotnet test UGTM.sln
dotnet format UGTM.sln --verify-no-changes
```

Identity có 22 test bao phủ domain state, login handler, password hashing, authorization, rate limiting và hai trường hợp tranh chấp refresh/challenge trên PostgreSQL thật bằng Testcontainers.

Khi điều tra lỗi:

- `401` ngay cả khi JWT còn hạn: kiểm tra session, account status và security stamp;
- `400 Invalid CSRF Token`: lấy token mới từ `/csrf`, gửi đúng cookie và `X-CSRF-TOKEN`;
- email chưa đến: xem Mailpit/SMTP, log `IdentityOutboxProcessor`, `attempt_count`, `next_attempt_at`, `last_error`;
- startup lỗi JWT: kiểm tra đường dẫn PEM, quyền đọc file và nội dung RSA private key;
- rate limit nhận sai IP: kiểm tra `Proxy:KnownProxies` và forwarded headers.

## Giới hạn đã biết

- Chưa có HTTP endpoint cấp tài khoản/import danh sách sinh viên.
- Chưa có Google login hoặc SSO.
- Chưa có màn hình frontend cho các luồng auth.
- Rate limiter dùng bộ nhớ từng instance; triển khai nhiều replica chưa có quota dùng chung.
- Audit log chưa được ghi từ các auth handler.
- Chưa có API quản trị user, role, permission và revoke session.
- Login trả lỗi riêng cho tài khoản pending, suspended và disabled trước khi kiểm tra mật khẩu; cần thống nhất lỗi nếu mô hình đe dọa yêu cầu chống dò trạng thái tài khoản.
- Response chứa token chưa đặt `Cache-Control: no-store` ở middleware.
- `IpAddressHash` của session đã có trong model nhưng login hiện chưa ghi giá trị.
