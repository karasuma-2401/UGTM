# Role & Permission Management API (RBAC/PBAC)

**Trạng thái:** đã triển khai · **Cập nhật:** 16/10/2026
**Base path:** `/api/permissions`, `/api/roles`

Tài liệu này đặc tả chi tiết các endpoint quản lý danh mục quyền và phân quyền cho vai trò (Role-Based & Permission-Based Access Control) trong module Identity.

---

## Quy ước chung

- Mọi request và response đều định dạng JSON `camelCase`; thời gian theo chuẩn ISO 8601 UTC.
- Tất cả endpoint đều yêu cầu xác thực bằng header: `Authorization: Bearer <access-token>`.
- Quyền truy cập được kiểm soát thông qua Permission Policies kết hợp **Admin Role Bypass**:
  - `GET /api/permissions`: yêu cầu quyền `identity.permissions.read` hoặc vai trò `Admin`.
  - `GET /api/roles/{id}/permissions`: yêu cầu quyền `identity.roles.manage` hoặc vai trò `Admin`.
  - `PUT /api/roles/{id}/permissions`: yêu cầu quyền `identity.roles.manage` hoặc vai trò `Admin`.
  > [!NOTE]
  > **Cơ chế Admin Role Bypass:** Người dùng sở hữu vai trò `Admin` (Role claim: `Admin`) được tự động cấp quyền thực thi tất cả các API phân quyền mà không phụ thuộc vào claim `permission` được gán trong DB.

- Cấu trúc phản hồi tuân thủ envelope chuẩn `ApiResponse<T>`:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

Nếu thất bại:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Identity.RoleNotFound",
    "message": "Không tìm thấy vai trò người dùng.",
    "errors": null
  }
}
```

Các phản hồi từ tầng pipeline ASP.NET Core & OpenApi/Scalar:
- Thiếu hoặc token JWT không hợp lệ / phiên đã thu hồi: `401 Unauthorized` (body rỗng).
- Không đủ quyền hạn (thiếu permission và không phải Admin): `403 Forbidden` (body rỗng).
- Lỗi nghiệp vụ dữ liệu đầu vào: `400 Bad Request` (schema `ApiResponse<object>`).
- Lỗi không tìm thấy: `404 Not Found` (schema `ApiResponse<object>`).
- Lỗi chưa xử lý: `500 InternalServerError`.

---

## Danh mục Endpoints

| Phương thức | Đường dẫn | Quyền yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| **GET** | `/api/permissions` | `identity.permissions.read` hoặc vai trò `Admin` | Lấy danh mục tất cả quyền trong hệ thống, nhóm theo Module |
| **GET** | `/api/roles/{id}/permissions` | `identity.roles.manage` hoặc vai trò `Admin` | Lấy thông tin vai trò và danh sách quyền đang được cấp |
| **PUT** | `/api/roles/{id}/permissions` | `identity.roles.manage` hoặc vai trò `Admin` | Cập nhật lại toàn bộ danh sách quyền cho vai trò |

---

## Chi tiết từng Endpoint

### 1. `GET /api/permissions` — Lấy danh mục quyền theo Module

Trả về danh mục toàn bộ quyền (Permissions) có trong hệ thống, được nhóm theo từng phân hệ (Module) để phục vụ việc hiển thị cây/bảng phân quyền trên giao diện người dùng.

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": [
    {
      "module": "DefenseCouncil",
      "permissions": [
        {
          "id": 10,
          "permissionCode": "council.evaluate.manage",
          "permissionName": "Chấm điểm và đánh giá hội đồng bảo vệ",
          "module": "DefenseCouncil"
        }
      ]
    },
    {
      "module": "Identity",
      "permissions": [
        {
          "id": 1,
          "permissionCode": "identity.permissions.read",
          "permissionName": "Xem danh mục quyền hệ thống",
          "module": "Identity"
        },
        {
          "id": 2,
          "permissionCode": "identity.roles.manage",
          "permissionName": "Quản lý và phân quyền vai trò",
          "module": "Identity"
        },
        {
          "id": 3,
          "permissionCode": "identity.users.manage",
          "permissionName": "Tạo, sửa thông tin và trạng thái người dùng",
          "module": "Identity"
        },
        {
          "id": 4,
          "permissionCode": "identity.users.read",
          "permissionName": "Xem danh sách và chi tiết người dùng",
          "module": "Identity"
        }
      ]
    },
    {
      "module": "ThesisManagement",
      "permissions": [
        {
          "id": 20,
          "permissionCode": "thesis.reports.review",
          "permissionName": "Duyệt báo cáo tiến độ và đề cương",
          "module": "ThesisManagement"
        }
      ]
    }
  ],
  "error": null
}
```

| HTTP Status | Mã lỗi | Nguyên nhân |
| :--- | :--- | :--- |
| 401 | — | Thiếu Access Token hoặc Token không hợp lệ / hết hạn |
| 403 | — | Người dùng không có quyền `identity.permissions.read` và không phải Admin |

