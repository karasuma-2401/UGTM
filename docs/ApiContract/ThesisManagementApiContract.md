# ThesisManagement API contract

Trạng thái: SCRUM-88 (báo cáo tiến độ) và SCRUM-89 (thẩm định đề cương chi tiết, báo cáo giữa kỳ) đã triển khai.

Mọi endpoint trong tài liệu này yêu cầu Bearer token. Response thành công có dạng `ApiResponse<T>`; response lỗi chứa `code`, `message` và HTTP status phù hợp. Thời điểm truyền trong JSON dùng ISO 8601 UTC. Các endpoint tải tệp không trả object key nội bộ hoặc URL public.

## Phân quyền và quy ước tệp

| Nghiệp vụ | Chủ thể được phép |
| --- | --- |
| Cấu hình deadline Cổng 1/Cổng 2 | `Admin`, `AcademicAffairs` có `thesis.review-gate-config.manage` |
| Nộp hồ sơ Cổng 1/Cổng 2 | Bất kỳ sinh viên nào thuộc nhóm |
| Thẩm định Cổng 1/Cổng 2 | `Faculty`, `Admin` có `thesis.review-gates.review` |
| Tạo/xem Milestone, đánh giá báo cáo tiến độ | GVHD hiện được gán cho `TopicId` của nhóm, hoặc `Admin` |
| Nộp/xem báo cáo tiến độ | Thành viên nhóm; GVHD được gán, Khoa có quyền thẩm định và `Admin` được xem/tải |

Các file nộp chỉ chấp nhận `.pdf`, `.doc`, `.docx`, tối đa 20 MiB. Server kiểm tra extension, kích thước và chữ ký nội dung trước khi lưu vào bucket S3/MinIO private. URL tải là presigned URL, hiệu lực 5 phút.

## Tổng quan endpoint

| Method | Route | Response |
| --- | --- | --- |
| `PUT` | `/api/thesis/review-gate-configurations/{batchId}/{gateType}` | `ReviewGateConfigurationDto` |
| `GET` | `/api/thesis/review-gate-configurations/{batchId}` | `IReadOnlyList<ReviewGateConfigurationDto>` |
| `POST` | `/api/thesis/groups/{groupId}/syllabus/submissions` | `SyllabusSubmissionDto` |
| `POST` | `/api/thesis/groups/{groupId}/syllabus/review` | `SubmissionReviewDto` |
| `POST` | `/api/thesis/groups/{groupId}/midterm-report/submissions` | `MidtermReportSubmissionDto` |
| `POST` | `/api/thesis/groups/{groupId}/midterm-report/review` | `SubmissionReviewDto` |
| `GET` | `/api/thesis/groups/{groupId}/submissions/{gateType}` | lịch sử Cổng 1/Cổng 2 |
| `GET` | `/api/thesis/submission-files/{fileId}/download` | `SubmissionFileDownloadDto` |
| `POST` | `/api/thesis/groups/{groupId}/progress-milestones` | `ProgressMilestoneDto` |
| `GET` | `/api/thesis/groups/{groupId}/progress-milestones` | `IReadOnlyList<ProgressMilestoneDto>` |
| `POST` | `/api/thesis/progress-milestones/{milestoneId}/report` | `ProgressReportSubmissionDto` |
| `POST` | `/api/thesis/progress-milestones/{milestoneId}/report/review` | `ProgressReportReviewDto` |
| `GET` | `/api/thesis/progress-milestones/{milestoneId}/report` | `ProgressReportHistoryDto` |
| `GET` | `/api/thesis/progress-report-files/{fileId}/download` | `SubmissionFileDownloadDto` |

`{batchId}`, `{groupId}`, `{milestoneId}` và `{fileId}` là GUID. `gateType` chỉ nhận `Syllabus` hoặc `Midterm`.

## Cấu hình deadline thẩm định

`PUT /api/thesis/review-gate-configurations/{batchId}/{gateType}` tạo mới hoặc thay thế idempotent cấu hình của đúng một cổng. `gateType` là `Syllabus` (Cổng 1 — đề cương chi tiết) hoặc `Midterm` (Cổng 2 — báo cáo giữa kỳ).

