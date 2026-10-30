# Advisor evaluation & reviewer-handoff API contract — SCRUM-103

Trạng thái: **Đã triển khai và nghiệm thu CP6 của SCRUM-103**. Hai endpoint ghi và endpoint đọc trong tài liệu này đã được map vào API; contract eligibility đã sẵn sàng để SCRUM-104 gọi ngay trước khi ghi phân công GVPB. Bằng chứng PostgreSQL/MinIO và cách chạy lại ở [AdvisorEvaluationApprovalVerification.md](AdvisorEvaluationApprovalVerification.md).

Phạm vi là GVHD hoặc Admin chấm báo cáo cuối kỳ đã bị khóa, sau đó quyết định có cho phép phân công GVPB hay không. Không bao gồm màn hình SCRUM-112, phân công GVPB SCRUM-104, chấm phản biện SCRUM-105, xét điều kiện bảo vệ, công bố/tổng hợp điểm, hay nghiệp vụ gia hạn, chuyển đợt và mở khóa.

## Quy ước chung

Tất cả endpoint yêu cầu Bearer token. Hai endpoint ghi yêu cầu thêm `Content-Type: application/json`; response thành công theo `ApiResponse<T>` và thời điểm là ISO 8601 UTC. Endpoint không trả object key nội bộ hoặc URL public của file.

Hai write endpoint yêu cầu header `Idempotency-Key` là UUID khác rỗng. Phạm vi một key là **endpoint + tài khoản thực hiện**.

- Gửi lại cùng key với body có nội dung tương đương sẽ trả lại HTTP status và response thành công đã lưu, không ghi thêm dữ liệu hay audit.
- Dùng lại key cho body khác trả `409 AdvisorEvaluation.IdempotencyKeyReused`.
- Server kiểm tra bản ghi idempotency trước kiểm tra khóa phiếu; vì vậy client vẫn nhận được kết quả của request đã commit nếu mất response rồi retry cùng key.
- Key mới không bỏ qua optimistic concurrency hoặc khóa. Client tạo key mới cho một ý định nghiệp vụ mới.

## Quyền và bảo mật

| Thao tác | GVHD hiện được phân công cho `TopicId` của nhóm | Admin | Student, Faculty chỉ có quyền xem báo cáo, GVHD cũ, người khác |
| --- | --- | --- | --- |
| Lưu/sửa phiếu | Có | Có | 403 |
| Quyết định chuyển phản biện | Có | Có | 403 |
| Đọc phiếu | Có | Có | 403 |
| Đọc lịch sử/tải báo cáo cuối kỳ SCRUM-102 | Theo contract SCRUM-102 | Theo contract SCRUM-102 | Không suy ra quyền xem phiếu |

Server tra cứu lại phân công GVHD tại từng request qua contract của TopicCatalog. Client không truyền `advisorId` để làm căn cứ cấp quyền. Admin được thao tác thay GVHD nhưng phải ghi actor thực tế trong audit. Sinh viên, kể cả thành viên `Confirmed` của nhóm, không được đọc điểm, nhận xét hoặc lý do quyết định trước công bố; SCRUM-103 không mở quyền công bố đó.

## Trạng thái và quy tắc nghiệp vụ

| Trạng thái báo cáo/nhóm | Phiếu GVHD | Có thể làm gì |
| --- | --- | --- |
| `InitialFinal` đang `Submitted`; nhóm `FinalReportSubmitted` | Chưa có | Phải gọi API bắt đầu chấm SCRUM-102 trước |
| `InitialFinal` đang `UnderEvaluation`; nhóm `FinalReportSubmitted` | Chưa có, đang nhập/sửa hoặc `allowsReviewerAssignment = false` | Lưu/sửa điểm, nhận xét; có thể quyết định chưa đồng ý hoặc đồng ý |
| `InitialFinal` đang `UnderEvaluation`; nhóm `FinalReportSubmitted` | `allowsReviewerAssignment = false`, có lý do | Chưa phân công GVPB; GVHD/Admin được sửa điểm/nhận xét và xét lại bằng quyết định mới |
| `InitialFinal` đang `UnderEvaluation`; nhóm `ReadyForReviewerAssignment` *(được thêm ở CP1)* | `allowsReviewerAssignment = true`, khóa | SCRUM-104 có thể kiểm tra điều kiện và phân công GVPB; SCRUM-103 không cho sửa hoặc thu hồi |

