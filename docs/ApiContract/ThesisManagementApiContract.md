# ThesisManagement API contract

Trạng thái: SCRUM-88 (báo cáo tiến độ) và SCRUM-89 (thẩm định đề cương chi tiết, báo cáo giữa kỳ) đã triển khai.
SCRUM-102 đã triển khai đến CP6: nộp/nộp lại báo cáo cuối kỳ, xem lịch sử/tải từng phiên bản theo quyền và bắt đầu chấm để khóa phiên bản. Chấm điểm/duyệt phản biện thuộc SCRUM-103.

CP7 đồng bộ yêu cầu file và data model, kiểm tra ba bộ test ThesisManagement/AcademicPeriods/Architecture cùng luồng thực tế trên PostgreSQL/MinIO; xem [hướng dẫn và kết quả nghiệm thu](FinalReportSubmissionVerification.md). Không bao gồm triển khai frontend hoặc xác minh môi trường đang deploy.

Mọi endpoint trong tài liệu này yêu cầu Bearer token. Response thành công có dạng `ApiResponse<T>`; response lỗi chứa `code`, `message` và HTTP status phù hợp. Thời điểm truyền trong JSON dùng ISO 8601 UTC. Các endpoint tải tệp không trả object key nội bộ hoặc URL public.

## Phân quyền và quy ước tệp

| Nghiệp vụ | Chủ thể được phép |
| --- | --- |
| Cấu hình deadline Cổng 1/Cổng 2 | `Admin`, `AcademicAffairs` có `thesis.review-gate-config.manage` |
| Nộp hồ sơ Cổng 1/Cổng 2 | Bất kỳ sinh viên nào thuộc nhóm |
| Nộp/nộp lại báo cáo cuối kỳ | Role `Student`, thành viên `Confirmed` của chính nhóm, đã được duyệt giữa kỳ |
| Xem lịch sử/tải báo cáo cuối kỳ | `Student` đã `Confirmed` trong nhóm, GVHD hiện được phân công cho đề tài, `Faculty` có `thesis.review-gates.review`, hoặc `Admin` |
| Bắt đầu chấm báo cáo cuối kỳ | GVHD hiện được phân công cho đề tài hoặc `Admin`; quyền xem của Faculty không bao gồm thao tác này |
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
| `POST` | `/api/thesis/final-reports` | `FinalReportSubmissionDto` |
| `GET` | `/api/thesis/groups/{groupId}/final-report` | `FinalReportHistoryDto` |
| `GET` | `/api/thesis/final-report-files/{fileId}/download` | `SubmissionFileDownloadDto` |
| `POST` | `/api/thesis/final-reports/{finalReportId}/begin-evaluation` | `FinalReportEvaluationStartedDto` |
| `GET` | `/api/thesis/groups/{groupId}/submissions/{gateType}` | lịch sử Cổng 1/Cổng 2 |
| `GET` | `/api/thesis/submission-files/{fileId}/download` | `SubmissionFileDownloadDto` |
| `POST` | `/api/thesis/groups/{groupId}/progress-milestones` | `ProgressMilestoneDto` |
| `GET` | `/api/thesis/groups/{groupId}/progress-milestones` | `IReadOnlyList<ProgressMilestoneDto>` |
| `POST` | `/api/thesis/progress-milestones/{milestoneId}/report` | `ProgressReportSubmissionDto` |
| `POST` | `/api/thesis/progress-milestones/{milestoneId}/report/review` | `ProgressReportReviewDto` |
| `GET` | `/api/thesis/progress-milestones/{milestoneId}/report` | `ProgressReportHistoryDto` |
| `GET` | `/api/thesis/progress-report-files/{fileId}/download` | `SubmissionFileDownloadDto` |

`{batchId}`, `{groupId}`, `{milestoneId}` và `{fileId}` là GUID. `gateType` chỉ nhận `Syllabus` hoặc `Midterm`.

## Nộp và nộp lại báo cáo cuối kỳ (SCRUM-102 CP4)

`POST /api/thesis/final-reports`, Bearer token, body `multipart/form-data`:

