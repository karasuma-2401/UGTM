# Role & Permission Management API (RBAC/PBAC)

**Trạng thái:** đã triển khai · **Cập nhật:** 20/10/2026
**Base path:** `/api/permissions`, `/api/roles`

Tài liệu này đặc tả chi tiết các endpoint quản lý danh mục quyền và phân quyền cho vai trò (Role-Based & Permission-Based Access Control) trong module Identity.

---

## Quy ước chung

- Mọi request và response đều định dạng JSON `camelCase`; thời gian theo chuẩn ISO 8601 UTC.
- Tất cả endpoint đều yêu cầu xác thực bằng header: `Authorization: Bearer <access-token>`.
- Quyền truy cập được kiểm soát thông qua Permission Policies kết hợp **Admin Role Bypass**:
  - `GET /api/permissions`: yêu cầu quyền `identity.permissions.read` hoặc vai trò `Admin`.
  - `POST /api/roles`: yêu cầu quyền `identity.roles.manage` hoặc vai trò `Admin`.
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
- Lỗi trùng lặp dữ liệu: `409 Conflict` (schema `ApiResponse<object>`).
- Lỗi không tìm thấy: `404 Not Found` (schema `ApiResponse<object>`).
- Lỗi chưa xử lý: `500 InternalServerError`.

---

## Danh mục Endpoints

| Phương thức | Đường dẫn | Quyền yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| **GET** | `/api/permissions` | `identity.permissions.read` hoặc vai trò `Admin` | Lấy danh mục tất cả quyền trong hệ thống, nhóm theo Module |
| **POST** | `/api/roles` | `identity.roles.manage` hoặc vai trò `Admin` | Tạo vai trò người dùng mới trong hệ thống kèm quyền ban đầu (tùy chọn) |
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

### 2. `POST /api/roles` — Tạo vai trò người dùng mới

Tạo một vai trò người dùng mới trong hệ thống. Có thể gán danh sách quyền ban đầu cho vai trò ngay khi tạo.

#### Request Body

```json
{
  "roleName": "DepartmentHead",
  "description": "Trưởng Bộ môn",
  "permissionIds": [1, 2, 4]
}
```

| Trường | Kiểu | Bắt buộc | Ràng buộc & Mô tả |
| :--- | :--- | :---: | :--- |
| `roleName` | `string` | Có | Tên vai trò (độ dài 2-50 ký tự, phải chứa chữ cái, không được là chuỗi số thuần túy hoặc chứa ký tự đặc biệt không hợp lệ, không phân biệt hoa thường khi kiểm tra trùng lặp) |
| `description` | `string?` | Không | Mô tả vai trò (tối đa 255 ký tự) |
| `permissionIds` | `integer[]?` | Không | Danh sách ID các quyền gán ban đầu cho vai trò. Mọi ID phải tồn tại trong bảng `identity.permission`. Tự động loại bỏ ID trùng lặp |

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "roleId": 7,
    "roleName": "DepartmentHead",
    "description": "Trưởng Bộ môn",
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
| 400 | `Identity.InvalidRoleName` | Tên vai trò trống, độ dài không hợp lệ (< 2 hoặc > 50 ký tự), là chuỗi số hoặc chứa ký tự đặc biệt không hợp lệ |
| 400 | `Identity.InvalidPermission` | Danh sách quyền chứa ID không tồn tại hoặc ID $\le 0$ |
| 400 | `Identity.InvalidInput` | Trường `description` vượt quá 255 ký tự |
| 401 | — | Thiếu token hoặc token không hợp lệ |
| 403 | — | Người dùng không có quyền `identity.roles.manage` và không phải Admin |
| 409 | `Identity.RoleAlreadyExists` | Tên vai trò đã tồn tại trong hệ thống (kiểm tra không phân biệt hoa thường) |

---

### 3. `GET /api/roles/{id}/permissions` — Lấy danh sách quyền của Vai trò

Lấy thông tin vai trò cùng danh sách chi tiết các quyền hiện tại đang được gán.

#### Path Parameters
- `id` (`integer`): ID của vai trò (Ví dụ: `1` = Admin, `2` = Faculty, `3` = AcademicAffairs, `4` = Lecturer, `5` = Student, `6` = CouncilMember, hoặc ID của custom role vừa tạo).

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
| 401 | — | Thiếu token hoặc token không hợp lệ |
| 403 | — | Người dùng không có quyền `identity.roles.manage` và không phải Admin |
| 404 | `Identity.RoleNotFound` | Không tìm thấy vai trò với `id` cung cấp |

---

### 4. `PUT /api/roles/{id}/permissions` — Cập nhật danh sách quyền cho Vai trò

Đồng bộ lại toàn bộ danh sách quyền của vai trò theo `permissionIds` được truyền lên.

#### Path Parameters
- `id` (`integer`): ID của vai trò cần cập nhật.

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
