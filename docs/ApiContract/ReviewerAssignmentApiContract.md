# Reviewer assignment API contract — SCRUM-104

Trạng thái: **CP0–CP5 đã có nền tảng, POST/DELETE, bốn API đọc và quyền tải đúng file cho GVPB; phạm vi/kết quả nghiệm thu CP6 tại [ReviewerAssignmentVerification.md](ReviewerAssignmentVerification.md)**. Cập nhật 30/10/2026. Nền tảng CP1/CP2 ở mục 12/13; POST/DELETE ở mục 14/15; API đọc/tải ở mục 16. Nền bàn giao đã có ở [SCRUM-103](AdvisorEvaluationApprovalApiContract.md); báo cáo/file thuộc [SCRUM-102](ThesisManagementApiContract.md).

Phạm vi: phân công/hủy một GVPB cho mỗi nhóm; tra cứu phục vụ quản trị và nhiệm vụ của GVPB; cấp link tải đúng bản báo cáo đã duyệt. Không gồm UI SCRUM-113, nhập điểm/phiếu SCRUM-105, phản biện thứ hai/xử lý không đồng thuận, tổng hợp/công bố điểm, lưu chiểu, phân công hàng loạt, email/in-app hay API thay GVHD.

Phần tiếp nhận chấm GVPB tại [ReviewerEvaluationApiContract.md](ReviewerEvaluationApiContract.md) đã có POST nháp/chốt, GET phiếu riêng tư và gate/feedback trong SCRUM-105 CP2–CP3. Nháp GVPB cũng là một ReviewerEvaluation nên chặn hủy phân công. Writer dùng `LockForEvaluationAsync` trong coordinator đến commit; nhánh ghi SCRUM-105 kiểm tra Open riêng, không thêm guard Open vào quyền tải/đọc SCRUM-104. Không đổi DTO phân công hoặc tự chuyển trạng thái nhóm để biểu diễn gate Hội đồng.

## 1. Quy ước HTTP, authentication và phân trang