Quy tắc chung:

- Phiếu phải gắn với đúng `InitialFinal`, `CurrentVersionNo` và `SubmissionFile` mà SCRUM-102 đã khóa. Server không tự chọn phiên bản mới.
- `score` nhận số thập phân có giá trị gốc từ `0` đến `10`, bao gồm biên. Server làm tròn `AwayFromZero` đến hai chữ số thập phân sau khi kiểm tra khoảng giá trị. Ví dụ `8.345` thành `8.35`; `10.004` bị từ chối trước khi làm tròn.
- `comment` bắt buộc; sau khi trim phải dài từ 1 đến 2.000 ký tự Unicode.
- Quyết định chưa đồng ý bắt buộc có `rejectionReason`; sau khi trim phải dài từ 1 đến 1.000 ký tự Unicode. Quyết định đồng ý không nhận lý do từ chối.
- Không có ngưỡng điểm GVHD để được chuyển phản biện trong SCRUM-103. Ngưỡng điểm phản biện để xét bảo vệ là nghiệp vụ sau.
- Quyết định chưa đồng ý giữ nguyên bản báo cáo đã khóa, không dừng nhóm, không mở nộp lại và không tự gán điểm 0. Việc gia hạn, mở khóa hay chuyển đợt thuộc workflow khác.
- Quyết định đồng ý là quyết định cuối: khóa điểm, nhận xét và quyết định với cả GVHD lẫn Admin.

`evaluationVersion` là số nguyên dương opaque đại diện phiên bản concurrency của phiếu. Client chỉ lưu và gửi lại chính xác giá trị server trả; không được suy diễn hay tự tăng giá trị này.

## Endpoint

### `POST /api/thesis/advisor-evaluations`

Tạo phiếu lần đầu hoặc sửa phiếu chưa khóa. `POST` được dùng theo semantics rõ ràng dưới đây, không upsert ngầm định.

```http
POST /api/thesis/advisor-evaluations
Authorization: Bearer <token>
Idempotency-Key: <idempotency-uuid>
Content-Type: application/json
```

```json
{
  "finalReportId": "b4b098e4-6848-4d2f-8647-fa427c7eb0e5",
  "expectedReportVersionNo": 2,
  "expectedEvaluationVersion": null,
  "score": 8.345,
  "comment": "Báo cáo đạt yêu cầu; cần chỉnh sửa phần đánh giá thực nghiệm."
}
```

| Trường | Kiểu | Quy tắc |
| --- | --- | --- |
| `finalReportId` | GUID | Bắt buộc; thuộc `InitialFinal` đang `UnderEvaluation` |
| `expectedReportVersionNo` | integer | Bắt buộc, dương; bằng đúng `CurrentVersionNo` đã khóa |
| `expectedEvaluationVersion` | integer hoặc `null` | `null`/bỏ qua chỉ cho tạo phiếu đầu tiên; số nguyên dương chỉ cho cập nhật phiếu đã có và chưa khóa |
| `score` | decimal | Bắt buộc, nằm trong [0, 10] trước làm tròn |
| `comment` | string | Bắt buộc, 1–2.000 ký tự sau trim |

Điều kiện tạo: nhóm đang `FinalReportSubmitted`, báo cáo có file cho phiên bản đang khóa và chưa có phiếu. Nếu đã có phiếu, request tạo trả `409 AdvisorEvaluation.AlreadyExists`; client phải `GET` phiếu để lấy `evaluationVersion` rồi gửi request cập nhật.