```json
{
  "initialSubmissionDeadlineUtc": "2026-10-01T16:59:00Z",
  "facultyResponseDeadlineUtc": "2026-10-03T16:59:00Z",
  "finalClosureDeadlineUtc": "2026-10-05T16:59:00Z"
}
```

Ba deadline bắt buộc là UTC và tuân theo thứ tự nghiêm ngặt `initialSubmissionDeadlineUtc < facultyResponseDeadlineUtc < finalClosureDeadlineUtc`. `GET /api/thesis/review-gate-configurations/{batchId}` trả danh sách cấu hình hiện có (có thể rỗng hoặc mới có một cổng). Hai endpoint chỉ dành cho `Admin`/`AcademicAffairs`; mọi mutation thành công đều ghi audit outbox trong cùng transaction.

## Cổng 1 và Cổng 2

### Nộp hồ sơ

- `POST /api/thesis/groups/{groupId}/syllabus/submissions`
- `POST /api/thesis/groups/{groupId}/midterm-report/submissions`

Request là `multipart/form-data` với trường bắt buộc `file`. Mọi thành viên nhóm có thể nộp thay nhóm. Lần đầu chỉ được nộp không quá `initialSubmissionDeadlineUtc`. Khi nhận `NeedRevision`, nhóm được nộp v2, v3... đến `finalClosureDeadlineUtc`; không thể nộp khi phiên bản đang chờ thẩm định, đã duyệt hoặc bị từ chối. Mỗi phiên bản giữ file và review riêng, không ghi đè lịch sử.

Riêng Cổng 2 yêu cầu nhóm đã qua Cổng 1 và đang `InProgress`. Cả hai endpoint trả `400` với file/request lỗi, `403` khi người nộp không thuộc nhóm, `404` khi không có nhóm, `409` khi thiếu cấu hình/trễ hạn/trạng thái không hợp lệ và `413` khi multipart vượt giới hạn.

### Thẩm định

- `POST /api/thesis/groups/{groupId}/syllabus/review`
- `POST /api/thesis/groups/{groupId}/midterm-report/review`

```json
{
  "decision": "Approved",
  "feedback": null
}
```

`decision` nhận `Approved`, `NeedRevision` hoặc `Rejected` (không phân biệt hoa thường). `feedback` bắt buộc khi `NeedRevision` hoặc `Rejected`. Người thẩm định phải có `thesis.review-gates.review`.

| Cổng | `Approved` | `NeedRevision` | `Rejected` |
| --- | --- | --- | --- |
| Cổng 1 | Chuyển nhóm sang `InProgress`, mở Milestone | Mở quyền nộp phiên bản mới | Chuyển nhóm sang `Stopped` |
| Cổng 2 | Trả nhóm về `InProgress` | Mở quyền nộp phiên bản mới | Chuyển nhóm sang `Stopped` |

Phiên bản đầu phải được phản hồi trước `facultyResponseDeadlineUtc`; mọi quyết định phải hoàn tất trước `finalClosureDeadlineUtc`. Request muộn, thẩm định phiên bản không còn `Submitted`, hoặc đụng độ cập nhật trả `409`.

### Lịch sử và tải file

`GET /api/thesis/groups/{groupId}/submissions/{gateType}` trả metadata file, các version và review của Cổng 1 hoặc Cổng 2. Thành viên chỉ xem hồ sơ nhóm mình; Faculty/Admin có quyền thẩm định có thể tra cứu để xử lý.

`GET /api/thesis/submission-files/{fileId}/download` xác thực quyền trước khi trả URL tải tạm thời. Hai endpoint này trả `403` nếu không được phép và `404` khi group/file/hồ sơ không tồn tại.

Đến `finalClosureDeadlineUtc`, background worker đóng các nhóm chưa được duyệt ở cổng tương ứng thành `Stopped`; thao tác chạy lặp phải idempotent.

## Milestone tiến độ

`POST /api/thesis/groups/{groupId}/progress-milestones` tạo mốc công việc cho nhóm đã ở `InProgress`:

```json
{
  "title": "Hoàn thành chương 3",
  "description": "Gửi bản thảo để GVHD phản hồi",
  "deadlineUtc": "2026-11-15T16:59:00Z"
}
```

