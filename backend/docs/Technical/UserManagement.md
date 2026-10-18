# User Management Backend

**Trạng thái:** đã triển khai · **Cập nhật:** 17/10/2026

Tài liệu này giải thích phân hệ Quản lý Người dùng (User Management) trong module Identity: cấu trúc mã nguồn, luồng dữ liệu, cơ chế bảo mật và cách vận hành. Hợp đồng request/response công khai được đặc tả chi tiết tại [User Management API Contract](../../../docs/ApiContract/UserManagementApiContract.md).

## Phạm vi hiện tại

Backend phân hệ User Management hỗ trợ:
- Tra cứu danh sách người dùng có phân trang (`PagedResult<T>`), tìm kiếm đa trường (Email, Họ tên, Mã người dùng) và lọc theo Vai trò (Role), Trạng thái (Status);
- Tạo tài khoản người dùng đơn lẻ bởi Quản trị viên, hỗ trợ hai phương thức: gửi email kích hoạt tự động qua Transactional Outbox (Magic Link) hoặc kích hoạt ngay bằng mật khẩu ban đầu;
- Import Excel tối đa 5.000 Sinh viên hoặc Giảng viên theo cơ chế all-or-nothing; tài khoản chờ người dùng chủ động yêu cầu magic link;
- Cập nhật thông tin hồ sơ (Họ tên, Số điện thoại, Mã người dùng) và đồng bộ danh sách vai trò hệ thống;
- Cập nhật trạng thái tài khoản: Khóa (`Disabled`), Tạm ngưng (`Suspended`) hoặc Mở khóa (`Active`);
- Cơ chế thu hồi phiên tức thời: Mọi thao tác khóa tài khoản hoặc thay đổi vai trò đều kích hoạt xoay `SecurityStamp`, khiến các token JWT đang lưu hành bị `SessionValidator` từ chối ngay lập tức;
- Quản trị phiên của từng tài khoản: xem, thu hồi một phiên hoặc toàn bộ phiên bằng `identity.sessions.revoke`, kèm Audit Log;
- Phân quyền theo Policy động `Permission:identity.users.read` và `Permission:identity.users.manage`.
- Bảo vệ Admin cuối cùng bằng khóa transaction: không thể đồng thời gỡ vai trò, tạm ngưng hoặc vô hiệu hóa toàn bộ Admin đang hoạt động.

---

## Bố cục mã nguồn

```text
backend/
  BuildingBlocks/Application/Responses/
    PagedResult.cs            # Model phân trang generic dùng chung cho toàn hệ thống
  Modules/Identity/
    Domain/
      Entities/User.cs        # Aggregate Root User (Create, UpdateProfile, Reactivate, Suspend, Disable)
      Repositories/IUserRepository.cs
    Application/Users/
      Dtos/UserSummaryDto.cs  # DTO kết quả người dùng cho danh sách và chi tiết
      GetUsers/               # Query, Handler và Read Model Interface (IUserQueryReader)
      CreateUser/             # Command và Handler tạo người dùng (gán vai trò, hash password hoặc outbox)
      ImportUsers/            # Đọc model trung gian, validation toàn file và import atomically
      UpdateUser/             # Command và Handler cập nhật hồ sơ và vai trò
      UpdateUserStatus/       # Command và Handler đổi trạng thái tài khoản
    Infrastructure/
      Authorization/          # PermissionPolicyProvider và PermissionAuthorizationHandler
      Endpoints/UserEndpoints.cs # Minimal API tại /api/users
      Importing/ClosedXmlUserImportFileReader.cs # Đọc worksheet .xlsx và ánh xạ header
      Persistence/Repositories/
        UserRepository.cs     # Triển khai phương thức GetByIdWithRoles, Exists, UpdateRoles
        UserQueryReader.cs    # Triển khai truy vấn phân trang, tìm kiếm ILike với AsNoTracking
```

---

## Mô hình Dữ liệu & Aggregate Root `User`

Tài khoản người dùng được đại diện bởi Aggregate Root [`User`](../../Modules/Identity/Domain/Entities/User.cs):

