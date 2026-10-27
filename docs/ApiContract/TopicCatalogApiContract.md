# Topic Catalog, Proposal, Review & Tracking API

**Trạng thái:** mục 1–4 đã triển khai; mục 5 phân biệt API đã có và các phần còn thiếu · **Cập nhật:** 27/10/2026

**Base path hiện có:** `/api/topics`

**Base path đề xuất cho đăng ký/nhóm hướng dẫn:** `/api/thesis` (ThesisManagement)

Tài liệu này đặc tả chi tiết các endpoint thuộc phân hệ **Đề tài khóa luận (TopicCatalog)**:
1. **Đề xuất & Soạn thảo đề tài (GVHD):** Tạo mới, chỉnh sửa và gửi duyệt đề tài.
2. **Thẩm định & Công bố danh mục đề tài (Văn phòng Khoa):** Duyệt (`Approved`), yêu cầu sửa (`NeedRevision`), từ chối (`Rejected`) kèm lý do thẩm định (`reviewNote`), và công bố danh mục đề tài (`Published` / `isPublished = true`).
3. **Tra cứu & Theo dõi đề tài (Sinh viên / GVHD / Khoa / Hội đồng):** Sinh viên tra cứu đề tài đã công bố; GVHD theo dõi trạng thái đăng ký, thông tin sinh viên và lý do thẩm định của Khoa.

---

## 1. Quy ước chung & Ma trận phân quyền

- Request và Response sử dụng định dạng JSON `camelCase`; thời gian chuẩn ISO 8601 UTC.
- **Vòng đời trạng thái đề tài (`TopicStatus`):**
  - `Draft` (Nháp) ➔ `Submitted` (Chờ duyệt) ➔ `Approved` (Khoa duyệt) / `NeedRevision` (Khoa yêu cầu sửa) / `Rejected` (Từ chối).
  - Khi ở `NeedRevision`, GVHD có thể chỉnh sửa và gửi duyệt lại (`NeedRevision` ➔ `Submitted`). Lý do thẩm định cũ (`reviewNote`) vẫn được giữ lại để GVHD và Khoa tiện theo dõi lịch sử chỉnh sửa.
  - Khi Khoa duyệt (`Approved`) không nhập ghi chú mới, `reviewNote` cũ được xóa về `null`.
  - Khi Khoa bấm lệnh **Công bố danh mục đề tài**, các đề tài đang ở `Approved` chuyển sang `Published` (`isPublished = true`).
- **Phạm vi hiển thị trường `reviewNote`:**
  - GVHD (trong `my-topics`, `tracking` và response review) và các vai trò quản trị/thẩm định (`Faculty`, `Admin`, `CouncilMember`) xem được `reviewNote`.
  - Sinh viên tra cứu công khai qua `all-topics` **không** nhận trường này (`reviewNote = null`).
- **Ma trận phân quyền (Role-based Authorization Matrix):**
  - **Sinh viên (`Student`) / Công khai:**
    - `GET /api/topics/all-topics`: Chỉ tra cứu được các đề tài đã được Khoa duyệt và công bố (`status == "Published"` và `isPublished == true`).
  - **Giảng viên hướng dẫn (`Lecturer`):**
    - `POST /api/topics`: Tạo đề tài mới cho chính mình (`studentNumber` chỉ nhận `1` hoặc `2`).
    - `PUT /api/topics/{id}`: Chỉnh sửa đề tài của chính mình khi đang ở trạng thái `Draft` hoặc `NeedRevision`.
    - `POST /api/topics/{id}/submit`: Gửi đề tài của chính mình từ `Draft` / `NeedRevision` sang `Submitted`.
    - `GET /api/topics/my-topics` & `GET /api/topics/{id}/tracking`: Xem danh sách và chi tiết các đề tài do chính mình hướng dẫn kèm thông tin nhóm sinh viên đăng ký và lý do thẩm định.
  - **Văn phòng Khoa (`Faculty`):**
    - `GET /api/topics/my-topics` & `GET /api/topics/{id}/tracking`: Xem và lọc toàn bộ đề tài theo mọi trạng thái (`Submitted`, `Approved`, `NeedRevision`, `Rejected`, `Published`, `Draft`) để thẩm định và công bố.
    - `POST /api/topics/{id}/review`: Thẩm định đề tài đang ở `Submitted` (`Approved`, `NeedRevision`, `Rejected`) kèm lý do thẩm định `reviewNote`.
    - `POST /api/topics/publish`: Công bố danh mục đề tài đã duyệt (`Approved` ➔ `Published` / `isPublished = true`).
  - **Quản trị viên (`Admin`):**
    - Có toàn quyền thực hiện và tra cứu trên tất cả các endpoint.

---

## 2. Danh sách Endpoint