| Trường | Kiểu | Yêu cầu |
| --- | --- | --- |
| `groupId` | GUID | Bắt buộc, đúng nhóm của sinh viên |
| `file` | File | Bắt buộc, đúng một tệp PDF/DOC/DOCX, không rỗng, tối đa 20 MiB (20,971,520 byte) |

Lần đầu yêu cầu nhóm đang `InProgress`; nộp lại yêu cầu nhóm đang `FinalReportSubmitted` và báo cáo hiện tại còn `Submitted`. Cả hai trường hợp đều yêu cầu báo cáo giữa kỳ đã `Approved`, đợt đang `Open` và thời điểm nhận nằm trong cửa sổ `FINAL_EVALUATION` theo lịch/múi giờ của đợt. Giờ mở và deadline đều được tính bao gồm chính thời điểm đó. Thiếu giai đoạn hoặc múi giờ không hợp lệ sẽ khóa nộp. Sinh viên cùng nhóm nhưng chưa `Confirmed` không được nộp.

Thành công trả `200 OK` với `ApiResponse<FinalReportSubmissionDto>`; `data` gồm:

- `finalReportId`, `groupId`, `versionType: "InitialFinal"`, `versionNo` (1 ở lần đầu, tăng dần khi nộp lại), `status: "Submitted"`, `submittedAtUtc` (thời điểm nộp phiên bản hiện tại).
- `file`: `id`, `fileName`, `contentType`, `fileSize`, `fileExtension`, `checksumSha256`, `uploadedBy`, `uploadedAtUtc`. MIME type và checksum do server xác định; không trả object key hay URL public.

File được lưu trong bucket private, metadata dùng `entity_type = 'FinalReport'`. Báo cáo, metadata, trạng thái nhóm `FinalReportSubmitted` và audit action `ThesisManagement.FinalReport.Submitted` được lưu cùng transaction database. Nộp lại giữ nguyên mã báo cáo và loại `InitialFinal`, tăng `CurrentVersionNo`, cập nhật thời điểm nộp và thêm file/audit mới với `IsResubmission = true`; không đổi thành `RevisedAfterReviewer`, không ghi đè hay xóa file/audit cũ. `SubmissionFile.UploadedBy` và `UploadedAt` ghi nhận người nộp và thời điểm của từng phiên bản.

Upload lỗi không tạo phiên bản. Lỗi database đã rollback sẽ xóa bù object vừa upload và giữ nguyên phiên bản trước; số phiên bản không bị bỏ trống. Nếu save có kết quả chưa rõ, server kiểm tra bản ghi file trước khi xóa; lỗi kiểm tra/xóa được ghi log để xử lý lại. Không xóa các object của phiên bản trước.

| HTTP | Mã lỗi / trường hợp |
| --- | --- |
| `400` | `FinalReport.InvalidRequest`, `FinalReport.InvalidFile`: form/tệp không hợp lệ, sai định dạng hoặc thiếu/thừa trường file |
| `401` | Thiếu/không hợp lệ Bearer token hoặc `FinalReport.AuthenticationRequired` |
| `403` | `FinalReport.AccessDenied`: sai role, ngoài nhóm hoặc thành viên chưa xác nhận |
| `404` | `FinalReport.GroupNotFound`, `FinalReport.BatchNotFound` |
| `409` | `FinalReport.MidtermNotApproved`, `FinalReport.InvalidGroupState`, `FinalReport.OutsideAllowedWindow`, `FinalReport.EvaluationStarted`, `FinalReport.ConcurrencyConflict` |
| `413` | `FinalReport.FileTooLarge`: file vượt 20 MiB hoặc request vượt 20 MiB + 64 KiB overhead, kể cả request chunked |
| `415` | Content-Type không phải `multipart/form-data` |
| `500` | Lỗi hạ tầng storage/database; response không chứa chi tiết nội bộ |

Khi báo cáo đã chuyển sang `UnderEvaluation`, nộp lại trả `409 FinalReport.EvaluationStarted` trước khi upload. Khi hai lần nộp cùng đọc một phiên bản, chỉ một lần được lưu; request còn lại trả `409 FinalReport.ConcurrencyConflict`, rollback file metadata/audit mới và xóa object của chính request đó. Nếu nhóm bị dừng hoặc báo cáo bắt đầu chấm trong lúc upload, request dùng dữ liệu cũ cũng nhận `409`. Việc kiểm tra concurrency dùng `xmin` của cả nhóm và báo cáo trong cùng transaction; client cần tải lại dữ liệu trước khi thử lại.