### Các trạng thái tài khoản (`AccountStatus`):
| Trạng thái | Quyền đăng nhập | Ý nghĩa |
| :--- | :---: | :--- |
| `PendingActivation` | Không | Tài khoản mới được cấp bởi Admin, đang chờ người dùng đặt mật khẩu qua Magic Link email |
| `Active` | Có | Tài khoản hoạt động bình thường |
| `Suspended` | Không | Tài khoản bị tạm ngưng hoạt động bởi Admin |
| `Disabled` | Không | Tài khoản bị vô hiệu hóa / khóa vĩnh viễn |

### Vai trò của `SecurityStamp` và Concurrency:
- `SecurityStamp`: Đại diện cho phiên bản bảo mật của tài khoản. Mỗi khi trạng thái tài khoản chuyển sang `Suspended` hoặc `Disabled`, hoặc khi danh sách vai trò bị thay đổi, phương thức domain sẽ gọi `RotateSecurityStamp()`.
- `SessionValidator` đọc `SecurityStamp` snapshot từ phiên lưu trong cơ sở dữ liệu. Ngay khi `SecurityStamp` lệch, các request kế tiếp dùng JWT Access Token cũ (dù còn hạn 10 phút) sẽ bị trả về `401 Unauthorized` ngay lập tức.
- `ConcurrencyStamp`: Token kiểm soát tương tranh lạc quan (Optimistic Concurrency Control), tự động làm mới qua `Touch(now)` mỗi khi bản ghi có cập nhật.

---

## Các luồng nghiệp vụ chính

### 1. Luồng Lấy danh sách người dùng (`GET /api/users`)
1. Controller/Endpoint nhận query parameters: `keyword`, `role`, `status`, `pageNumber`, `pageSize`.
2. `GetUsersHandler` chuẩn hóa tham số phân trang (`pageNumber >= 1`, `1 <= pageSize <= 100`).
3. `UserQueryReader` thực thi truy vấn EF Core trên bảng `identity.user`:
   - Sử dụng `.AsNoTracking()` để tối ưu hóa bộ nhớ và tốc độ truy vấn.
   - Tìm kiếm không phân biệt hoa thường bằng `EF.Functions.ILike` trên `NormalizedEmail`, `FullName` và `UserCode`.
   - Lọc theo `status` và `role` (thông qua bảng liên kết `identity.user_role`). Nếu bộ lọc chứa chuỗi số (ví dụ: `"999"`, `"0"`, `"-1"`) hoặc giá trị ngoài enum, trả lỗi `400 Bad Request` (`Identity.InvalidRole` / `Identity.InvalidStatus`).
   - Sắp xếp mặc định: `CreatedAt DESC`.
   - Đếm tổng số bản ghi (`CountAsync`) và phân trang (`Skip().Take()`).
4. Trả về `PagedResult<UserSummaryDto>` bọc trong envelope `ApiResponse<T>`.

### 2. Luồng Tạo người dùng (`POST /api/users`)
1. Admin gửi thông tin: Email, Họ tên, Mã người dùng, SĐT, Danh sách vai trò, và tùy chọn `initialPassword`.
2. `CreateUserHandler` kiểm tra:
   - Tính bắt buộc của Email, Họ tên và tính hợp lệ của các vai trò (`SystemRoleName`). Chặn đứng chuỗi số (ví dụ: `"999"`, `"0"`, `"-1"`) hoặc tên vai trò ngoài danh mục với lỗi `Identity.InvalidRole` (400).
   - Tính hợp lệ của Số điện thoại nếu có cung cấp (`PhoneNumberValidator`: 10 chữ số chuẩn Việt Nam bắt đầu bằng `0` hoặc `+84`).
   - Kiểm tra tính duy nhất của Email và Mã người dùng (`ExistsByEmailAsync`, `ExistsByUserCodeAsync`).
3. **Phân nhánh xử lý:**
   - **Trường hợp có `initialPassword`:** Mật khẩu được kiểm tra qua `IPasswordPolicy` ($\ge 12$ ký tự, chữ hoa, chữ thường, số, ký tự đặc biệt). Mật khẩu được băm bằng `IPasswordService` (sử dụng ASP.NET Core `PasswordHasher<User>`). Tài khoản được kích hoạt `Active` ngay lập tức.
   - **Trường hợp không có `initialPassword` (Mặc định):** Tài khoản được tạo ở trạng thái `PendingActivation`. Một `OneTimeChallenge` (mục đích `ActivateAccount`, thời hạn 24 giờ) được tạo ra. Nội dung email chứa Magic Link được mã hóa bằng ASP.NET Core Data Protection và đưa vào bảng `identity.outbox_message`.