| Method | Path | Quyền truy cập | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/topics/all-topics` | Mọi vai trò / Công khai | Tra cứu danh mục đề tài đã được Khoa duyệt và công bố (`isPublished = true`) |
| `GET` | `/api/topics/my-topics` | `Lecturer` (chính chủ), `Faculty`, `Admin`, `CouncilMember` | GVHD theo dõi đề tài của mình; Khoa/Admin/Hội đồng xem & lọc toàn bộ đề tài |
| `GET` | `/api/topics/{id}/tracking` | `Lecturer` (chính chủ), `Faculty`, `Admin`, `CouncilMember` | Xem chi tiết đề tài kèm trạng thái đăng ký, nhóm sinh viên và lý do thẩm định |
| `POST` | `/api/topics` | `Lecturer`, `Admin` | GVHD tạo đề tài mới (`Draft` hoặc gửi duyệt ngay `Submitted`) |
| `PUT` | `/api/topics/{id}` | `Lecturer` (chính chủ), `Admin` | GVHD chỉnh sửa đề tài khi ở trạng thái `Draft` hoặc `NeedRevision` |
| `POST` | `/api/topics/{id}/submit` | `Lecturer` (chính chủ), `Admin` | GVHD gửi đề tài lên Văn phòng Khoa thẩm định (`Draft`/`NeedRevision` ➔ `Submitted`) |
| `POST` | `/api/topics/{id}/review` | `Faculty`, `Admin` | Văn phòng Khoa thẩm định đề tài (`Submitted` ➔ `Approved`/`NeedRevision`/`Rejected`) |
| `POST` | `/api/topics/publish` | `Faculty`, `Admin` | Văn phòng Khoa công bố danh mục đề tài (`Approved` ➔ `Published`, `isPublished = true`) |

---

### Số lượng đăng ký trong danh mục công khai

`GET /api/topics/all-topics` đã trả thêm ba trường trong `TopicSummaryDto`:

| Trường | Ý nghĩa |
| --- | --- |
| `confirmedStudentsCount` | SV đã có hiệu lực (`Confirmed`), gồm đăng ký cá nhân và GV chỉ định. |
| `reservedSlots` | Chỗ đang giữ cho lời mời `Pending` còn hạn theo UTC server. |
| `availableSlots` | `max(0, studentNumber - confirmedStudentsCount - reservedSlots)`; `null` nếu chỉ tiêu thiếu/không hợp lệ. |

Lời mời tại hoặc sau thời điểm hết hạn không chiếm chỗ, kể cả worker chưa dọn bản ghi. Không công khai danh tính SV, token hoặc ghi chú thẩm định. Các response tạo/sửa dùng cùng DTO có thể để ba trường này `null`; API danh mục là nguồn đọc số liệu.

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
| `studentCodes` | `string[]?` | Không | Danh sách MSSV sinh viên muốn gán trực tiếp cho đề tài (tuần 0-1) |
| `studentIds` | `uuid[]?` | Không | Danh sách UserId sinh viên muốn gán trực tiếp cho đề tài |

> [!NOTE]
> Khi truyền `studentCodes` hoặc `studentIds`, hệ thống tự động gán nhóm sinh viên với trạng thái `Confirmed` ngay lập tức và gửi email thông báo. Nếu quá trình gán sinh viên thất bại, thao tác tạo đề tài sẽ tự động rollback.

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
  "submitImmediately": true,
  "studentCodes": ["24521680", "24521681"],
  "studentIds": [],
  "clearAssignedStudents": false
}
```

| Trường | Kiểu | Bắt buộc | Ràng buộc |
| :--- | :--- | :---: | :--- |
| `titleVi` | `string` | Có | Không rỗng, tối đa 300 ký tự |
| `titleEn` | `string?` | Không | Tối đa 350 ký tự |
| `description` | `string?` | Không | Mục tiêu đề tài |
| `requirements` | `string?` | Không | Yêu cầu công nghệ / kiến thức |
| `studentNumber` | `integer` | Có | Chỉ nhận giá trị `1` hoặc `2`. Không được giảm xuống thấp hơn số sinh viên đang được gán |
| `submitImmediately` | `boolean` | Không | Mặc định `false`. Nếu `true` chuyển sang `Submitted` |
| `studentCodes` | `string[]?` | Không | Danh sách MSSV sinh viên cập nhật cho đề tài |
| `studentIds` | `uuid[]?` | Không | Danh sách UserId sinh viên cập nhật cho đề tài |
| `clearAssignedStudents` | `boolean` | Không | Nếu `true`, hủy gán toàn bộ nhóm sinh viên đã có |

---

### 3.3. `POST /api/topics/{id}/submit` — GVHD gửi duyệt đề tài

Chuyển trạng thái đề tài từ `Draft` hoặc `NeedRevision` sang `Submitted`. Không cần request body.

---

### 3.4. `POST /api/topics/{id}/review` — Văn phòng Khoa thẩm định đề tài

Chỉ vai trò `Faculty` hoặc `Admin` được phép thực hiện trên đề tài đang ở trạng thái `Submitted`.

#### Request Body
```json
{
  "decision": "NeedRevision",
  "reviewNote": "Cần làm rõ mục tiêu thực nghiệm và bổ sung công nghệ dự kiến sử dụng."
}
```

| Trường | Kiểu | Bắt buộc | Ràng buộc |
| :--- | :--- | :---: | :--- |
| `decision` | `string` | Có | Chỉ chấp nhận `'Approved'`, `'NeedRevision'`, hoặc `'Rejected'` (không phân biệt hoa thường). |
| `reviewNote` | `string?` | Có (khi NeedRevision/Rejected) | **Bắt buộc** khi `decision` là `'NeedRevision'` hoặc `'Rejected'`. Tùy chọn khi `'Approved'`. Tối đa 2000 ký tự. |