---

### 2. `GET /api/roles/{id}/permissions` — Lấy danh sách quyền của Vai trò

Lấy thông tin vai trò cùng danh sách chi tiết các quyền hiện tại đang được gán.

#### Path Parameters
- `id` (`integer`): ID của vai trò (Ví dụ: `1` = Admin, `2` = Faculty, `3` = AcademicAffairs, `4` = Lecturer, `5` = Student, `6` = CouncilMember).

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "roleId": 4,
    "roleName": "Lecturer",
    "description": "Giảng viên",
    "permissions": [
      {
        "id": 4,
        "permissionCode": "identity.users.read",
        "permissionName": "Xem danh sách và chi tiết người dùng",
        "module": "Identity"
      },
      {
        "id": 20,
        "permissionCode": "thesis.reports.review",
        "permissionName": "Duyệt báo cáo tiến độ và đề cương",
        "module": "ThesisManagement"
      }
    ]
  },
  "error": null
}
```

| HTTP Status | Mã lỗi | Nguyên nhân |
| :--- | :--- | :--- |
| 401 | — | Thiếu Access Token hoặc Token không hợp lệ / hết hạn |
| 403 | — | Người dùng không có quyền `identity.roles.manage` và không phải Admin |
| 404 | `Identity.RoleNotFound` | Không tìm thấy vai trò với `id` cung cấp |

---

### 3. `PUT /api/roles/{id}/permissions` — Cập nhật danh sách quyền cho Vai trò

Cập nhật lại danh sách quyền của một vai trò theo cơ chế đối soát (reconcile).

#### Cơ chế bảo mật & thu hồi phiên tức thời:
- **Bắt buộc truyền mảng `permissionIds`:** Nếu thiếu trường `permissionIds` hoặc truyền `"permissionIds": null`, hệ thống trả lỗi `400 Bad Request` (`Identity.InvalidPermission`). Chỉ khi truyền mảng rỗng `[]` tường minh mới thực hiện gỡ toàn bộ quyền (đối với vai trò khác Admin).
- **Tự động khử trùng lặp:** Hệ thống tự động loại bỏ các `permissionIds` trùng lặp trong mảng trước khi kiểm tra tính hợp lệ và cập nhật vào cơ sở dữ liệu.
- **Tự động xoay Security Stamp khi có thay đổi:** Hệ thống chỉ thực hiện ghi DB và xoay `SecurityStamp` cho người dùng thuộc vai trò đó khi tập quyền mới **thực sự khác** với tập quyền hiện tại. Nếu danh sách quyền giữ nguyên (no-op), các phiên đăng nhập hiện tại của người dùng sẽ không bị gián đoạn.
- Nhờ đó, khi quyền thực sự thay đổi, tất cả token JWT cũ của những người dùng này sẽ bị `SessionValidator` từ chối ngay lập tức và buộc phải làm mới phiên để nhận claims quyền mới nhất từ database.
- **Bảo vệ vai trò Admin:** Không cho phép gỡ bỏ sạch toàn bộ quyền của vai trò `Admin` (phải giữ ít nhất 1 quyền) để tránh tình trạng khóa chết quyền quản trị hệ thống.

#### Path Parameters
- `id` (`integer`): ID của vai trò cần phân quyền.

#### Request Body

```json
{
  "permissionIds": [1, 2, 3, 4]
}
```

| Trường | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `permissionIds` | `integer[]` | Có | Danh sách ID các quyền gán cho vai trò (mảng rỗng `[]` tường minh nếu gỡ hết quyền, trừ vai trò Admin; không chấp nhận `null`). Các giá trị trùng lặp sẽ tự động được gộp |

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "roleId": 4,
    "roleName": "Lecturer",
    "description": "Giảng viên",
    "permissions": [
      {
        "id": 1,
        "permissionCode": "identity.permissions.read",
        "permissionName": "Xem danh mục quyền hệ thống",
        "module": "Identity"
      },
      {
        "id": 4,
        "permissionCode": "identity.users.read",
        "permissionName": "Xem danh sách và chi tiết người dùng",
        "module": "Identity"
      }
    ]
  },
  "error": null
}
```

| HTTP Status | Mã lỗi | Nguyên nhân |
| :--- | :--- | :--- |
| 400 | `Identity.InvalidPermission` | Thiếu trường `permissionIds` (`null`) hoặc một/nhiều `permissionId` trong danh sách không tồn tại trong hệ thống |
| 400 | `Identity.CannotRevokeAllAdminPermissions` | Cố tình xóa sạch toàn bộ quyền của vai trò Admin |
| 401 | — | Thiếu token hoặc token không hợp lệ |
| 403 | — | Người dùng không có quyền `identity.roles.manage` và không phải Admin |
| 404 | `Identity.RoleNotFound` | Không tìm thấy vai trò với `id` cung cấp |
