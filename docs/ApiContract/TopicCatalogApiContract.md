# Topic Catalog & Tracking API

**Trạng thái:** đã triển khai · **Cập nhật:** 17/10/2026  
**Base path:** `/api/topics`

Tài liệu này đặc tả chi tiết các endpoint Tra cứu Đề tài và **Theo dõi Đề tài (Advisor Topic Tracking)** trong hệ thống UGTM. Tính năng cho phép Giảng viên hướng dẫn (GVHD) theo dõi trạng thái đề tài của mình (tình trạng sinh viên đăng ký, chỉ tiêu còn trống) kèm danh sách và thông tin liên hệ của sinh viên trong nhóm.

---

## 1. Quy ước chung

- Request và Response sử dụng định dạng JSON `camelCase`; thời gian chuẩn ISO 8601 UTC.
- Các endpoint tra cứu công khai (`/api/topics/all-topics`) không bắt buộc đăng nhập.
- Các endpoint theo dõi đề tài (`/api/topics/my-topics`, `/api/topics/{id}/tracking`) yêu cầu Bearer JWT token: `Authorization: Bearer <access-token>`.
- **Ma trận phân quyền (Role-based Authorization Matrix):**
  - **Giảng viên hướng dẫn (`Lecturer`):**
    - `GET /api/topics/my-topics`: Chỉ xem danh sách đề tài do chính mình phụ trách (`advisorId == currentUser.UserId`). Mọi hành vi truyền tham số `advisorId` của giảng viên khác đều bị hệ thống tự động ghi đè về `currentUser.UserId` để bảo mật.
    - `GET /api/topics/{id}/tracking`: Chỉ xem chi tiết các đề tài do mình hướng dẫn. Nếu truy cập đề tài của GVHD khác sẽ nhận mã `403 Forbidden`.
  - **Quản trị viên (`Admin`) & Thành viên hội đồng (`CouncilMember`):**
    - Có đặc quyền **Bypass Authorization**: Có thể xem toàn bộ đề tài trong hệ thống mà không bị giới hạn theo người hướng dẫn.
    - Có thể lọc theo `advisorId` cụ thể để kiểm tra tiến độ đề tài của từng giảng viên.
  - **Các vai trò khác (`Student`, `Faculty`, `AcademicAffairs`, v.v.):**
    - Bị từ chối truy cập các endpoint theo dõi (`my-topics`, `{id}/tracking`) với mã phản hồi `403 Forbidden`.

- **Mô hình Envelope phản hồi `ApiResponse<T>`:**
```json
{
  "success": true,
  "data": {},
  "error": null
}
```

- **Mô hình phản hồi lỗi nghiệp vụ:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Topic.NotFound",
    "message": "Không tìm thấy đề tài yêu cầu.",
    "errors": null
  }
}
```

---

## 2. Danh sách Endpoint

| Method | Path | Quyền truy cập | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/topics/all-topics` | Mọi vai trò / Công khai | Tra cứu danh sách tất cả đề tài (tên, mục tiêu, yêu cầu, chỉ tiêu) |
| `GET` | `/api/topics/my-topics` | `Lecturer` (chính chủ), `Admin`, `CouncilMember` | GVHD theo dõi danh sách đề tài kèm tình trạng đăng ký và sinh viên |
| `GET` | `/api/topics/{id}/tracking` | `Lecturer` (chính chủ), `Admin`, `CouncilMember` | Xem chi tiết tiến độ theo dõi của 1 đề tài kèm thông tin nhóm SV |

---

## 3. Chi tiết các Endpoint

### 3.1. `GET /api/topics/all-topics` — Tra cứu danh sách tất cả đề tài

Lấy danh mục đề tài trong hệ thống, hỗ trợ tìm kiếm từ khóa và lọc theo đợt KLTN.

#### Query Parameters