Điều kiện cập nhật: phiếu tồn tại, `expectedEvaluationVersion` khớp, phiếu chưa khóa. Cập nhật thay đổi điểm/nhận xét, không tự đổi quyết định đã lưu. Nếu quyết định trước đó là chưa đồng ý, GVHD/Admin vẫn có thể sửa phiếu rồi gọi API duyệt lại với `allowsReviewerAssignment: true`.

Thành công trả `200 OK` với `ApiResponse<AdvisorEvaluationDto>` và phiên bản phiếu mới. Việc lưu phiếu, kiểm tra concurrency báo cáo/nhóm, bản ghi idempotency và audit được commit cùng giao dịch.

### `PUT /api/thesis/final-reports/{finalReportId}/approval`

Ghi quyết định chuyển sang phản biện. Endpoint không nhận điểm hoặc nhận xét; client phải lưu phiếu trước.

```http
PUT /api/thesis/final-reports/b4b098e4-6848-4d2f-8647-fa427c7eb0e5/approval
Authorization: Bearer <token>
Idempotency-Key: <idempotency-uuid>
Content-Type: application/json
```

Quyết định chưa đồng ý:

```json
{
  "expectedReportVersionNo": 2,
  "expectedEvaluationVersion": 3,
  "allowsReviewerAssignment": false,
  "rejectionReason": "Cần hoàn thiện thực nghiệm trước khi gửi phản biện."
}
```

Quyết định đồng ý:

```json
{
  "expectedReportVersionNo": 2,
  "expectedEvaluationVersion": 4,
  "allowsReviewerAssignment": true,
  "rejectionReason": null
}
```

| Trường | Kiểu | Quy tắc |
| --- | --- | --- |
| `expectedReportVersionNo` | integer | Bắt buộc, dương và khớp `CurrentVersionNo` của báo cáo đã khóa |
| `expectedEvaluationVersion` | integer | Bắt buộc, dương và khớp phiên bản phiếu vừa đọc |
| `allowsReviewerAssignment` | boolean | Bắt buộc |
| `rejectionReason` | string hoặc `null` | Bắt buộc và 1–1.000 ký tự sau trim khi giá trị quyết định là `false`; phải là `null` hoặc bỏ qua khi là `true` |

Khi `allowsReviewerAssignment = false`, server lưu lý do và giữ nhóm ở `FinalReportSubmitted`. Điểm/nhận xét và quyết định này có thể được xét lại bằng các request sau dùng `evaluationVersion` mới.

Khi `allowsReviewerAssignment = true`, server phải kiểm tra phiếu, báo cáo và nhóm vẫn hợp lệ, sau đó nguyên tử: khóa phiếu/quyết định, chuyển nhóm sang `ReadyForReviewerAssignment`, lưu idempotency và audit. Từ thời điểm đó, mọi write request mới dùng key khác trả `409 AdvisorEvaluation.Locked`; không có API thu hồi trong SCRUM-103.

Thành công trả `200 OK` với `ApiResponse<AdvisorEvaluationDto>`. Không có trường hợp PUT mới trả thành công mà chỉ một trong phiếu, trạng thái nhóm hoặc audit được lưu.

### `GET /api/thesis/groups/{groupId}/advisor-evaluation`

Đọc phiếu GVHD mới nhất của nhóm cho GVHD hiện được phân công hoặc Admin. Endpoint chỉ đọc, không sinh audit ghi dữ liệu và không cần `Idempotency-Key`.

Thành công trả `200 OK` với `ApiResponse<AdvisorEvaluationDto>`. Không có phiếu trả `404 AdvisorEvaluation.NotFound`.

### Luồng tích hợp FE

