# Role & Permission Management Backend (RBAC / PBAC)

**Trạng thái:** đã triển khai · **Cập nhật:** 14/10/2026

Tài liệu này giải thích phân hệ Quản lý Quyền và Phân quyền Vai trò (Role & Permission Management) trong module Identity: kiến trúc, luồng xử lý, cơ chế bảo mật và thu hồi phiên tức thời. Hợp đồng request/response công khai được đặc tả chi tiết tại [Role & Permission API Contract](../../../docs/ApiContract/RolePermissionApiContract.md).

---

## Phạm vi hiện tại

Backend phân hệ Role & Permission Management hỗ trợ:
- **Tra cứu danh mục quyền theo Module (`GET /api/permissions`):** Trả về toàn bộ quyền trong hệ thống được gom nhóm theo từng phân hệ (`Identity`, `ThesisManagement`, v.v.), phục vụ giao diện ma trận phân quyền;
- **Tra cứu quyền của vai trò (`GET /api/roles/{id}/permissions`):** Lấy chi tiết thông tin vai trò và danh sách các quyền hiện tại đang được cấp;
- **Cập nhật phân quyền vai trò (`PUT /api/roles/{id}/permissions`):** Đồng bộ lại toàn bộ danh sách quyền được cấp cho một vai trò;
- **Kiểm soát tính hợp lệ dữ liệu:** Kiểm tra nghiêm ngặt sự tồn tại của tất cả `permissionIds` trước khi cập nhật; trả lỗi `Identity.InvalidPermission` (400) nếu phát hiện bất kỳ ID nào không có trong hệ thống;
- **Bảo vệ vai trò Quản trị viên (`CannotRevokeAllAdminPermissions`):** Không cho phép gỡ bỏ toàn bộ quyền của vai trò `Admin`, phòng tránh rủi ro khóa chết hệ thống (lockout);
- **Cơ chế thu hồi phiên và cập nhật quyền thời gian thực:** Mọi thao tác thay đổi quyền của một vai trò đều tự động kích hoạt xoay `SecurityStamp` cho tất cả người dùng thuộc vai trò đó trong cùng một Database Transaction. Các token JWT cũ sẽ bị `SessionValidator` từ chối, buộc người dùng phải refresh token để nhận Access Token mới chứa các claims quyền cập nhật;
- **Phân quyền theo Policy động:** Endpoint được bảo vệ bằng `Permission:identity.permissions.read` và `Permission:identity.roles.manage`.

---

## Bố cục mã nguồn

```text
backend/
  Modules/Identity/
    Contracts/Authorization/
      PermissionCodes.cs          # Hằng số mã quyền: IdentityPermissionsRead, IdentityRolesManage
    Domain/
      Entities/
        Role.cs                   # Entity vai trò (Id: int, RoleName: SystemRoleName, Description)
        Permission.cs             # Entity quyền (Id: int, PermissionCode, PermissionName, Module)
        RolePermission.cs         # Quan hệ N-N (RoleId, PermissionId, AssignedAt)
      Repositories/
        IRolePermissionRepository.cs # Cổng persistence quản lý quyền và phân quyền vai trò
    Application/
      Common/
        IdentityErrors.cs         # Mã lỗi nghiệp vụ: RoleNotFound, InvalidPermission, CannotRevokeAllAdminPermissions
      Permissions/
        Dtos/PermissionDto.cs     # DTO thông tin quyền và nhóm quyền theo Module
        GetPermissions/           # Query và Handler lấy danh mục quyền gom nhóm theo Module
      Roles/
        Dtos/RoleDetailDto.cs     # DTO thông tin vai trò kèm danh sách quyền hiện tại
        GetRolePermissions/      # Query và Handler lấy quyền của một vai trò
        UpdateRolePermissions/   # Command và Handler cập nhật quyền & xoay Security Stamp
    Infrastructure/
      Endpoints/
        RolePermissionEndpoints.cs # Minimal API tại /api/permissions và /api/roles/{id}/permissions
      Persistence/
        Configurations/
          PermissionConfiguration.cs     # EF Core mapping cho bảng identity.permission
          RolePermissionConfiguration.cs # EF Core mapping cho bảng identity.role_permission
        Repositories/
          RolePermissionRepository.cs    # Triển khai IRolePermissionRepository tối ưu hóa truy vấn
```

---

## Mô hình Dữ liệu & Quan hệ

### 1. Bảng `identity.permission`
| Cột | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `id` | `integer` (PK) | Mã định danh quyền (tự tăng) |
| `permission_code` | `varchar(100)` | Mã quyền duy nhất (ví dụ: `identity.permissions.read`) |
| `permission_name` | `varchar(200)` | Tên hiển thị mô tả quyền |
| `module` | `varchar(50)` | Phân hệ sở hữu quyền (`Identity`, `ThesisManagement`, ...) |

### 2. Bảng `identity.role`
| Cột | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `id` | `integer` (PK) | Mã vai trò (`1`: Admin, `2`: Faculty, `3`: AcademicAffairs, `4`: Lecturer, `5`: Student, `6`: CouncilMember) |
| `role_name` | `varchar(50)` | Enum `SystemRoleName` định danh vai trò |
| `description` | `text` | Mô tả trách nhiệm của vai trò |

### 3. Bảng `identity.role_permission`
Bảng trung gian thiết lập quan hệ Nhiều - Nhiều (Many-to-Many):
- Khóa chính kết hợp: `(role_id, permission_id)`.
- Khóa ngoại liên kết tới `identity.role(id)` và `identity.permission(id)`.
- Cột `assigned_at` (`timestamp with time zone`): Thời điểm cấp quyền.