| Tham số | Kiểu | Mặc định | Mô tả |
| :--- | :--- | :--- | :--- |
| `pageNumber` | `integer` | `1` | Số trang ($\ge 1$, nhỏ hơn 1 tự động điều chỉnh về 1) |
| `pageSize` | `integer` | `10` | Kích thước trang ($1 \le pageSize \le 100$, vượt ngưỡng tự điều chỉnh) |
| `keyword` | `string?` | — | Từ khóa tìm kiếm theo tên đề tài tiếng Việt (`titleVi`) hoặc tiếng Anh (`titleEn`) |
| `batchId` | `uuid?` | — | Lọc theo mã đợt KLTN |

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "7b5879a9-3d08-4127-b5b4-aa8aef9f3a01",
        "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
        "advisorId": "2d17c918-091a-464a-9ef8-e0471b058a99",
        "titleVi": "Xây dựng hệ thống phát hiện bất thường dựa trên học sâu",
        "titleEn": "Deep Learning-based Anomaly Detection System",
        "description": "Nghiên cứu áp dụng các kiến trúc Transformer trong phân tích log chuỗi thời gian.",
        "requirements": "Kiến thức vững về Python, PyTorch, hiểu biết về học máy cơ bản.",
        "status": "Approved",
        "studentNumber": 2,
        "createdAt": "2026-10-15T08:30:00Z"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  },
  "error": null
}
```

---

### 3.2. `GET /api/topics/my-topics` — GVHD theo dõi danh sách đề tài

Cho phép giảng viên hướng dẫn theo dõi danh sách đề tài của mình, số lượng sinh viên đã đăng ký, số lượng chỉ tiêu còn trống, trạng thái đăng ký (`Empty`, `Registered`, `Full`) và danh sách sinh viên đăng ký.

#### Query Parameters

| Tham số | Kiểu | Mặc định | Mô tả |
| :--- | :--- | :--- | :--- |
| `pageNumber` | `integer` | `1` | Số trang ($\ge 1$, nhỏ hơn 1 tự căn chỉnh về 1) |
| `pageSize` | `integer` | `10` | Kích thước trang ($1 \le pageSize \le 100$) |
| `keyword` | `string?` | — | Từ khóa tìm kiếm theo tên đề tài (hỗ trợ tiếng Việt, ký tự đặc biệt được escape an toàn) |
| `batchId` | `uuid?` | — | Lọc theo mã đợt KLTN |
| `status` | `string?` | — | Lọc theo trạng thái đề tài: `Draft`, `Submitted`, `NeedRevision`, `Approved`, `Rejected`, `Published`, `Closed`, `Archived`. (Không phân biệt hoa thường; chuỗi số hoặc giá trị không hợp lệ trả về lỗi 400). |
| `registrationStatus` | `string?` | — | Lọc theo tình trạng đăng ký sinh viên: `All`, `Empty` (chưa có SV), `Registered` (đã có SV nhưng chưa đủ chỉ tiêu), `Full` (đã đủ chỉ tiêu). Không phân biệt hoa thường. |
| `advisorId` | `uuid?` | — | Dành cho `Admin` hoặc `CouncilMember` muốn lọc theo GVHD cụ thể. Giảng viên thường truyền tham số này sẽ bị bỏ qua và buộc áp dụng `UserId` của chính mình. |

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "7b5879a9-3d08-4127-b5b4-aa8aef9f3a01",
        "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
        "advisorId": "2d17c918-091a-464a-9ef8-e0471b058a99",
        "titleVi": "Xây dựng hệ thống phát hiện bất thường dựa trên học sâu",
        "titleEn": "Deep Learning-based Anomaly Detection System",
        "description": "Nghiên cứu áp dụng các kiến trúc Transformer trong phân tích log.",
        "requirements": "Python, PyTorch, Machine Learning.",
        "status": "Approved",
        "studentNumber": 2,
        "createdAt": "2026-10-15T08:30:00Z",
        "registeredStudentsCount": 2,
        "remainingSlots": 0,
        "registrationStatus": "Full",
        "hasRegistration": true,
        "isAvailable": false,
        "group": {
          "groupId": "e89d1234-5678-4abc-9def-0123456789ab",
          "status": "Approved",
          "createdAt": "2026-10-16T09:00:00Z",
          "students": [
            {
              "studentId": "11111111-1111-4111-8111-111111111111",
              "userCode": "24521186",
              "fullName": "Nguyễn Văn A",
              "email": "24521186@gm.uit.edu.vn",
              "phoneNumber": "0912345678",
              "joinedAt": "2026-10-16T09:00:00Z"
            },
            {
              "studentId": "22222222-2222-4222-8222-222222222222",
              "userCode": "24521187",
              "fullName": "Trần Thị B",
              "email": "24521187@gm.uit.edu.vn",
              "phoneNumber": "0912345679",
              "joinedAt": "2026-10-16T09:05:00Z"
            }
          ]
        }
      },
      {
        "id": "8c6980ba-4e19-4238-c6c5-bb9bf00f4b02",
        "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
        "advisorId": "2d17c918-091a-464a-9ef8-e0471b058a99",
        "titleVi": "Ứng dụng Blockchain trong xác thực văn bằng đại học",
        "titleEn": "Blockchain Application in Academic Credential Verification",
        "description": "Xây dựng Smart Contract trên mạng Ethereum.",
        "requirements": "Solidity, Web3.js, NodeJS.",
        "status": "Approved",
        "studentNumber": 2,
        "createdAt": "2026-10-15T09:00:00Z",
        "registeredStudentsCount": 0,
        "remainingSlots": 2,
        "registrationStatus": "Empty",
        "hasRegistration": false,
        "isAvailable": true,
        "group": null
      }
    ],
    "totalCount": 2,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  },
  "error": null
}
```

