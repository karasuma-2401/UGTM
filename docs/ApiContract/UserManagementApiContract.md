# User Management API

**Trạng thái:** đã triển khai · **Cập nhật:** 13/10/2026
**Base path:** `/api/users`

Tài liệu này đặc tả chi tiết các endpoint Quản lý Người dùng (User Management) trong hệ thống UGTM. Tài liệu kỹ thuật kiến trúc đối chiếu nằm tại [User Management Technical](../../backend/docs/Technical/UserManagement.md).

## Quy ước chung

- Request và Response dùng định dạng JSON `camelCase`; thời gian dùng chuẩn ISO 8601 UTC.
- Tất cả các endpoint quản lý người dùng đều yêu cầu gửi kèm header `Authorization: Bearer <access-token>`.
- Quyền truy cập được kiểm soát thông qua Permission Policies:
  - Xem danh sách: `identity.users.read` (hoặc vai trò `Admin`).
  - Quản trị (tạo, sửa, đổi trạng thái): `identity.users.manage` (hoặc vai trò `Admin`).
- Phản hồi chuẩn theo mô hình Envelope `ApiResponse<T>`:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

Phản hồi khi có lỗi nghiệp vụ:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Identity.UserNotFound",
    "message": "Không tìm thấy tài khoản.",
    "errors": null
  }
}
```

Các phản hồi từ tầng pipeline ASP.NET Core:
- Thiếu hoặc token JWT không hợp lệ / phiên đã thu hồi: `401 Unauthorized` (body rỗng).
- Không đủ quyền hạn (thiếu permission và không phải Admin): `403 Forbidden` (body rỗng).
- Lỗi chưa xử lý: `500 application/problem+json` kèm `requestId`.

---

## Danh sách Endpoint

| Method | Path | Quyền yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/users` | `identity.users.read` hoặc `Admin` | Tra cứu, tìm kiếm và phân trang danh sách người dùng |
| `POST` | `/api/users` | `identity.users.manage` hoặc `Admin` | Tạo tài khoản người dùng đơn lẻ |
| `PUT` | `/api/users/{id}` | `identity.users.manage` hoặc `Admin` | Cập nhật thông tin hồ sơ và danh sách vai trò |
| `PATCH` | `/api/users/{id}/status` | `identity.users.manage` hoặc `Admin` | Khóa / Mở khóa / Tạm ngưng tài khoản người dùng |

---

## Chi tiết các Endpoint

### 1. `GET /api/users` — Lấy danh sách người dùng

Hỗ trợ phân trang, tìm kiếm theo từ khóa và lọc theo vai trò, trạng thái tài khoản.

#### Query Parameters

| Tham số | Kiểu | Mặc định | Mô tả |
| :--- | :--- | :--- | :--- |
| `pageNumber` | `integer` | `1` | Số trang cần lấy ($\ge 1$) |
| `pageSize` | `integer` | `10` | Số lượng người dùng trên một trang ($1 \le pageSize \le 100$) |
| `keyword` | `string` | — | Từ khóa tìm kiếm không phân biệt hoa thường (so khớp Email, FullName, UserCode) |
| `role` | `string` | — | Lọc theo vai trò (`Student`, `Lecturer`, `Admin`, `Faculty`, `AcademicAffairs`, `CouncilMember`) |
| `status` | `string` | — | Lọc theo trạng thái (`PendingActivation`, `Active`, `Suspended`, `Disabled`) |

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "11111111-1111-4111-8111-111111111111",
        "email": "24521186@gm.uit.edu.vn",
        "fullName": "Nguyễn Văn A",
        "userCode": "24521186",
        "phoneNumber": "0912345678",
        "status": "Active",
        "roles": [
          "Student"
        ],
        "createdAt": "2026-10-12T01:00:00Z",
        "lastLoginAt": "2026-10-13T08:10:00Z"
      },
      {
        "id": "22222222-2222-4222-8222-222222222222",
        "email": "gvhd@ugtm.edu.vn",
        "fullName": "TS. Trần Văn B",
        "userCode": "GV001",
        "phoneNumber": "0987654321",
        "status": "Active",
        "roles": [
          "Lecturer",
          "CouncilMember"
        ],
        "createdAt": "2026-10-12T02:00:00Z",
        "lastLoginAt": null
      }
    ],
    "totalCount": 42,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 5,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "error": null
}
```

---

### 2. `POST /api/users` — Tạo người dùng đơn lẻ

Tạo tài khoản người dùng mới và phân vai trò.

#### Cơ chế kích hoạt:
- **Nếu `initialPassword` để trống (hoặc `null`):** Tài khoản được tạo ở trạng thái `PendingActivation`. Nếu `sendActivationEmail = true`, hệ thống tự động sinh `OneTimeChallenge` (thời hạn 24 giờ) và đưa email chứa Magic Link vào Transactional Outbox để gửi đến hòm thư người dùng tự đặt mật khẩu.
- **Nếu `initialPassword` được cung cấp:** Hệ thống kiểm tra độ mạnh mật khẩu ($\ge 12$ ký tự, đủ chữ hoa, chữ thường, chữ số, ký tự đặc biệt), băm mật khẩu và kích hoạt tài khoản sang trạng thái `Active` ngay lập tức.

#### Request Body

```json
{
  "email": "lecturer02@ugtm.edu.vn",
  "fullName": "ThS. Lê Thị C",
  "userCode": "GV002",
  "phoneNumber": "0901234567",
  "roles": [
    "Lecturer"
  ],
  "initialPassword": null,
  "sendActivationEmail": true
}
```

| Trường | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `email` | `string` | Có | Địa chỉ email của người dùng (tối đa 150 ký tự) |
| `fullName` | `string` | Có | Họ và tên đầy đủ (tối đa 150 ký tự) |
| `userCode` | `string` | Không | Mã số sinh viên / Mã cán bộ giảng viên (tối đa 20 ký tự) |
| `phoneNumber` | `string?` | Không | Số điện thoại liên hệ (định dạng 10 chữ số, ví dụ: 0901234567 hoặc +84901234567) |
| `roles` | `string[]` | Có | Danh sách ít nhất 1 vai trò hệ thống hợp lệ |
| `initialPassword` | `string?` | Không | Mật khẩu khởi tạo ban đầu (nếu muốn kích hoạt ngay) |
| `sendActivationEmail` | `boolean` | Không | Mặc định `true` (chỉ áp dụng khi không truyền initialPassword) |

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "id": "33333333-3333-4333-8333-333333333333",
    "email": "lecturer02@ugtm.edu.vn",
    "fullName": "ThS. Lê Thị C",
    "userCode": "GV002",
    "phoneNumber": "0901234567",
    "status": "PendingActivation",
    "roles": [
      "Lecturer"
    ],
    "createdAt": "2026-10-13T14:00:00Z",
    "lastLoginAt": null
  },
  "error": null
}
```