1. Sau khi SCRUM-102 khóa `InitialFinal`, FE gửi `POST /api/thesis/advisor-evaluations` để tạo phiếu; các lần sửa gửi lại POST với `expectedEvaluationVersion` vừa nhận.
2. Khi cần tải lại màn hình, mất response hoặc nhận `409`, FE gọi `GET /api/thesis/groups/{groupId}/advisor-evaluation`; dùng `evaluationVersion` và `reportVersionNo` của response làm `expectedEvaluationVersion`/`expectedReportVersionNo` cho request tiếp theo, cùng điểm, nhận xét và trạng thái khóa.
3. Nếu chưa đồng ý, FE gửi PUT với `allowsReviewerAssignment: false` và lý do. Response vẫn có `isLocked: false`; FE có thể sửa phiếu rồi GET/PUT xét lại với phiên bản mới.
4. Nếu đồng ý, FE gửi PUT với `allowsReviewerAssignment: true` và không có lý do. Response có `isLocked: true`; từ đó màn hình chỉ đọc, mọi POST/PUT mới với key khác nhận `409 AdvisorEvaluation.Locked`.

### `AdvisorEvaluationDto`

```json
{
  "id": "317af0da-5260-4f99-8173-5f8463c8897c",
  "groupId": "0be0cfe1-60f0-4b6b-a7cf-5ce5a5ff5d9a",
  "finalReportId": "b4b098e4-6848-4d2f-8647-fa427c7eb0e5",
  "reportVersionNo": 2,
  "submissionFileId": "3ac9c337-8e53-45ea-8e86-e21c396516eb",
  "submissionChecksumSha256": "a3b1...",
  "score": 8.35,
  "comment": "Báo cáo đạt yêu cầu; cần chỉnh sửa phần đánh giá thực nghiệm.",
  "allowsReviewerAssignment": false,
  "rejectionReason": "Cần hoàn thiện thực nghiệm trước khi gửi phản biện.",
  "isLocked": false,
  "evaluationVersion": 4,
  "evaluatedBy": "a45f0ebb-a416-4adc-8d40-8743385c820f",
  "evaluatedAtUtc": "2026-10-28T08:00:00Z",
  "decisionBy": "a45f0ebb-a416-4adc-8d40-8743385c820f",
  "decisionAtUtc": "2026-10-28T08:10:00Z"
}
```

`allowsReviewerAssignment` là `null` khi phiếu đã lưu nhưng chưa có quyết định. `rejectionReason`, `decisionBy` và `decisionAtUtc` là `null` trong trường hợp đó. `isLocked` chỉ là `true` khi quyết định đồng ý đã commit. `evaluatedBy`/`evaluatedAtUtc` phản ánh lần lưu phiếu gần nhất; `decisionBy`/`decisionAtUtc` phản ánh lần quyết định gần nhất. Các GUID actor là tài khoản thao tác thực tế, nên Admin thao tác thay vẫn hiển thị Admin.

## Lỗi