#### Phản hồi lỗi

- **400 Bad Request — Bộ lọc tình trạng đăng ký không hợp lệ:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Topic.InvalidRegistrationStatusFilter",
    "message": "Bộ lọc trạng thái đăng ký không hợp lệ (chỉ chấp nhận: 'All', 'Empty', 'Registered', 'Full').",
    "errors": null
  }
}
```

- **400 Bad Request — Bộ lọc trạng thái đề tài không hợp lệ:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Topic.InvalidTopicStatusFilter",
    "message": "Bộ lọc trạng thái đề tài không hợp lệ.",
    "errors": null
  }
}
```

- **403 Forbidden — Không đủ quyền truy cập:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Topic.AccessDenied",
    "message": "Bạn không có quyền xem thông tin theo dõi của đề tài này. Chỉ GVHD phụ trách hoặc Quản trị viên/Hội đồng mới có quyền truy cập.",
    "errors": null
  }
}
```

---

### 3.3. `GET /api/topics/{id}/tracking` — Chi tiết trạng thái theo dõi đề tài

Xem chi tiết thông tin theo dõi một đề tài cụ thể kèm danh sách nhóm sinh viên và thông tin liên hệ của từng sinh viên.

#### Path Parameters

| Tham số | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | `uuid` | Có | Định danh UUID của đề tài cần theo dõi |

#### Phản hồi thành công (200 OK)

```json
{
  "success": true,
  "data": {
    "id": "7b5879a9-3d08-4127-b5b4-aa8aef9f3a01",
    "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
    "advisorId": "2d17c918-091a-464a-9ef8-e0471b058a99",
    "titleVi": "Xây dựng hệ thống phát hiện bất thường dựa trên học sâu",
    "titleEn": "Deep Learning-based Anomaly Detection System",
    "description": "Nghiên cứu áp dụng các kiến trúc Transformer trong phân tích log chuỗi thời gian.",
    "requirements": "Kiến thức vững về Python, PyTorch, hiểu biết về học máy cơ bản.",
    "status": "Approved",
    "studentNumber": 2,
    "createdAt": "2026-10-15T08:30:00Z",
    "registeredStudentsCount": 2,
    "remainingSlots": 0,
    "registrationStatus": "Full",
    "hasRegistration": true,
    "isAvailable": false,
    "group": {
      "groupId": "e89d1234-5678-4abc-9def-0123456789ab",
      "status": "Approved",
      "createdAt": "2026-10-16T09:00:00Z",
      "students": [
        {
          "studentId": "11111111-1111-4111-8111-111111111111",
          "userCode": "24521186",
          "fullName": "Nguyễn Văn A",
          "email": "24521186@gm.uit.edu.vn",
          "phoneNumber": "0912345678",
          "joinedAt": "2026-10-16T09:00:00Z"
        },
        {
          "studentId": "22222222-2222-4222-8222-222222222222",
          "userCode": "24521187",
          "fullName": "Trần Thị B",
          "email": "24521187@gm.uit.edu.vn",
          "phoneNumber": "0912345679",
          "joinedAt": "2026-10-16T09:05:00Z"
        }
      ]
    }
  },
  "error": null
}
```

#### Phản hồi lỗi

- **400 Bad Request — Mã UUID rỗng:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Topic.InvalidInput",
    "message": "Mã đề tài không hợp lệ.",
    "errors": null
  }
}
```