| HTTP Status | Mã lỗi | Nguyên nhân |
| :--- | :--- | :--- |
| 400 | `Identity.InvalidInput` | Thiếu Email hoặc Họ tên |
| 400 | `Identity.InvalidRole` | Danh sách vai trò rỗng hoặc chứa vai trò không tồn tại |
| 400 | `Identity.InvalidPhoneNumber` | Số điện thoại không hợp lệ (yêu cầu định dạng 10 chữ số) |
| 400 | `Identity.WeakPassword` | `initialPassword` không đáp ứng chính sách bảo mật ($\ge 12$ ký tự, hoa, thường, số, ký tự đặc biệt) |
| 409 | `Identity.DuplicateAccount` | Email hoặc mã người dùng đã tồn tại trong hệ thống |

---

### 3. `PUT /api/users/{id}` — Cập nhật thông tin và vai trò

Cập nhật thông tin cá nhân và thay đổi danh sách vai trò của người dùng.

#### Path Parameter
- `id` (`uuid`): Mã định danh duy nhất của người dùng cần cập nhật.

#### Request Body

```json
{
  "fullName": "TS. Lê Thị C (Đã cập nhật)",
  "userCode": "GV002",
  "phoneNumber": "0909999888",
  "roles": [
    "Lecturer",
    "CouncilMember"
  ]
}
```

| Trường | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `fullName` | `string` | Có | Họ và tên mới của người dùng |
| `userCode` | `string?` | Không | Mã số sinh viên / cán bộ mới |
| `phoneNumber` | `string?` | Không | Số điện thoại mới |
| `roles` | `string[]?` | Không | Danh sách vai trò mới (nếu gửi, sẽ thay thế danh sách vai trò cũ) |