## Lịch sử và tải báo cáo cuối kỳ (SCRUM-102 CP5)

`GET /api/thesis/groups/{groupId}/final-report` trả `ApiResponse<FinalReportHistoryDto>` cho báo cáo loại `InitialFinal` của nhóm:

- `finalReportId`, `groupId`, `versionType`, `currentVersionNo`, `currentStatus`, `submittedAtUtc` (thời điểm nộp phiên bản hiện tại).
- `versions`: danh sách tăng dần theo `versionNo`; mỗi phần tử gồm `versionNo` và `file` với cùng metadata như response nộp báo cáo. `file.uploadedBy` và `file.uploadedAtUtc` ghi nhận người nộp/thời điểm của từng phiên bản. Response lịch sử không chứa object key hoặc URL tải.
- `currentStatus` là trạng thái hiện tại của toàn báo cáo, không phải trạng thái riêng của mỗi phiên bản file. Khi có nộp lại đồng thời với truy vấn, response không đưa vào file có số phiên bản mới hơn `currentVersionNo` đã đọc.

`GET /api/thesis/final-report-files/{fileId}/download` trả `ApiResponse<SubmissionFileDownloadDto>` gồm `fileId`, `downloadUrl`, `expiresAtUtc`. URL là presigned GET hiệu lực 5 phút cho đúng file được yêu cầu; bucket vẫn private, không có trường `objectKey` riêng trong response. Được tải cả bản hiện tại lẫn bản cũ. Endpoint không nhận file thuộc Syllabus/MidtermReport/ProgressReport; các endpoint tải loại hồ sơ khác cũng không dùng thay cho báo cáo cuối kỳ.

Cả hai endpoint chỉ cho phép Student đã Confirmed trong chính nhóm, GVHD hiện được phân công cho `TopicId` của nhóm, Faculty có quyền `thesis.review-gates.review`, hoặc Admin. Chỉ có role Faculty hoặc chỉ có permission nhưng không phải Faculty đều không đủ quyền; sinh viên đang chờ xác nhận, người ngoài nhóm và GVHD cũ không được xem/tải. Phân công GVHD được tra cứu lại mỗi request. Quyền đọc không phụ thuộc cửa sổ nộp: vẫn được xem sau deadline, khi đợt đóng, nhóm đã dừng hoặc báo cáo đã chuyển sang `UnderEvaluation`.

| HTTP | Mã lỗi / trường hợp |
| --- | --- |
| `400` | `FinalReport.InvalidRequest`: GUID rỗng; route không khớp định dạng GUID trả `404` |
| `401` | Thiếu/không hợp lệ Bearer token hoặc `FinalReport.AuthenticationRequired` |
| `403` | `FinalReport.AccessDenied`: không có quyền xem/tải trên nhóm |
| `404` | `FinalReport.GroupNotFound`, `FinalReport.ReportNotFound` (nhóm chưa nộp), `FinalReport.FileNotFound` (không có file, sai loại hoặc thiếu báo cáo/nhóm liên quan) |
| `500` | Lỗi hạ tầng; không trả chi tiết nội bộ |

Hệ thống kiểm tra quyền trước khi đọc lịch sử hoặc cấp URL tải; request bị từ chối không gọi storage để ký URL. Hai API đọc không thay đổi phiên bản, trạng thái nhóm/báo cáo hoặc audit nộp file.

## Bắt đầu chấm báo cáo cuối kỳ (SCRUM-102 CP6)

`POST /api/thesis/final-reports/{finalReportId}/begin-evaluation`, Bearer token, `Content-Type: application/json`:

```json
{
  "expectedVersionNo": 2
}
```

`expectedVersionNo` bắt buộc là số nguyên dương, lấy từ `currentVersionNo` của báo cáo GVHD vừa xem. Server không tự chọn phiên bản mới hơn khi sinh viên đã nộp lại; nếu số phiên bản hiện tại khác request, trả `409 FinalReport.ConcurrencyConflict` để GVHD tải lại lịch sử/file và xác nhận bản mới trước khi bắt đầu chấm.