`title` là bắt buộc, tối đa 250 ký tự; `description` tùy chọn; `deadlineUtc` phải là UTC trong tương lai. `GET` cùng route trả các milestone theo deadline tăng dần. Cả hai endpoint kiểm tra GVHD theo phân công hiện tại của đề tài, không chỉ dựa trên role `Faculty`; `Admin` có quyền thay thế. Tạo milestone ghi audit log. `409` biểu thị nhóm chưa qua Cổng 1 hoặc có xung đột đồng thời.

## Báo cáo tiến độ

### Nộp phiên bản

`POST /api/thesis/progress-milestones/{milestoneId}/report` nhận `multipart/form-data`:

| Field | Bắt buộc | Ràng buộc |
| --- | --- | --- |
| `file` | Có | Quy ước file chung |
| `gitUrl` | Có | URL tuyệt đối `http`/`https`, tối đa 2048 ký tự; server không gọi URL để tránh SSRF |
| `studentNote` | Không | Ghi chú của nhóm |

Chỉ thành viên nhóm và nhóm đang `InProgress` được nộp. Lần đầu tạo v1. Chỉ sau khi GVHD quyết định `NeedRevision`, nhóm mới có thể nộp v2, v3... Các version cũ, file, Git URL, ghi chú và review luôn được bảo toàn. `isLate` do server tính và chỉ là `true` khi thời điểm nhận thực tế muộn hơn deadline của milestone.

### GVHD đánh giá và tra cứu

`POST /api/thesis/progress-milestones/{milestoneId}/report/review`:

```json
{
  "decision": "NeedRevision",
  "feedback": "Bổ sung phần đánh giá thực nghiệm."
}
```

`decision` nhận `Accepted` hoặc `NeedRevision`; feedback bắt buộc cho `NeedRevision`. Chỉ GVHD đang được gán hoặc `Admin` đánh giá phiên bản hiện tại có trạng thái `Submitted`. `Accepted` khép lại milestone; `NeedRevision` mở lần nộp tiếp theo, không sửa lịch sử. Nếu đã có request đánh giá khác thay đổi phiên bản, API trả `409`; client phải tải lại lịch sử trước khi thao tác lại.

`GET /api/thesis/progress-milestones/{milestoneId}/report` trả toàn bộ version, trạng thái, Git URL, ghi chú, cờ trễ, metadata file và review. `GET /api/thesis/progress-report-files/{fileId}/download` trả URL tải tạm thời sau kiểm tra quyền.

## HTTP status và error code

- `400 Bad Request`: dữ liệu body/form, UTC deadline, enum decision/gate type hoặc file/Git URL không hợp lệ.
- `401 Unauthorized`: thiếu hoặc không hợp lệ Bearer token.
- `403 Forbidden`: không thuộc nhóm, không phải GVHD được gán hoặc thiếu permission thẩm định/cấu hình.
- `404 Not Found`: batch, group, milestone, submission/report hoặc file không tồn tại.
- `409 Conflict`: deadline/quy trình không cho phép, không đúng trạng thái, hoặc optimistic-concurrency conflict.
- `413 Payload Too Large`: multipart file vượt mức 20 MiB (cộng overhead request).

Các nhóm mã lỗi nghiệp vụ hiện dùng gồm `ReviewGateConfiguration.*`, `ReviewGate.*`, `Syllabus.*`, `MidtermReport.*`, `ProgressMilestone.*`, `ProgressReport.*`, `TopicRegistration.*` và `SubmissionFile.*`. Mutation thành công được audit; các thao tác cạnh tranh không ghi đè âm thầm.

## Đăng ký đề tài khóa luận & Xác nhận ghép nhóm

### Tổng quan endpoint đăng ký đề tài

| Method | Route | Response | Quyền truy cập |
| --- | --- | --- | --- |
| `POST` | `/api/thesis/topic-registrations` | `TopicRegistrationResultDto` | `Student` (đã xác thực) |
| `POST` | `/api/thesis/topic-registrations/confirm` | `ConfirmRegistrationResultDto` | Public / Student (dùng confirmation token) |
| `GET` | `/api/thesis/topic-registrations/my-status` | `StudentRegistrationStatusDto` | `Student` (đã xác thực) |