4. Gán vai trò cho người dùng (`AddToRoleAsync`) và lưu tất cả thay đổi trong một Database Transaction duy nhất (`IIdentityUnitOfWork`).
5. Background Worker `IdentityOutboxProcessor` định kỳ 5 giây quét outbox và gửi email kích hoạt qua SMTP/Mailpit.

### 3. Luồng Import Excel (`POST /api/users/import`)

1. Client tải template động từ `GET /api/users/import-template?role=Student|Lecturer`, sau đó endpoint import nhận multipart file `.xlsx` tối đa 5 MB và vai trò tương ứng; cả hai yêu cầu permission `identity.users.import` hoặc Admin bypass.
2. `ClosedXmlUserImportFileReader` đọc worksheet đầu, ánh xạ bốn cột Email/FullName/UserCode/PhoneNumber và từ chối ô công thức.
3. `ImportUsersHandler` chuẩn hóa dữ liệu, kiểm tra định dạng, giới hạn 5.000 dòng, trùng email/code trong file và database.
4. Nếu có bất kỳ lỗi nào, trả lỗi theo số dòng/cột và không ghi dữ liệu. Nếu hợp lệ, toàn bộ user cùng role được ghi trong một transaction ở trạng thái `PendingActivation`.
5. Import không tạo `OneTimeChallenge` và không ghi email outbox. Người dùng gọi `/api/identity/auth/activation/resend` bằng email/mã người dùng; magic link chỉ được tạo và gửi ở thời điểm đó.
6. Các luồng tạo tài khoản đơn lẻ, provisioning và import dùng chung transaction advisory lock để tránh hai request đồng thời vượt qua kiểm tra trùng email/code.

### 4. Luồng Cập nhật hồ sơ & vai trò (`PUT /api/users/{id}`)
1. `UpdateUserHandler` tìm user theo `id` kèm thông tin vai trò hiện tại. Nếu không tồn tại $\rightarrow$ trả `Identity.UserNotFound` (404).
2. Kiểm tra tính hợp lệ của SĐT mới nếu có cung cấp (`PhoneNumberValidator`: 10 chữ số).
3. Nếu `userCode` thay đổi, kiểm tra xem mã mới có trùng với bất kỳ tài khoản nào khác không $\rightarrow$ trả `Identity.DuplicateAccount` (409).
4. Gọi domain method `user.UpdateProfile(...)` để cập nhật Họ tên, SĐT, Mã người dùng (chỉ xoay `SecurityStamp` khi danh sách vai trò thay đổi).
5. Nếu có danh sách vai trò mới, kiểm tra tính hợp lệ (chặn chuỗi số hoặc vai trò lạ), sau đó gọi `UpdateRolesAsync` để thực hiện đồng bộ vi mô: gỡ bỏ các vai trò cũ không còn và thêm vào các vai trò mới.
6. Commit transaction và trả về thông tin `UserSummaryDto` sau cập nhật.

### 5. Luồng Cập nhật trạng thái tài khoản (`PATCH /api/users/{id}/status`)
1. Nhận trạng thái mục tiêu: `Active`, `Suspended`, hoặc `Disabled`. Chặn đứng chuỗi số (ví dụ `"999"`, `"0"`, `"-1"`), giá trị lạ, hoặc cố tình gán thủ công `PendingActivation` với lỗi `Identity.InvalidStatus` (400).
2. `UpdateUserStatusHandler` tìm user theo `id`:
   - Nếu `Active`: Gọi `user.Reactivate(now)` $\rightarrow$ chuyển trạng thái, xóa lockout, reset số lần đăng nhập sai về 0, xoay `SecurityStamp`.
   - Nếu `Suspended`: Gọi `user.Suspend(now)` $\rightarrow$ chuyển trạng thái, xoay `SecurityStamp`.
   - Nếu `Disabled`: Gọi `user.Disable(now)` $\rightarrow$ chuyển trạng thái, xoay `SecurityStamp`.
3. Commit transaction. Mọi phiên đăng nhập của tài khoản này ngay lập tức bị vô hiệu hóa.