Chỉ GVHD **đang** được phân công cho `TopicId` của nhóm hoặc Admin được gọi. Chỉ có role giảng viên, quyền thẩm định của Faculty, hay là thành viên nhóm đều không đủ quyền. Nhóm phải đang `FinalReportSubmitted`; báo cáo phải thuộc loại `InitialFinal`, đang `Submitted` và có metadata file cho phiên bản hiện tại. Thao tác không bị giới hạn bởi deadline/cửa sổ nộp hoặc trạng thái Open của đợt, nên có thể bắt đầu chấm sau khi đã hết hạn nộp.

Thành công trả `200 OK`, `ApiResponse<FinalReportEvaluationStartedDto>` với:

- `finalReportId`, `groupId`, `versionType: "InitialFinal"`, `versionNo`, `status: "UnderEvaluation"`.
- `startedBy`, `startedAtUtc`: tài khoản thực hiện và thời điểm bắt đầu chấm được lưu trong audit.
- `file`: metadata của đúng phiên bản đã khóa; không chứa object key hoặc URL tải.

Việc chuyển `Submitted → UnderEvaluation` và audit action `ThesisManagement.FinalReport.EvaluationStarted` được lưu cùng transaction. Audit ghi actor/thời điểm, nhóm, loại báo cáo, số phiên bản, file ID và checksum. Không tạo phiên bản/file mới, không sửa/xóa file cũ, không đổi trạng thái nhóm hoặc thời điểm nộp. `UnderEvaluation` khóa `CurrentVersionNo`; sinh viên không thể nộp lại. SCRUM-103 có thể dùng mã báo cáo, số phiên bản và file ID này để chấm đúng bản đã khóa; CP6 chưa thực hiện chấm điểm hoặc duyệt phản biện.

Server kiểm tra `xmin` của cả báo cáo và nhóm khi lưu. Nếu nộp lại thắng trước, bắt đầu chấm dùng dữ liệu cũ trả `409`; nếu bắt đầu chấm thắng trước, request nộp lại đang upload bị rollback và dọn object mới. Nếu nhóm bị dừng trong lúc xử lý, bắt đầu chấm cũng trả `409`. Hai request bắt đầu chấm đồng thời chỉ lưu một lần chuyển trạng thái và một audit; request còn lại trả `409`.

| HTTP | Mã lỗi / trường hợp |
| --- | --- |
| `400` | `FinalReport.InvalidRequest`: GUID rỗng, JSON sai/thiếu body hoặc expectedVersionNo không hợp lệ |
| `401` | Thiếu/không hợp lệ Bearer token hoặc `FinalReport.AuthenticationRequired` |
| `403` | `FinalReport.EvaluationAccessDenied`: không phải GVHD hiện tại/Admin |
| `404` | `FinalReport.ReportNotFound`, `FinalReport.GroupNotFound`, `FinalReport.FileNotFound` |
| `409` | `FinalReport.InvalidGroupState`, `FinalReport.InvalidEvaluationState` (sai loại hoặc đã bắt đầu chấm), `FinalReport.ConcurrencyConflict` |
| `415` | Content-Type không phải JSON |
| `500` | Lỗi hạ tầng; không trả chi tiết nội bộ |

Gọi lại sau khi đã bắt đầu chấm trả `409 FinalReport.InvalidEvaluationState`, không tạo audit trùng. Lỗi database trước commit rollback cả trạng thái và audit, nên có thể thử lại sau khi lỗi được xử lý. Nếu mất phản hồi sau commit, báo cáo vẫn bị khóa và audit vẫn giữ nguyên; client cần tải lại lịch sử để kiểm tra trạng thái trước khi thao tác tiếp.

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

Các nhóm mã lỗi nghiệp vụ hiện dùng gồm `ReviewGateConfiguration.*`, `ReviewGate.*`, `Syllabus.*`, `MidtermReport.*`, `ProgressMilestone.*`, `ProgressReport.*`, `FinalReport.*`, `TopicRegistration.*` và `SubmissionFile.*`. Mutation thành công được audit; các thao tác cạnh tranh không ghi đè âm thầm.