### 1. Đăng ký đề tài
`POST /api/thesis/topic-registrations`: Sinh viên đăng ký đề tài khóa luận (1 mình hoặc kèm bạn cùng nhóm).
```json
{
  "topicId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "partnerStudentId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "partnerUserCode": "SV002"
}
```
- **Chống race condition:** Sử dụng Database Transaction và Pessimistic Lock (`SELECT ... FOR UPDATE` trên bảng `topics.topic`) để đảm bảo không bị vượt chỉ tiêu `StudentNumber` (1 hoặc 2 sinh viên/đề tài).
- **Quy tắc gộp nhóm theo slot:** Nếu đề tài có chỉ tiêu 2 sinh viên và có 2 sinh viên đăng ký đơn lẻ lần lượt vào đề tài, hệ thống tự động gộp 2 sinh viên vào chung một `StudentGroup`.
- **Cơ chế xác nhận & giữ chỗ:**
  - Đăng ký 1 mình: Trạng thái `Confirmed` ngay lập tức.
  - Đăng ký kèm bạn (SV A kèm SV B): SV A ở trạng thái `Confirmed`; SV B ở trạng thái `Pending` và nhận email kèm token xác nhận (thời hạn 24 giờ). Slot của SV B được tạm giữ trong thời gian chờ.
- **Mã lỗi đặc trưng:**
  - `TopicRegistration.TopicNotFound` (404)
  - `TopicRegistration.TopicAlreadyTaken` (409)
  - `TopicRegistration.TopicNotPublished` (409)
  - `TopicRegistration.RegistrationWindowClosed` (409)
  - `TopicRegistration.StudentAlreadyRegisteredInBatch` (409)
  - `TopicRegistration.StudentRegistrationBlocked` (403)
  - `TopicRegistration.PartnerRegistrationBlocked` (403)
  - `TopicRegistration.GroupSizeExceedsTopicQuota` (400)
  - `TopicRegistration.PartnerNotFound` (404)
  - `TopicRegistration.CannotInviteSelf` (400)

### 2. Xác nhận tham gia nhóm đề tài
`POST /api/thesis/topic-registrations/confirm`:
```json
{
  "token": "sample-confirmation-token"
}
```
- Sinh viên B xác nhận tham gia nhóm đề tài bằng mã token nhận qua email.
- Sau khi xác nhận thành công, trạng thái thành viên chuyển thành `Confirmed`.
- **Mã lỗi:**
  - `TopicRegistration.InvalidConfirmationToken` (400)
  - `TopicRegistration.AlreadyConfirmed` (400)
  - `TopicRegistration.ConfirmationExpired` (400)
  - `TopicRegistration.Forbidden` (403)
  - `TopicRegistration.AlreadyConfirmed` (409)

### 3. Tra cứu trạng thái đăng ký cá nhân
`GET /api/thesis/topic-registrations/my-status`:
- Trả về thông tin nhóm đề tài hiện tại, trạng thái xác nhận (`Confirmed` / `Pending`), thời hạn xác nhận còn lại (nếu có), số lần vi phạm cảnh cáo (`violationCount`) và cờ khóa quyền đăng ký (`isRegistrationBlocked`).

### 4. Background Worker: Xử lý hết hạn & Phạt cảnh cáo
- Định kỳ quét các yêu cầu `Pending` có `confirmationExpiresAtUtc < nowUtc`.
- Tự động hủy yêu cầu của sinh viên B, nhả slot cho đề tài.
- Ghi nhận vi phạm (+1) cho sinh viên không bấm xác nhận:
  - Lần 1 và lần 2: Gửi email cảnh cáo.
  - Lần 3 (`violationCount >= 3`): Khóa quyền đăng ký đề tài (`isRegistrationBlocked = true`), gửi email thông báo khóa quyền đăng ký (tài khoản vẫn đăng nhập hệ thống bình thường).
- Cơ chế lazy cleanup ngay trong transaction đăng ký mới cũng tự động thu hồi slot quá hạn nếu có lượt đăng ký mới đến trước khi worker chạy.