#### Phản hồi thành công (200 OK)
```json
{
  "success": true,
  "data": {
    "id": "7b5879a9-3d08-4127-b5b4-aa8aef9f3a01",
    "batchId": "c3e38719-7975-4d2c-81bb-987a02b1c4e1",
    "advisorId": "0199d750-a92c-7b56-b076-eb34d3d1fe55",
    "titleVi": "Xây dựng hệ thống phát hiện bất thường dựa trên học sâu",
    "titleEn": "Deep Learning-based Anomaly Detection System",
    "description": "Nghiên cứu áp dụng các kiến trúc Transformer trong phân tích log chuỗi thời gian.",
    "requirements": "Python, PyTorch, Docker.",
    "status": "NeedRevision",
    "studentNumber": 2,
    "createdAt": "2026-10-20T08:00:00Z",
    "isPublished": false,
    "reviewNote": "Cần làm rõ mục tiêu thực nghiệm và bổ sung công nghệ dự kiến sử dụng."
  },
  "error": null
}
```

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
| `Topic.ReviewNoteRequired` | 400 Bad Request | Vui lòng nhập lý do khi từ chối hoặc yêu cầu chỉnh sửa đề tài. | `decision` là `NeedRevision` hoặc `Rejected` nhưng không có `reviewNote` hoặc chỉ toàn khoảng trắng. |
| `Topic.InvalidReviewNote` | 400 Bad Request | Lý do thẩm định không được vượt quá 2000 ký tự. | `reviewNote` dài hơn 2000 ký tự sau khi cắt tỉa khoảng trắng. |
| `Topic.NoApprovedTopicsToPublish` | 400 Bad Request | Không có đề tài nào ở trạng thái đã duyệt (Approved) để công bố. | Bấm công bố theo đợt nhưng chưa có đề tài nào ở trạng thái `Approved`. |
| `Topic.InvalidRegistrationStatusFilter` | 400 Bad Request | Bộ lọc trạng thái đăng ký không hợp lệ (chỉ chấp nhận: 'All', 'Empty', 'Registered', 'Full'). | `registrationStatus` không thuộc các giá trị hợp lệ. |
| `Topic.InvalidTopicStatusFilter` | 400 Bad Request | Bộ lọc trạng thái đề tài không hợp lệ. | `status` là chuỗi số hoặc không map được sang enum `TopicStatus`. |
| `Topic.AccessDenied` | 403 Forbidden | Bạn không có quyền thực hiện thao tác trên đề tài này. | Người dùng chưa đăng nhập, không đúng vai trò (`Lecturer`/`Faculty`/`Admin`), hoặc `Lecturer` thao tác trên đề tài của giảng viên khác. |
| `Topic.NotFound` | 404 Not Found | Không tìm thấy đề tài yêu cầu. | Đề tài không tồn tại trong cơ sở dữ liệu. |
| `Topic.BatchNotFound` | 404 Not Found | Không tìm thấy đợt khóa luận tốt nghiệp. | `batchId` không tồn tại trong bảng `academic.kltn_batch`. |
| `Topic.DuplicateTitle` | 409 Conflict | Đề tài có cùng tên tiếng Việt đã tồn tại trong đợt khóa luận này của giảng viên. | Trùng `titleVi` của cùng GVHD trong cùng đợt KLTN. |
| `Topic.InvalidStatusTransition` | 409 Conflict | Không thể thực hiện thao tác ở trạng thái đề tài hiện tại. | Sửa đề tài đã gửi/duyệt, gửi duyệt đề tài không ở `Draft`/`NeedRevision`, hoặc duyệt đề tài không ở `Submitted`. |

---

## 5. Yêu cầu bổ sung — Đăng ký đề tài & danh sách nhóm hướng dẫn

### 5.0. Trạng thái tích hợp ngày 27/10/2026

**Thời hạn giữ chỗ đã chốt lại: 24 giờ**, phù hợp `RegisterTopicHandler.DefaultConfirmationWindow`. Thay thế mốc 48 giờ trước đây.

Topic Advising đã nối `GET /api/topics/my-topics` và `GET /api/topics/{id}/tracking`; danh sách đợt dùng `GET /api/academic-periods/batches`. Chi tiết phần đã nối và API còn thiếu: [TopicAdvisingIntegration.md](TopicAdvisingIntegration.md).

Backend đã có đăng ký/xác nhận trong `/api/thesis/topic-registrations`, và chỉ định SV bằng `studentIds`/`studentCodes` khi tạo/sửa đề tài hoặc `/advisor-assign`. Không coi các API này là chưa triển khai chỉ vì đường dẫn khác bản đề xuất cũ.

