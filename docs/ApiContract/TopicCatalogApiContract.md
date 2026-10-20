# Topic Catalog, Proposal, Review & Tracking API

**Trạng thái:** đã triển khai · **Cập nhật:** 20/10/2026  
**Base path:** `/api/topics`

Tài liệu này đặc tả chi tiết các endpoint thuộc phân hệ **Đề tài khóa luận (TopicCatalog)**:
1. **Đề xuất & Soạn thảo đề tài (GVHD):** Tạo mới, chỉnh sửa và gửi duyệt đề tài.
2. **Thẩm định & Công bố danh mục đề tài (Văn phòng Khoa):** Duyệt (`Approved`), yêu cầu sửa (`NeedRevision`), từ chối (`Rejected`), và công bố danh mục đề tài (`Published` / `isPublished = true`).
3. **Tra cứu & Theo dõi đề tài (Sinh viên / GVHD / Khoa / Hội đồng):** Sinh viên tra cứu đề tài đã công bố; GVHD theo dõi trạng thái đăng ký và thông tin sinh viên của đề tài mình hướng dẫn.

---

## 1. Quy ước chung & Ma trận phân quyền

- Request và Response sử dụng định dạng JSON `camelCase`; thời gian chuẩn ISO 8601 UTC.
- **Vòng đời trạng thái đề tài (`TopicStatus`):**
  - `Draft` (Nháp) ➔ `Submitted` (Chờ duyệt) ➔ `Approved` (Khoa duyệt) / `NeedRevision` (Khoa yêu cầu sửa) / `Rejected` (Từ chối).
  - Khi ở `NeedRevision`, GVHD có thể chỉnh sửa và gửi duyệt lại (`NeedRevision` ➔ `Submitted`).
  - Khi Khoa bấm lệnh **Công bố danh mục đề tài**, các đề tài đang ở `Approved` chuyển sang `Published` (`isPublished = true`).
- **Ma trận phân quyền (Role-based Authorization Matrix):**
  - **Sinh viên (`Student`) / Công khai:**
    - `GET /api/topics/all-topics`: Chỉ tra cứu được các đề tài đã được Khoa duyệt và công bố (`status == "Published"` và `isPublished == true`).
  - **Giảng viên hướng dẫn (`Lecturer`):**
    - `POST /api/topics`: Tạo đề tài mới cho chính mình (`studentNumber` chỉ nhận `1` hoặc `2`).
    - `PUT /api/topics/{id}`: Chỉnh sửa đề tài của chính mình khi đang ở trạng thái `Draft` hoặc `NeedRevision`.
    - `POST /api/topics/{id}/submit`: Gửi đề tài của chính mình từ `Draft` / `NeedRevision` sang `Submitted`.
    - `GET /api/topics/my-topics` & `GET /api/topics/{id}/tracking`: Xem danh sách và chi tiết các đề tài do chính mình hướng dẫn kèm thông tin nhóm sinh viên đăng ký.
  - **Văn phòng Khoa (`Faculty`):**
    - `GET /api/topics/my-topics` & `GET /api/topics/{id}/tracking`: Xem và lọc toàn bộ đề tài theo mọi trạng thái (`Submitted`, `Approved`, `NeedRevision`, `Rejected`, `Published`, `Draft`) để thẩm định và công bố.
    - `POST /api/topics/{id}/review`: Thẩm định đề tài đang ở `Submitted` (`Approved`, `NeedRevision`, `Rejected`).
    - `POST /api/topics/publish`: Công bố danh mục đề tài đã duyệt (`Approved` ➔ `Published` / `isPublished = true`).
  - **Quản trị viên (`Admin`):**
    - Có toàn quyền thực hiện và tra cứu trên tất cả các endpoint.

---

## 2. Danh sách Endpoint