- Tất cả endpoint yêu cầu Bearer token; hai endpoint ghi nhận JSON `Content-Type: application/json` và header `Idempotency-Key` UUID khác rỗng.
- Thành công theo `ApiResponse<T>`: `success: true`, `data`, `error: null`. Lỗi nghiệp vụ theo `success: false`, `data: null`, `error: { code, message }`. Middleware authentication/authorization, route binding và content type có thể trả lỗi framework không có envelope; FE phải kiểm tra HTTP status trước.
- POST tạo trả **201 Created**; DELETE hủy mềm trả **200 OK**; GET trả 200. POST không dùng Location trỏ tới endpoint GET theo ID chưa có; FE lấy DTO trong response hoặc tải lại danh sách theo `groupId`.
- Thời điểm JSON là ISO 8601 UTC, tên field camelCase. ID là GUID; GUID rỗng không hợp lệ. `assignmentVersion` là số nguyên `uint` dương opaque của concurrency (`xmin`), không để FE tự tăng/suy diễn.
- Mọi danh sách trả `ApiResponse<PagedResult<T>>`: `items`, `totalCount`, `pageNumber`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`. Đếm tổng sau lọc và trước phân trang.
- `pageNumber` mặc định 1, chuẩn hóa tối thiểu 1; `pageSize` mặc định 20, chuẩn hóa trong 1–100 theo convention repository. Giá trị không parse được hoặc vượt kiểu integer trả 400. Trang vượt tổng trả `items: []` với metadata tổng đúng.
- `keyword` tùy chọn: trim, tối đa 200 ký tự, rỗng sau trim tương đương bỏ trống. Tìm chuỗi con không phân biệt hoa thường, có phân biệt dấu; `%`/`_` là ký tự thường. Không tự tìm theo mã nhóm/mã đề tài chưa có trong model.
- GUID query filter đúng định dạng nhưng không khớp dữ liệu trả trang rỗng. Tham số context bắt buộc như `groupId` của candidate phải tồn tại, nếu không trả 404. Query boolean chỉ nhận `true`/`false`, không phân biệt hoa thường. Filter sai kiểu hoặc lặp nhiều giá trị trả 400.

## 2. Quyền và bảo mật

Permission **đã khai báo và có seed trong migration CP1**: `thesis.reviewer-assignments.read` và `thesis.reviewer-assignments.manage`. Khi áp migration, seed mặc định cả hai cho Faculty, AcademicAffairs, Admin. CP3–CP5 thực thi quyền quản trị ở endpoint và application; nhiệm vụ cá nhân/tải theo assignment còn kiểm tra account Active và Lecturer grant hiện tại trong database:

- Admin theo bypass permission hiện có của Identity; không bypass điều kiện nghiệp vụ, giới hạn một active, concurrency hoặc chặn hủy khi có phiếu.
- Faculty/AcademicAffairs: ghi cần `.manage`; đọc quản trị cần `.read` **hoặc** `.manage`. Role cho biết đối tượng nghiệp vụ; có permission nhưng không thuộc ba role quản trị thì vẫn không được quản trị phân công.
- Lecturer: nhiệm vụ cá nhân và quyền tải theo assignment của chính mình, không cần permission quản trị. Có role Lecturer tự nó không cấp quyền tạo/hủy. Với tài khoản đa vai trò, xét từng quyền độc lập; người Faculty có thêm Lecturer vẫn chỉ là một tài khoản/actor.
- Phạm vi quản trị toàn hệ thống, chưa giới hạn Khoa/đợt được giao. Không cấp `identity.users.read` chỉ để chọn GVPB; endpoint candidate trả projection tối thiểu.

| Thao tác | Admin | Faculty/AcademicAffairs có permission tương ứng | Lecturer được phân công | Người khác, kể cả Student/GVHD nếu chỉ có trách nhiệm đó |
| --- | --- | --- | --- | --- |
| Tạo/hủy | Có | Có | Không | Không |
| Danh sách quản trị/lịch sử, eligibility, candidate | Có | Có | Không | Không |
| Nhiệm vụ của chính GVPB | Chỉ khi có Lecturer và là chủ assignment | Chỉ khi có Lecturer và là chủ assignment | Có | Không |
| Tải file nhờ phân công GVPB | Theo nhánh quyền độc lập hoặc assignment hợp lệ | Theo nhánh quyền độc lập hoặc assignment hợp lệ | Chỉ đúng file/version được giao | Không được cấp quyền mới |
| Đọc phiếu GVHD | Theo SCRUM-103 | Không được suy ra từ quyền phân công | Không được suy ra từ quyền phản biện | Theo SCRUM-103 |

Chưa xác thực trả 401. Có token nhưng không đủ role/policy trả 403 **trước khi tra cứu resource**, tránh tiết lộ nhóm/tài khoản/phân công. Quyền xem báo cáo SCRUM-102 không suy ra quyền quản trị phân công hoặc xem phiếu GVHD. Quyền quản trị phân công của AcademicAffairs cũng không tự mở toàn bộ lịch sử/file báo cáo.

## 3. Quy tắc và trạng thái nghiệp vụ

### 3.1. Tạo phân công

Tất cả điều kiện sau phải hợp lệ trong cùng giao dịch đến lúc commit:

1. Actor đủ quyền. Nhóm, topic, batch và GVHD hiện tại tồn tại; `batchId` lấy từ topic của nhóm, kiểm tra nhất quán với đợt của nhóm qua contract ThesisManagement.
2. Đợt **Open**. Không chặn thêm theo `FINAL_EVALUATION`, tuần 11–18 hoặc hạn nộp báo cáo. Không dùng `GetFinalReportSubmissionWindowAsync` làm guard phân công.
3. `IReviewerAssignmentEligibilityContract` trả binding hợp lệ: phiếu GVHD không legacy, đã đồng ý và khóa; nhóm `ReadyForReviewerAssignment`; bản `InitialFinal` còn `UnderEvaluation`, version/file/SHA-256 khớp bản được duyệt.
4. `expectedAdvisorEvaluationId`, `expectedReportVersionNo` khớp dữ liệu hiện tại. Không chọn một bản báo cáo mới thay cho ý định FE đã gửi.
5. Tài khoản `reviewerId` tồn tại, AccountStatus `Active`, có role `Lecturer`. Tài khoản đa vai trò có Lecturer được chọn. Chỉ có CouncilMember/Admin/Faculty hoặc tên xuất hiện trong reader không đủ. Không tự thêm điều kiện học vị, bộ môn hoặc giới hạn tải khi chưa có dữ liệu nghiệp vụ.
6. `reviewerId != advisorId` hiện tại do server tra qua TopicCatalog, kể cả Admin thực hiện. Thiếu context GVHD phải chặn, không coi là không trùng.
7. Nhóm chưa có assignment `IsActive = true`, kể cả active legacy. Database bảo đảm tối đa một active/nhóm. AssignmentOrder mới luôn là 1, không tăng vì lịch sử đã hủy và không nhận từ client.

Server tạo ID mới, lưu snapshot approval/report/version/file/checksum/GVHD lúc phân công; `AssignedBy` là actor thực tế, `AssignedAt` lấy từ clock server UTC. Không tự sửa phiếu GVHD, trạng thái báo cáo hoặc nhóm. POST không upsert; đổi GVPB là hủy bản ghi cũ rồi tạo bản ghi mới.

### 3.2. Hủy mềm

Chỉ hủy assignment active, đúng `expectedAssignmentVersion` và **chưa có bất kỳ ReviewerEvaluation nào**. Chỉ cần tồn tại phiếu là chặn, bất kể điểm, kết luận, trạng thái công bố hay cờ loại khỏi tính điểm; Admin không ngoại lệ. Không hard delete hoặc xóa phiếu để thay người.

Lý do sau trim phải từ 1 đến 1.000 Unicode scalar values (runes), nhất quán với domain CP1 và `char_length` PostgreSQL; một emoji ngoài BMP tính một ký tự. Lưu `CancelledBy`, `CancelledAt`, `CancellationReason`; giữ AssignedBy/AssignedAt và toàn bộ snapshot. Hủy không cần batch Open/eligibility/khung thời gian tạo còn hợp lệ và không đòi reviewer vẫn Active; nhờ đó Khoa có thể sửa phân công sai, kể cả khi nhóm đã đổi trạng thái hoặc reviewer bị khóa. Thao tác này không mở lại hoặc thay đổi trạng thái nhóm/báo cáo/workflow đã kết thúc.

Hủy xong không có active: có thể POST lại cùng hoặc khác giảng viên bằng **ID và idempotency key mới**, kiểm tra lại đầy đủ điều kiện tạo. Không bật lại dòng cũ. DELETE lần nữa bằng key mới trả 409 `ReviewerAssignment.AlreadyCancelled`; retry cùng key thành công trước đó trả kết quả cũ.

| Tình trạng | POST mới | DELETE | Quyền tải suy từ assignment |
| --- | --- | --- | --- |
| Chưa có active, Open và đủ eligibility | Tạo được | Không có ID để hủy | Chưa có |
| Active, chưa có phiếu | 409 AlreadyAssigned | Hủy được với version/lý do | Có nếu binding/chủ thể còn hợp lệ |
| Active, đã có phiếu | 409 AlreadyAssigned | 409 EvaluationExists | Theo binding/quyền hiện tại |
| Đã hủy, chưa có active khác | Tạo được nếu đủ điều kiện hiện tại | Key mới: 409 AlreadyCancelled | Không được cấp URL mới từ dòng cũ |
| Active nhưng điều kiện tạo về sau không còn hợp lệ | Chặn tạo; không tự thay người | Vẫn hủy được nếu chưa có phiếu | Kiểm tra quyền đọc riêng, không dùng batch Open làm hạn tải |

Nếu về sau đổi GVHD thành chính GVPB, không tự thay snapshot/lịch sử. Assignment không được dùng cấp quyền tải/chấm cho người đang là GVHD; quản trị nhìn thấy tình trạng bị chặn để xử lý. Workflow đổi GVHD về sau cần kiểm tra xung đột này; SCRUM-104 không tạo API thay GVHD.

## 4. Endpoint ghi

### 4.1. `POST /api/thesis/reviewer-assignments`

```http
POST /api/thesis/reviewer-assignments
Authorization: Bearer <token>
Idempotency-Key: <uuid>
Content-Type: application/json
```

```json
{
  "groupId": "157b5ced-47c4-43bf-8152-0280fdd36fe9",
  "reviewerId": "31f4c8e4-74bc-4300-a1a2-b943a4c49523",
  "expectedAdvisorEvaluationId": "a12c772d-d77c-443f-b651-503948178cbe",
  "expectedReportVersionNo": 2
}
```

Ba ID bắt buộc khác rỗng; version bắt buộc là integer dương. Không nhận trường điều khiển `advisorId`, `assignedBy`, `assignedAt`, `assignmentOrder`, `isActive`, score hoặc file/checksum từ client. Field không thuộc DTO không có tác dụng cấp quyền hoặc thay dữ liệu server; không map JSON trực tiếp vào entity.

Thành công `201` với `ApiResponse<ReviewerAssignmentDto>`. Chỉ trả 201 sau khi assignment, idempotency và audit cùng commit. Response cùng cấu trúc khi replay. DTO thành công tạo luôn `isLegacy: false`, `isActive: true` và binding đầy đủ.

### 4.2. `DELETE /api/thesis/reviewer-assignments/{id}`

Chốt JSON body trên DELETE, không truyền lý do qua URL/query. Route GUID khác rỗng; version integer uint dương; lý do bắt buộc theo mục 3.2.

```http
DELETE /api/thesis/reviewer-assignments/801ff559-0ed4-4155-bcb5-f79b7680d838
Authorization: Bearer <token>
Idempotency-Key: <uuid-moi>
Content-Type: application/json
```

```json
{
  "expectedAssignmentVersion": 42,
  "reason": "Điều chỉnh phân công theo kế hoạch của Khoa."
}
```

Thành công `200` với `ApiResponse<ReviewerAssignmentDto>`: `isActive: false`, metadata hủy có giá trị, `assignmentVersion` mới. Cơ chế hủy/kiểm tra phiếu/idempotency/audit cùng transaction. Nếu phiếu đã tồn tại, không thay đổi assignment, version hoặc audit.

FE dùng Axios hiện có:

```typescript
await httpClient.delete(`/api/thesis/reviewer-assignments/${assignment.id}`, {
  headers: { 'Idempotency-Key': crypto.randomUUID() },
  data: {
    expectedAssignmentVersion: assignment.assignmentVersion,
    reason,
  },
});
```

Đây là ví dụ ý định mới; khi retry do mất response phải **giữ lại key/body gốc**, không gọi randomUUID lần nữa. Client HTTP hiện tại là Axios, CORS cho mọi method/header và Caddy chuyển tiếp `/api/*`; source không có cấu hình loại body DELETE. CP4/CP6 kiểm chứng request body qua Kestrel và Caddy container dùng cấu hình/image production tại mục 15. Domain/TLS và frontend trên hạ tầng đang deploy chưa được nghiệm thu trong phiên local này; không tự đổi sang POST hủy mà không cập nhật contract.

### 4.3. Idempotency và thứ tự lỗi

- Scope key: `(actorId, operation, idempotencyKey)`, operation khác nhau cho Create và Cancel. Fingerprint Cancel bao gồm route ID, expected version, lý do đã trim; Create bao gồm bốn trường request. JSON field order/whitespace không làm khác ý định. Không lưu token hoặc raw HTTP request.
- Xác thực, role/policy, content type, parse body/header và validation hình thức trước. Sau đó tra key trong transaction. Có key khác fingerprint: 409 IdempotencyKeyReused. Có key cùng fingerprint: trả **status/DTO gốc**, không ghi audit hoặc kiểm tra lại business state khiến replay bị từ chối. Chỉ lưu kết quả thành công.
- POST đã commit rồi assignment bị hủy: retry cùng key vẫn trả DTO lúc tạo (`isActive: true` lúc đó), không tạo lại và không kích hoạt lại. Đây là receipt của request cũ; FE GET dữ liệu hiện tại trước ý định mới.
- Request mới Create: kiểm tra tồn tại context → eligibility/batch Open → expected binding → tài khoản GVPB → không trùng GVHD → không có active. Khi nhiều điều kiện cùng sai, ưu tiên thứ tự này; race ở cuối vẫn trả 409 phù hợp.
- Request mới Cancel: tồn tại assignment → đã hủy → expected version → có phiếu → hủy. Vì kiểm tra phiên bản trước phiếu, FE có version cũ nhận VersionConflict và tải lại trước khi kết luận có thể hủy.
- Key mới không bỏ qua điều kiện hoặc version. Hai request cùng key đua nhau chỉ được một write/audit; request còn lại replay kết quả sau commit hoặc trả ConcurrencyConflict để retry **cùng key**, không trả 500 do unique constraint nội bộ.

## 5. DTO và endpoint đọc

### 5.1. `ReviewerAssignmentDto` — ghi và quản trị

```json
{
  "success": true,
  "data": {
    "id": "801ff559-0ed4-4155-bcb5-f79b7680d838",
    "groupId": "157b5ced-47c4-43bf-8152-0280fdd36fe9",
    "topicId": "d0997898-051b-4932-ac60-554f78b2ebec",
    "batchId": "b9146ecb-b54a-4a4d-a583-7449923f59aa",
    "topicTitleVi": "Hệ thống hỗ trợ quản lý khóa luận",
    "topicTitleEn": null,
    "reviewerId": "31f4c8e4-74bc-4300-a1a2-b943a4c49523",
    "reviewerFullName": "Nguyễn Văn An",
    "advisorIdAtAssignment": "20e19d89-8c84-4ee0-9ebe-e3643a88b978",
    "advisorEvaluationId": "a12c772d-d77c-443f-b651-503948178cbe",
    "finalReportId": "49bb1c11-924b-4b2c-9069-4fadf638159c",
    "reportVersionNo": 2,
    "submissionFileId": "a854c35a-7f7e-47ea-b5ca-fd68a5427805",
    "submissionChecksumSha256": "a8f06e9387cb4d2ba8f06e9387cb4d2ba8f06e9387cb4d2ba8f06e9387cb4d2b",
    "assignmentOrder": 1,
    "isActive": true,
    "isLegacy": false,
    "assignmentVersion": 42,
    "assignedBy": "27336269-50f6-4781-aef7-cc256bc8f014",
    "assignedAtUtc": "2026-10-30T03:00:00Z",
    "cancelledBy": null,
    "cancelledAtUtc": null,
    "cancellationReason": null
  },
  "error": null
}
```

Response write là receipt của dữ liệu tại lần thao tác đó; không chứa `canCancel` hay trạng thái phiếu tính lại để làm receipt phụ thuộc dữ liệu tương lai. Mọi field trong ví dụ là thuộc tính DTO, không phải cam kết cột database đều cùng bảng. `reviewerFullName`, title là projection tại lúc response; GET tải lại tên/title hiện tại. Dùng ID, không dùng tên để cấp quyền hoặc phát hiện thay đổi.

Trừ `topicTitleEn`, metadata hủy và binding legacy được nêu dưới đây, field không null. Binding (`advisorIdAtAssignment`, `advisorEvaluationId`, `finalReportId`, `reportVersionNo`, `submissionFileId`, `submissionChecksumSha256`) nullable **chỉ để biểu diễn legacy**. Bản ghi mới bắt buộc đầy đủ; checksum 64 ký tự hex. Tên GVPB trong lịch sử có thể null nếu thiếu projection từ Identity; không bịa tên hoặc mất dòng lịch sử. `assignmentOrder` legacy giữ giá trị gốc, không đổi mọi dòng thành 1.

### 5.2. `GET /api/thesis/reviewer-assignments`

Quyền đọc quản trị. Trả `ApiResponse<PagedResult<ReviewerAssignmentListItemDto>>`; mỗi item gồm toàn bộ `ReviewerAssignmentDto` **và** `hasEvaluation: boolean`, `canCancel: boolean`. `canCancel` khi actor đủ quyền ghi, assignment active và chưa có phiếu; chỉ là gợi ý tại thời điểm GET, DELETE vẫn phải kiểm tra lại trong transaction. Người chỉ có read luôn nhận canCancel false. Legacy active chưa có phiếu vẫn được hủy theo cùng version/lý do, không tự backfill binding.

| Query | Quy tắc |
| --- | --- |
| `batchId`, `groupId`, `reviewerId` | GUID tùy chọn, có thể kết hợp; bỏ trống không lọc, không tự chọn đợt Open |
| `isActive` | Boolean tùy chọn; mặc định `true`; bỏ lọc để xem cả hai bằng cách dùng `includeHistory=true` |
| `includeHistory` | Boolean mặc định false; khi true và không truyền isActive, lấy cả active/đã hủy; khi truyền isActive rõ ràng thì giá trị isActive thắng |
| `keyword` | Tìm tên đề tài Việt/Anh hoặc tên/mã giảng viên phản biện; không tìm lý do hủy/nội dung phiếu |
| `pageNumber`, `pageSize` | Theo mục 1 |

Sắp xếp `assignedAtUtc` giảm dần, rồi `id` tăng dần. Đợt đóng/nhóm dừng/assignment legacy vẫn tra cứu được. GET không cấp URL tải và không nhận giả danh actor. Dùng `groupId` cùng `includeHistory=true` để reload sau 409 hoặc xem lịch sử; không có endpoint GET chi tiết theo ID riêng trong SCRUM-104.

### 5.3. `GET /api/thesis/reviewer-assignment-eligibility`

Quyền đọc quản trị. `batchId` GUID **bắt buộc**; `keyword`, `pageNumber`, `pageSize` theo mục 1; `includeAssigned` boolean mặc định false.

Chỉ trả các nhóm của đợt Open có context group/topic/batch/GVHD nhất quán và approval/binding hiện tại hợp lệ. Mặc định loại nhóm đã có active, kể cả active legacy. `includeAssigned=true` giữ các nhóm đã có active để quản trị biết tình trạng; nhóm thiếu context/approval/binding không xuất hiện. Đợt hợp lệ nhưng chưa tồn tại/đã đóng trả trang rỗng, không lỗi business state của POST.

Item `ReviewerAssignmentEligibilityItemDto` có:

| Field | Kiểu và nghĩa |
| --- | --- |
| `groupId`, `topicId`, `batchId` | GUID |
| `topicTitleVi`, `topicTitleEn` | string, string nullable; keyword chỉ tìm hai title |
| `advisorId`, `advisorFullName` | GUID hiện tại, string nullable |
| `advisorEvaluationId`, `finalReportId`, `submissionFileId` | GUID từ binding được duyệt |
| `reportVersionNo`, `submissionChecksumSha256`, `approvedAtUtc` | integer dương, SHA-256 64 hex, UTC |
| `hasActiveAssignment`, `activeAssignmentId` | boolean, GUID nullable; true thì ID không null |
| `canAssign` | true khi actor đủ quyền ghi và không có active; người chỉ có read nhận false; không bảo đảm một reviewer bất kỳ hợp lệ |

Sắp xếp `approvedAtUtc` tăng dần rồi `groupId` tăng dần; lọc đủ điều kiện trước count/page, không lấy một trang nhóm rồi mới loại ở memory. Không có điểm, nhận xét/lý do quyết định GVHD hoặc metadata file khác version.

### 5.4. `GET /api/thesis/reviewer-candidates`

Quyền đọc quản trị; `groupId` GUID **bắt buộc**; `keyword`, `pageNumber`, `pageSize` theo mục 1. Group không tồn tại: 404; thiếu context topic/GVHD: 409 ContextInvalid. Không đòi nhóm đủ eligibility để người quản trị chuẩn bị lựa chọn; POST vẫn kiểm tra đủ điều kiện.

Trả `ApiResponse<PagedResult<ReviewerCandidateDto>>` với `userId: GUID`, `fullName: string`, `userCode: string?`. Chỉ tài khoản Active có Lecturer và khác GVHD hiện tại; keyword tìm fullName/userCode. Không trả email, điện thoại, trạng thái bảo mật, toàn bộ roles hay số tải phân công chưa có quy tắc. Không loại người chỉ vì họ được phân công nhóm khác hoặc từng được phân công nhóm này rồi hủy. Sort fullName tăng dần rồi userId tăng dần; count sau lọc. ID hợp lệ trong danh sách không miễn kiểm tra lại khi POST.

### 5.5. `GET /api/thesis/me/reviewer-assignments`

Tài khoản hiện tại Active có Lecturer. Query `batchId` tùy chọn, `keyword` tìm tên đề tài Việt/Anh, `pageNumber`, `pageSize`; không nhận `reviewerId`, `includeHistory`, `isActive` để chuyển chủ thể hoặc xem lịch sử quản trị. Những filter bị cấm này trả 400 InvalidRequest. Server luôn lọc `ReviewerId = currentUser.UserId AND IsActive = true` trước count/page. Sort assignedAtUtc giảm dần, rồi id tăng dần.

Trả `ApiResponse<PagedResult<ReviewerAssignmentTaskDto>>`; item gồm:

- `id`, `groupId`, `topicId`, `batchId`, `topicTitleVi`, `topicTitleEn`, `assignedAtUtc`, `assignmentVersion`.
- `isLegacy`, `finalReportId`, `reportVersionNo`, `submissionFileId`; binding nullable chỉ khi legacy.
- `reportFile`: null hoặc `{ fileId, fileName, contentType, sizeBytes, versionNo }` của đúng file được giao; chỉ trả khi quyền tải hiện tại hợp lệ. Kiểu lần lượt là GUID, string, string, số nguyên byte và integer dương. Không có object key, URL tải sẵn hoặc lịch sử mọi phiên bản.
- `canDownloadReport: boolean`, `accessBlockCode: string?`. Khi true thì code null; khi false thì reportFile là null. Các mã cờ theo ưu tiên: `LegacyRecordNeedsResolution`, `ContextInvalid`, `AdvisorConflict`, `BindingInvalid`; đây không phải HTTP error code của cả trang.

Assignment active nhưng không còn dùng được vẫn xuất hiện với canDownloadReport false để GVPB liên hệ Khoa; bản đã hủy không xuất hiện. Tài khoản không Active/không còn Lecturer bị 403 ở cả endpoint, không chỉ gắn cờ từng item. Đợt đóng tự nó không chặn tra cứu nhiệm vụ/cấp link. Không trả lý do hủy, người hủy, audit, phiếu GVHD hoặc phiếu điểm GVPB; phần phiếu thuộc SCRUM-105.

### 5.6. Tải báo cáo: `GET /api/thesis/final-report-files/{fileId}/download`

Tái sử dụng endpoint SCRUM-102; giữ response `ApiResponse<SubmissionFileDownloadDto>` với `fileId`, `downloadUrl`, `expiresAtUtc` và TTL **5 phút**. Thêm nhánh cấp quyền suy từ phân công GVPB, không đổi nhánh quyền độc lập đã có.

Nhánh mới chỉ hợp lệ khi current user Active có Lecturer, sở hữu assignment active không legacy, không trùng GVHD hiện tại, approval vẫn khóa/đồng ý, và group/report/version/file/checksum hiện tại khớp snapshot. Chỉ fileId trong snapshot được tải; không mở quyền lịch sử theo cả GroupId. Không dùng batch Open hay FINAL_EVALUATION làm hạn đọc; nếu nhóm/report/binding không còn thỏa workflow hiện tại thì nhánh này bị chặn.

Không được tải qua nhánh GVPB: 403 `FinalReport.AccessDenied` (mã hiện có của `FinalReportSubmissionErrors.ReadAccessDenied`); file không tồn tại/sai loại giữ 404 theo SCRUM-102. `GET /api/thesis/groups/{groupId}/final-report` không mở thêm quyền xem toàn lịch sử cho GVPB. `GET /api/thesis/groups/{groupId}/advisor-evaluation` vẫn áp dụng quyền SCRUM-103.

Hủy ngăn cấp URL **mới** từ assignment đó. URL đã cấp vẫn có thể dùng đến hết TTL; không cam kết thu hồi presigned URL tức thì. Nếu người dùng còn quyền độc lập như Admin/GVHD hiện tại/Student của nhóm theo SCRUM-102, việc hủy assignment không thu hồi quyền độc lập đó. Không đổi sang streaming proxy trong task này.

## 6. Mã lỗi

Các code cho POST/DELETE và API đọc đã triển khai ở CP3–CP5. `FinalReport.AccessDenied` và lỗi tải khác giữ theo SCRUM-102; accessBlockCode của từng nhiệm vụ theo mục 5.5.

| HTTP | Code | Ý nghĩa |
| --- | --- | --- |
| 400 | `ReviewerAssignment.InvalidRequest` | ID rỗng, field/header key/version sai hoặc thiếu, query/body không hợp lệ |
| 400 | `ReviewerAssignment.InvalidCancellationReason` | Lý do rỗng sau trim hoặc dài quá 1.000 ký tự |
| 401 | `ReviewerAssignment.AuthenticationRequired` | Application được gọi không có actor xác thực; middleware có thể trả 401 framework |
| 403 | `ReviewerAssignment.AccessDenied` | Không đủ role/policy hoặc không đủ điều kiện dùng endpoint me |
| 404 | `ReviewerAssignment.GroupNotFound` | Nhóm context không tồn tại khi ghi hoặc tra candidate |
| 404 | `ReviewerAssignment.ReviewerNotFound` | Tài khoản được chọn không tồn tại khi POST |
| 404 | `ReviewerAssignment.NotFound` | Không có assignment route ID khi DELETE |
| 409 | `ReviewerAssignment.ContextInvalid` | Thiếu hoặc mâu thuẫn topic/batch/GVHD/đợt của nhóm; không suy đoán để tiếp tục |
| 409 | `ReviewerAssignment.NotEligible` | Chưa duyệt/phiếu legacy/nhóm chưa ready/report hoặc file binding không hợp lệ |
| 409 | `ReviewerAssignment.BatchNotOpen` | Tạo khi batch hiện tại không Open |
| 409 | `ReviewerAssignment.BindingConflict` | Expected approval ID hoặc version đã xem không khớp binding hiện tại |
| 409 | `ReviewerAssignment.ReviewerNotEligible` | Tài khoản tồn tại nhưng không Active hoặc không có Lecturer |
| 409 | `ReviewerAssignment.AdvisorConflict` | Reviewer trùng GVHD hiện tại |
| 409 | `ReviewerAssignment.AlreadyAssigned` | Nhóm đã có active; có thể do race khác key |
| 409 | `ReviewerAssignment.AlreadyCancelled` | DELETE bằng ý định mới trên dòng đã hủy |
| 409 | `ReviewerAssignment.VersionConflict` | expectedAssignmentVersion cũ |
| 409 | `ReviewerAssignment.EvaluationExists` | Không cho hủy vì đã có phiếu |
| 409 | `ReviewerAssignment.IdempotencyKeyReused` | Cùng actor/operation/key nhưng khác ý định |
| 409 | `ReviewerAssignment.ConcurrencyConflict` | Race dữ liệu, lock timeout, serialization/deadlock; server không tự chạy lại callback, client retry cùng key |
| 415 | — | Write Content-Type không phải application/json; không bắt buộc envelope |
| 500 | — | Lỗi hạ tầng không dự kiến; không trả SQL, connection string hoặc stack trace |

Role/policy kiểm tra trước resource và code 404/409. Đối với lỗi 409 khác nhau, FE đọc code và reload resource đúng phạm vi, không tự đổi expected version rồi gửi lại ý định cũ. Client không tự coi 500/network timeout là chưa commit: retry cùng key trước khi tạo ý định mới.

## 7. Dữ liệu legacy, migration và audit

DefenseCouncil sở hữu assignment, metadata hủy, snapshot, idempotency và audit. Bản ghi mới có binding đầy đủ; `isLegacy` chỉ đánh dấu dữ liệu cũ không chứng minh được liên kết báo cáo/approval. Không suy đoán rằng báo cáo mới nhất là bản đã được phân công trong quá khứ.

- Bỏ unique toàn lịch sử `(group_id, reviewer_id)` và `(group_id, assignment_order)` để hủy rồi tạo lại được. Thay bằng unique partial `group_id WHERE is_active = true` và index đọc lịch sử phù hợp; task GVPB thứ hai phải thay luật/index riêng.
- Migration preflight nhiều active/nhóm hoặc dữ liệu mâu thuẫn: dừng với chẩn đoán để xử lý có kiểm soát, không tự hủy/chọn bản mới nhất hoặc xóa phiếu.
- Binding cũ thiếu được để null và isLegacy true. Active legacy vẫn chiếm slot, không cấp quyền tải/chấm mới; quản trị được xem và hủy nếu chưa có phiếu, đúng version/lý do. Sau đó tạo bản mới theo điều kiện hiện tại. Legacy đã có phiếu cần quy trình xử lý dữ liệu riêng, không cho DELETE vượt chặn.
- Metadata hủy cũ chưa có bằng chứng được giữ null trên legacy đã inactive; không gán actor/thời gian hiện tại giả làm lịch sử. Chỉ thao tác hủy mới ghi đủ metadata thật.
- Đổi FK assignment → ReviewerEvaluation từ Cascade sang Restrict theo thiết kế bảo vệ phiếu; CP1 phải kiểm tra ảnh hưởng chuỗi cascade của nhóm trên PostgreSQL thật. Không thêm API xóa vật lý nhóm/assignment trong task.

Mỗi write mới thành công ghi đúng một action DefenseCouncil: `DefenseCouncil.ReviewerAssignment.Created` hoặc `DefenseCouncil.ReviewerAssignment.Cancelled`. Payload audit gồm assignment/group/reviewer, snapshot binding có sẵn, actor và UTC; actor Admin là chính Admin, không ghi GVHD thay thế. Cancel ghi `hasCancellationReason: true`, **không chép raw lý do vào audit outbox**. Lý do lưu trên assignment và chỉ quản trị đọc được qua DTO. Không đưa score/comment GVHD, URL tải hoặc token vào audit.

Không tạo audit ThesisManagement riêng vì SCRUM-104 không đổi trạng thái nhóm/report. Không ghi mutation/audit cho GET, request validation bị từ chối hoặc replay. Assignment/idempotency/outbox phải rollback cùng nhau khi audit/save lỗi.

## 8. Contract liên module và tính nguyên tử phải chứng minh ở CP2

| Owner | Boundary cần có hoặc tái sử dụng |
| --- | --- |
| DefenseCouncil | Tái sử dụng `IReviewerAssignmentEligibilityContract`; repository assignment/phiếu, idempotency, audit; contract active assignment + exact binding cho đọc file và SCRUM-105 |
| ThesisManagement | Context group/topic/batch, locked InitialFinal/binding và guard version/state tới commit; không trả điểm GVHD |
| TopicCatalog | GVHD hiện tại và guard chống đổi sau khi check nhưng trước assignment commit |
| AcademicPeriods | Batch status Open và guard khi ghi; không mượn deadline nộp bài |
| Identity | Projection candidate theo lô/phân trang, xác minh Active + Lecturer và bảo vệ điều kiện khi ghi; không dùng reader tên làm validation |
| Composition root | Transaction/coordinator cho participant cần thiết, thứ tự khóa cố định, rollback; module application không dùng DbContext của module khác hoặc migration context |

Điều kiện POST phải được giữ ổn định trong giao dịch đến commit. GET eligibility hoặc SELECT lại ngay trước SaveChanges trên các connection độc lập không đủ. CP2 đã chọn row locks trên một PostgreSQL transaction `ReadCommitted` dùng chung cho năm module, giữ architectural boundary; chi tiết triển khai và bằng chứng ở mục 13.

Nếu thay đổi xảy ra sau commit theo một thứ tự serial hợp lệ, assignment trước đó vẫn là một thao tác hợp lệ; không hứa đóng băng GVHD/account/batch mãi mãi. Read/download kiểm tra điều kiện hiện tại và fail closed khi cần. Group/report không bị update ngầm để mở lại eligibility.

DELETE phải serialize với writer ReviewerEvaluation trên cùng assignment. SCRUM-105 bắt buộc dùng primitive/contract xác minh active + binding trong cùng transaction ghi phiếu; không chỉ GET assignment rồi save phiếu ở transaction khác. CP2/CP4 chưa tạo endpoint chấm nhưng phải thử writer đại diện theo đúng primitive này: phiếu thắng → hủy 409; hủy thắng → writer không lưu phiếu. CAS/xmin chỉ trên assignment không tự bảo vệ được một lần insert phiếu độc lập không tuân guard.

CP2 kiểm tra ít nhất: hai Create khác key; hai Create cùng key; đổi GVHD thành reviewer; dừng nhóm/đổi report binding; đóng batch; đổi eligibility tài khoản; hủy đua ghi phiếu; lỗi sau insert assignment nhưng trước audit/idempotency hoàn tất. Chấp nhận kết quả tương đương một thứ tự serial; không chấp nhận hai active, assignment commit trên context bị thay trước đó hoặc phiếu mới trên assignment đã hủy. Error path không được commit một phần dữ liệu đã track.

## 9. Luồng tích hợp FE

1. Quản trị chọn batch; GET eligibility để lấy nhóm/expected binding và GET candidate theo groupId. Chưa cần quyền đọc toàn bộ người dùng hoặc phiếu GVHD.
2. Chọn reviewer và POST với key mới cho ý định tạo; giữ key/body cho retry khi timeout. Thành công lấy DTO, reload danh sách hiện tại thay vì suy diễn từ receipt cũ.
3. Khi đổi người: GET danh sách theo groupId (có thể includeHistory), lấy assignmentVersion mới nhất; nhập lý do; DELETE với JSON body/key mới. Sau 200, POST mới chỉ khi batch/binding hiện tại vẫn đủ điều kiện.
4. GVPB dùng GET me; chỉ khi canDownloadReport true mới gọi endpoint tải bằng submissionFileId được giao. Không dùng GET lịch sử hay GET phiếu GVHD thay cho API nhiệm vụ.
5. 409 AlreadyAssigned/BindingConflict/VersionConflict/ConcurrencyConflict: tải lại danh sách/eligibility; để người dùng xác nhận ý định mới, không ghi đè âm thầm. EvaluationExists hiển thị không thể hủy vì đã có phiếu, không đề xuất xóa phiếu.

## 10. Ma trận nghiệm thu CP1–CP6

Đây là **test bắt buộc khi triển khai**, chưa có bằng chứng pass ở CP0.

| ID | Tình huống | Kết quả cần đạt |
| --- | --- | --- |
| RA-01 | Faculty/PĐT đủ policy hoặc Admin tạo | 201, actor/thời gian server đúng, một active, binding bản khóa đúng |
| RA-02 | Student/GVHD/GVPB/CouncilMember không có vai trò quản trị; gán riêng permission cho Lecturer | 403 trước resource lookup; không mutate |
| RA-03 | Faculty/PĐT thiếu manage, chỉ có read; hoặc token thiếu | Write 403; thiếu token 401 |
| RA-04 | Batch Open, đủ eligibility nhưng ngoài FINAL_EVALUATION/quá hạn nộp bài | Tạo được; batch không Open thì 409 |
| RA-05 | Chưa có điểm/approval hoặc chưa đồng ý/legacy/nhóm không ready/binding sai | 409 NotEligible; không tự mở nhóm/report |
| RA-06 | Expected approval ID/version cũ | 409 BindingConflict; không tự chọn bản khác |
| RA-07 | Reviewer không tồn tại/không Active/thiếu Lecturer; Admin/CouncilMember đơn vai trò | 404 ReviewerNotFound hoặc 409 ReviewerNotEligible |
| RA-08 | Reviewer trùng GVHD hiện tại, kể cả Admin tạo | 409 AdvisorConflict; candidate không liệt kê người đó |
| RA-09 | Hai POST khác key đua nhau hoặc chọn cùng/khác người | Tối đa một active; request thua 409, không audit dư |
| RA-10 | Retry cùng key/body, đổi thứ tự JSON, mất response; key khác body | Replay status/DTO gốc và không ghi thêm; khác ý định 409 |
| RA-11 | Hủy thiếu lý do/version cũ; có bất kỳ phiếu | 400 lý do, 409 version hoặc EvaluationExists; dữ liệu nguyên |
| RA-12 | Hủy khi batch đóng/nhóm đổi trạng thái/reviewer không còn Active, chưa có phiếu | 200, ghi metadata thật, không đổi group/report; tạo lại vẫn phải đủ điều kiện |
| RA-13 | Hủy rồi phân công lại cùng người/thứ tự | ID mới, giữ lịch sử, một active; replay Create cũ không kích hoạt lại |
| RA-14 | Hủy đua writer phiếu | Phiếu thắng thì hủy bị chặn; hủy thắng thì không có phiếu mới trên dòng đó |
| RA-15 | GVHD/group/report/batch/reviewer eligibility đổi trong lúc tạo | Kết quả serial hợp lệ hoặc 409/retry có giới hạn, không commit dựa trên guard đã vô hiệu |
| RA-16 | Lỗi audit hoặc lỗi save ở participant sau | Rollback assignment, hủy, idempotency và audit; không có dữ liệu một phần |
| RA-17 | Lookup/page/filter/Unicode keyword/history/legacy | Count sau lọc, sort ổn định, page giới hạn; không mất lịch sử hoặc lộ PII |
| RA-18 | GVPB A đọc nhiệm vụ B hoặc thêm reviewerId query | Không giả danh; filter cấm 400; quyền quản trị độc lập không được ngầm dùng ở me |
| RA-19 | GVPB tải file đúng snapshot, file v1/khác nhóm, legacy, GVHD xung đột | Chỉ đúng file hợp lệ được cấp URL; nhánh sai bị 403; missing file 404 |
| RA-20 | Hủy và yêu cầu URL mới; URL đã cấp còn TTL | Không cấp URL mới qua assignment cũ; không cam kết thu hồi URL cũ trước 5 phút |
| RA-21 | GVPB gọi lịch sử báo cáo/phiếu GVHD; Student/GVHD/Admin dùng quyền cũ | Không mở quyền lịch sử/phiếu mới; regression quyền SCRUM-102/103 vẫn pass |
| RA-22 | Migration có nhiều active, legacy thiếu snapshot hoặc assignment có phiếu | Không tự chọn/xóa/backfill; preflight chặn an toàn, fresh và rollback/reapply được kiểm chứng |
| RA-23 | GET, replay hoặc write thất bại; Admin thao tác thay | Không audit mutation dư; actor write thành công là người thực tế; outbox không chứa lý do/phiếu/URL thô |
| RA-24 | DELETE JSON bằng HTTP client và qua gateway | Body reason/version đến backend nguyên vẹn; response 200/envelope đúng; không dùng query lý do |
| RA-25 | Toàn luồng v1/v2 → khóa v2 → GVHD duyệt → phân công → tải → hủy → tạo lại | Kestrel/composition production + PostgreSQL/MinIO thật, FK hoạt động; quyền và audit đúng |

## 11. Hoàn thành CP0

- [x] Hai write endpoint Jira và các read endpoint bổ sung có route, quyền, DTO/filter/page/sort cụ thể.
- [x] Chốt một active, Lecturer Active, chống trùng GVHD, thời gian batch Open + approval/binding; không dùng hạn nộp bài.
- [x] Chốt JSON DELETE, lý do, soft cancel, version, idempotency, xử lý receipt/retry và lỗi theo thứ tự.
- [x] Chốt snapshot/legacy, bảo vệ phiếu, audit actor thực tế và không đưa raw lý do vào outbox.
- [x] Chốt quyền tải đúng file và TTL; không mở lịch sử/phiếu GVHD; quyền độc lập SCRUM-102 được giữ.
- [x] Có điều kiện atomicity/race cho CP2, boundary writer SCRUM-105 và ma trận nghiệm thu tái hiện được.

Tại CP0 chỉ thêm/cập nhật tài liệu. Các ca nghiệm thu toàn luồng không được xem là đã pass chỉ vì contract đã chốt; phần đã thực hiện trong CP1 được ghi riêng dưới đây.

## 12. Nền tảng CP1 đã triển khai

- [ReviewerAssignment](../../backend/Modules/DefenseCouncil/Domain/Entities/ReviewerAssignment.cs) tạo snapshot đầy đủ, chặn reviewer trùng advisor snapshot, giữ AssignmentOrder 1 cho dòng mới; hủy có lý do, UTC/actor thật và giữ metadata tạo/binding. `TryCancel` kiểm tra cả cờ đã có phiếu do caller cung cấp và navigation đã load; kiểm tra phiếu dưới lock/transaction liên quan là phạm vi CP2, chưa có API hủy ở CP1.
- [Mapping](../../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/ReviewerAssignmentConfiguration.cs) có unique partial một active/nhóm, index lịch sử, check binding/cancellation và concurrency `xmin`. FK ReviewerEvaluation → assignment đổi sang Restrict; xóa nhóm cũng bị chặn nếu cascade sẽ xóa assignment có phiếu. Không thêm FK vật lý cho các snapshot mới.
- [ReviewerAssignmentIdempotencyRecord](../../backend/Modules/DefenseCouncil/Domain/Entities/ReviewerAssignmentIdempotencyRecord.cs) và mapping lưu receipt status 201/200, fingerprint, JSON object và unique theo actor/operation/key. Repository/coordinator hoàn tất receipt trước commit sẽ được bổ sung ở CP2; CP1 không tự tạo audit hoặc xử lý replay HTTP.
- [Migration 20261030022105](../../backend/Database.Migrations/Migrations/20261030022105_AddReviewerAssignmentWorkflowFoundation.cs) giữ dòng cũ là legacy, không backfill binding/actor hủy. Preflight khóa bảng và chặn nhiều active/nhóm hoặc phiếu orphan trước đổi schema. Dữ liệu cũ đã inactive giữ metadata hủy null; legacy chưa có phiếu có thể hủy bằng domain khi caller đủ guard.
- Downgrade chặn nếu lịch sử phân công lại không còn thỏa hai unique index cũ; không tự xóa lịch sử để rollback. Rollback/reapply của dữ liệu legacy tương thích được kiểm tra riêng. Permission/grant đã tùy chỉnh được giữ khi rollback theo convention repository; reapply không ghi đè tên hoặc thời điểm grant có sẵn.
- Test domain ở [ReviewerAssignmentTests](../../backend/tests/DefenseCouncil.Tests/ReviewerAssignmentTests.cs); PostgreSQL thật ở [ReviewerAssignmentWorkflowPersistenceIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentWorkflowPersistenceIntegrationTests.cs), với FK hiện có hoạt động. Snapshot GUID trong test persistence kiểm tra lưu trữ/constraint; xác minh approval/report tồn tại và đủ eligibility thuộc contract/giao dịch CP2.

CP1 chưa triển khai API đọc/ghi/tải mới, chưa áp migration vào database ứng dụng và chưa thay đổi workflow SCRUM-103.

Kết quả CP1 ngày 30/10/2026: DefenseCouncil **55/55** (31 ca mới), PostgreSQL persistence CP1 **10/10**, Architecture **74/74**, regression migration SCRUM-103 **2/2** và permission Identity **1/1** pass. Build Release solution có 0 error; còn hai warning ở OpenAPI (`CS8602`) và xung đột EF Core `10.0.4`/`10.0.12` của Architecture.Tests thuộc mã không đổi. Format solution `--verify-no-changes` và `git diff --check` pass. Đây là kết quả các suite mục tiêu tại CP1, không phải toàn bộ suite ThesisManagement/Identity hoặc nghiệm thu API CP3–CP6. Cách chạy lại phần CP1:

```powershell
dotnet build UGTM.sln --configuration Release --artifacts-path artifacts/scrum104-cp1
dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp1 --no-build --no-restore
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp1 --no-build --no-restore --filter FullyQualifiedName~ReviewerAssignmentWorkflowPersistenceIntegrationTests
```

## 13. Nền tảng CP2: contracts và giao dịch liên module

### Contracts và phạm vi trách nhiệm

- [IReviewerAssignmentWorkflowContract](../../backend/Modules/DefenseCouncil/Contracts/ReviewerAssignments/IReviewerAssignmentWorkflowContract.cs): `LockForCreationAsync` trả outcome/binding có guard; `GetActiveReportAccessAsync` kiểm tra đúng file theo assignment active, không trả điểm/nhận xét; `LockForEvaluationAsync` giữ khóa cho writer SCRUM-105 trong cùng transaction. Chưa nối contract đọc vào HTTP tải file ở CP2.
- [IReviewerAssignmentTransactionCoordinator](../../backend/Modules/DefenseCouncil/Contracts/ReviewerAssignments/IReviewerAssignmentTransactionCoordinator.cs): callback trả `Commit(value)` hoặc `Abort(value)` rõ ràng. Abort rollback và clear tracker dù không có exception; không dựa vào việc handler nhớ ném lỗi sau khi đã track dữ liệu.
- Reader thuộc owner: [ThesisManagement](../../backend/Modules/ThesisManagement/Contracts/ReviewerAssignments/IReviewerAssignmentThesisReader.cs) cho group/membership/report/file; [TopicCatalog](../../backend/Modules/TopicCatalog/Contracts/ReviewerAssignments/IReviewerAssignmentTopicReader.cs) cho GVHD/batch; [AcademicPeriods](../../backend/Modules/AcademicPeriods/Contracts/ReviewerAssignments/IReviewerAssignmentBatchReader.cs) cho trạng thái đợt; [Identity](../../backend/Modules/Identity/Contracts/ReviewerAssignments/IReviewerCandidateReader.cs) cho account và Lecturer grant. Candidate search đếm/lọc/sort/page tại database, escape ký tự `%`/`_`, chỉ trả ID/tên/mã.
- Repository assignment/idempotency được đăng ký cùng `IDefenseCouncilUnitOfWork` và audit outbox hiện có. [ReviewerAssignmentAudit](../../backend/Modules/DefenseCouncil/Application/ReviewerAssignments/ReviewerAssignmentAudit.cs) kiểm tra actor đúng dòng tạo/hủy, ghi action chuẩn và không chép raw lý do/phiếu vào outbox. HTTP authorization và so sánh expected fields vẫn thuộc CP3/CP4.

### Transaction và thứ tự khóa

[Coordinator production](../../backend/host/Api/ReviewerAssignments/ReviewerAssignmentTransactionCoordinator.cs) enlists DefenseCouncil, ThesisManagement, TopicCatalog, AcademicPeriods và Identity trên **một connection/transaction PostgreSQL ReadCommitted**. Chỉ save DefenseCouncil; bốn context còn lại giữ read locks và bị từ chối nếu callback track thay đổi để ghi schema khác. Coordinator hai-context của SCRUM-103 được giữ nguyên.

Thứ tự dùng khi tạo: khóa idempotency request → group (`FOR UPDATE`) → membership → report → file → topic → batch → advisor approval → account và Lecturer grant/role. Các dòng guard sau group dùng `FOR SHARE`, nên UPDATE/DELETE liên quan phải chờ transaction kết thúc. Group lock serialize việc kiểm tra slot active khi chưa có assignment để khóa. Dữ liệu được đọc mới dưới khóa, không dựa vào entity tracker hoặc cache của GET trước đó.

- Khóa idempotency là `pg_advisory_xact_lock` theo SHA-256 rút gọn của actor/operation/key; chỉ serialize việc tra receipt, không thay unique index/fingerprint. Hai request cùng key chờ nhau rồi đọc receipt đã commit. Hash collision chỉ làm chờ thêm; không dùng hash để nhận diện replay.
- `ExecuteWithPostSaveAsync` save lần đầu để nhận `xmin`, rồi hoàn tất JSON receipt và save lần hai **trước commit**. Lỗi save/audit/post-save rollback cả hai lần; receipt không phản ánh version chưa commit.
- Lock timeout, deadlock hoặc serialization error PostgreSQL được chuyển thành `DefenseCouncilConcurrencyException`, kể cả khi nằm trong DbUpdateException. Coordinator không tự chạy lại callback; HTTP handler ở CP3/CP4 phải map 409 theo contract và client retry cùng key khi phù hợp.
- Hủy khóa assignment bằng `IReviewerAssignmentRepository.LockByIdAsync`, sau đó kiểm tra phiếu trong cùng transaction. Chấm dùng `LockForEvaluationAsync` (guard group/report/GVHD/account trước, assignment lock sau) rồi insert phiếu trước commit. Ở ReadCommitted, bên chờ lấy được khóa sẽ đọc trạng thái đã commit của bên thắng; không dùng snapshot cũ để ghi.
- Các hàm Lock từ chối khi context không có transaction; caller phải dùng coordinator để các context cùng enlisted trên một transaction. Các hàm Get chỉ là read projection; không dùng `GetActiveReportAccessAsync` làm bằng chứng để lưu phiếu trong transaction khác. Hủy không cần batch còn Open; đọc/chấm theo binding không lấy hạn nộp bài hoặc trạng thái Open làm hạn đọc.

### Bằng chứng CP2

[ReviewerAssignmentTransactionIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentTransactionIntegrationTests.cs) dùng `AddUgtmModules` và source coordinator production, PostgreSQL Testcontainers với FK hoạt động. Fixture không start worker/email/storage; không tạo endpoint SCRUM-104 hoặc endpoint chấm giả. Writer ReviewerEvaluation đại diện cho SCRUM-105 thực hiện insert database thật qua primitive khóa đã công bố.

Lượt CP2 **24/24** test PostgreSQL pass ngày 30/10/2026: cùng backend PID giữa năm context; sáu loại thay đổi theo cả hai thứ tự khóa/commit (GVHD, batch, nhóm, file binding, account status, Lecturer grant); hai request khác key/cùng key; hai thứ tự hủy/chấm; explicit abort; lỗi DB sau save đầu; ownership/lock timeout; candidate và quyền đọc binding. Test concurrency dùng hai connection và quan sát `pg_blocking_pids`, không dùng việc chưa hoàn tất sau sleep làm bằng chứng khóa. `jsonb` chuẩn hóa field order/whitespace, nên receipt được so sánh theo dữ liệu JSON và status, không so chuỗi byte.

Kiểm tra source cuối: **14/14** ca bổ sung/hồi quy pass (10 persistence CP1, 2 transaction SCRUM-103, 1 toàn luồng SCRUM-103 với PostgreSQL/MinIO và chạy lại 1 ca ownership đã bổ sung assignment/receipt/audit vào tracker trước abort); DefenseCouncil **55/55**, Architecture **74/74** pass. Build Release solution 0 error; còn warning OpenAPI và xung đột phiên bản EF Core ở Architecture.Tests thuộc mã/package version không đổi. `dotnet format UGTM.sln --no-restore --verify-no-changes` và `git diff --check` pass. Không tuyên bố đã chạy toàn bộ suite ThesisManagement hoặc nghiệm thu API CP3–CP6.

Source CP2 không có migration mới và chưa triển khai HTTP tạo/hủy/đọc/tải. Artifact/TRX nằm trong `artifacts/scrum104-cp2/` bị Git ignore. Chạy lại từ root với Docker Desktop Linux engine:

```powershell
dotnet build UGTM.sln --configuration Release --artifacts-path artifacts/scrum104-cp2
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp2 --no-build --no-restore --filter FullyQualifiedName~ReviewerAssignmentTransactionIntegrationTests
```

## 14. CP3: POST tạo phân công

[ReviewerAssignmentEndpoints](../../backend/Modules/DefenseCouncil/Infrastructure/Endpoints/ReviewerAssignmentEndpoints.cs) đã map POST đúng mục 4.1 vào module production. Endpoint kiểm tra role và permission trước khi đọc request. [CreateReviewerAssignmentHandler](../../backend/Modules/DefenseCouncil/Application/ReviewerAssignments/CreateReviewerAssignmentHandler.cs) kiểm tra lại quyền, UUID/header/body, lấy actor từ `ICurrentUser` và thời điểm UTC từ `TimeProvider`. Header bắt buộc cả trong Development; không tự sinh key. Các trường ngoài request DTO không điều khiển entity. Thành công trả 201 cùng DTO, không đặt Location tới route chưa có.

Handler khóa key trước, replay receipt tương đương hoặc trả `IdempotencyKeyReused` trước khi kiểm tra nghiệp vụ. Request mới dùng overload có expected binding của guard CP2: context → eligibility/Open → expected evaluation/version → tài khoản Active Lecturer → khác GVHD hiện tại → chưa có active. Giữ nguyên cơ chế khóa tới commit, không thêm điều kiện giai đoạn/hạn nộp và không đổi trạng thái nhóm/báo cáo. Topic contract bổ sung title; tên GVPB lấy qua Identity contract. Các projection chỉ phục vụ DTO, không dùng tên/title để quyết định quyền.

Assignment, snapshot, receipt và audit cùng transaction; receipt chỉ hoàn tất sau khi database cấp `assignmentVersion`. Lỗi unique active trả `AlreadyAssigned`; lỗi concurrency/unique còn lại trả `ConcurrencyConflict`, không upsert hoặc tự retry callback. Replay trả DTO lúc tạo, kể cả sau khi phân công đã hủy; không kích hoạt lại bản ghi. Permission hiện tại vẫn phải hợp lệ khi replay.

[ReviewerAssignmentCreationHttpIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentCreationHttpIntegrationTests.cs) mở Kestrel khi cần, dùng production module registration/coordinator, PostgreSQL Testcontainers và FK thật của fixture CP2. Authentication trong test dùng claims kiểm soát được; không thay handler hoặc guard bằng mock. Bộ test gồm quyền Faculty/PĐT/Admin và handler trực tiếp; JSON/header/body không hợp lệ; đủ eligibility ngoài lịch sinh viên; chưa duyệt/legacy/binding lỗi/tài khoản không hợp lệ/trùng GVHD; request cùng/khác key; scope actor, chuẩn hóa JSON/GUID; replay sau hủy; mất response sau commit và rollback lỗi cả hai lần save rồi retry. Hủy trong test replay thực hiện qua domain/persistence để kiểm tra receipt, chưa phải nghiệm thu DELETE CP4. Các ca race có barrier và quan sát khóa ở CP2 vẫn chạy hồi quy.

Không có migration mới hoặc thay đổi database ứng dụng tại CP3. DELETE, danh sách/candidate/nhiệm vụ và quyền cấp link tải mới vẫn chờ checkpoint tiếp theo; chưa nghiệm thu toàn bộ SCRUM-104.

### Kết quả kiểm chứng CP3 ngày 30/10/2026

- **59 ca khác nhau đã có kết quả pass**: 35 ca CP3 (32 HTTP và 3 kiểm tra handler trực tiếp), 24 hồi quy transaction CP2. Lượt gộp có 58/59 pass; một ca lỗi ở SQL dựng constraint của fixture trước khi gọi API. Sau sửa fixture, chạy lại cả hai nhánh lỗi save và validation hình thức trên source cuối: **3/3 pass**, bao gồm từ chối version là chuỗi JSON. Đây là kết quả gộp theo ca của hai lượt, không phải một lượt 59/59 hoặc toàn bộ suite ThesisManagement.
- DefenseCouncil **55/55**, Architecture **74/74** pass. Build Release solution **0 error**; còn warning OpenAPI `CS8602` và xung đột phiên bản EF Core của Architecture.Tests ở mã/package không đổi. Format solution `--verify-no-changes` và `git diff --check` pass.
- Bằng chứng local bị Git ignore: `artifacts/scrum104-cp3/test-results/reviewer-cp3.trx`, `reviewer-cp3-final.trx`, `defense-cp3.trx`, `architecture-cp3.trx`; log build/format ở cùng thư mục CP3. Không stage/commit hoặc migrate database ứng dụng.

Chạy lại từ root với Docker Desktop Linux engine:

```powershell
dotnet build UGTM.sln --configuration Release --artifacts-path artifacts/scrum104-cp3
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp3 --no-build --no-restore --filter FullyQualifiedName~ReviewerAssignmentTransactionIntegrationTests --logger 'trx;LogFileName=reviewer-cp3.trx' --results-directory artifacts/scrum104-cp3/test-results
dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp3 --no-build --no-restore
dotnet test backend/tests/Architecture.Tests/UGTM.Architecture.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp3 --no-build --no-restore
dotnet format UGTM.sln --no-restore --verify-no-changes
```

## 15. CP4: DELETE hủy mềm và phân công lại

[CancelReviewerAssignmentHandler](../../backend/Modules/DefenseCouncil/Application/ReviewerAssignments/CancelReviewerAssignmentHandler.cs) được map vào `DELETE /api/thesis/reviewer-assignments/{id}` qua endpoint production. Endpoint và handler cùng kiểm tra quyền quản lý như POST. DELETE nhận JSON body, `expectedAssignmentVersion` là số nguyên JSON thuộc uint và khác 0; ID/key khác rỗng. Lý do được trim, kiểm tra giới hạn runes trước khi vào transaction. Không nhận người/thời điểm hủy hoặc trường điều khiển assignment từ client.

Thứ tự trong transaction: khóa actor/operation/key → replay hoặc kiểm tra fingerprint → khóa assignment bằng `FOR UPDATE` → tồn tại → còn active → expected version → tồn tại **bất kỳ** ReviewerEvaluation → hủy mềm và ghi audit/receipt. Writer chấm phải dùng primitive CP2 giữ cùng khóa parent đến commit; việc kiểm tra phiếu không chỉ dựa vào navigation. Operation Cancel tách khỏi Create, fingerprint chứa route ID, version và lý do đã trim. Receipt lưu DTO có xmin mới sau save đầu, chỉ trả 200 sau commit; retry không cập nhật thêm assignment/audit. Lỗi save đầu hoặc save receipt phải rollback cả metadata hủy và outbox.

Hủy không gọi guard tạo và không khóa group/batch/topic/account để kiểm tra eligibility. Context nhóm/topic và tên giảng viên chỉ là projection cho DTO; state, đợt, approval/report binding, tài khoản hoặc Lecturer grant thay đổi không tự cấm hủy. Shared DTO mapper hỗ trợ snapshot nullable của legacy mà không tạo binding giả; legacy giữ nguyên order. Nếu context nhóm/topic bị thiếu bất thường, trả ContextInvalid thay vì bịa metadata. Nhóm/báo cáo/phiếu GVHD giữ nguyên; thời điểm hủy lấy UTC server.

Sau hủy, POST hiện có tạo dòng mới cùng hoặc khác GVPB, vẫn kiểm tra đầy đủ eligibility/Open. Dòng cũ và receipt cũ giữ nguyên: replay POST cũ không bật lại assignment, replay DELETE cũ không hủy assignment mới. Key mới trên assignment đã hủy trả AlreadyCancelled; cùng key nhưng đổi route/version/lý do trả IdempotencyKeyReused. Admin vẫn bị chặn hủy khi có phiếu, kể cả phiếu 0 điểm, bị loại khỏi tính điểm hoặc chưa công bố.

Kiểm thử CP4 nằm ở [HTTP cancellation](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentCancellationHttpIntegrationTests.cs), [concurrency](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentCancellationConcurrencyIntegrationTests.cs) và [Caddy gateway](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentCancellationGatewayIntegrationTests.cs). Các ca race dùng PostgreSQL thật, các connection độc lập, barrier và `pg_blocking_pids`: writer chấm thắng thì DELETE trả EvaluationExists; DELETE thắng thì writer không được chèn phiếu. Tạo trong lúc hủy chưa commit thấy active cũ và bị từ chối; sau commit có thể tạo bằng key chưa bị tiêu thụ. Không dùng sleep như bằng chứng race.

Test gateway dùng đúng image từ `docker/docker-compose.prod.yml` và nội dung `docker/Caddyfile`, thay upstream sang Kestrel của test, cấu hình HTTP trên cổng ngẫu nhiên. Giữ matcher `/api/*`, reverse proxy, header và encoding của source; kiểm tra JSON reason có Unicode/xuống dòng cùng receipt replay. Phạm vi này kiểm chứng proxy trong container local; TLS/domain và hạ tầng deployment thực tế thuộc CP6. Không có migration mới hoặc thay đổi database ứng dụng tại CP4; API đọc và tích hợp cấp link tải mới chờ CP5.

### Kết quả kiểm chứng CP4 ngày 30/10/2026

- Lượt cuối **75/75 pass** trên source cuối: **40 ca CP4** (37 HTTP, gồm Caddy và race thực; 3 gọi handler trực tiếp) và **35 ca hồi quy CP3**. Kiểm chứng lưu/hủy/phân công lại, quyền và thứ tự lỗi, Unicode/legacy, idempotency/replay/mất response, rollback cả hai lần save, hủy đua chấm theo cả hai thứ tự khóa và hủy đua tạo. Đây là bộ mục tiêu POST/DELETE, không phải toàn bộ suite ThesisManagement hoặc nghiệm thu CP5–CP6.
- DefenseCouncil **55/55**, Architecture **74/74** pass. Build Release toàn solution tuần tự **0 error**; còn warning OpenAPI `CS8602` và xung đột EF Core của Architecture.Tests ở phần không đổi. Lượt build song song gặp MSBuild child node thoát đột ngột; build lại với `-m:1 -nr:false -p:UseSharedCompilation=false` thành công. Format solution `--verify-no-changes` và `git diff --check` pass.
- Artifact local bị Git ignore: `artifacts/scrum104-cp4/test-results/reviewer-cp4-final.trx`, `defense-cp4.trx`, `architecture-cp4.trx`; log build cuối `build-final-serial.log`, format `format-final.log`. Các lượt chẩn đoán fixture timestamp và readiness Caddy được giữ riêng; báo cáo cuối đã chạy lại đầy đủ 75 ca sau khi sửa. Không stage/commit hoặc migrate database ứng dụng.

Chạy lại từ root với Docker Desktop Linux engine:

```powershell
dotnet build UGTM.sln --configuration Release --artifacts-path artifacts/scrum104-cp4 -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp4 --no-build --no-restore --filter 'FullyQualifiedName~Cancellation_http|FullyQualifiedName~Cancellation_handler|FullyQualifiedName~Creation_http|FullyQualifiedName~Creation_handler' --logger 'trx;LogFileName=reviewer-cp4-final.trx' --results-directory artifacts/scrum104-cp4/test-results
dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp4 --no-build --no-restore
dotnet test backend/tests/Architecture.Tests/UGTM.Architecture.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp4 --no-build --no-restore
dotnet format UGTM.sln --no-restore --verify-no-changes
```

## 16. CP5: API đọc và quyền tải đúng báo cáo

[ReviewerAssignmentReadEndpoints](../../backend/Modules/DefenseCouncil/Infrastructure/Endpoints/ReviewerAssignmentReadEndpoints.cs) map đầy đủ bốn GET ở mục 5.2–5.5, trả `ApiResponse<PagedResult<T>>`. Endpoint quản trị dùng role + read/manage policy; [ReviewerAssignmentReadHandler](../../backend/Modules/DefenseCouncil/Application/ReviewerAssignments/ReviewerAssignmentReadHandler.cs) kiểm tra lại quyền trước context lookup. Endpoint me lấy ID từ tài khoản xác thực và kiểm tra account Active/Lecturer thực tế, không dựa riêng vào role claim còn hạn trong token. Parse query từ chối kiểu sai/lặp giá trị; me từ chối các selector giả danh/lịch sử đã nêu. Pagination chuẩn hóa và dùng phép tính long trước Skip để trang rất lớn vẫn trả trang rỗng có count đúng.

[ReviewerAssignmentReadRepository](../../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Repositories/ReviewerAssignmentReadRepository.cs) chỉ truy vấn schema DefenseCouncil của mình. Lookup context, tên/code, topic và batch qua contracts owner với các phép đọc theo lô. Admin keyword được chuyển thành tập group/user ID rồi lọc SQL trước count/page; page mới hydrate DTO/name và cờ phiếu. Eligibility giới hạn theo batch/topic, đọc cả tập context phù hợp theo lô, ghép approval hiện tại và active assignment rồi lọc/sort **trước** count/page. Không lấy trước một trang group rồi bỏ nhóm không hợp lệ, không dùng tên làm quyền hoặc miễn guard POST. Tên người không Active vẫn tìm được trong lịch sử; candidate chỉ trả Active Lecturer khác GVHD hiện tại.

Me lọc owner + active trong SQL trước count/page; lookup context/approval/file metadata cho page theo lô, giữ các dòng bị chặn với mã cờ đúng thứ tự. Không cấp URL trong danh sách, không trả phiếu/điểm, object key, metadata bản khác, lý do hủy hoặc lịch sử quản trị. `hasEvaluation/canCancel/canAssign/canDownloadReport` là projection tại thời điểm đọc; write/download vẫn xác minh lại. GET không gọi SaveChanges, không tạo audit hoặc thay xmin.

[GetFinalReportFileDownloadHandler](../../backend/Modules/ThesisManagement/Application/FinalReportSubmissions/GetFinalReportFileDownload/GetFinalReportFileDownloadHandler.cs) giữ quyền độc lập SCRUM-102, sau đó mới xét nhánh GVPB qua `IReviewerAssignmentWorkflowContract.GetActiveReportAccessAsync`. Server đối chiếu group/report/file/version/checksum một lần nữa trước khi gọi storage ký URL. Nhánh GVPB không mở GET lịch sử nhóm hoặc phiếu GVHD, không phụ thuộc batch Open/deadline; sai owner/legacy/role/account/binding hoặc đã hủy bị từ chối. TTL vẫn 5 phút; URL đã ký trước đó hết hiệu lực theo TTL, không hứa thu hồi tức thì. Composition production đăng ký contract đầy đủ; khi contract không được cung cấp trong host dùng module độc lập, nhánh GVPB từ chối quyền, các quyền độc lập cũ vẫn hoạt động.

Kiểm thử [read HTTP](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentReadHttpIntegrationTests.cs) và [report access HTTP](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentReportAccessHttpIntegrationTests.cs) dùng `AddUgtmModules`, Kestrel/PostgreSQL thật và FK hoạt động, không mock handler/reader nghiệp vụ. Storage kiểm soát được để kiểm tra điểm cấp URL, TTL và số lần gọi; đọc byte qua MinIO thật/toàn workflow được nghiệm thu riêng ở CP6. Test đếm SQL qua interceptor so sánh trang 1 dòng với 13 dòng để phát hiện N+1; fixture SCRUM-102/103 chỉ có hai module tiếp tục kiểm tra quyền độc lập, fixture production riêng kiểm tra nhánh GVPB.

Không có migration/permission seed mới, không thay guard giao dịch tạo/hủy hoặc schema owner. CP6 tiếp tục nghiệm thu luồng nộp/duyệt/phân công/tải/hủy/phân công lại với MinIO, gateway/deployment và đồng bộ tài liệu nghiệp vụ tổng thể.

### Kết quả kiểm chứng CP5 ngày 30/10/2026

- Lượt cuối **113/113 pass**: **38 ca CP5** (35 HTTP, 3 gọi handler trực tiếp) và **75 hồi quy CP3–CP4**, gồm DELETE qua Caddy sau khi chuyển readiness probe sang ping. Kiểm tra đủ role/read/manage, account/grant database dù token cũ, selector bị cấm, keyword literal/case, paging/count/overflow, eligibility trước page, legacy/cờ ưu tiên và quyền tải đúng file. Thử trang 1 và 13 dòng chứng minh số SQL không tăng theo số item cho cả quản trị và me; GET không ký URL/audit hoặc đổi xmin.
- **23/23** ca hồi quy đọc/tải SCRUM-102 pass: quyền độc lập Student/GVHD/Faculty/Admin, lịch sử/nhiều phiên bản, đổi GVHD, deadline/stopped/evaluation state và lỗi loại file/ID. Nhánh reviewer riêng được kiểm tra bằng production module registration, PostgreSQL/FK thật và storage kiểm soát TTL/số lần ký; MinIO toàn luồng vẫn thuộc CP6. Không tuyên bố đã chạy toàn bộ suite ThesisManagement.
- DefenseCouncil **55/55**, Architecture **74/74** pass. Build Release toàn solution tuần tự **0 error**; còn warning OpenAPI `CS8602` và xung đột EF Core Architecture.Tests ở phần không đổi. Format toàn solution `--verify-no-changes` và `git diff --check` pass.
- TRX/log local bị Git ignore ở `artifacts/scrum104-cp5/`: `test-results/reviewer-cp5-final.trx`, `final-report-read-regression.trx`, `defense-cp5.trx`, `architecture-cp5.trx`, `build-final.log`, `format-final.log`. Hai lỗi helper fixture nhiều nhóm đã sửa và toàn bộ 113 ca chạy lại thành công trên source cuối. Không stage/commit hoặc migrate database ứng dụng.

Chạy lại từ root với Docker Desktop Linux engine:

```powershell
dotnet build UGTM.sln --configuration Release --artifacts-path artifacts/scrum104-cp5 -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp5 --no-build --no-restore --filter 'FullyQualifiedName~Reviewer_reads|FullyQualifiedName~Creation_http|FullyQualifiedName~Creation_handler|FullyQualifiedName~Cancellation_http|FullyQualifiedName~Cancellation_handler' --logger 'trx;LogFileName=reviewer-cp5-final.trx' --results-directory artifacts/scrum104-cp5/test-results
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp5 --no-build --no-restore --filter 'FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.History_|FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.Reads_remain|FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.Read_endpoints_require|FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.Missing_groups_reports_files|FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.Download_rejects_files|FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.Advisor_reassignment_revokes|FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.Other_download_endpoints|FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests.A_file_without' --logger 'trx;LogFileName=final-report-read-regression.trx' --results-directory artifacts/scrum104-cp5/test-results
dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp5 --no-build --no-restore
dotnet test backend/tests/Architecture.Tests/UGTM.Architecture.Tests.csproj --configuration Release --artifacts-path artifacts/scrum104-cp5 --no-build --no-restore
dotnet format UGTM.sln --no-restore --verify-no-changes
```

## 17. CP6: nghiệm thu xuyên module và OpenAPI

[ReviewerAssignmentVerification.md](ReviewerAssignmentVerification.md) ghi nguồn test, lệnh chạy lại, kết quả cuối và giới hạn môi trường. Hai ca mới dùng production composition, PostgreSQL/FK và MinIO thật để đi từ nộp v1/v2, khóa/duyệt v2 đến phân công, tải byte thật, hủy, mất quyền cấp URL mới và phân công lại cùng/khác giảng viên. Writer test dùng contract khóa SCRUM-105 để lưu phiếu 0/ẩn/bị loại rồi xác nhận Admin không thể hủy; chưa có API chấm phản biện.

OpenAPI production bổ sung query của bốn GET (GUID, bool, keyword, paging và selector bắt buộc) cùng header UUID `Idempotency-Key` bắt buộc ở mọi môi trường cho POST/DELETE. Giữ parser runtime nghiêm ngặt và JSON DELETE; metadata không thay đổi luật nghiệp vụ. Test gọi document thật và xác nhận schema list phẳng, body/header/query. Không có migration mới ở CP6.

Requirements, UseCases, DataFlows, DataModel và DBML đã đồng bộ một active/nhóm, lịch sử/phân công lại, snapshot/legacy/xmin, receipt, FK Restrict và giao thức khóa chấm–hủy. DBML ghi predicate partial index trong note; không dùng DBML export thay migration. Các mục CP1–CP5 phía trên là bằng chứng lịch sử tại checkpoint tương ứng; kết quả/nghiệm thu hiện tại xem tài liệu CP6. Caddy được kiểm tra trong container local; domain/TLS/frontend đang deploy chưa được kiểm chứng trong phiên này.