**Bổ sung DTO tracking:** cả `GET /api/topics/my-topics` và `GET /api/topics/{id}/tracking` trả `confirmedStudentsCount: number` (số thành viên Confirmed) và `pendingConfirmationsCount: number` (Pending chưa hết hạn tại thời điểm đọc; hạn bằng hiện tại đã hết hạn). Pending cũ không có hạn vẫn được tính để khớp quy tắc giữ chỗ hiện tại. Không có nhóm trả 0 cho cả hai. Frontend cộng hai trường trên các nhóm đang hướng dẫn trong phạm vi GV/đợt, loại Completed/Stopped. Không trả token xác nhận.

**Tên giảng viên trong danh mục công khai:** `GET /api/topics/all-topics` trả thêm `advisorName: string | null` trong từng `TopicSummaryDto`, đọc tên hiện tại từ Identity theo `advisorId`. Chỉ tra cứu một lần cho các giảng viên trong trang kết quả; không trả email, số điện thoại hoặc danh sách tài khoản. Nếu không tìm thấy tài khoản thì trả null. Frontend dùng trường này cho danh sách và chi tiết/đăng ký; không cần endpoint tra cứu tài khoản riêng hay quyền `identity.users.read`.

**Giới hạn DTO tracking hiện tại:** `group.students` chứa cả bản ghi thành viên `Pending` và `Confirmed`, nhưng không trả `confirmationStatus`, `confirmedAtUtc`, `confirmationExpiresAtUtc`, `invitedByStudentId`. `registeredStudentsCount`/`remainingSlots` vẫn tính theo số bản ghi thành viên; không phải các số liệu xác nhận mới. Frontend không suy ra xác nhận từ `joinedAt`, số người hoặc trạng thái nhóm. Chỉ hiển thị `—` cho thống kê xác nhận nếu API cũ thiếu trường hoặc tải dữ liệu lỗi.

### 5.1. Luồng nghiệp vụ đã chốt

| Trường hợp | Xử lý |
| --- | --- |
| Một nhóm cho một đề tài | Mỗi đề tài chỉ có **một nhóm đăng ký đang hoạt động**, tối đa `studentNumber` thành viên (1 hoặc 2). Sinh viên đăng ký thêm vào chỗ trống phải vào cùng nhóm hiện có; không tạo nhóm thứ hai. Giữ lịch sử nhóm/hồ sơ đã hủy. |
| Đăng ký 1 sinh viên | A đăng ký hợp lệ thì được ghi nhận thành viên ngay và vào danh sách hướng dẫn, không cần email xác nhận. Đề tài chỉ tiêu 2 vẫn còn 1 chỗ để sinh viên khác đăng ký. |
| Đăng ký kèm lời mời SV thứ hai | A được ghi nhận ngay; hệ thống chỉ gửi email mời **B**, giữ chỗ thứ hai cho B **24 giờ** từ khi tạo lời mời. B chấp nhận hợp lệ thì được thêm vào cùng nhóm A. Không chờ B để ghi nhận A, không yêu cầu A xác nhận lại. |
| B không xác nhận hoặc từ chối | B không phản hồi trong 24 giờ thì lời mời hết hạn; B chủ động không tham gia thì lời mời bị từ chối. Chỉ giải phóng chỗ của B, giữ nguyên đăng ký của A và mở chỗ trống cho sinh viên khác. |
| Sinh viên khác đăng ký chỗ trống | C đăng ký cá nhân vào đề tài chỉ tiêu 2 đang có A và còn chỗ khả dụng thì được thêm ngay vào **cùng nhóm**, không gửi email xác nhận cá nhân, không cần A/GV duyệt. |
| GV chỉ định sinh viên | GV có thể chọn trước sinh viên khi đề xuất đề tài, không vượt chỉ tiêu. API tạo/cập nhật phải hỗ trợ lưu và trả danh sách chỉ định; xem mục 5.7. |
| Giảng viên phụ trách | Chỉ theo dõi đề tài, hồ sơ đăng ký và nhóm hướng dẫn; không duyệt, tiếp nhận, từ chối hoặc tự hủy nhóm. |
| Ngoại lệ hủy nhóm | Giảng viên liên hệ Văn phòng Khoa. Khoa xem xét và xử lý hủy, lưu lý do và cập nhật chỉ tiêu. |

Không bổ sung API GV duyệt/từ chối nhóm hoặc tải phiếu. Xác nhận tham gia nhóm là nghiệp vụ riêng, không sử dụng trạng thái kích hoạt tài khoản làm bằng chứng tham gia.

**Nhóm và lời mời là hai đối tượng trạng thái riêng:** nhóm có A đã có hiệu lực vẫn thuộc danh sách hướng dẫn dù lời mời B đang chờ/hết hạn. Không chuyển toàn bộ nhóm sang chờ hoặc hết hạn theo trạng thái của lời mời B.

### 5.2. API hiện có cần mở rộng

| API | Yêu cầu bổ sung |
| --- | --- |
| `GET /api/topics/my-topics` | Bổ sung `topicCode`, `confirmedStudentsCount`, `reservedSlots`, `availableSlots`, `pendingInvitationsCount`, thông tin chỉ định; tìm thêm mã đề tài/nhóm/hồ sơ, họ tên/MSSV/email. Giữ lọc theo đợt, trạng thái và phân trang hiện có. |
| `GET /api/topics/{id}/tracking` | Giữ một `group` hiện tại; bổ sung thành viên có hiệu lực, lời mời, chỉ định và lịch sử hồ sơ. Có thể lấy lịch sử từ API đăng ký bằng `topicId`, không thay `group` thành mảng nhóm đang hoạt động. |
| `GET /api/topics/all-topics` | Đã bổ sung `confirmedStudentsCount`, `reservedSlots`, `availableSlots`, tính hết hạn theo server. Điều kiện đăng ký cá nhân vẫn do API đăng ký kiểm tra; không công khai token/email lời mời hoặc thông tin SV. |