> [!NOTE]
> Khi danh sách vai trò (`roles`) thay đổi, hệ thống sẽ tự động xoay `SecurityStamp` của người dùng. Nhờ đó, người dùng sẽ bắt buộc phải làm mới phiên (refresh token) ở lần gọi tiếp theo để nhận claims và permissions mới từ database.

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "id": "33333333-3333-4333-8333-333333333333",
    "email": "lecturer02@ugtm.edu.vn",
    "fullName": "TS. Lê Thị C (Đã cập nhật)",
    "userCode": "GV002",
    "phoneNumber": "0909999888",
    "status": "PendingActivation",
    "roles": [
      "Lecturer",
      "CouncilMember"
    ],
    "createdAt": "2026-10-13T14:00:00Z",
    "lastLoginAt": null
  },
  "error": null
}
```

| HTTP Status | Mã lỗi | Nguyên nhân |
| :--- | :--- | :--- |
| 400 | `Identity.InvalidInput` | Họ tên bị để trống |
| 400 | `Identity.InvalidRole` | Danh sách vai trò không hợp lệ |
| 400 | `Identity.InvalidPhoneNumber` | Số điện thoại không hợp lệ (yêu cầu định dạng 10 chữ số) |
| 404 | `Identity.UserNotFound` | Không tìm thấy người dùng với `id` cung cấp |
| 409 | `Identity.DuplicateAccount` | `userCode` mới bị trùng với một tài khoản khác |

---

### 4. `PATCH /api/users/{id}/status` — Khóa / Mở khóa / Tạm ngưng tài khoản

Thay đổi trạng thái tài khoản người dùng (`Active`, `Suspended`, `Disabled`).

#### Path Parameter
- `id` (`uuid`): Mã định danh duy nhất của người dùng cần cập nhật trạng thái.

#### Request Body

```json
{
  "status": "Suspended"
}
```

| Trường | Kiểu | Giá trị hợp lệ | Mô tả |
| :--- | :--- | :--- | :--- |
| `status` | `string` | `Active`, `Suspended`, `Disabled` | Trạng thái mới muốn thiết lập |

> [!IMPORTANT]
> **Cơ chế ngắt phiên làm việc tức thời:**
> - Khi chuyển sang `Suspended` hoặc `Disabled`: `SecurityStamp` được tạo mới ngay lập tức. Middleware `SessionValidator` của backend sẽ đối chiếu `SecurityStamp` của phiên với snapshot trong cơ sở dữ liệu ở mọi request kế tiếp. Dù Access Token (JWT) vẫn còn hạn 10 phút, request gửi từ tài khoản này sẽ bị từ chối ngay lập tức với mã `401 Unauthorized` / `403 Forbidden`.
> - Khi chuyển về `Active` (`Reactivate`): Tài khoản được mở khóa, số lần đăng nhập sai (`AccessFailedCount`) được reset về 0 và thời hạn khóa tạm thời (`LockoutEndAt`) được gỡ bỏ.

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "id": "33333333-3333-4333-8333-333333333333",
    "email": "lecturer02@ugtm.edu.vn",
    "fullName": "TS. Lê Thị C (Đã cập nhật)",
    "userCode": "GV002",
    "phoneNumber": "0909999888",
    "status": "Suspended",
    "roles": [
      "Lecturer",
      "CouncilMember"
    ],
    "createdAt": "2026-10-13T14:00:00Z",
    "lastLoginAt": null
  },
  "error": null
}
```

| HTTP Status | Mã lỗi | Nguyên nhân |
| :--- | :--- | :--- |
| 400 | `Identity.InvalidStatus` | Trạng thái rỗng, không hợp lệ hoặc cố tình đặt `PendingActivation` |
| 400 | `Identity.InvalidStatusTransition` | Chuyển đổi trạng thái không hợp lệ (ví dụ tài khoản chờ kích hoạt hoặc tạm ngưng tài khoản không Active) |
| 404 | `Identity.UserNotFound` | Không tìm thấy người dùng với `id` cung cấp |

---

## Danh mục Mã lỗi (Error Codes)

| Mã lỗi | HTTP Status | Diễn giải |
| :--- | :---: | :--- |
| `Identity.UserNotFound` | 404 | Không tìm thấy tài khoản người dùng tương ứng |
| `Identity.DuplicateAccount` | 409 | Email hoặc mã người dùng đã tồn tại trong hệ thống |
| `Identity.InvalidRole` | 400 | Tên vai trò không hợp lệ trong danh mục `SystemRoleName` |
| `Identity.InvalidStatus` | 400 | Trạng thái tài khoản không hợp lệ |
| `Identity.InvalidStatusTransition` | 400 | Không thể chuyển đổi trạng thái tài khoản theo yêu cầu |
| `Identity.WeakPassword` | 400 | Mật khẩu khởi tạo không đạt tiêu chuẩn chính sách bảo mật |
| `Identity.InvalidInput` | 400 | Dữ liệu đầu vào bị thiếu hoặc không đúng định dạng |

---

## TypeScript Interfaces cho Frontend

```typescript
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface UserSummary {
  id: string;
  email: string;
  fullName: string;
  userCode: string | null;
  phoneNumber: string | null;
  status: 'PendingActivation' | 'Active' | 'Suspended' | 'Disabled';
  roles: string[];
  createdAt: string;
  lastLoginAt: string | null;
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  userCode?: string | null;
  phoneNumber?: string | null;
  roles: string[];
  initialPassword?: string | null;
  sendActivationEmail?: boolean;
}

export interface UpdateUserRequest {
  fullName: string;
  userCode?: string | null;
  phoneNumber?: string | null;
  roles?: string[];
}

export interface UpdateUserStatusRequest {
  status: 'Active' | 'Suspended' | 'Disabled';
}
```

---

## Ví dụ gọi API từ Client

```typescript
const BASE_URL = 'http://localhost:5287/api/users';
const token = '<access-token-from-auth-login>';

// 1. Lấy danh sách người dùng phân trang
const getResponse = await fetch(`${BASE_URL}?pageNumber=1&pageSize=10&keyword=nguyen&role=Lecturer`, {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});
const pagedUsers = await getResponse.json();

// 2. Tạo người dùng mới
const createResponse = await fetch(BASE_URL, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    email: 'lecturer@ugtm.edu.vn',
    fullName: 'TS. Nguyễn Văn B',
    userCode: 'GV001',
    roles: ['Lecturer'],
    sendActivationEmail: true
  })
});
const createdUser = await createResponse.json();

// 3. Khóa tài khoản
const lockResponse = await fetch(`${BASE_URL}/${createdUser.data.id}/status`, {
  method: 'PATCH',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    status: 'Suspended'
  })
});
```