---

## Các luồng nghiệp vụ chính

### 1. Lấy danh mục quyền hệ thống (`GET /api/permissions`)
1. Endpoint nhận request có Bearer token hợp lệ sở hữu quyền `identity.permissions.read`.
2. `GetPermissionsHandler` gọi `IRolePermissionRepository.GetAllPermissionsAsync()`.
3. Repository thực hiện truy vấn `identity.permission` với `.AsNoTracking()`, sắp xếp theo `Module ASC, PermissionCode ASC`.
4. Handler gom nhóm (`GroupBy(p => p.Module)`) và trả về danh sách `ModulePermissionsDto` bọc trong `ApiResponse<T>`.

### 2. Lấy thông tin quyền của vai trò (`GET /api/roles/{id}/permissions`)
1. Tham số `{id:int}` được ràng buộc kiểu số nguyên trên route.
2. `GetRolePermissionsHandler` tìm kiếm Role theo `id` kèm liên kết `RolePermissions -> Permission`.
3. Nếu không tìm thấy vai trò $\rightarrow$ trả về lỗi `Identity.RoleNotFound` (404).
4. Ánh xạ dữ liệu sang `RoleDetailDto` gồm: `id`, `roleName`, `description` và danh sách quyền sắp xếp theo mã quyền.

### 3. Cập nhật phân quyền cho vai trò (`PUT /api/roles/{id}/permissions`)
1. Nhận danh sách `permissionIds` từ request body.
2. `UpdateRolePermissionsHandler` thực hiện kiểm tra an toàn theo thứ tự:
   - **Tìm Role:** Nếu Role không tồn tại $\rightarrow$ trả về `Identity.RoleNotFound` (404).
   - **Xác thực quyền đầu vào:** Truy vấn `GetPermissionsByIdsAsync(permissionIds)`. Nếu số lượng tìm thấy khác với số lượng ID yêu cầu $\rightarrow$ trả về `Identity.InvalidPermission` (400).
   - **Bảo vệ tài khoản Quản trị:** Nếu `role.RoleName == SystemRoleName.Admin` và danh sách `permissionIds` bị rỗng $\rightarrow$ chặn ngay lập tức với lỗi `Identity.CannotRevokeAllAdminPermissions` (400).
3. **Thực thi đồng bộ trong 1 Transaction (`IIdentityUnitOfWork`):**
   - Xóa bỏ các quyền cũ không còn trong danh sách và thêm các quyền mới vào bảng `identity.role_permission`.
   - Tìm danh sách tất cả tài khoản `User` đang được gán vai trò này thông qua bảng `identity.user_role`.
   - Gọi `user.RotateSecurityStamp()` cho từng tài khoản để làm mới phiên bảo mật.
   - Commit transaction.
4. Trả về `RoleDetailDto` với danh sách quyền mới được cấp.

---

## Cơ chế Phân quyền & Thu hồi Phiên tức thời

1. **Permission-Based Access Control (PBAC):**
   - Hệ thống dùng `PermissionAuthorizationHandler` để kiểm tra claim `permission` được đính kèm trong JWT Access Token.
   - Khi người dùng đăng nhập (`POST /api/auth/login`), `UserProfileReader` sẽ truy vấn toàn bộ quyền từ các vai trò của người dùng trong cơ sở dữ liệu và đóng gói vào danh sách claims của token.

2. **Thu hồi phiên khi phân quyền thay đổi (Zero-Trust Session Invalidation):**
   - Khi một vai trò bị thay đổi quyền (thêm bớt quyền), việc cập nhật chỉ có ý nghĩa nếu người dùng đang đăng nhập không tiếp tục dùng các đặc quyền cũ.
   - Bằng cách tự động xoay `SecurityStamp` cho toàn bộ người dùng thuộc vai trò bị thay đổi, ở ngay request kế tiếp, `SessionValidator` sẽ phát hiện sự không khớp giữa `SecurityStamp` trong session snapshot với `User.SecurityStamp` hiện tại trong DB.
   - Kết quả: Phiên cũ lập tức bị vô hiệu hóa; client được hướng dẫn gọi `/api/auth/refresh` để nhận Access Token mới chứa danh sách claims quyền đã được cập nhật từ database.

---

## Hướng dẫn Kiểm thử

### Chạy Unit Tests
```powershell
dotnet test backend/tests/UGTM.Modules.Identity.Tests/UGTM.Modules.Identity.Tests.csproj --filter "FullyQualifiedName~RolePermission"
```

Bộ kiểm thử [`RolePermissionHandlerTests.cs`](../../tests/UGTM.Modules.Identity.Tests/Application/RolePermissionHandlerTests.cs) bao quát 100% các tình huống:
- Lấy danh mục quyền thành công và gom nhóm chính xác theo Module;
- Lấy thông tin quyền của Role thành công;
- Trả 404 `Identity.RoleNotFound` khi Role không tồn tại;
- Cập nhật quyền thành công và xoay `SecurityStamp` cho người dùng thuộc vai trò;
- Trả 400 `Identity.InvalidPermission` khi truyền danh sách `permissionIds` chứa mã không hợp lệ;
- Trả 400 `Identity.CannotRevokeAllAdminPermissions` khi cố gắng gỡ sạch quyền của vai trò Admin.