- `confirmedStudentsCount` là số sinh viên thuộc đăng ký thành công còn được tính vào chỉ tiêu, bao gồm cá nhân đăng ký thành công ngay. Không tính dựa trên số email đã xác nhận.
- `pendingInvitationsCount` là số lời mời còn hiệu lực, tối đa 1 cho một đề tài tại cùng thời điểm.
- `reservedSlots` là số chỗ giữ cho lời mời còn trong hạn 24 giờ. Nếu thống nhất thêm cơ chế giữ chỗ cho chỉ định GV chưa có hiệu lực, phải ghi rõ nguồn và không đếm trùng.
- `availableSlots = studentNumber - confirmedStudentsCount - reservedSlots`, luôn >= 0. `isAvailable` phản ánh còn chỗ; `canRegister` còn kiểm tra công bố, thời gian và điều kiện người đăng nhập. Có nhóm chưa đồng nghĩa đã hết chỗ.
- Đề tài 2 SV: A đăng ký cá nhân → `1/0/1`; A đăng ký kèm mời B → `1/1/0`; B nhận lời → `2/0/0`; B từ chối/hết 24h → `1/0/1`; C vào chỗ trống → `2/0/0`. Thứ tự số là thành viên/chỗ giữ/chỗ trống, cùng một `groupId`.
- Bộ lọc `registrationStatus` hiện có (`All`, `Empty`, `Registered`, `Full`) phản ánh tình trạng chỉ tiêu. Không đổi ý nghĩa bộ lọc này thành trạng thái hồ sơ/lời mời; trạng thái mới thuộc API đăng ký bên dưới.

### 5.3. API đọc cần bổ sung cho màn hình giảng viên

| Method | Path đề xuất | Quyền đề xuất | Mục đích |
| --- | --- | --- | --- |
| `GET` | `/api/thesis/registrations` | `Lecturer` chính chủ; `Faculty`, `Admin` trong phạm vi được cấp | Danh sách hồ sơ đăng ký và lời mời, gồm lịch sử hết hạn/từ chối/hủy; dùng chung cho tab Nhóm đăng ký và Đang hướng dẫn. |
| `GET` | `/api/thesis/registrations/{id}` | Như danh sách, kiểm tra quyền trên hồ sơ cụ thể | Chi tiết hồ sơ, đề tài, nhóm, thành viên, lời mời và tiến độ khóa luận. |
| `GET` | `/api/thesis/advising/statistics` | `Lecturer` chính chủ; `Faculty`, `Admin` trong phạm vi được cấp | Tổng đề tài, nhóm chờ xác nhận tham gia, nhóm đang hướng dẫn và sinh viên đang hướng dẫn theo đợt. |

#### Query danh sách đăng ký

| Tham số | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `batchId` | `uuid?` | Lọc theo đợt KLTN; bỏ trống để xem các đợt được phép. |
| `topicId` | `uuid?` | Lọc các hồ sơ của một đề tài; giữ lịch sử nhóm cũ. |
| `keyword` | `string?` | Tìm tên tiếng Việt/Anh, mã đề tài/nhóm/hồ sơ, tên sinh viên, MSSV hoặc email; hỗ trợ tìm tiếng Việt không dấu. |
| `registrationStatus` | `string?` | `Confirmed`, `Cancelled` cho đăng ký có hiệu lực/đã hủy; không dùng thay trạng thái lời mời. |
| `invitationStatus` | `string?` | `Pending`, `Accepted`, `Expired`, `Declined`. Nhóm có A đã đăng ký và B đang chờ phải lọc được bằng `registrationStatus=Confirmed` và `invitationStatus=Pending`. |
| `progressStatus` | `string?` | Mã tiến độ khóa luận; tách khỏi trạng thái đăng ký, ánh xạ enum thật của ThesisManagement. |
| `scope` | `string` | `all` (mặc định): hồ sơ trong phạm vi quyền; `advising`: đăng ký thành công và còn thuộc danh sách hướng dẫn. |
| `pageNumber` | `integer` | >= 1, mặc định 1. |
| `pageSize` | `integer` | 1–100, mặc định 10. |
| `advisorId` | `uuid?` | Chỉ dành cho vai trò có quyền xem nhiều GV. GV thông thường luôn bị giới hạn theo tài khoản đăng nhập. |