Nếu tài khoản mục tiêu là Admin đang hoạt động, thao tác chuyển sang `Suspended` hoặc `Disabled` lấy PostgreSQL transaction advisory lock trước khi đếm các Admin còn lại. Cùng khóa này được dùng khi gỡ vai trò Admin và khi bootstrap Admin đầu tiên, nên các request hoặc replica chạy đồng thời không thể cùng vượt qua kiểm tra “Admin cuối cùng”.

---

## Phân quyền & Bảo mật

Phân hệ sử dụng cơ chế Dynamic Permission Policy kết hợp phân quyền theo vai trò:
- Endpoint `GET /api/users` yêu cầu: `Permission:identity.users.read`.
- Các endpoint `POST`, `PUT`, `PATCH` yêu cầu: `Permission:identity.users.manage`.
- Các endpoint `/api/users/{userId}/sessions` yêu cầu: `Permission:identity.sessions.revoke`.
- **Cơ chế Phân quyền (PBAC & Admin Bypass):** [`PermissionAuthorizationHandler`](../../Modules/Identity/Infrastructure/Authorization/PermissionAuthorizationHandler.cs) kiểm tra claim `permission` được nạp từ cơ sở dữ liệu vào JWT token. Đặc biệt, người dùng sở hữu vai trò `Admin` (Role claim: `Admin`) được tự động cấp quyền thực thi tất cả các API mà không cần phải gán quyền thủ công trong cơ sở dữ liệu mỗi khi có quyền mới phát sinh.

Migration Identity tạo danh mục 6 quyền bằng `permission_code` (không phụ thuộc ID cố định) và chỉ bổ sung các gán mặc định còn thiếu:

| Vai trò | Quyền mặc định |
| --- | --- |
| `Admin` | Toàn bộ 6 quyền Identity |
| `Faculty` | `identity.users.read` |
| `AcademicAffairs` | `identity.users.read`, `identity.users.import` |

Migration không xóa quyền có sẵn hoặc gán quyền do đơn vị vận hành tùy chỉnh. Migration hardening chỉ dọn ba gán mặc định sai của phiên bản Phase 1 cũ khi chúng còn mang đúng timestamp seed.

---

## Khởi tạo Admin đầu tiên

Áp dụng migration trước, sau đó chạy một lần từ thư mục gốc:

```powershell
./scripts/Database.ps1 -Action Update
./scripts/BootstrapAdmin.ps1 -Email admin@ugtm.edu.vn -FullName 'System Administrator'
```

Script đọc `BOOTSTRAP_ADMIN_PASSWORD` từ environment/`.env`; nếu chưa có thì hỏi bằng secure prompt. Mật khẩu chỉ được chuyển cho API process qua environment và không được đưa vào command line. Không truyền mật khẩu bằng tham số PowerShell hoặc `dotnet --password`.

Có thể chạy trực tiếp trong môi trường deploy bằng cách đặt `BOOTSTRAP_ADMIN_EMAIL` và `BOOTSTRAP_ADMIN_PASSWORD`. Bootstrap tạo ngay một Admin `Active`, chỉ thành công khi hệ thống chưa có bất kỳ tài khoản mang vai trò Admin nào, và an toàn khi nhiều replica khởi động đồng thời. Sau khi có Admin đầu tiên, quản trị viên import/tạo người dùng; người dùng tự yêu cầu/kích hoạt qua magic link theo luồng activation hiện có.

---

## Hướng dẫn Kiểm thử

### Chạy Unit Tests
```powershell
dotnet test backend/tests/UGTM.Modules.Identity.Tests/UGTM.Modules.Identity.Tests.csproj --filter "FullyQualifiedName!~Integration"
```
Bộ kiểm thử bao gồm:
- Domain tests: `UserTests.cs` (kiểm tra invariants, `User.Create`, `UpdateProfile`, `Reactivate`, `Suspend`, `Disable`).
- Application tests: `UsersHandlerTests.cs` (kiểm tra phân trang, validation, trùng email/code, outbox email, đổi status).
- Infrastructure tests: `AuthorizationTests.cs` (kiểm tra Admin role bypass).

### Kiểm tra định dạng & biên dịch Solution
```powershell
dotnet build UGTM.sln
dotnet format UGTM.sln --verify-no-changes
```