| Method | Path | Quyền truy cập | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/topics/all-topics` | Mọi vai trò / Công khai | Tra cứu danh mục đề tài đã được Khoa duyệt và công bố (`isPublished = true`) |
| `GET` | `/api/topics/my-topics` | `Lecturer` (chính chủ), `Faculty`, `Admin`, `CouncilMember` | GVHD theo dõi đề tài của mình; Khoa/Admin/Hội đồng xem & lọc toàn bộ đề tài |
| `GET` | `/api/topics/{id}/tracking` | `Lecturer` (chính chủ), `Faculty`, `Admin`, `CouncilMember` | Xem chi tiết đề tài kèm trạng thái đăng ký và thông tin nhóm sinh viên |
| `POST` | `/api/topics` | `Lecturer`, `Admin` | GVHD tạo đề tài mới (`Draft` hoặc gửi duyệt ngay `Submitted`) |
| `PUT` | `/api/topics/{id}` | `Lecturer` (chính chủ), `Admin` | GVHD chỉnh sửa đề tài khi ở trạng thái `Draft` hoặc `NeedRevision` |
| `POST` | `/api/topics/{id}/submit` | `Lecturer` (chính chủ), `Admin` | GVHD gửi đề tài lên Văn phòng Khoa thẩm định (`Draft`/`NeedRevision` ➔ `Submitted`) |
| `POST` | `/api/topics/{id}/review` | `Faculty`, `Admin` | Văn phòng Khoa thẩm định đề tài (`Submitted` ➔ `Approved`/`NeedRevision`/`Rejected`) |
| `POST` | `/api/topics/publish` | `Faculty`, `Admin` | Văn phòng Khoa công bố danh mục đề tài (`Approved` ➔ `Published`, `isPublished = true`) |

---

## 3. Chi tiết các Endpoint Đề xuất, Thẩm định & Công bố

### 3.1. `POST /api/topics` — GVHD tạo đề tài mới

#### Request Body
```json
{
  "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
  "titleVi": "Xây dựng hệ thống phát hiện bất thường dựa trên học sâu",
  "titleEn": "Deep Learning-based Anomaly Detection System",
  "description": "Nghiên cứu áp dụng các kiến trúc Transformer trong phân tích log chuỗi thời gian.",
  "requirements": "Python, PyTorch, hiểu biết về học máy cơ bản.",
  "studentNumber": 2,
  "submitImmediately": false
}
```

| Trường | Kiểu | Bắt buộc | Ràng buộc |
| :--- | :--- | :---: | :--- |
| `batchId` | `uuid` | Có | Phải tồn tại trong bảng `academic.kltn_batch` |
| `titleVi` | `string` | Có | Không rỗng, tối đa 300 ký tự, không trùng tên trong cùng đợt của cùng GVHD |
| `titleEn` | `string?` | Không | Tối đa 350 ký tự |
| `description` | `string?` | Không | Mục tiêu đề tài |
| `requirements` | `string?` | Không | Yêu cầu công nghệ / kiến thức |
| `studentNumber` | `integer` | Có | Chỉ nhận giá trị `1` hoặc `2` |
| `submitImmediately` | `boolean` | Không | Mặc định `false` (`Draft`). Nếu `true` sẽ chuyển thẳng sang `Submitted` |

---

### 3.2. `PUT /api/topics/{id}` — GVHD chỉnh sửa đề tài

Chỉ cho phép chỉnh sửa khi đề tài đang ở trạng thái `Draft` hoặc `NeedRevision`.

#### Request Body
```json
{
  "titleVi": "Xây dựng hệ thống phát hiện bất thường dựa trên học sâu (Bản cập nhật)",
  "titleEn": "Deep Learning-based Anomaly Detection System",
  "description": "Bổ sung phạm vi thực nghiệm theo góp ý của Khoa.",
  "requirements": "Python, PyTorch, Docker.",
  "studentNumber": 2,
  "submitImmediately": true
}
```

---

### 3.3. `POST /api/topics/{id}/submit` — GVHD gửi duyệt đề tài

Chuyển trạng thái đề tài từ `Draft` hoặc `NeedRevision` sang `Submitted`. Không cần request body.

---

### 3.4. `POST /api/topics/{id}/review` — Văn phòng Khoa thẩm định đề tài

Chỉ vai trò `Faculty` hoặc `Admin` được phép thực hiện trên đề tài đang ở trạng thái `Submitted`.

#### Request Body
```json
{
  "decision": "Approved"
}
```
- `decision`: Chỉ chấp nhận `"Approved"`, `"NeedRevision"`, hoặc `"Rejected"` (không phân biệt hoa thường).

---

### 3.5. `POST /api/topics/publish` — Văn phòng Khoa công bố danh mục đề tài

Chỉ vai trò `Faculty` hoặc `Admin` được phép thực hiện. Chuyển các đề tài `Approved` sang `Published` (`isPublished = true`).

#### Request Body
```json
{
  "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
  "topicIds": null
}
```
- Nếu `topicIds` là `null` hoặc `[]`: Công bố toàn bộ các đề tài đang ở trạng thái `Approved` trong đợt `batchId`.
- Nếu `topicIds` có danh sách UUID: Chỉ công bố các đề tài được chỉ định (tất cả phải thuộc `batchId` và đang ở trạng thái `Approved`).

#### Phản hồi thành công (200 OK)
```json
{
  "success": true,
  "data": {
    "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
    "publishedCount": 2,
    "publishedTopicIds": [
      "7b5879a9-3d08-4127-b5b4-aa8aef9f3a01",
      "8c6980ba-4e19-4238-c6c5-bb9bf00f4b02"
    ]
  },
  "error": null
}
```

---

## 4. Bảng mã lỗi (Error Codes)

| Mã lỗi (`code`) | HTTP Status | Thông điệp (`message`) | Nguyên nhân |
| :--- | :--- | :--- | :--- |
| `Topic.InvalidInput` | 400 Bad Request | Mã đề tài không hợp lệ. | `id` truyền vào là `Guid.Empty` hoặc sai định dạng UUID. |
| `Topic.InvalidBatchId` | 400 Bad Request | Mã đợt khóa luận tốt nghiệp không hợp lệ. | `batchId` là `Guid.Empty`. |
| `Topic.InvalidTitleVi` | 400 Bad Request | Tên đề tài tiếng Việt không được để trống và không vượt quá 300 ký tự. | `titleVi` rỗng hoặc dài hơn 300 ký tự. |
| `Topic.InvalidTitleEn` | 400 Bad Request | Tên đề tài tiếng Anh không được vượt quá 350 ký tự. | `titleEn` dài hơn 350 ký tự. |
| `Topic.InvalidStudentNumber` | 400 Bad Request | Chỉ tiêu sinh viên tối đa chỉ nhận giá trị 1 hoặc 2. | `studentNumber` khác `1` và `2`. |
| `Topic.InvalidReviewDecision` | 400 Bad Request | Kết quả thẩm định không hợp lệ (chỉ chấp nhận: 'Approved', 'NeedRevision', 'Rejected'). | `decision` không thuộc 3 giá trị hợp lệ. |
| `Topic.NoApprovedTopicsToPublish` | 400 Bad Request | Không có đề tài nào ở trạng thái đã duyệt (Approved) để công bố. | Bấm công bố theo đợt nhưng chưa có đề tài nào ở trạng thái `Approved`. |
| `Topic.InvalidRegistrationStatusFilter` | 400 Bad Request | Bộ lọc trạng thái đăng ký không hợp lệ (chỉ chấp nhận: 'All', 'Empty', 'Registered', 'Full'). | `registrationStatus` không thuộc các giá trị hợp lệ. |
| `Topic.InvalidTopicStatusFilter` | 400 Bad Request | Bộ lọc trạng thái đề tài không hợp lệ. | `status` là chuỗi số hoặc không map được sang enum `TopicStatus`. |
| `Topic.AccessDenied` | 403 Forbidden | Bạn không có quyền thực hiện thao tác trên đề tài này. | Người dùng chưa đăng nhập, không đúng vai trò (`Lecturer`/`Faculty`/`Admin`), hoặc `Lecturer` thao tác trên đề tài của giảng viên khác. |
| `Topic.NotFound` | 404 Not Found | Không tìm thấy đề tài yêu cầu. | Đề tài không tồn tại trong cơ sở dữ liệu. |
| `Topic.BatchNotFound` | 404 Not Found | Không tìm thấy đợt khóa luận tốt nghiệp. | `batchId` không tồn tại trong bảng `academic.kltn_batch`. |
| `Topic.DuplicateTitle` | 409 Conflict | Đề tài có cùng tên tiếng Việt đã tồn tại trong đợt khóa luận này của giảng viên. | Trùng `titleVi` của cùng GVHD trong cùng đợt KLTN. |
| `Topic.InvalidStatusTransition` | 409 Conflict | Không thể thực hiện thao tác ở trạng thái đề tài hiện tại. | Sửa đề tài đã gửi/duyệt, gửi duyệt đề tài không ở `Draft`/`NeedRevision`, hoặc duyệt đề tài không ở `Submitted`. |