Response danh sách dùng `ApiResponse<PagedResult<RegistrationSummaryDto>>` theo chuẩn dự án: `items`, `totalCount`, `pageNumber`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`. Lọc, đếm và phân trang tại server; không đếm riêng trên dữ liệu của trang hiện tại. Sắp xếp ổn định theo thời điểm đăng ký giảm dần, rồi ID để phân trang nhất quán.

#### Dữ liệu tối thiểu của hồ sơ/chi tiết

| Trường đề xuất | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `registrationId`, `registrationCode` | `uuid`, `string` | ID và mã hiển thị hồ sơ. |
| `groupId`, `groupCode` | `uuid`, `string` | Nhóm duy nhất hiện tại, tạo ngay khi SV đầu tiên có hiệu lực; SV thứ hai dùng lại nhóm đó. Giữ liên kết lịch sử khi Khoa hủy. |
| `topicId`, `topicCode`, `titleVi`, `titleEn` | Theo dữ liệu đề tài | Liên kết và hiển thị đề tài. Response chi tiết cần có nội dung/yêu cầu hoặc lấy lại từ API tracking hiện có. |
| `batchId`, `advisorId` | `uuid` | Đợt và giảng viên phụ trách; tên có thể lấy từ nguồn tra cứu hiện có. |
| `registeredByStudentId` | `uuid?` | Người đăng ký lấy từ token; có thể `null` với GV chỉ định trực tiếp, khi đó trả `assignedBy`. |
| `registrationSource` | `string` | `SelfRegistration`, `Invitation`, `AdvisorAssignment`; nguồn ghi nhận SV, không phải quyền trưởng nhóm. |
| `registrationStatus` | `string` | `Confirmed`: đăng ký có hiệu lực; `Cancelled`: Khoa đã hủy. B hết hạn không làm A mất trạng thái có hiệu lực. |
| `registeredAt` | `datetime` | Thời điểm tạo hồ sơ. |
| `invitations` | `array` | Lịch sử lời mời: `invitationId`, `invitedStudentId`, `invitationStatus`, `invitedAt`, `invitationExpiresAt`, `invitationAcceptedAt`, `declinedAt`. Tối đa một lời mời `Pending` còn hạn/đề tài; không trả token cho GV. |
| `effectiveAt` | `datetime?` | Thời điểm hồ sơ/thành viên có hiệu lực: A ngay khi đăng ký, B khi nhận lời; không chờ B để ghi nhận A. |
| `progressStatus` | `string?` | Tiến độ khóa luận riêng, không dùng thay trạng thái đăng ký. |
| `isCurrentlyAdvising` | `boolean` | Backend xác định theo đăng ký và vòng đời nhóm, dùng nhất quán với `scope=advising` và thống kê. |
| `cancelledAt`, `cancelledBy`, `cancellationReason` | Nullable | Thời điểm, người xử lý và lý do Khoa hủy. |
| `students` | `array` | Thành viên **đã có hiệu lực**: ID, MSSV, họ tên, email, điện thoại, nguồn ghi nhận và `effectiveAt`. B chưa đồng ý chỉ nằm trong `invitations`, chưa tính là thành viên. |
| `assignedBy`, `assignedAt` | Nullable | Người và thời điểm GV chỉ định. |
| `confirmedStudentsCount`, `reservedSlots`, `availableSlots` | `integer` | Số thành viên có hiệu lực, chỗ giữ và chỗ trống theo mục 5.2. |

Các trường frontend demo như `PendingEmail`, `emailConfirmedAt`/`participationConfirmedAt` hoặc `confirmedAt` chưa phải contract backend đã chốt. Khi tích hợp phải tách thành viên có hiệu lực với lời mời, hiển thị chỗ giữ/chỗ trống. Không coi mọi thành viên đều phải xác nhận email. Một nhóm có thể vừa thuộc danh sách hướng dẫn vừa có lời mời đang chờ.

#### Response thống kê đề xuất

Query: `batchId` tùy chọn; `advisorId` chỉ dành cho vai trò được phép xem nhiều GV. Các số liệu tính trên toàn bộ đợt/phạm vi quyền, không phụ thuộc từ khóa, trạng thái hoặc trang đang xem.

```json
{
  "success": true,
  "data": {
    "totalTopics": 7,
    "pendingInvitationGroups": 2,
    "advisingGroups": 2,
    "advisingStudents": 3
  },
  "error": null
}
```

`advisingStudents` đếm thành viên đã có hiệu lực: A được tính ngay dù B còn chờ, B chỉ tính khi nhận lời. `pendingInvitationGroups` có thể giao với `advisingGroups`, không cộng hai chỉ số thành tổng nhóm. Đếm mỗi nhóm một lần; lời mời hết hạn/từ chối không loại A khỏi danh sách hay thống kê.

### 5.4. API nghiệp vụ sinh viên và Khoa — trạng thái hiện tại

| Trạng thái | Method / Path | Request / Phạm vi |
| --- | --- | --- |
| Đã có | `POST /api/thesis/topic-registrations/` | `{ "topicId": "<uuid>", "partnerStudentId": null, "partnerUserCode": null }`; cá nhân được ghi nhận ngay. Truyền ID hoặc MSSV B nếu mời thêm; B có 24h để xác nhận, A không cần xác nhận lại. |
| Đã có | `POST /api/thesis/topic-registrations/confirm` | `{ "token": "<confirmation-token>" }`; xác nhận tham gia của B. Không dùng đường dẫn đề xuất cũ `/confirm-participation`. |
| Đã có, chỉ dành cho SV | `GET /api/thesis/topic-registrations/my-status` | Trạng thái đăng ký của SV đang đăng nhập, thời hạn xác nhận và thông tin vi phạm; không thay thế API danh sách dành cho GV. |
| Chưa có; path đề xuất | `POST /api/thesis/registrations/decline-participation` | `{ "token": "<invitation-token>" }`; B từ chối, giải phóng chỗ B, giữ A. |
| Chưa có; path đề xuất | `POST /api/thesis/registrations/{id}/resend-invitation` | Người tạo lời mời gửi lại; có rate limit, vô hiệu token cũ, không gia hạn mốc 24h. |
| Chưa có; path đề xuất | `POST /api/thesis/registrations/{id}/cancel` | Khoa/Admin xử lý hủy nhóm với `{ "reason": "..." }`, lưu audit và giải phóng chỉ tiêu. GV không có quyền hủy nhóm SV tự đăng ký. |

Token xác nhận chỉ gửi B; mail thông báo cho A không yêu cầu A xác nhận. Hết hạn giải phóng chỗ B, không xóa đăng ký A. Backend hiện gửi token qua mail; liên kết xác nhận và màn hình sinh viên là phần tích hợp riêng.

### 5.5. Ràng buộc và xử lý lỗi cần có

- Kiểm tra đề tài đã công bố, đợt/thời gian đăng ký hợp lệ, sinh viên đủ điều kiện và chưa có đăng ký xung đột; nhóm tối đa 2 SV, phù hợp chỉ tiêu, cùng đợt. Không cho người đăng ký tự mời chính mình.
- Chỉ gửi email mời đến địa chỉ của B trong hệ thống. Gửi mail thành công không đồng nghĩa B đã là thành viên; A vẫn có hiệu lực ngay khi giao tác đăng ký hoàn tất.
- Request kèm lời mời phải kiểm tra cả A, B và đủ chỗ trước khi ghi nhận. Lưu A, nhóm, lời mời giữ chỗ và yêu cầu gửi mail trong transaction/outbox; không ghi dở dang hoặc âm thầm bỏ B khi request không hợp lệ.
- Đăng ký và xác nhận lời mời phải kiểm tra chỉ tiêu trong transaction với cơ chế xử lý đồng thời. Kiểm tra lại tính hợp lệ của đề tài/thành viên tại thời điểm xác nhận.
- Ràng buộc DB và transaction/locking bảo đảm mỗi đề tài chỉ một nhóm hoạt động. Hai SV đăng ký cá nhân đồng thời vào đề tài 2 chỗ phải dùng chung một nhóm, không tạo hai nhóm riêng.
- `invitationExpiresAt = invitedAt + 24 giờ`, theo UTC server. Tại `now >= invitationExpiresAt`, lời mời hết hạn, token không thể nhận chỗ. Phải giải phóng chỗ kể cả khi worker xử lý hết hạn chưa chạy; thao tác đọc/đăng ký dùng cùng quy tắc.
- Xác nhận, từ chối, hết hạn và C đăng ký chỗ trống phải tuần tự hóa trong transaction. Không thêm thành viên/trừ hoặc trả chỗ hai lần, không gửi trùng do retry. B hết hạn/từ chối không hủy A hoặc nhóm.
- Hủy đăng ký bởi Khoa là nghiệp vụ riêng với việc dừng khóa luận do không đạt cổng thẩm định; không tự đồng nhất `Cancelled` với `Stopped` hiện có.
- Mọi API đọc kiểm tra quyền ở server. Không dựa vào `advisorId` client truyền để cho phép GV xem hồ sơ của người khác; không trả token lời mời cho GV.

| HTTP status đề xuất | Trường hợp |
| --- | --- |
| `400` | Dữ liệu/mã bộ lọc không hợp lệ, tự mời bản thân, thiếu lý do hủy hoặc token không hợp lệ. |
| `401` / `403` | Chưa xác thực khi endpoint yêu cầu đăng nhập / sai vai trò, sai người nhận hoặc ngoài phạm vi quyền. |
| `404` | Không tìm thấy đề tài, sinh viên hoặc hồ sơ. |
| `409` | Hết chỗ khả dụng (kể cả chỗ đang giữ), đăng ký/chỉ định xung đột, ngoài thời gian đăng ký, lời mời đã đóng hoặc cố tạo nhóm thứ hai. Không từ chối chỉ vì đề tài đã có nhóm nếu còn chỗ khả dụng. |
| `410` | Lời mời đã hết hạn. |
| `429` | Vượt giới hạn gửi lại lời mời. |

Response lỗi dùng `ApiResponse` theo chuẩn dự án; mã lỗi cụ thể cần được chốt khi triển khai, không sử dụng bảng lỗi mục 4 để khẳng định nghiệp vụ đăng ký đã được hỗ trợ.

### 5.6. Tình huống nghiệm thu và phần còn cần thống nhất

| Tình huống | Kết quả bắt buộc |
| --- | --- |
| Đề tài chỉ tiêu 1, A đăng ký | Một nhóm chứa A có hiệu lực ngay; thành viên/chỗ giữ/chỗ trống = `1/0/0`. |
| Đề tài chỉ tiêu 2, A đăng ký cá nhân | Một nhóm chứa A có hiệu lực ngay, còn chỗ công khai; `1/0/1`. |
| C đăng ký cá nhân vào chỗ trống | C cùng nhóm A, `groupId` không đổi; `2/0/0`, không email xác nhận. |
| A đăng ký kèm mời B | A có hiệu lực ngay, B giữ chỗ 24h; `1/1/0`; C chưa thể lấy chỗ của B. |
| B nhận lời trước hạn | B vào nhóm A; `2/0/0`; retry không thêm thành viên trùng. |
| B không phản hồi đủ 24h hoặc từ chối | Lời mời hết hạn/bị từ chối; A vẫn có hiệu lực, `1/0/1`, C được đăng ký vào cùng nhóm. |
| B mở link cũ sau hạn, C đã đăng ký | Trả lỗi hết hạn; không thêm B, không thay C, không tạo nhóm khác. |
| Gửi lại mail ở giờ thứ 23 | Mốc hết hạn vẫn là 24h từ lời mời gốc, không cộng thêm 24h. |
| Hai SV cạnh tranh chỗ cuối | Một request thành công, request còn lại lỗi hết chỗ; không vượt chỉ tiêu. |
| GV chỉ định một SV cho đề tài chỉ tiêu 2 | Lưu chỉ định; khi có hiệu lực chỉ dùng một nhóm và một chỗ, chỗ còn lại mở theo điều kiện công bố/đăng ký. |

**Đã chốt:** giữ chỗ 24h, một nhóm/đề tài, giải phóng chỗ của lời mời không thành công, đăng ký cá nhân có hiệu lực ngay và cho phép đăng ký vào chỗ còn lại, GV được chọn SV khi đề xuất.

**Còn cần thống nhất/bổ sung:** cách tính nhóm dừng thực hiện/bảo lưu/đã bảo vệ/hoàn thành; lịch sử lời mời và hồ sơ hủy. Chỉ định GV hiện được áp dụng ngay trong giai đoạn `TOPIC_PREPARATION`; không dùng lại phương án cũ đợi công bố. Không đưa những quy tắc người dùng đã chốt trở lại danh sách câu hỏi mở.

**Ưu tiên bổ sung cho màn hình GV:** 3 API GET ở mục 5.3, DTO xác nhận/lời mời và chỉ tiêu ở mục 5.2. Đăng ký, xác nhận và chỉ định SV đã có backend; từ chối lời mời, gửi lại lời mời và Khoa hủy nhóm vẫn chưa có. Xem bảng trạng thái mục 5.4 và báo cáo tích hợp; không coi toàn bộ mục 5 là chưa triển khai.

### 5.7. GV chỉ định sinh viên khi đề xuất — API đã có

| API hiện có | Contract thực tế |
| --- | --- |
| `POST /api/topics` | Nhận `studentIds: uuid[]` hoặc `studentCodes: string[]` bên cạnh các trường đề xuất; không dùng `assignedStudentIds`. |
| `PUT /api/topics/{id}` | Nhận `studentIds` / `studentCodes` và `clearAssignedStudents: boolean`. |
| `PUT /api/thesis/topic-registrations/advisor-assign` | Gán/cập nhật sinh viên cho đề tài; điều kiện chủ sở hữu, chỉ tiêu và giai đoạn `TOPIC_PREPARATION`. |
| `DELETE /api/thesis/topic-registrations/advisor-assign/{topicId}` | Bỏ chỉ định trong giai đoạn chuẩn bị; không phải API Khoa hủy nhóm SV tự đăng ký ở giai đoạn đăng ký. |

Sinh viên được chỉ định có trạng thái `Confirmed` ngay, nhận email thông báo; kiểm tra chỉ tiêu và đăng ký xung đột. Đây là hành vi backend hiện tại, thay thế phương án cũ `Configured` → `Effective` khi công bố.

Còn thiếu thông tin nguồn chỉ định/ai chỉ định/thời điểm hiệu lực trong DTO tracking. Nếu cần giao diện tìm SV cho Lecturer, cần nguồn tra cứu giới hạn phạm vi như `GET /api/topics/assignment-candidates?batchId=...&keyword=...&pageNumber=1&pageSize=10` (path đề xuất, chưa có), không cấp quyền đọc toàn bộ tài khoản chỉ để chọn SV.

Frontend Topic Advising đọc dữ liệu theo dõi. Form đề xuất đã nối `studentCodes` qua API tạo/sửa đề tài; hỗ trợ đọc lại danh sách, lưu tạm, xem trước, kiểm tra chỉ tiêu/trùng MSSV và bỏ chỉ định bằng `clearAssignedStudents` khi người dùng xóa danh sách rồi lưu. Không gửi lại chỉ định khi chỉ sửa nội dung, không sao chép sinh viên khi nhân bản đề tài. Đây là sửa chỉ định trong giai đoạn chuẩn bị, không thêm quyền GV hủy nhóm SV tự đăng ký. Các giới hạn hiện tại được ghi tại [TopicAdvisingIntegration.md](TopicAdvisingIntegration.md).