## Đăng ký đề tài khóa luận & Xác nhận ghép nhóm

### Tổng quan endpoint đăng ký đề tài

| Method | Route | Response | Quyền truy cập |
| --- | --- | --- | --- |
| `POST` | `/api/thesis/topic-registrations` | `TopicRegistrationResultDto` | `Student` (đã xác thực) |
| `POST` | `/api/thesis/topic-registrations/confirm` | `ConfirmRegistrationResultDto` | Public / Student (dùng confirmation token) |
| `GET` | `/api/thesis/topic-registrations/my-status` | `StudentRegistrationStatusDto` | `Student` (đã xác thực) |
| `PUT` | `/api/thesis/topic-registrations/advisor-assign` | `AdvisorTopicRegistrationResultDto` | `Lecturer` (chính chủ đề tài), `Admin` |
| `DELETE` | `/api/thesis/topic-registrations/advisor-assign/{topicId}` | `200 OK` (hoặc `Result`) | `Lecturer` (chính chủ đề tài), `Admin` |

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


### 5. GVHD Đăng ký / Gán nhóm sinh viên cho đề tài (Giai đoạn chuẩn bị)
`PUT /api/thesis/topic-registrations/advisor-assign`:
- **Chủ thể thực hiện:** Giảng viên hướng dẫn sở hữu đề tài hoặc `Admin`.
- **Mục đích:** Đăng ký / gán trực tiếp nhóm sinh viên theo chỉ tiêu của đề tài trong giai đoạn chuẩn bị đề tài (`TOPIC_PREPARATION`, tuần 0-1) trước khi mở cổng đăng ký đại trà cho sinh viên.
- **Request Body:**
```json
{
  "topicId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "studentCodes": ["24521680", "24521681"],
  "studentIds": ["9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d"]
}
```
- **Quy tắc nghiệp vụ:**
  - Trạng thái đề tài hợp lệ: `Draft`, `Submitted`, `NeedRevision`, hoặc `Approved` (trước khi chuyển sang `Published`).
  - Giai đoạn thực hiện: Đợt KLTN phải đang mở (`Open`) và trong thời gian giai đoạn chuẩn bị đề tài (`TOPIC_PREPARATION`, tuần 0-1).
  - Số lượng sinh viên: `1 <= count <= topic.StudentNumber`.
  - Không cho phép trùng lặp mã sinh viên/ID trong danh sách (`TopicRegistration.DuplicateStudentInGroup`).
  - Sinh viên không được ở trạng thái bị khóa quyền đăng ký vi phạm (`TopicRegistration.StudentRegistrationBlocked`).
  - Sinh viên chưa được gán vào bất kỳ đề tài nào khác trong cùng đợt (`TopicRegistration.StudentAlreadyRegisteredInBatch`).
  - Toàn bộ sinh viên được gán chuyển thẳng sang trạng thái `Confirmed` ngay lập tức (không cần token xác nhận hay chờ 24 giờ).
  - Tự động gửi email thông báo đề tài đã được gán đến từng sinh viên.
  - Sử dụng Pessimistic Lock (`SELECT ... FOR UPDATE` trên đề tài) và Database Transaction để ngăn chặn race condition.
- **Mã lỗi đặc trưng:**
  - `TopicRegistration.TopicNotFound` (404)
  - `TopicRegistration.OnlyAdvisorsAllowed` (403)
  - `TopicRegistration.NotTopicAdvisor` (403)
  - `TopicRegistration.TopicPreparationWindowClosed` (409)
  - `TopicRegistration.InvalidTopicStatusForAdvisorAssign` (409)
  - `TopicRegistration.EmptyStudentList` (400)
  - `TopicRegistration.DuplicateStudentInGroup` (400)
  - `TopicRegistration.GroupSizeExceedsTopicQuota` (400)
  - `TopicRegistration.StudentNotFound` (404)
  - `TopicRegistration.StudentRegistrationBlocked` (403)
  - `TopicRegistration.StudentAlreadyRegisteredInBatch` (409)