| HTTP | Mã lỗi | Trường hợp |
| --- | --- | --- |
| 400 | `AdvisorEvaluation.InvalidRequest` | GUID rỗng, body thiếu/sai JSON, version không dương hoặc `Idempotency-Key` thiếu/sai UUID |
| 400 | `AdvisorEvaluation.InvalidScore` | Điểm không phải số hữu hạn hoặc ngoài [0, 10] trước làm tròn |
| 400 | `AdvisorEvaluation.InvalidComment` | Nhận xét rỗng sau trim hoặc vượt 2.000 ký tự |
| 400 | `AdvisorEvaluation.InvalidDecision` | Lý do thiếu/sai điều kiện theo quyết định hoặc vượt 1.000 ký tự |
| 401 | `AdvisorEvaluation.AuthenticationRequired` | Chưa xác thực hoặc UserId không hợp lệ |
| 403 | `AdvisorEvaluation.AccessDenied` | Không phải GVHD hiện được phân công hoặc Admin; bao gồm Student, Faculty chỉ có quyền xem báo cáo và GVHD cũ |
| 404 | `FinalReport.ReportNotFound`, `FinalReport.GroupNotFound`, `FinalReport.FileNotFound` | Không có báo cáo, nhóm hoặc file của phiên bản khóa |
| 404 | `AdvisorEvaluation.NotFound` | GET không có phiếu của nhóm; cập nhật/duyệt phiếu chưa tồn tại |
| 409 | `FinalReport.InvalidEvaluationState`, `FinalReport.InvalidGroupState` | Báo cáo không phải InitialFinal UnderEvaluation hoặc nhóm không ở trạng thái hợp lệ |
| 409 | `FinalReport.ConcurrencyConflict`, `AdvisorEvaluation.VersionConflict` | Báo cáo/nhóm/phiếu vừa đổi hoặc version client gửi không khớp |
| 409 | `AdvisorEvaluation.AlreadyExists` | Tạo phiếu khi nhóm đã có phiếu |
| 409 | `AdvisorEvaluation.Locked` | Sửa/duyệt sau khi đã đồng ý chuyển phản biện |
| 409 | `AdvisorEvaluation.IdempotencyKeyReused` | Key đã dùng cho body khác trong cùng endpoint và actor |
| 409 | `AdvisorEvaluation.LegacyRecordNeedsResolution` | Phiếu cũ không có liên kết chắc chắn tới báo cáo/file đã khóa |
| 415 | — | Content-Type không phải `application/json` |
| 500 | — | Lỗi hạ tầng; response không chứa chi tiết nội bộ |

`AdvisorEvaluation.AlreadyExists` và `AdvisorEvaluation.NotFound` ở write endpoint không được dùng để che giấu lỗi stale version. Nếu phiếu đã được đọc bởi client rồi bị thay đổi, server ưu tiên trả xung đột version để client tải lại.

## Audit, concurrency và tính nguyên tử

Lưu phiếu thành công tạo một audit integration event DefenseCouncil. Mỗi quyết định thành công tạo hai audit events trong cùng transaction: một ở DefenseCouncil và một ở ThesisManagement để ghi nhận bàn giao trạng thái nhóm. Tên action đang dùng:

- `DefenseCouncil.AdvisorEvaluation.Saved`
- `DefenseCouncil.AdvisorEvaluation.Rejected`
- `DefenseCouncil.AdvisorEvaluation.Approved`
- `ThesisManagement.AdvisorEvaluation.ReviewerAssignmentRejected`
- `ThesisManagement.AdvisorEvaluation.ReviewerAssignmentApproved`

Metadata quyết định chứa group/report/file binding, phiên bản phiếu, điểm sau làm tròn, trạng thái quyết định, actor và thời điểm. Audit không ghi nội dung nhận xét hoặc lý do từ chối; chỉ ghi `HasRejectionReason` khi cần thể hiện có lý do.

CP2 phải dùng một transaction PostgreSQL chung để kiểm tra và commit thay đổi của phiếu DefenseCouncil, FinalReport/StudentGroup ThesisManagement, idempotency record và hai audit outbox liên quan. Nếu một phần lỗi, toàn bộ phải rollback. Không dùng hai lần `SaveChanges` độc lập để mô phỏng tính nguyên tử.

## Contract cho SCRUM-104

Phân công, hủy mềm và quyền tải đúng bản cho GVPB được mô tả ở [ReviewerAssignmentApiContract.md](ReviewerAssignmentApiContract.md). SCRUM-104 có POST/DELETE, bốn API đọc và quyền tải đúng bản được duyệt; phạm vi/kết quả nghiệm thu CP6 xem [ReviewerAssignmentVerification.md](ReviewerAssignmentVerification.md). GVPB không được cấp quyền xem phiếu/điểm GVHD; semantics eligibility của SCRUM-103 giữ nguyên.