- **403 Forbidden — GVHD truy cập đề tài của GVHD khác hoặc role không hợp lệ:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Topic.AccessDenied",
    "message": "Bạn không có quyền xem thông tin theo dõi của đề tài này. Chỉ GVHD phụ trách hoặc Quản trị viên/Hội đồng mới có quyền truy cập.",
    "errors": null
  }
}
```

- **404 Not Found — Không tìm thấy đề tài:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Topic.NotFound",
    "message": "Không tìm thấy đề tài yêu cầu.",
    "errors": null
  }
}
```

---

## 4. Bảng mã lỗi (Error Codes)

| Mã lỗi (`code`) | HTTP Status | Thông điệp (`message`) | Nguyên nhân |
| :--- | :--- | :--- | :--- |
| `Topic.InvalidInput` | 400 Bad Request | Mã đề tài không hợp lệ. | `id` truyền vào là `Guid.Empty` hoặc sai định dạng UUID. |
| `Topic.InvalidRegistrationStatusFilter` | 400 Bad Request | Bộ lọc trạng thái đăng ký không hợp lệ (chỉ chấp nhận: 'All', 'Empty', 'Registered', 'Full'). | `registrationStatus` không thuộc các giá trị hợp lệ. |
| `Topic.InvalidTopicStatusFilter` | 400 Bad Request | Bộ lọc trạng thái đề tài không hợp lệ. | `status` là chuỗi số hoặc không map được sang enum `TopicStatus`. |
| `Topic.AccessDenied` | 403 Forbidden | Bạn không có quyền xem thông tin theo dõi của đề tài này. Chỉ GVHD phụ trách hoặc Quản trị viên/Hội đồng mới có quyền truy cập. | Người dùng chưa đăng nhập, thuộc role không được phép (`Student`, v.v.), hoặc `Lecturer` cố truy cập đề tài của giảng viên khác. |
| `Topic.NotFound` | 404 Not Found | Không tìm thấy đề tài yêu cầu. | Đề tài không tồn tại trong cơ sở dữ liệu. |

---

## 5. TypeScript Interfaces

```typescript
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: ApiError | null;
}

export interface ApiError {
  code: string;
  message: string;
  errors?: Record<string, string[]> | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface TopicStudentDto {
  studentId: string;
  userCode?: string | null;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  joinedAt: string;
}

export interface TopicGroupDto {
  groupId: string;
  status: string;
  createdAt: string;
  students: TopicStudentDto[];
}

export interface TopicSummaryDto {
  id: string;
  batchId: string;
  advisorId: string;
  titleVi: string;
  titleEn?: string | null;
  description?: string | null;
  requirements?: string | null;
  status: string;
  studentNumber?: number | null;
  createdAt: string;
}

export interface TopicTrackingSummaryDto {
  id: string;
  batchId: string;
  advisorId: string;
  titleVi: string;
  titleEn?: string | null;
  description?: string | null;
  requirements?: string | null;
  status: string;
  studentNumber?: number | null;
  createdAt: string;
  registeredStudentsCount: number;
  remainingSlots: number;
  registrationStatus: 'Empty' | 'Registered' | 'Full';
  hasRegistration: boolean;
  isAvailable: boolean;
  group?: TopicGroupDto | null;
}

export interface TopicTrackingDetailDto extends TopicTrackingSummaryDto {}
```