### 6. GVHD Hủy gán nhóm sinh viên khỏi đề tài
`DELETE /api/thesis/topic-registrations/advisor-assign/{topicId}`:
- **Chủ thể thực hiện:** Giảng viên hướng dẫn sở hữu đề tài hoặc `Admin`.
- **Mục đích:** Xóa nhóm sinh viên đã được gán trước đó trong giai đoạn `TOPIC_PREPARATION`, giải phóng trạng thái đăng ký của các sinh viên để có thể gán sang đề tài khác hoặc tham gia đăng ký sau này.
- **Mã lỗi đặc trưng:**
  - `TopicRegistration.TopicNotFound` (404)
  - `TopicRegistration.OnlyAdvisorsAllowed` (403)
  - `TopicRegistration.NotTopicAdvisor` (403)
  - `TopicRegistration.TopicPreparationWindowClosed` (409)
  - `TopicRegistration.InvalidTopicStatusForAdvisorAssign` (409)
  - `TopicRegistration.RegistrationNotFound` (404)


## Đơn xin Đổi tên Đề tài (Quy trình thẩm định 3 cấp trực tuyến)

Quy trình cho phép nhóm sinh viên làm đơn xin điều chỉnh tên đề tài trực tuyến qua 3 cấp thẩm định tuần tự: **Cấp 1 (GVHD) ➔ Cấp 2 (Văn phòng Khoa) ➔ Cấp 3 (Phòng Đào tạo)**.

### Bảng tóm tắt Endpoint Đổi tên Đề tài

| Method | Route | Chủ thể được phép | Mô tả |
| --- | --- | --- | --- |
| `POST` | `/api/thesis/groups/{groupId}/rename-requests` | Sinh viên thành viên nhóm (`Confirmed`) | Tạo đơn xin đổi tên đề tài trong Tuần 3 đến Tuần 10 |
| `GET` | `/api/thesis/groups/{groupId}/rename-requests` | Thành viên nhóm, GVHD, Văn phòng Khoa, PĐT, `Admin` | Xem toàn bộ lịch sử các đơn đổi tên đề tài của nhóm |
| `GET` | `/api/thesis/rename-requests` | GVHD, Văn phòng Khoa, PĐT, `Admin` | Tra cứu danh sách đơn đổi tên đề tài (hỗ trợ lọc theo `groupId`, `status`) |
| `POST` | `/api/thesis/rename-requests/{requestId}/advisor-review` | GVHD của đề tài, `Admin` | **Cấp 1**: GVHD thẩm định đơn đổi tên đề tài |
| `POST` | `/api/thesis/rename-requests/{requestId}/faculty-review` | Cán bộ Khoa (`Faculty`), `Admin` | **Cấp 2**: Văn phòng Khoa thẩm định đơn đổi tên đề tài |
| `POST` | `/api/thesis/rename-requests/{requestId}/academic-affairs-review` | Chuyên viên PĐT (`AcademicAffairs`), `Admin` | **Cấp 3**: Phòng Đào tạo phê duyệt chính thức đơn đổi tên đề tài |

---

### 1. Tạo đơn xin đổi tên đề tài (Sinh viên)
`POST /api/thesis/groups/{groupId}/rename-requests`

- **Chủ thể:** Sinh viên là thành viên đã được xác nhận (`Confirmed`) của nhóm sinh viên thực hiện đề tài.
- **Thời hạn quy định (Tuần 3 - Tuần 10):**
  - Đợt KLTN phải đang ở trạng thái `Open`.
  - Thời điểm tạo đơn phải nằm trong khoảng thời gian từ ngày bắt đầu của Giai đoạn thực hiện (`PROGRESS_EXECUTION`, Stage 3) đến hạn chót của Cổng giữa kỳ (`MIDTERM_GATE`, Stage 4) theo múi giờ đợt KLTN. Quá hạn hoặc ngoài giai đoạn này hệ thống khóa API và báo lỗi `400 Bad Request` (`RenameRequest.OutsideAllowedWindow`).