Tiếp nhận phiếu GVPB tại [ReviewerEvaluationApiContract.md](ReviewerEvaluationApiContract.md) đã có POST nháp/chốt và GET riêng tư trong SCRUM-105 CP2; gate Hội đồng/feedback thuộc CP3. Gate vẫn cần approval GVHD của đúng snapshot; DTO góp ý sinh viên dự kiến chỉ có comment đã chốt theo membership, không nhận phiếu/điểm/kết luận GVHD. Việc khóa Submitted của phiếu GVPB không tạo API thu hồi quyết định SCRUM-103.

SCRUM-104 phải kiểm tra lại điều kiện ngay trước khi phân công GVPB; không dựa trên danh sách hoặc dữ liệu FE cache. Contract đọc dự kiến từ SCRUM-103 là:

```text
ReviewerAssignmentEligibilityContractDto? GetReviewerAssignmentEligibilityAsync(groupId)

AdvisorEvaluationId
GroupId
FinalReportId
ReportVersionNo
SubmissionFileId
SubmissionChecksumSha256
ApprovedAtUtc
```

Contract chỉ trả dữ liệu khi cùng lúc thỏa: phiếu `allowsReviewerAssignment = true` và khóa; nhóm `ReadyForReviewerAssignment`; `InitialFinal` còn `UnderEvaluation`; phiên bản/file tham chiếu còn đúng dữ liệu đã khóa. Không thỏa điều kiện thì trả `null`/không đủ điều kiện, không phải snapshot cũ. Contract nằm tại `DefenseCouncil.Contracts.AdvisorEvaluations.IReviewerAssignmentEligibilityContract`; SCRUM-104 tiếp tục chịu trách nhiệm kiểm tra GVPB không trùng GVHD hiện tại và quyền của người phân công.

## Dữ liệu legacy và migration

Schema hiện tại có `AdvisorEvaluation` theo `GroupId`, điểm, nhận xét, thời điểm và quyết định, nhưng không đủ bằng chứng liên kết tới `FinalReport`, phiên bản/file bị khóa hoặc actor. CP1 xử lý theo nguyên tắc bảo toàn:

- Không tự backfill `FinalReportId`, `ReportVersionNo`, `SubmissionFileId`, actor hoặc thời điểm duyệt từ dữ liệu suy đoán.
- Bản ghi thiếu một liên kết bắt buộc được coi là legacy, không thể đọc/sửa/duyệt bằng workflow SCRUM-103; endpoint trả `409 AdvisorEvaluation.LegacyRecordNeedsResolution` cho người có quyền.
- Chỉ migration dữ liệu có kiểm soát do người được ủy quyền thực hiện mới có thể liên kết record legacy với báo cáo đã khóa khi có bằng chứng rõ ràng; thao tác phải ghi audit riêng.
- Bản ghi mới luôn có đầy đủ liên kết, actor, trạng thái khóa, concurrency và dữ liệu idempotency cần thiết trước khi endpoint được mở.

## Tiêu chí nghiệm thu CP0

- [x] Chốt hai endpoint write theo Jira và một endpoint read bổ sung phục vụ reload FE.
- [x] Chốt quyền GVHD hiện tại/Admin, quyền riêng của phiếu và việc Student không xem được điểm/nhận xét.
- [x] Chốt state machine: lưu/sửa → chưa đồng ý có lý do và được xét lại → đồng ý khóa phiếu và chuyển trạng thái nhóm; không có thu hồi trong task.
- [x] Chốt thang điểm, cách làm tròn, giới hạn nhận xét/lý do và không đặt ngưỡng điểm GVHD không có trong yêu cầu.
- [x] Chốt optimistic concurrency, retry sau mất response bằng `Idempotency-Key`, hành vi replay và audit không trùng.
- [x] Chốt dữ liệu legacy không được tự gán liên kết và contract kiểm tra lại điều kiện cho SCRUM-104.
- [x] Liệt kê các điều kiện phải được giao dịch nguyên tử xuyên hai module để CP2 kiểm chứng.