- **Ràng buộc trạng thái nhóm:** Nhóm sinh viên phải ở trạng thái hoạt động trong giai đoạn này (`InProgress` hoặc `MidtermSubmitted`).
- **Ràng buộc số lượng đơn:** Tại một thời điểm, mỗi nhóm chỉ được có tối đa 1 đơn đang trong tiến trình duyệt (`Created`, `AdvisorConfirmed`, `FacultyApproved`). Khi đơn cũ đã kết thúc (`Completed` hoặc bị từ chối), sinh viên có thể tạo đơn mới nếu vẫn còn trong thời hạn tuần 3-10.
- **Request Body:**
```json
{
  "newTitleVi": "Nghiên cứu ứng dụng Học máy trong chẩn đoán hình ảnh y khoa",
  "newTitleEn": "Research on Applying Machine Learning in Medical Imaging Diagnosis",
  "reason": "Điều chỉnh phạm vi nghiên cứu tập trung chuyên sâu vào phân khúc ảnh CT và MRI theo hướng dẫn của GVHD."
}
```
  - `newTitleVi` (string, bắt buộc, tối đa 300 ký tự): Tên tiếng Việt mới của đề tài.
  - `newTitleEn` (string, tùy chọn, tối đa 350 ký tự): Tên tiếng Anh mới của đề tài.
  - `reason` (string, bắt buộc): Lý do xin điều chỉnh tên đề tài.
  - Tên đề tài mới phải khác ít nhất một trong hai tên (tiếng Việt hoặc tiếng Anh) so với tên đề tài hiện tại (`RenameRequest.TitleUnchanged`).

---

### 2. Tra cứu lịch sử & Danh sách đơn
- `GET /api/thesis/groups/{groupId}/rename-requests`: Lấy danh sách toàn bộ các đơn của nhóm sinh viên (bao gồm các đơn bị từ chối và đơn đã hoàn tất), sắp xếp mới nhất lên đầu.
- `GET /api/thesis/rename-requests?groupId={groupId}&status={status}`: Hỗ trợ cán bộ quản lý và GVHD lọc danh sách đơn theo nhóm hoặc theo trạng thái đơn:
  - `Created`: Chờ GVHD thẩm định.
  - `AdvisorConfirmed`: GVHD đã đồng ý, chờ Văn phòng Khoa duyệt.
  - `FacultyApproved`: Khoa đã duyệt, chờ Phòng Đào tạo duyệt.
  - `Completed`: Đã được Phòng Đào tạo phê duyệt chính thức thành công.
  - `RejectedByAdvisor`: Bị GVHD từ chối.
  - `RejectedByFaculty`: Bị Văn phòng Khoa từ chối.
  - `RejectedByAA`: Bị Phòng Đào tạo từ chối.

---

### 3. Quy trình duyệt 3 cấp tuần tự & Xử lý sau khi PĐT duyệt

#### Cấp 1: GVHD thẩm định (`POST /api/thesis/rename-requests/{requestId}/advisor-review`)
- **Chủ thể:** Giảng viên hướng dẫn sở hữu đề tài (hoặc `Admin`).
- **Trạng thái đơn hợp lệ:** `Created`.
- **Request Body:**
```json
{
  "decision": "Approve",
  "rejectionReason": null
}
```
- **Xử lý:**
  - `Approve`: Đơn chuyển sang trạng thái `AdvisorConfirmed`, lưu thời điểm `AdvisorConfirmedAt`.
  - `Reject`: Bắt buộc nhập `rejectionReason`. Đơn chuyển sang `RejectedByAdvisor`, lưu lý do và thời điểm từ chối. Dừng luồng duyệt, giữ nguyên tên đề tài ban đầu.

#### Cấp 2: Văn phòng Khoa thẩm định (`POST /api/thesis/rename-requests/{requestId}/faculty-review`)
- **Chủ thể:** Cán bộ có vai trò `Faculty` hoặc quyền `thesis.review-gates.review` (hoặc `Admin`).
- **Trạng thái đơn hợp lệ:** `AdvisorConfirmed`.
- **Request Body:** Tương tự Cấp 1 (`decision`: `Approve` / `Reject`).
- **Xử lý:**
  - `Approve`: Đơn chuyển sang trạng thái `FacultyApproved`, lưu thời điểm `FacultyApprovedAt`.
  - `Reject`: Bắt buộc nhập `rejectionReason`. Đơn chuyển sang `RejectedByFaculty`, dừng luồng duyệt, giữ nguyên tên đề tài ban đầu.

#### Cấp 3: Phòng Đào tạo phê duyệt chính thức (`POST /api/thesis/rename-requests/{requestId}/academic-affairs-review`)
- **Chủ thể:** Chuyên viên có vai trò `AcademicAffairs` (hoặc `Admin`).
- **Trạng thái đơn hợp lệ:** `FacultyApproved`.
- **Xử lý khi từ chối (`Reject`):** Bắt buộc nhập `rejectionReason`. Đơn chuyển sang `RejectedByAA`, dừng luồng duyệt, giữ nguyên tên đề tài ban đầu.
- **Xử lý khi phê duyệt (`Approve`):**
  1. Đơn chuyển sang trạng thái `Completed`, lưu thời điểm `AaApprovedAt`.
  2. **Tự động cập nhật tên đề tài:** Cập nhật `TitleVi` và `TitleEn` mới vào bảng `topics.topic`.
  3. **Tự động mở cờ quyền nộp lại Đề cương:** Kích hoạt cờ `AllowSyllabusResubmission = true` trên thực thể `StudentGroup`. Sinh viên được phép nộp lại bản Đề cương cập nhật thông qua endpoint `POST /api/thesis/groups/{groupId}/syllabus/submissions`. Lần nộp này sẽ vượt qua kiểm tra hạn chót của Cổng 1, tạo phiên bản đề cương mới và tự động tiêu thụ cờ (`AllowSyllabusResubmission = false`) mà không làm thay đổi hay gián đoạn trạng thái `InProgress` của nhóm.
  4. **Ghi vết Audit Log hệ thống:** Gửi sự kiện tích hợp vào Audit Outbox qua `IThesisManagementAuditOutbox` với action `ThesisManagement.RenameRequest.Approved`, lưu thông tin `GroupId`, `TopicId`, `OldTitleVi`, `NewTitleVi`, `OldTitleEn`, `NewTitleEn`, `ApprovedBy` vào `MetadataJson`.

---

### Bảng Mã lỗi đặc trưng (Error Codes)

| Mã lỗi | HTTP Status | Mô tả |
| --- | --- | --- |
| `RenameRequest.OutsideAllowedWindow` | `400 Bad Request` | Chỉ được phép tạo đơn xin đổi tên đề tài trong giai đoạn Tuần 3 đến Tuần 10 của đợt khóa luận. |
| `RenameRequest.InvalidRequest` | `400 Bad Request` | Thông tin đơn không hợp lệ (tên tiếng Việt rỗng, lý do rỗng, độ dài vượt giới hạn...). |
| `RenameRequest.TitleUnchanged` | `400 Bad Request` | Tên đề tài mới phải khác với tên đề tài hiện tại. |
| `RenameRequest.RejectionReasonRequired` | `400 Bad Request` | Bắt buộc nhập lý do từ chối khi không đồng ý đơn đổi tên đề tài. |
| `RenameRequest.InvalidDecision` | `400 Bad Request` | Quyết định thẩm định chỉ chấp nhận `Approve` hoặc `Reject`. |
| `RenameRequest.AccessDenied` | `403 Forbidden` | Không có quyền thực hiện thao tác (không phải thành viên nhóm / không phải GVHD / không đúng vai trò). |
| `RenameRequest.GroupNotFound` | `404 Not Found` | Không tìm thấy nhóm sinh viên tương ứng. |
| `RenameRequest.TopicNotFound` | `404 Not Found` | Không tìm thấy đề tài tương ứng với nhóm. |
| `RenameRequest.RequestNotFound` | `404 Not Found` | Không tìm thấy đơn xin đổi tên đề tài. |
| `RenameRequest.ActiveRequestAlreadyExists` | `409 Conflict` | Nhóm đang có một đơn xin đổi tên đề tài chờ thẩm định. |
| `RenameRequest.InvalidGroupState` | `409 Conflict` | Trạng thái nhóm hiện tại không cho phép nộp đơn xin đổi tên đề tài. |
| `RenameRequest.InvalidReviewState` | `409 Conflict` | Đơn xin đổi tên đề tài không ở trạng thái hợp lệ để thực hiện xét duyệt này. |
| `RenameRequest.ConcurrencyConflict` | `409 Conflict` | Dữ liệu đơn đã bị sửa đổi bởi thao tác khác. Vui lòng thử lại. |
