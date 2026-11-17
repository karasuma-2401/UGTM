# Reviewer evaluation, defense eligibility & student feedback — SCRUM-105

Trạng thái: **CP3 đã được người dùng review/commit tại `0e9dd57`; CP4 đã nghiệm thu source cuối, sẵn sàng review và người dùng tự commit**. Ngày 30/10/2026. Nền tảng CP1 ở mục 13, CP2 ở mục 14, triển khai/bằng chứng CP3 ở mục 15. Phạm vi và đối chiếu RV-01–RV-36 của CP4 ở [ReviewerEvaluationVerification.md](ReviewerEvaluationVerification.md).

Phạm vi: GVPB/Admin lưu nháp, sửa và chốt phiếu phản biện; Khoa/PĐT đọc để điều phối; sinh viên xem góp ý đã chốt; cung cấp điều kiện chuyên môn ra Hội đồng cho workflow kế tiếp. Hai endpoint Jira là POST reviewer-evaluations và GET group feedback; hai GET bổ sung phục vụ đọc phiếu/gate đã nằm trong plan được người dùng chấp thuận.

Không bao gồm UI SCRUM-114, phản biện thứ hai/xử lý không đồng thuận, sửa báo cáo sau phản biện, mở khóa/thu hồi phiếu, xếp lịch Hội đồng SCRUM-107, tổng hợp/công bố điểm SCRUM-110, lưu chiểu SCRUM-106 hoặc thông báo. Không tự triển khai production.

Nền bàn giao: [báo cáo và file SCRUM-102](ThesisManagementApiContract.md), [duyệt GVHD SCRUM-103](AdvisorEvaluationApprovalApiContract.md), [phân công và quyền tải SCRUM-104](ReviewerAssignmentApiContract.md). Các API đó giữ semantics hiện tại; contract này không mở thêm quyền xem phiếu GVHD.

## 1. Quy ước HTTP và phân quyền

- Bearer token; JSON camelCase, enum trả string canonical, thời gian ISO 8601 UTC. GUID phải hợp lệ và khác GUID rỗng. Version là JSON integer `uint` > 0, không phải số có dấu phẩy hoặc numeric string.
- Dùng `ApiResponse<T>` hiện có: `{ "success": true, "data": ..., "error": null }`. Lỗi nghiệp vụ có `success=false`, `data=null`, `error.code/message` và `errors` nếu có. Authentication/authorization middleware và 413/415 có thể trả body rỗng; client dựa vào HTTP status trước khi đọc envelope.
- POST cần `Content-Type: application/json`, một header `Idempotency-Key` UUID không rỗng ở **mọi môi trường**, kể cả Development. GET không cần key. Không có pagination hoặc query selector trên bốn endpoint này; query không được định nghĩa bị 400, gồm reviewerId/userId/includeDraft/includeScores trên feedback.
- Request field name và enum input không phân biệt hoa thường; enum trim và chuẩn hóa về tên canonical. Không nhận enum integer, enum lạ, field lạ hoặc field JSON lặp sau khi quy về cùng tên không phân biệt hoa thường. Không dùng global JSON setting để nới numeric parsing hoặc tự bỏ field null của feedback.

| Chủ thể | POST phiếu | GET phiếu đầy đủ | GET gate | GET feedback sinh viên |
| --- | --- | --- | --- | --- |
| GVPB của assignment, tài khoản Active có Lecturer hiện tại | Có, nếu các guard ghi hợp lệ | Phiếu của mình | Group của mình | Chỉ khi đồng thời có Student + membership Confirmed của group |
| Admin | Có, ghi actor Admin thật | Có | Có | Không có override; chỉ theo nhánh Student như trên |
| Faculty / AcademicAffairs | Không | Có khi có permission đọc riêng | Có khi có permission đọc riêng | Chỉ theo nhánh Student nếu thực sự thỏa điều kiện |
| GVHD / Lecturer khác / CouncilMember | Không tự có quyền | Không tự có quyền | Không tự có quyền | Chỉ theo nhánh Student nếu thỏa điều kiện |
| Student đã Confirmed trong group | Không theo vai trò Student | Không theo vai trò Student | Không theo vai trò Student | Có, chỉ group của mình |
| Student Pending, người ngoài group, anonymous | Không | Không | Không | 403; anonymous 401 |

Permission CP1 đã bổ sung/seed: `thesis.reviewer-evaluations.read` cho Faculty, AcademicAffairs, Admin. Role **và** permission cần đồng thời đúng với nhánh quản trị; Lecturer/Student có riêng permission này không được đọc phiếu toàn hệ thống. Admin bypass permission đọc, không bypass guard nghiệp vụ hoặc khóa Submitted. Không thêm quyền ghi cho Faculty/PĐT, không tái sử dụng `thesis.reviewer-assignments.manage` để sửa điểm. Tài khoản đa vai trò được xét từng nhánh quyền rõ ràng; không suy luận quyền từ tên hoặc actorId client gửi.

GVPB được xác minh account Active/Lecturer trong DB, không chỉ dựa vào claim cũ. Admin không cần Lecturer nhưng reviewer của assignment vẫn phải hợp lệ khi ghi. Quyền tải file tiếp tục theo SCRUM-104; đọc phiếu không tự cấp quyền xem lịch sử file, phiếu GVHD hoặc tải một file khác snapshot.

## 2. Lifecycle và quy tắc nghiệp vụ

### 2.1. Nháp và chốt

| Trạng thái phiếu | SaveDraft | Submit | Công khai comment | Gate chuyên môn | DELETE phân công |
| --- | --- | --- | --- | --- | --- |
| Chưa có | Tạo Draft | Tạo Submitted trực tiếp nếu đủ nội dung | Không có góp ý trước Submit | Không đủ điều kiện | Theo SCRUM-104, có thể hủy nếu các điều kiện hủy khác đúng |
| Draft | Sửa cùng ID, tăng evaluationVersion | Chốt cùng ID, tăng evaluationVersion | Chưa hiện | Không đủ điều kiện | Chặn vì đã có phiếu |
| Submitted / Approved | 409 Locked | 409 Locked với ý định mới | Hiện comment | Xét thêm ngưỡng/binding/cấu hình | Chặn |
| Submitted / Rejected | 409 Locked | 409 Locked với ý định mới | Hiện comment | Không đủ điều kiện | Chặn |
| Legacy | Không sửa qua workflow mới | Không tự chuyển thành Submitted | Không tự công khai | Không tự đủ điều kiện | Vẫn chặn nếu có phiếu |

Retry đúng key/ý định thành công cũ trả receipt gốc, không thực hiện lại transition ở bảng trên. Một phiếu trên một assignment; không tạo row mới cho mỗi lần sửa, không xóa phiếu để thay người. Không có thao tác xóa nháp trong SCRUM-105: nháp đã tồn tại tiếp tục chặn hủy phân công như người dùng đã chốt.

### 2.2. Điều kiện ghi

- Assignment tồn tại, active, nonlegacy; group đang `ReadyForReviewerAssignment`, đúng topic/batch, report `InitialFinal` đang `UnderEvaluation`, phiếu GVHD đã đồng ý/khóa và đúng report/version/file/checksum của snapshot assignment.
- Reviewer của assignment phải Active, có Lecturer và khác GVHD **hiện tại**; kể cả Admin ghi thay vẫn không bỏ qua các điều kiện này. Actor ghi là chính GVPB hoặc Admin đã xác thực; client không chọn reviewer/group/advisor khác bằng body.
- Batch phải Open. Không kiểm tra FINAL_EVALUATION, tuần mặc định, hạn nộp bài sinh viên hoặc hạn nộp báo cáo như deadline chấm.
- Submitted khóa điểm, comment và kết luận đối với tất cả actor, gồm Admin. Điểm dưới sàn hoặc Rejected không tự mở lại phiếu, tự Stopped, mở nộp lại, gán 0 hoặc phân công reviewer thứ hai.
- Việc lưu/chốt không yêu cầu cấu hình gate đã hợp lệ. Cấu hình thiếu/mâu thuẫn chỉ chặn gate, không chặn ghi phiếu đủ điều kiện.

### 2.3. Nội dung và chuẩn hóa

- Score nhận JSON number biểu diễn được bằng .NET decimal. Kiểm tra khoảng **0–10 trước làm tròn**; `10.004` bị từ chối dù có thể làm tròn thành 10. Chuẩn hóa `decimal.Round(score, 2, AwayFromZero)` như SCRUM-103. `8.345 → 8.35`; `0` là điểm thật, không đại diện cho nháp chưa chấm. Không bắt buộc hiện đủ hai số 0 cuối trong JSON.
- Comment là góp ý sẽ công khai, không phải ghi chú nội bộ. Trim theo .NET Unicode whitespace; đếm Unicode rune, tối đa 2.000; nháp có thể null/rỗng và rỗng sau trim được chuẩn hóa null. Submit bắt buộc 1–2.000 rune. Không cắt ngắn, tự sửa hoặc tự phân loại nội dung comment.
- DefenseApproval chỉ có Approved hoặc Rejected, nullable ở nháp; Submit phải có một trong hai. Không dùng giá trị enum mặc định Approved để điền khi client bỏ trống.
- SaveDraft và Submit đều gửi **toàn bộ nội dung dự định lưu**, không phải PATCH. Trường nội dung bị bỏ hoặc null được hiểu là null; sửa nháp chỉ gửi comment có thể xóa score/decision nháp trước. Client phải giữ và gửi đủ form khi muốn giữ các giá trị cũ.
- Client không được ghi status, actor/time, binding, IsLegacy, IsVisibleToStudent, RevisionFeedback, IsIgnoredInCalculation hoặc ExclusionReason. Trường không thuộc request bị 400; không âm thầm nhận metadata giả.

## 3. POST `/api/thesis/reviewer-evaluations`

### 3.1. Request

| Field | Kiểu | Quy tắc |
| --- | --- | --- |
| assignmentId | UUID | Bắt buộc, khác rỗng; luôn lấy reviewer và snapshot từ assignment này |
| action | string | Bắt buộc SaveDraft hoặc Submit; không nhận số |
| expectedAssignmentVersion | uint | Bắt buộc > 0, lấy từ SCRUM-104 list hoặc GET phiếu |
| expectedEvaluationVersion | uint? | Omitted/null chỉ khi chưa có phiếu; đã có Draft phải gửi version hiện tại > 0 |
| score | decimal? | Null được ở Draft; nếu có phải 0–10; Submit bắt buộc |
| comment | string? | Quy tắc mục 2.3 |
| defenseApproval | string? | Null được ở Draft; Approved/Rejected khi Submit |

Header ví dụ (UUID minh họa): `Idempotency-Key: 22222222-2222-4222-8222-222222222222`.

Tạo nháp chưa đủ nội dung:

```json
{
  "assignmentId": "11111111-1111-4111-8111-111111111111",
  "action": "SaveDraft",
  "expectedAssignmentVersion": 741,
  "expectedEvaluationVersion": null,
  "score": null,
  "comment": "Cần bổ sung phân tích kết quả thực nghiệm.",
  "defenseApproval": null
}
```

Chốt nháp đang có, sử dụng key **mới** cho ý định Submit:

```json
{
  "assignmentId": "11111111-1111-4111-8111-111111111111",
  "action": "Submit",
  "expectedAssignmentVersion": 741,
  "expectedEvaluationVersion": 746,
  "score": 8.345,
  "comment": "Cần bổ sung phân tích kết quả thực nghiệm.",
  "defenseApproval": "Approved"
}
```

Submit trực tiếp khi chưa có nháp dùng body đầy đủ với expectedEvaluationVersion=null. Tạo phiếu lần đầu trả **201**, kể cả Submitted trực tiếp; sửa nháp/chốt phiếu đã tồn tại trả **200**. Có thể trả Location trỏ GET private evaluation của chính assignment; không tạo URI không có endpoint đọc.

### 3.2. DTO phiếu đầy đủ

POST và GET private cùng shape `ReviewerEvaluationDto`; không chứa eligibility động, điểm GVHD, object key hoặc URL tải. Binding projection lấy từ assignment bất biến; `assignmentVersion` là version parent hiện tại, `evaluationVersion` là xmin phiếu sau save. `updatedBy/updatedAtUtc` là người/lúc lưu nội dung gần nhất, kể cả Admin.

```json
{
  "success": true,
  "data": {
    "id": "22222222-2222-4222-8222-222222222222",
    "assignmentId": "11111111-1111-4111-8111-111111111111",
    "groupId": "33333333-3333-4333-8333-333333333333",
    "topicId": "44444444-4444-4444-8444-444444444444",
    "batchId": "55555555-5555-4555-8555-555555555555",
    "reviewerId": "66666666-6666-4666-8666-666666666666",
    "advisorEvaluationId": "77777777-7777-4777-8777-777777777777",
    "finalReportId": "88888888-8888-4888-8888-888888888888",
    "reportVersionNo": 2,
    "submissionFileId": "99999999-9999-4999-8999-999999999999",
    "submissionChecksumSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    "assignmentVersion": 741,
    "evaluationVersion": 749,
    "status": "Submitted",
    "isLegacy": false,
    "score": 8.35,
    "comment": "Cần bổ sung phân tích kết quả thực nghiệm.",
    "defenseApproval": "Approved",
    "isLocked": true,
    "isVisibleToStudent": true,
    "isIgnoredInCalculation": false,
    "createdBy": "66666666-6666-4666-8666-666666666666",
    "createdAtUtc": "2026-10-30T07:00:00Z",
    "updatedBy": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    "updatedAtUtc": "2026-10-30T07:15:00Z",
    "submittedBy": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    "submittedAtUtc": "2026-10-30T07:15:00Z"
  },
  "error": null
}
```

Draft trả status=Draft, isLocked=false, isVisibleToStudent=false, submittedBy/submittedAtUtc=null; score/comment/defenseApproval có thể null. IsLocked là thuộc tính suy ra, không cột mới. API mới trả 409 cho legacy thay vì giả metadata; không trả partial legacy dưới hình thức phiếu workflow mới.

### 3.3. Version, replay và thứ tự kiểm tra

1. Xác thực/coarse role trước resource lookup; POST chỉ cho Admin hoặc nhánh Lecturer. Sau đó content-type, key/body/JSON/query/shape/giới hạn nội dung. Body hợp lệ mới xét replay; input sai vẫn 400, không biến thành collision 409.
2. Đọc assignment tối thiểu để kiểm tra ownership; Lecturer phải chính reviewer, Active/Lecturer hiện tại. Quyền đọc/ghi người dùng hiện tại vẫn được kiểm tra trước replay; key không phải capability vượt quyền. Admin dùng reviewerId của assignment khi gọi guard, không dùng actorId thay reviewer.
3. Trong coordinator: khóa actor/operation/key, đọc receipt. Có receipt cùng fingerprint thì trả status/DTO gốc; khác fingerprint trả 409 IdempotencyConflict. Không kiểm tra lại Open/version/live binding cho replay đã được cấp quyền, không mutate.
4. Với ý định mới: tồn tại assignment → assignment legacy bị chặn → đọc phiếu **nontracking, không khóa**. Nếu phiếu legacy trả LegacyRecordNeedsResolution; nếu Submitted nonlegacy trả Locked trước version/Open/binding. Mọi ý định tiếp tục ghi phải khóa parent qua `LockForEvaluationAsync`, rồi khóa/reload evaluation; không lấy khóa evaluation trước chuỗi khóa parent.
5. Guard kiểm tra binding/account/current advisor rồi lấy parent lock. Guard trả null thì Abort/NotEligible, không dùng snapshot pre-read làm proof hoặc cố lấy parent version để ưu tiên lỗi khác. Nếu guard thành công: đối chiếu expectedAssignmentVersion, rồi kiểm tra Open trên batch đã được guard khóa; không đọc Open từ một connection khác. Chưa cần config Active cho bước ghi phiếu.
6. Khóa/reload evaluation, kiểm tra Legacy/Submitted lại. Chưa có nhưng expectedEvaluationVersion khác null, hoặc đã có Draft nhưng version thiếu/sai: 409 VersionConflict. Không upsert ghi đè dựa trên assignmentId mà bỏ version.
7. Domain validate/apply SaveDraft hoặc Submit, stage receipt thành công; save lần đầu lấy xmin phiếu. Trong post-save callback, tạo audit có version thực tế và hoàn thiện DTO/receipt; save lần hai rồi commit. Bất kỳ lỗi nào rollback cả hai save, gồm phiếu/receipt/audit. Chỉ trả thành công sau commit.

Các lỗi cùng xuất hiện theo thứ tự kiểm tra trên; không hứa trả đủ mọi lý do trong một response. Early read chỉ để báo Locked ổn định, không được dùng làm proof cho ghi sau đó. Không tự retry workflow mutation khi DB báo conflict; client đọc lại state cho ý định mới.

Operation duy nhất `ReviewerEvaluation.Save` cho POST; action nằm trong fingerprint để cùng key đổi SaveDraft thành Submit luôn collision. Fingerprint SHA-256 theo actor scope, assignmentId, action canonical, hai expected version (có marker null), **score đã chuẩn hóa**, comment đã trim/null và decision canonical. GUID canonical, số invariant; JSON field order/casing không ảnh hưởng. `8.345` và `8.35` cùng score lưu 8.35 là cùng nội dung nếu mọi field khác giống; thay version/action/comment chuẩn hóa là ý định khác.

Encoding fingerprint không dùng ghép chuỗi delimiter có thể xuất hiện trong comment. Hash UTF-8 của JSON array theo thứ tự `[assignmentId N lowercase, action canonical, expectedAssignmentVersion, expectedEvaluationVersion|null, score F2 invariant string|null, comment|null, defenseApproval canonical|null]`. JSON null khác chuỗi `"null"`; comment giữ hoa thường/nội dung sau trim. Actor/operation/key được scope ở khóa và unique receipt, không lấy từ body.

Key scope gồm actorId + operation + UUID, độc lập khỏi receipt SCRUM-103/104. Receipt chỉ lưu thành công 201/200 và DTO chứa evaluationVersion sau save. Cùng key/body nhận lại receipt gốc dù đã sửa nháp/chốt sau đó hoặc đợt vừa đóng; **receipt Draft cũ không làm phiếu Submitted trở lại Draft**. Không coi isLocked/version trong receipt cũ là state hiện tại: reload GET. Không có audit mutation cho replay, GET hoặc lỗi.

## 4. GET `/api/thesis/reviewer-assignments/{assignmentId}/evaluation`

- Quyền mục 1; own reviewer được đọc phiếu của assignment thuộc mình kể cả điều kiện viết/binding đã đổi; vẫn cần account Active/Lecturer. Faculty/PĐT có role + read permission; Admin bypass permission. Không cho GVHD tự đọc phiếu của GVPB chỉ vì là advisor.
- Assignment không có: 404 AssignmentNotFound. Assignment tồn tại nhưng không có phiếu: 404 NotFound, **không tạo Draft**. Phiếu hoặc assignment legacy: 409 LegacyRecordNeedsResolution, không tự backfill. Lecturer khác: 403 trước lookup phiếu/nội dung.
- Thành công 200 DTO mục 3.2; không bị chặn chỉ vì batch đóng, nhóm đã sang giai đoạn tiếp hoặc reviewer không còn đủ điều kiện ghi. Khi reviewer account không Active/Lecturer, chính reviewer bị 403; quản trị vẫn tra cứu theo quyền.
- Không có query, không cho client chọn một userId/reviewerId khác. GET side-effect free; không ghi receipt/audit, không đổi xmin hay visibility. Không trả `canEdit` phụ thuộc batch/binding dễ bị stale; client dùng state và lỗi write để điều khiển thao tác.

## 5. GET `/api/thesis/groups/{groupId}/feedback`

- Cần Student và membership **Confirmed** thuộc đúng group trong DB. Pending, ngoài group, GVHD, quản trị không có nhánh Student tương ứng: 403. Group không tồn tại: 404 GroupNotFound sau coarse role check. Không có override đọc theo Admin/permission quản trị.
- 200 với đúng shape public bên dưới; không tái sử dụng entity, private DTO hoặc enum status. Chưa có Submitted nonlegacy đã phát hành: comment=null, không phân biệt ra ngoài giữa chưa chấm/nháp/legacy/không có góp ý. Explicit null phải còn trong JSON.
- Chỉ chọn phiếu Submitted/nonlegacy/IsVisibleToStudent=true trên assignment active/nonlegacy của group, có snapshot và liên kết hợp lệ với phiếu đã phát hành. Không fallback RevisionFeedback/phiếu cũ hoặc xuất nháp. Tối đa một active theo SCRUM-104; nếu dữ liệu mâu thuẫn nhiều nguồn hợp lệ thì fail closed bằng 409 ContextInvalid, không tự chọn mới nhất.
- Đây là góp ý đã phát hành của snapshot assignment, không phải eligibility để ghi/xếp lịch. Sau chốt, việc batch đóng/reviewer ngừng hoạt động/group chuyển giai đoạn **không tự thu hồi comment**; không yêu cầu current group ReadyForReviewerAssignment, reviewer Active hoặc rule gate hiện tại. Không nối nội dung với file/report mới bằng cách suy đoán version hiện tại. Việc xử lý phiên bản báo cáo sau phản biện thuộc task riêng.
- Phiếu điểm bị loại tính điểm vẫn giữ góp ý đã công khai; cờ loại không phải thao tác gỡ comment. Không có API đổi visibility/thu hồi trong task này. DTO không có IsIgnoredInCalculation/ExclusionReason.

```json
{
  "success": true,
  "data": {
    "comment": "Cần bổ sung phân tích kết quả thực nghiệm."
  },
  "error": null
}
```

```json
{
  "success": true,
  "data": {
    "comment": null
  },
  "error": null
}
```

Allowlist `data` chính xác **chỉ `comment`**. Không có score, advisorScore/advisor_score, reviewerScore/reviewer_score, approval, gate/reason, ngưỡng, evaluationVersion, timestamps chấm, audit metadata hoặc nested private DTO. Không gửi các field bị cấm dưới dạng null/0. Query includeScores/includeDraft/reviewerId/userId bị từ chối, không thêm chức năng gỡ ẩn bằng query.

## 6. GET `/api/thesis/groups/{groupId}/defense-eligibility`

### 6.1. Quyền và DTO

Admin; Faculty/PĐT có read permission; own reviewer Active/Lecturer của active assignment trong group. Student/GVHD/Lecturer khác không được đọc qua các role đó. Reviewer đã nghỉ vẫn có thể có phiếu hợp lệ cho gate, nhưng account nghỉ không được tự truy cập endpoint.

Group không tồn tại: 404. Group tồn tại nhưng thiếu điều kiện: **200 và canProceed=false**, không trả 409 chỉ vì điểm thấp, chưa chấm, thiếu config hoặc Rejected. GET phục vụ điều phối, không phải proof cho scheduling write; không mutate/audit, không chứa điểm/nhận xét GVHD hoặc raw score/comment GVPB.

```json
{
  "success": true,
  "data": {
    "groupId": "33333333-3333-4333-8333-333333333333",
    "batchId": "55555555-5555-4555-8555-555555555555",
    "batchStatus": "Open",
    "assignmentId": "11111111-1111-4111-8111-111111111111",
    "evaluationId": "22222222-2222-4222-8222-222222222222",
    "evaluationVersion": 749,
    "scoreConfigurationId": "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
    "scoreConfigurationVersionNo": 3,
    "minimumReviewerScore": 5.5,
    "canProceed": true,
    "reasonCode": null
  },
  "error": null
}
```

Các field context/config chưa xác định được có thể null; groupId và canProceed luôn có. Khi canProceed=false thì reasonCode có một giá trị canonical ở mục 6.3. Không trả array gồm mọi lỗi và dữ liệu nội bộ đã bị short-circuit. `minimumReviewerScore` là ngưỡng, không phải điểm sinh viên, và không thuộc DTO feedback.

### 6.2. Resolve ngưỡng Active hiện tại

Chọn đúng một ScoreWeightConfig Active của batch, projection các component có `Source=Reviewer` (không dựa vào ComponentCode). Không có Active hoặc không có component Reviewer: ConfigurationMissing. Nhiều Active hoặc projection config/component Reviewer có ID/version/Applicability/giá trị ngưỡng bất hợp lệ: ConfigurationInvalid, không chọn mới nhất. Sau khi chọn một Active, ưu tiên kiểm tra dữ liệu bất hợp lệ trước, rồi thiếu coverage/ngưỡng, cuối cùng mâu thuẫn giữa các ngưỡng đã hợp lệ; không trả Ambiguous chỉ vì một giá trị thực tế nằm ngoài 0–10.

Resolve hai nhánh có thể xảy ra trước xếp Hội đồng: InCouncil và OutsideCouncil. Với mỗi nhánh, lấy union component Always + component đúng nhánh. Nhánh không có component, hoặc bất kỳ MinimumScore nào null: ConfigurationMissing. Giá trị ngoài 0–10: ConfigurationInvalid. Nếu tất cả threshold của cả hai nhánh không cùng một giá trị: ConfigurationAmbiguous. Đồng nhất thì lấy giá trị đó; không lấy max/min, không chọn nhánh dựa vào reviewer chưa được xếp Hội đồng. Weight/IsRequired/RequiresApproval không loại component khỏi bước resolve hoặc bỏ yêu cầu Approved của Jira.

| Components Source=Reviewer | Kết quả |
| --- | --- |
| Always 5.0 | Resolve 5.0 |
| InCouncil 5.0 + OutsideCouncil 5.0 | Resolve 5.0 |
| Always 5.0 + InCouncil 5.0 | Resolve 5.0; OutsideCouncil được Always bao phủ |
| Hai Always đều 5.0 | Resolve 5.0; vẫn giữ config/version thực tế để tra cứu |
| Chỉ InCouncil 5.0 | Missing vì OutsideCouncil chưa được bao phủ |
| Always null hoặc một component có MinimumScore null | Missing, không mặc định 5.0 |
| InCouncil 5.0 + OutsideCouncil 6.0 | Ambiguous |
| Always 5.0 + OutsideCouncil 6.0 | Ambiguous |
| Always 0.0 | Resolve 0.0; vẫn bắt buộc Submitted + Approved |

Submit phiếu không chạy resolver để từ chối chốt. Khi publish cấu hình mới, gate dùng Active mới ở lần đọc/khóa tiếp theo; phiếu/comment/version đã chốt không thay đổi. Không đóng băng ngưỡng trong ReviewerEvaluation hoặc lưu ReadyForDefense boolean làm nguồn sự thật.

### 6.3. Điều kiện và thứ tự reason

Sau authorization và group tồn tại, xét theo thứ tự sau; trả reason đầu tiên không đạt. Chỉ các giá trị đạt qua bước trước mới được dùng cho bước sau.

| Thứ tự | reasonCode | Ý nghĩa |
| --- | --- | --- |
| 1 | ContextInvalid | Topic/batch/membership context thiếu hoặc mâu thuẫn |
| 2 | GroupNotReady | Group không ở ReadyForReviewerAssignment hoặc InitialFinal không còn UnderEvaluation |
| 3 | AssignmentMissing | Không có active assignment |
| 4 | LegacyAssignment | Assignment active là legacy |
| 5 | AdvisorConflict | Reviewer trùng GVHD hiện tại |
| 6 | BindingInvalid | Phiếu GVHD không còn đồng ý/khóa hoặc snapshot report/version/file/checksum/approval ID không khớp |
| 7 | EvaluationMissing | Assignment chưa có phiếu |
| 8 | LegacyEvaluation | Phiếu không đủ bằng chứng lifecycle mới |
| 9 | EvaluationNotSubmitted | Đang Draft |
| 10 | EvaluationExcluded | Điểm đã bị đánh dấu không được công nhận/loại tính điểm |
| 11 | ConfigurationInvalid / ConfigurationMissing / ConfigurationAmbiguous | Rule Active không resolve được theo mục 6.2 |
| 12 | DefenseRejected | Kết luận phiếu là Rejected |
| 13 | ScoreBelowMinimum | Điểm đã chuẩn hóa < ngưỡng hiện tại |

Submitted có đầy đủ dữ liệu theo DB CHECK; nếu phát hiện hỏng nội dung/lifecycle thì fail closed ContextInvalid, không coi null score là 0. Nếu không có reason, canProceed=true và reasonCode=null. Điểm bằng ngưỡng là đạt. Thiếu config và Rejected cùng lúc trả code cấu hình theo thứ tự trên; đây không phải công nhận Rejected.

BatchStatus là thông tin điều phối. Batch Closed không tự xóa giá trị chuyên môn của phiếu; gate có thể canProceed=true khi Closed nếu điều kiện chuyên môn hợp lệ. Điều này **không cấp quyền xếp lịch trong đợt đóng**: writer SCRUM-107 phải áp policy thời gian của chính nó và gọi gate khóa trong transaction. Gate không bổ sung trạng thái StudentGroup mới; giữ ReadyForReviewerAssignment để không phá guard đọc/tải/chấm SCRUM-104. Các trạng thái Defended/ScoreLocked/Completed không phải ứng viên xếp lịch mới, nên gate không mở lại chúng.

Account/grant của reviewer phải còn hợp lệ khi ghi phiếu, nhưng sau khi chốt việc nghỉ/vô hiệu tài khoản không tự hủy giá trị phiếu/góp ý. Gate không dùng lại toàn bộ guard `LockForEvaluationAsync` rồi vô tình loại phiếu vì live account flag; vẫn xét assignment, advisor conflict, binding, approval và cờ loại điểm như trên.

## 7. Lỗi và thứ tự xử lý

Prefix API errors `ReviewerEvaluation.`; gate thiếu điều kiện chuyên môn dùng reasonCode mục 6, không đổi thành lỗi HTTP. Message dưới đây là thông điệp đích; không đưa score/comment hoặc thông tin phiếu riêng vào error public.

| HTTP | Code | Tình huống / message |
| --- | --- | --- |
| 400 | InvalidRequest | Request, GUID/version/action/enum/JSON/header/query không hợp lệ. |
| 400 | InvalidScore | Điểm phản biện phải từ 0 đến 10; nháp được null. |
| 400 | InvalidComment | Góp ý vượt quá 2.000 Unicode rune hoặc dữ liệu text không hợp lệ. |
| 400 | IncompleteEvaluation | Chưa đủ điểm, góp ý và kết luận để chốt phiếu. |
| 401 | AuthenticationRequired | Cần đăng nhập; middleware có thể trả body rỗng. |
| 403 | AccessDenied | Không có quyền thực hiện thao tác với phiếu/nhóm này. |
| 404 | AssignmentNotFound | Không tìm thấy phân công phản biện. |
| 404 | GroupNotFound | Không tìm thấy nhóm đề tài. |
| 404 | NotFound | Chưa có phiếu phản biện của phân công này. |
| 409 | LegacyRecordNeedsResolution | Dữ liệu cũ cần được xử lý theo quy trình riêng. |
| 409 | Locked | Phiếu đã chốt; không thể sửa hoặc chốt lại bằng ý định mới. |
| 409 | AssignmentVersionConflict | Phân công đã thay đổi; hãy tải lại dữ liệu. |
| 409 | VersionConflict | Phiếu đã thay đổi hoặc expectedEvaluationVersion không phù hợp. |
| 409 | BatchNotOpen | Chỉ nhập/sửa/chốt khi đợt đang Open. |
| 409 | NotEligible | Phân công, tài khoản reviewer hoặc binding không còn hợp lệ để ghi. |
| 409 | ContextInvalid | Dữ liệu context mâu thuẫn, không thể xử lý an toàn. |
| 409 | IdempotencyConflict | Key đã dùng cho một ý định khác. |
| 409 | ConcurrencyConflict | Có thay đổi đồng thời; tải lại dữ liệu trước ý định mới. |
| 413 / 415 | Theo HTTP middleware | Body quá lớn hoặc content-type không được hỗ trợ; không hứa envelope. |

Shape/field validation sau xác thực: malformed/unknown/duplicate/action/number/enum/version → InvalidRequest; score ngoài khoảng → InvalidScore; comment quá giới hạn → InvalidComment; Submit thiếu một hay nhiều nội dung → IncompleteEvaluation. Comment rỗng sau trim ở Submit được tính là thiếu nội dung. Ownership được xác minh trước lookup phiếu/receipt, nên người không được phép không dùng lỗi để đọc phiên bản/nội dung.

GET feedback: nháp/legacy/không có góp ý trả 200/null, không trả code hé lộ trạng thái phiếu. Mất quyền hoặc group/context mâu thuẫn không được giả thành comment rỗng để bỏ qua authorization; áp 403/404/409 tương ứng.

## 8. Thiết kế dữ liệu cho một migration CP1

### 8.1. `councils.reviewer_evaluation`

| Property đích / cột | Thiết kế |
| --- | --- |
| Id / id; AssignmentId / assignment_id | Giữ UUID PK và FK Restrict đến ReviewerAssignment; unique AssignmentId, không thêm phiếu thứ hai |
| Score / score | Đổi CLR decimal? / numeric(4,2) nullable cho draft, giữ CHECK 0–10 khi có giá trị |
| Comment / comment | Text nullable cho draft; trim/rune 1–2.000 khi Submitted |
| DefenseApproval / defense_approval | Enum string Approved/Rejected nullable cho draft; Submitted bắt buộc |
| Status / status | Enum string Draft/Submitted nullable chỉ để giữ legacy chưa xác định; không default Draft cho dữ liệu cũ |
| IsLegacy / is_legacy | Boolean NOT NULL default true; factory workflow mới đặt false |
| CreatedBy / created_by; CreatedAtUtc / created_at_utc | UUID?/UTC?; mới bắt buộc, legacy không bịa actor/thời gian |
| EvaluatedBy / evaluated_by | UUID? của người lưu gần nhất, mới bắt buộc; DTO tên updatedBy |
| EvaluatedAt / evaluated_at | Giữ timestamp UTC NOT NULL cũ, là thời điểm lưu gần nhất cho dòng mới; DTO tên updatedAtUtc |
| SubmittedBy / submitted_by; SubmittedAtUtc / submitted_at_utc | UUID?/UTC?, chỉ có khi Submitted |
| Version / xmin | uint rowversion PostgreSQL system column; không tạo cột version vật lý riêng |
| IsVisibleToStudent / is_visible_to_student | Giữ boolean; dòng mới Draft=false, Submitted=true, không nhận client ghi |
| RevisionFeedback, IsIgnoredInCalculation, ExclusionReason | Giữ dữ liệu/cột cũ; không cho POST này sửa hoặc fallback công khai |

Binding không nhân đôi: giữ qua FK đến assignment bất biến với snapshot đầy đủ đã có ở SCRUM-104. Không thêm FK vật lý xuyên module cho metadata actor mới; kiểm tra qua identity/auth contract và audit; giữ toàn bộ FK vật lý đã có.

Constraint/index đích:

- Giữ `ix_reviewer_evaluation_assignment_id` unique, `ck_reviewer_evaluation_score`, FK `fk_reviewer_evaluation_reviewer_assignment_assignment_id` Restrict. Giữ index một active/nhóm của assignment.
- `ck_reviewer_evaluation_status`: Status null hoặc Draft/Submitted; `ck_reviewer_evaluation_defense_approval`: decision null hoặc Approved/Rejected, không để enum numeric default lấp chỗ null.
- `ck_reviewer_evaluation_workflow`: với !IsLegacy, IDs không rỗng; Status/created/updated actor-time đầy đủ; CreatedAtUtc <= EvaluatedAt; Comment null hoặc đã trim/1–2.000 ký tự; Draft có SubmittedBy/At null và visibility=false; Submitted bắt buộc score/comment/decision, actor/time Submitted đầy đủ, SubmittedAtUtc=EvaluatedAt, SubmittedBy=EvaluatedBy, visibility=true. Tại thời điểm tạo dòng, CreatedAtUtc có thể bằng EvaluatedAt.
- Domain dùng .NET Unicode Trim và Rune count; DB btrim/char_length là lớp bảo vệ dữ liệu (không tự biến thành Unicode Trim của .NET). Chốt payload phải qua domain, không dựa vào PostgreSQL numeric precision để làm tròn thay quy tắc API.
- IsLocked suy ra từ !IsLegacy && Status=Submitted. Không có field config threshold/gate boolean/ReadyForDefense trong phiếu hoặc StudentGroup. Cấu hình Active và gate không nằm trong receipt POST.

Không cần thêm index trùng unique AssignmentId hoặc bảng history phiếu cho mỗi SaveDraft. Audit ghi actor/action/version, không lưu raw nội dung nháp vào outbox. Những workflow tương lai sửa cờ loại điểm phải bảo vệ concurrency/khóa tương ứng; SCRUM-105 không cung cấp endpoint đó.

### 8.2. `councils.reviewer_evaluation_idempotency`

Id UUID PK; ActorId UUID; Operation varchar(100); IdempotencyKey UUID; RequestFingerprint varchar(64); ResponseStatusCode integer; ResponsePayloadJson jsonb; CreatedAtUtc timestamptz. Các cột NOT NULL. Unique `(actor_id, operation, idempotency_key)`, status IN (200,201), fingerprint 64 hex thường, operation đã trim 1–100 ký tự. Không liên kết receipt với bảng assignment/evaluation bằng FK cascade. Receipt có thể tạm `{}` trong transaction, phải hoàn thiện JSON chứa xmin trước commit.

Actor/key/operation unique bảo vệ scope trong DB; PG advisory transaction lock theo scope để request cùng key chờ receipt. Không dùng receipt của reviewer assignment làm receipt phiếu hoặc ghi receipt trước khi bắt đầu transaction dữ liệu.

### 8.3. Legacy, seed và rollback

- Up giữ nguyên ID/assignment/điểm/comment/RevisionFeedback/approval/cờ ẩn/cờ loại/thời gian cũ, đánh dấu IsLegacy=true, không tự Submit/công khai, không dựng created/submitted actor giả. Dòng cũ vẫn chặn hủy phân công.
- Preflight dữ liệu phiếu trùng AssignmentId, orphan, enum/score không hợp lệ hoặc schema mâu thuẫn nếu có; dừng có chẩn đoán, không xóa/chọn dòng/backfill để vượt lỗi. Relax NOT NULL Score/DefenseApproval cho draft không đổi điểm cũ thành null.
- Seed permission read cho ba role quản trị đã nêu, giữ permission metadata/grant tùy chỉnh; không cấp quyền write cho Khoa/PĐT. Không seed tài khoản/mật khẩu.
- Down chỉ chạy nếu bảo toàn dữ liệu/semantics và nullability cũ. Nếu còn receipt hoặc dòng workflow mới (Draft/Submitted nonlegacy) thì preflight chặn thay vì xóa/đổi status/điểm để downgrade. Test Down/Up ở trạng thái chỉ có legacy và preflight khi có workflow mới.
- Cập nhật ngay các fixture SCRUM-104 khi encapsulate entity/đổi nullable, giữ assertion hủy khi có bất kỳ phiếu và hai thứ tự race. Không thêm public setter hoặc đánh dấu phiếu mới là legacy để test tình cờ pass.

## 9. Contract liên module và giao thức khóa

| Owner | Boundary đích |
| --- | --- |
| DefenseCouncil | Repository phiếu/receipt; domain lifecycle; audit; `IReviewerDefenseEligibilityContract` có GetAsync(groupId) và LockForSchedulingAsync(groupId); projection không có phiếu GVHD/URL |
| AcademicPeriods | `IReviewerDefenseRuleReader` GetActiveAsync/LockActiveAsync(batchId), trả batchId/batchStatus và configId/version/components tối thiểu cùng outcome của rule; GetBatchStatusAsync đọc status riêng để early reason không cần đọc rule. Thiếu config vẫn giữ batch context, thiếu batch trả context not found. Lock cần transaction dùng chung; không sửa DTO batch reader SCRUM-104 |
| ThesisManagement | Tái sử dụng report/group guard SCRUM-104; `IReviewerFeedbackGroupReader` trả context tồn tại/group/topic/batch và Confirmed membership của actor, đồng thời kiểm tra report/file/version/checksum gốc qua IsReportSnapshotValidAsync, độc lập current version/status. Không trả scores hoặc gọi save |
| TopicCatalog | GVHD/topic/batch hiện tại qua contract Get/Lock đã có |
| Identity | Lecturer/account hiện tại qua candidate contract; permission constant/seed; không coi name reader là kiểm tra eligibility |
| Composition root | Tái sử dụng reviewer coordinator transaction ReadCommitted/năm context; chỉ DefenseCouncil ghi. Không dùng migration context runtime hoặc import DbContext module khác vào application |

### 9.1. POST và hủy đua chấm

Thứ tự write lock: **request key → group/membership/report/file → topic → batch → advisor approval → reviewer account/grants → parent assignment → reviewer evaluation**. Chuỗi giữa group và parent chính là `LockForEvaluationAsync` có sẵn. Kiểm tra Open trên batch đã khóa; không thêm Open vào guard đọc/tải SCRUM-104. Save phiếu/receipt/outbox giữ tất cả khóa đến commit; xóa tracker/rollback khi Abort hoặc exception.

Early read kiểm tra quyền/Locked không giữ khóa trước parent. Cancel SCRUM-104 khóa parent rồi kiểm tra any evaluation; không khóa group/config sau parent. Vì writer chấm chờ parent mà chưa giữ evaluation, không tạo thứ tự đảo parent/evaluation với cancel. Nếu Draft/Submit insert thắng, cancel thấy row và 409; cancel thắng thì guard chấm thấy inactive và không insert. Hai SaveDraft/Submit khác key serialize bằng parent + xmin phiếu, không last-write-wins/upsert.

### 9.2. Gate khóa cho writer SCRUM-107

GetAsync là projection điều phối tại lúc đọc, không đảm bảo giữ mọi guard ổn định qua các query/module; endpoint GET không tự mở quyền ghi. `LockForSchedulingAsync` phải ở **chính transaction ghi CouncilAssignment** theo coordinator hiện có; trả block/null thì caller Abort. Thứ tự: group/membership/report/file → topic → batch → advisor approval → parent assignment → evaluation → config Active. Không cần khóa live reviewer account cho giá trị phiếu đã chốt. Lock lấy context/binding mới, giữ assignment/evaluation/config ổn định đến scheduling commit. Không gọi Get rồi save ở transaction khác, không dùng receipt hoặc canProceed cache. Caller vẫn kiểm tra quyền xếp lịch, thời gian, chưa có phân công Hội đồng và các điều kiện scheduling riêng; contract này chỉ chứng minh gate chuyên môn đã mô tả.

- Gate bảo vệ config Active bằng row lock FOR SHARE trên parent ScoreWeightConfig; components Active bất biến theo owner domain. Publisher hiện retire Active cũ rồi publish Draft trong một transaction; update retire phải chờ lock này. Không giả định khóa batch tự ngăn publish, vì publisher hiện không khóa batch.
- Nếu publisher đã retire row đang chờ khóa, reader phải reselect Active bằng **SQL statement mới** sau khi chờ và xác minh status/version; tối đa 3 lượt acquire/recheck, chưa ổn định thì concurrency conflict/Abort, không dùng projection Active đã đọc trước lock. Không có Active thì block; không suy luận rằng việc không thấy row đã khóa được một “khoảng trống” để chống insert.
- Positive eligibility luôn có config Active thực sự đã khóa. Publisher chỉ chạm config rows, không lấy group/assignment/evaluation, nên không tạo vòng khóa ngược với gate. Những lock này cần test hai connection/quan sát blocking cả hai thứ tự; khả năng này **chưa được chứng minh runtime ở CP0**.
- Writer đại diện SCRUM-107 trong test được ghi CouncilAssignment thật trong schema DefenseCouncil với contract khóa để chứng minh boundary; SCRUM-105 không tạo endpoint scheduling. Việc đưa GET/Lock vào C# và cách map lỗi theo bảng trên thuộc CP1/CP3.

### 9.3. Audit và nguyên tử

Action đích `DefenseCouncil.ReviewerEvaluation.DraftSaved` hoặc `DefenseCouncil.ReviewerEvaluation.Submitted`, đúng một action trên mỗi ý định write thành công. Metadata gồm evaluation/assignment/group/reviewer/binding IDs, action, actor, UTC và evaluationVersion; không chứa raw score/comment/RevisionFeedback, URL hoặc token. Actor Admin là Admin thực tế; reviewerId vẫn là người được phân công.

Phiếu, receipt và audit cùng transaction; lỗi save đầu/save receipt/audit đều rollback. Sau save đầu có xmin thực tế mới Add audit và hoàn thiện DTO/receipt trong callback, để save hai ghi đúng version mà không cần một audit version giả đã commit. GET, replay hoặc write bị từ chối không audit mutation. Không thêm audit ThesisManagement vì không cập nhật group/report. DB unique/FK/xmin không thay giao thức giữ guard locks tới commit.

## 10. Luồng tích hợp FE

1. Dùng SCRUM-104 me/list để lấy assignmentId và assignmentVersion; GET private evaluation. 404 NotFound nghĩa chưa có phiếu; GET không tự tạo nháp.
2. SaveDraft gửi toàn bộ nội dung hiện có, key mới cho ý định mới, expectedEvaluationVersion=null khi tạo hoặc version đã đọc khi sửa. Giữ key/body để retry khi mất response; không sinh key mới cho retry chưa rõ kết quả.
3. Submit gửi đủ nội dung và version đã xem. Chốt xong khóa form cho cả Approved/Rejected; muốn thay đổi sau chốt phải qua quy trình ngoài task, không gọi POST ghi đè bằng Admin.
4. Khoa/PĐT dùng GET private/gate theo read permission. Gate config thiếu/mâu thuẫn hiển thị vấn đề cấu hình; không tự thêm 5.0 hoặc yêu cầu GVPB đổi điểm cho qua.
5. Student chỉ dùng GET feedback; comment=null là chưa có góp ý đã phát hành, không suy đoán điểm/trạng thái. Không gọi private evaluation/gate hoặc lịch sử file để tìm góp ý.
6. 409 Version/Concurrency/NotEligible: tải lại assignment/phiếu trước ý định mới; không tự resubmit nội dung cũ với version mới. Replay receipt cũ có thể chứa Draft cũ; dùng GET để lấy state hiện tại.

## 11. Ma trận nghiệm thu bắt buộc CP1–CP4

Các ID duy nhất RV-01–RV-36 được đối chiếu với test thực tế trong [báo cáo nghiệm thu CP4](ReviewerEvaluationVerification.md). CP0 chỉ xác lập acceptance, không phải bằng chứng runtime; kết quả CP1–CP3 bên dưới được giữ riêng theo checkpoint.

| ID | Ca nghiệm thu | Kết quả bắt buộc |
| --- | --- | --- |
| RV-01 | GVPB own Active/Lecturer và Admin ghi thay | 201/200 đúng lifecycle; actor/time server thực tế, reviewerId không đổi |
| RV-02 | Lecturer khác/GVHD/Student/CouncilMember, kể cả được gán read permission | Write 403; không lookup phiếu/receipt để lộ nội dung |
| RV-03 | Khoa/PĐT role + read, thiếu read, permission trên role không đúng; Admin | Private/gate theo ma trận; Khoa/PĐT không write; anonymous 401 |
| RV-04 | Batch Open quá hạn sinh viên/ngoài FINAL_EVALUATION; Closed/Draft/Archived | Open ghi được; batch khác Open write 409; đọc theo quyền vẫn hoạt động |
| RV-05 | Assignment inactive/legacy, reviewer nghỉ/mất Lecturer/trùng GVHD, binding/approval/report đổi | Không ghi dựa trên context cũ; Admin không bypass |
| RV-06 | Tạo/sửa Draft null/missing nội dung, score thật 0 | Null không bị đổi thành 0/Approved; cùng evaluation ID, chưa gate/công khai |
| RV-07 | Submit trực tiếp; chốt Draft Approved/Rejected | Submit đầy đủ 201 hoặc 200; chốt cả hai kết luận đều khóa |
| RV-08 | Thiếu score/comment/decision khi Submit | 400 IncompleteEvaluation; không lưu một phần hoặc tạo phiếu rỗng |
| RV-09 | Score 0/10/8.345/ngoài khoảng, numeric string/overflow; enum số/lạ | Biên hợp lệ/round AwayFromZero; số ngoài khoảng và parser sai bị chặn |
| RV-10 | Comment Unicode 2.000/2.001 rune, whitespace, full replace draft | Không đếm surrogate đôi thành hai; trim/null/replace đúng, không truncation |
| RV-11 | Query/unknown/duplicate JSON field, UUID/version/key thiếu/sai/lặp header, content-type | 400 hoặc 415/413 phù hợp, không mutate |
| RV-12 | Expected parent version cũ; eval version null/cũ/lạ hoặc phantom version | 409 phù hợp, không upsert/ghi đè |
| RV-13 | Sửa/chốt lại Submitted bằng GVPB/Admin, kể cả Rejected | 409 Locked; không mở khóa bằng ý định mới |
| RV-14 | Cùng key/body/normalized input/field order; cùng key khác action/version/ý định | Replay status/DTO gốc hoặc 409 collision, không audit đôi |
| RV-15 | Mất response sau commit, replay Draft cũ sau Submit/batch đóng | Receipt cũ không mutate/khôi phục Draft; GET trả state hiện tại |
| RV-16 | Hai draft create/update hoặc update vs Submit khác key | Một kết quả serial/version hợp lệ, không lost update/phiếu trùng |
| RV-17 | Cancel vs Draft insert và Cancel vs Submit hai thứ tự khóa | Phiếu thắng chặn hủy; hủy thắng writer không insert; Draft cũng any-row |
| RV-18 | Đóng batch/đổi GVHD/group/report/account trước và trong guard | Kết quả serial hợp lệ hoặc 409, không commit trên điều kiện đã vô hiệu |
| RV-19 | Save/audit/receipt save lỗi, Abort và ghi nhầm reader schema | Rollback mọi thay đổi, không receipt/audit/phiếu mồ côi |
| RV-20 | Private GET chưa có phiếu, legacy, actor khác, batch đóng | 404/409/403 đúng, không tạo Draft; truy cập lịch sử theo quyền |
| RV-21 | Student Confirmed own group/Pending/group khác, Admin/GVHD không có nhánh Student | Feedback đúng quyền; không được dùng management permission để override |
| RV-22 | Feedback chưa chấm/nháp/legacy vs Submitted Approved/Rejected | 200 comment null hoặc comment đã chốt; không suy ra lifecycle qua DTO |
| RV-23 | Serialization feedback/OpenAPI/malformed selector | Allowlist data chỉ comment, không score/approval/gate/threshold/private nested fields; null vẫn có key |
| RV-24 | Batch đóng/reviewer nghỉ/group Defended sau Submit; điểm bị loại | Góp ý đã phát hành không biến mất; gate không mở lại group đã bảo vệ |
| RV-25 | Gate thiếu assignment/approval/binding, legacy, Draft, phiếu bị loại | canProceed=false, reason theo thứ tự; không tự đổi group/report |
| RV-26 | Ngưỡng thiếu/null/invalid/ambiguous/0, branch coverage Always/In/Outside, tên code khác REVIEWER | Resolver theo Source và bảng ví dụ; không chọn nhánh/max/min/default 5 |
| RV-27 | Điểm dưới/bằng/trên ngưỡng và Approved/Rejected | Chỉ Submitted Approved + score >= minimum qua gate; Rejected bị chặn dù cao |
| RV-28 | Thiếu/mâu thuẫn config khi Submit | Vẫn chốt phiếu đủ điều kiện; gate bị chặn với reason cấu hình |
| RV-29 | Publish Active mới sau Submit, kể cả thay đổi ngưỡng làm đổi eligibility | Phiếu/comment/xmin không đổi; gate phản ánh config Active mới |
| RV-30 | Gate Lock vs publisher hai thứ tự, retired row khi chờ, config churn | Khóa đến scheduling commit; reselect SQL fresh/bounded conflict, không stale grant |
| RV-31 | Writer xếp lịch đại diện dùng Get cũ so với Lock trong transaction | Chỉ Lock là proof, invalid thì Abort; không tự thêm HTTP scheduling |
| RV-32 | Fresh migration, legacy roundtrip, unique/FK, nullable/checks, xmin và downgrade | Bảo toàn dữ liệu cũ, Submitted đủ dữ liệu, preflight Down an toàn; không auto backfill |
| RV-33 | Permission seed reapply/custom grants; unauthorized GET/replay | Giữ grant tùy chỉnh, quyền đúng role; replay không vượt auth hiện tại |
| RV-34 | Audit SaveDraft/Submit/Admin/read/replay/failure | Đúng actor/action/version, không raw score/comment/URL, không mutation audit dư |
| RV-35 | OpenAPI POST/GET private/gate/feedback | Header key bắt buộc mọi env, body enum/nullable/status/response/query đúng, public/private schema tách |
| RV-36 | Production composition PostgreSQL/MinIO/FK từ nộp v1/v2 → GVHD duyệt → phân công → nháp/chốt/gate/feedback | Đúng byte/binding/quyền, any-row cancel, khóa cuối; hồi quy SCRUM-102/103/104 và Architecture |

## 12. Phân bổ triển khai và trạng thái bằng chứng

| Checkpoint | Phần hoàn chỉnh | Kiểm tra phù hợp |
| --- | --- | --- |
| CP0 (tài liệu này) | Contract, schema/DTO/quyền/lock protocol, acceptance, cross-links | JSON examples, ID uniqueness, local links/whitespace/consistency; không build/test ứng dụng |
| CP1 | Lifecycle/migration/owner contracts/repositories/resolver, sửa các caller cũ bị ảnh hưởng | Domain, migration/PostgreSQL locks/constraints, Architecture và target regression |
| CP2 | POST SaveDraft/Submit, private GET đầy đủ quyền, audit/idempotency/OpenAPI | HTTP/PG/races/replay/rollback, OpenAPI document thật, workflow MinIO phần ghi |
| CP3 | Gate Get/Lock, GET gate/feedback, quyền đọc/DTO và docs mô hình | Config publisher race/writer đại diện, security allowlist, lifecycle đọc, workflow đầu ra |
| CP4 | Nghiệm thu source cuối, report RV-xx → test/lệnh/TRX | Broad targeted regression/Release/format/diff; ghi full/filter/pass/fail/skip rõ ràng |

CP0 không có permission seed, migration hoặc runtime proof SCRUM-105; serialization/SQL locks được kiểm tra khi triển khai CP1–CP3. Kết quả SCRUM-104 trong [ReviewerAssignmentVerification.md](ReviewerAssignmentVerification.md) là baseline lịch sử, không tính thành test đã chạy cho task này. OpenAPI và tài liệu liên quan hoàn chỉnh cùng checkpoint triển khai API; CP4 tập trung nghiệm thu source cuối và hồi quy.

## 13. CP1 — nền tảng đã triển khai

Baseline `5076627`, cập nhật 30/10/2026. Không có endpoint chấm/feedback/gate mới ở CP1; không đăng ký một implementation giả cho IReviewerDefenseEligibilityContract.

- [ReviewerEvaluation](../../backend/Modules/DefenseCouncil/Domain/Entities/ReviewerEvaluation.cs) encapsulate lifecycle, Draft nullable/full replacement, Unicode trim/rune validation, score kiểm tra trước rounding, Submit đầy đủ và khóa cả Approved/Rejected; created/updated/submitted actor/time, visibility và xmin. IsLocked suy ra, không phải cột.
- [Mapping](../../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/ReviewerEvaluationConfiguration.cs), receipt, repository và audit primitive đã có. Repository lock yêu cầu transaction, refresh entity đang tracked Unchanged và không chấp nhận row đã sửa trước lock. Caller vẫn giữ parent lock trước evaluation lock theo mục 9.
- Migration [20261030113753_AddReviewerEvaluationWorkflowFoundation](../../backend/Database.Migrations/Migrations/20261030113753_AddReviewerEvaluationWorkflowFoundation.cs) giữ unique/FK Restrict, bảo toàn legacy/cờ visibility/loại/feedback/điểm, relax nullability cho nháp, thêm CHECK/receipt và seed read. Up preflight chặn dữ liệu lịch sử sai; Down chặn row workflow/metadata mới hoặc receipt, không điền điểm 0. Không add/drop cột xmin vật lý; permission/custom grants giữ khi Down/Up.
- [IReviewerDefenseRuleReader](../../backend/Modules/AcademicPeriods/Contracts/ReviewerEvaluations/IReviewerDefenseRuleReader.cs) và implementation AcademicPeriods trả batch/config/components, khóa parent Active FOR SHARE, reselect SQL mới nếu row vừa Retired, tối đa ba lượt acquire/recheck. Contract exception riêng báo churn để consumer map conflict mà không tham chiếu AcademicPeriods.Application.
- [Resolver](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/ReviewerDefenseRuleResolver.cs) kiểm tra coverage hai nhánh, missing/invalid/ambiguous, ngưỡng 0 và metadata; không default 5. Components đọc theo Source=Reviewer, projection Applicability raw string để dữ liệu enum cũ không hợp lệ được resolver chặn thay vì EF enum converter ném lỗi trước khi resolve.
- [IReviewerFeedbackGroupReader](../../backend/Modules/ThesisManagement/Contracts/ReviewerEvaluations/IReviewerFeedbackGroupReader.cs) và implementation chỉ đọc group/topic/batch/membership Confirmed của actor, không xuất roster/điểm và không áp stage/group readiness của write lên đọc góp ý.
- [ReviewerEvaluationAudit](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/ReviewerEvaluationAudit.cs) kiểm tra actor xác thực/row đã save có xmin, stage DraftSaved/Submitted metadata không có raw score/comment; writer CP2 sẽ gọi sau save đầu trong coordinator có sẵn. Header/parser/fingerprint/replay handler thực tế thuộc CP2.
- IReviewerDefenseEligibilityContract đã khai báo DTO/reason/Get/Lock signatures để CP3 triển khai; chưa có gate consumer/scheduling writer HTTP. Owner readers/resolver không tự đổi StudentGroup hoặc báo cáo, không thêm Open vào quyền tải SCRUM-104.
- Các caller seed phiếu CP2/CP4/CP5/CP6 SCRUM-104 đã chuyển sang factory Draft; các fixture kiểm tra điểm 0/ẩn/ignored vẫn seed đúng cờ bằng persistence trong test, không mở setter hoặc endpoint sửa cờ. Model/DBML đồng bộ ngay tại CP1 để CP2/CP3 không phải thêm lại schema đã biết.

Kiểm thử mới: [domain lifecycle](../../backend/tests/DefenseCouncil.Tests/ReviewerEvaluationTests.cs), [receipt](../../backend/tests/DefenseCouncil.Tests/ReviewerEvaluationReceiptTests.cs), [resolver](../../backend/tests/DefenseCouncil.Tests/ReviewerDefenseRuleResolverTests.cs), [PostgreSQL foundation](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationFoundationIntegrationTests.cs), [migration](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationMigrationIntegrationTests.cs). Các test PostgreSQL dùng composition/reader/coordinator thật và FK thật; race publisher gọi handler production, dùng gate/trigger chỉ để giữ đúng thứ tự commit và quan sát pg_blocking_pids.

Lệnh build/format từ root; artifacts riêng để không đụng DLL của API đang chạy:

```powershell
dotnet build UGTM.sln --no-restore --configuration Release --artifacts-path artifacts/scrum105-cp1 -m:1 -nr:false -p:UseSharedCompilation=false
dotnet format UGTM.sln --no-restore --verify-no-changes --verbosity quiet
$cp1Filter = Get-Content artifacts/scrum105-cp1/regression-filter.txt -Raw
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --no-build --no-restore --configuration Release --artifacts-path artifacts/scrum105-cp1 --filter $cp1Filter.Trim() --logger 'trx;LogFileName=foundation-and-affected-regression-final.trx' --results-directory artifacts/scrum105-cp1/test-results
```

Filter local chọn 13 method gốc của ReviewerAssignmentTransactionIntegrationTests (24 theory/fact cases), 9 foundation cases mới, toàn bộ ReviewerAssignmentWorkflowPersistenceIntegrationTests (15 cases), cùng các method Cancellation_http_blocks_any_evaluation, Cancellation_http_serializes_with_a_real_grading_writer, Reviewer_reads_has_evaluation và Real_reviewer_workflow (7 cases). Không tính các lần chạy lặp vào tổng nghiệm thu. Bốn project/phạm vi còn lại dùng cùng options test; filter Identity là `FullyQualifiedName~IdentityPermissionMigrationTests.Permission_migrations_preserve_existing_data`.

Kết quả kiểm chứng CP1 ngày 30/10/2026:

| Phạm vi | Pass / tổng | TRX trong `artifacts/scrum105-cp1/test-results/` |
| --- | --- | --- |
| DefenseCouncil, toàn project | 90/90 | defense-final.trx |
| AcademicPeriods, toàn project | 45/45 | academic-final.trx |
| Architecture, toàn project | 74/74 | architecture-final.trx |
| ThesisManagement, filter nêu trên; PostgreSQL/MinIO thật | 55/55 | foundation-and-affected-regression-final.trx |
| Identity, filter permission migration | 1/1 | identity-permission-final.trx |
| **Tổng phạm vi đã chọn, không cộng lượt lặp** | **265/265; fail 0, skip 0** | |

Release build solution thành công, 0 error; lượt build cuối ghi 2 warning MSB3277 đã có về phiên bản EF Core của Architecture.Tests. Lượt build đầu còn ghi CS8602 tại OpenApiConfiguration hiện hữu; không có warning mới từ mã CP1. `dotnet format --verify-no-changes` toàn solution, git diff --check, JSON examples, 36 acceptance IDs, 123 link Markdown local và whitespace/code fences của 40 file thay đổi đều pass; index vẫn trống. Log build/format và script `Validate-Foundation.ps1` nằm trong artifacts local.

Lượt regression trước phát hiện ba test lỗi vì projection raw SQL dùng alias PascalCase trong khi EF áp snake_case cho DTO; đã sửa projection và chạy lại đủ 55 cases thành công. Validation domain cũng chặn NUL/UTF-16 lỗi trước persistence, tránh PostgreSQL từ chối public comment. Không cộng lượt lỗi hoặc các lượt lặp vào bảng trên.

Tại thời điểm kết thúc CP1, các API CP2–CP3 và nghiệm thu toàn task CP4 chưa thực hiện. Artifacts CP1 ở `artifacts/scrum105-cp1/`, bị Git ignore; migration chỉ được áp vào DB container kiểm thử, không áp vào DB ứng dụng. Người dùng sau đó đã review và tự commit CP1 tại `676d9bd`; kết quả triển khai CP2 xem mục 14.

## 14. CP2 — lưu nháp, chốt và đọc phiếu riêng tư

Baseline `676d9bd` (CP1 đã được người dùng review/commit). CP2 dùng trực tiếp domain/schema/coordinator của CP1 và guard parent SCRUM-104; không thêm migration, không sửa DTO phân công hoặc mở quyền ghi schema owner khác.

- [SaveReviewerEvaluationHandler](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/SaveReviewerEvaluationHandler.cs) thực hiện thứ tự mục 3.3: coarse authorization/normalization, ownership/account, request-key lock và replay, early Legacy/Locked, parent workflow guard, version/Open, child lock/reload, mutation và hai save nguyên tử. Sau khi chờ request key, kiểm tra lại quyền live trước receipt lookup; Admin dùng reviewer của assignment cho guard, audit ghi actor Admin thật.
- [ReviewerEvaluationInput](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/SaveReviewerEvaluationCommand.cs) kiểm tra GUID/version/action/decision, score trước rounding, Unicode/rune/NUL, completeness và fingerprint JSON array có marker null. Operation `ReviewerEvaluation.Save`, receipt riêng actor/operation/key, 201 tạo lần đầu và 200 sửa/chốt nháp. Replay giữ status/DTO gốc, không trả state hiện tại hoặc gate động.
- [GetReviewerEvaluationHandler](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/GetReviewerEvaluationHandler.cs) kiểm tra quyền riêng tư bằng [authorization](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/ReviewerEvaluationAuthorization.cs), trả [DTO](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/ReviewerEvaluationDto.cs) mục 3.2. Không tạo nháp/audit hoặc gọi write guard; đọc quản trị còn hiệu lực sau batch close/reviewer inactive, own reviewer vẫn phải Active/Lecturer. Context group/topic/batch mâu thuẫn trả 409.
- [Endpoints](../../backend/Modules/DefenseCouncil/Infrastructure/Endpoints/ReviewerEvaluationEndpoints.cs) có POST và GET riêng tư. Parser JSON kiểm tra field bắt buộc, chấp nhận omitted/null đúng contract và từ chối field lạ/lặp bất kể casing, enum integer/numeric string, version/UUID/key/query sai; body parser không dùng setting global để nới dữ liệu. Nháp rỗng không được tự điền score/decision. Coarse policy chạy trước content-type/body/resource; không dùng routing Accepts metadata để vô tình trả 415 trước authentication.
- [Operation transformer](../../backend/host/Api/OpenApi/ReviewerEvaluationOperationTransformer.cs) khai báo body/header đầy đủ cho handler HttpRequest, required/nonnullable inputs, canonical enums, nullable nội dung và response statuses. [Schema transformer](../../backend/host/Api/OpenApi/ReviewerEvaluationSchemaTransformer.cs) mô tả status/decision dạng string của DTO; scope đúng type, không sửa global JSON hoặc schema endpoint cũ.
- [HTTP tests](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationHttpIntegrationTests.cs), [write/replay/rollback/concurrency](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationWriteIntegrationTests.cs), [owner guard retention](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationGuardLockIntegrationTests.cs) và [real workflow/OpenAPI](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationRealWorkflowIntegrationTests.cs) dùng production registration/coordinator/reader và PostgreSQL/FK thật. Trigger test chỉ giữ writer ở boundary save; quan sát pg_blocking_pids để xác nhận writer khác đang chờ, không dùng delay làm bằng chứng race.

Luồng MinIO mới đi từ nộp v1/v2 → khóa/duyệt GVHD → phân công → nháp → chốt bằng GVPB/Admin, tải byte đúng v2 và chặn hủy sau nháp. OpenAPI được lấy qua HTTP từ host thật và kiểm tra hai route, header, body, statuses và schema private. Gate/feedback sinh viên chưa được triển khai/đăng ký trong CP2; việc đặt visibility khi chốt là nền tảng để CP3 đọc đúng dữ liệu, chưa phải mở endpoint sinh viên.

Lệnh build và lượt PostgreSQL/MinIO từ root:

```powershell
dotnet build UGTM.sln --no-restore --configuration Release --artifacts-path artifacts/scrum105-cp2 -m:1 -nr:false -p:UseSharedCompilation=false
$cp2Filter = @(
    'FullyQualifiedName~Evaluation_cp2_'
    'FullyQualifiedName~Reviewer_evaluation_foundation_'
    'FullyQualifiedName~Cancellation_http_blocks_any_evaluation'
    'FullyQualifiedName~Cancellation_http_serializes_with_a_real_grading_writer'
    'FullyQualifiedName~Reviewer_reads_has_evaluation'
    'FullyQualifiedName~Real_reviewer_workflow'
    'FullyQualifiedName~Creation_http_commits_the_approved_snapshot'
) -join '|'
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --no-build --no-restore --configuration Release --artifacts-path artifacts/scrum105-cp2 --filter $cp2Filter --logger 'trx;LogFileName=http-and-affected-regression-final.trx' --results-directory artifacts/scrum105-cp2/test-results
dotnet format UGTM.sln --no-restore --verify-no-changes --verbosity quiet
```

DefenseCouncil.Tests và Architecture.Tests dùng cùng options test, bỏ filter để chạy toàn project. Không cộng smoke/lượt lặp hoặc bằng chứng CP1 vào tổng CP2.

Kết quả ngày 30/10/2026:

| Phạm vi | Pass / tổng | TRX trong `artifacts/scrum105-cp2/test-results/` |
| --- | --- | --- |
| ThesisManagement filtered: 45 ca CP2 mới + 19 ca hồi quy liên quan, PostgreSQL/MinIO thật | 64/64 | http-and-affected-regression-final.trx |
| DefenseCouncil, toàn project | 90/90 | defense-final.trx |
| Architecture, toàn project | 74/74 | architecture-final.trx |
| **Tổng phạm vi đã chọn, không cộng lượt lặp** | **228/228; fail 0, skip 0** | |

Release build solution 0 error, còn 4 warning nền (CS8602 OpenApiConfiguration tại host/source-linked test, MSB3277 phiên bản EF Core tại Architecture.Tests); không có warning mới của CP2. Format verify toàn solution, JSON examples/36 acceptance IDs, link/whitespace/fences và diff check pass; index trống. Log `build-final.log`, `format-final.log`, filter và `Validate-Checkpoint.ps1` nằm trong artifacts local; OpenAPI HTTP export ở `artifacts/scrum105-cp2/bin/UGTM.Modules.ThesisManagement.Tests/release/reviewer-evaluation-openapi.json`.

Lượt trước có 63/64 pass: Accepts metadata khiến request chưa đăng nhập với text/plain bị routing trả 415 trước authentication. Đã chuyển body metadata sang operation transformer, giữ kiểm tra content-type trong handler sau policy, rồi build/rerun đủ 64 ca thành công. Các lượt smoke/lỗi/lặp giữ trong artifacts nhưng không cộng vào bảng trên. Các ca rollback kiểm tra cả insert và cập nhật/chốt nháp, ở save đầu và save hai; ca replay kiểm tra quyền live sau khi chờ key, ca race kiểm tra cả chốt thắng sửa và sửa thắng chốt.

TRX/log/filter/plan local bị Git ignore. CP2 không thêm hoặc áp migration vào DB ứng dụng, không stage/commit. Người dùng sau đó đã review và tự commit CP2 tại `ba5afa8`; gate/feedback CP3 xem mục 15, nghiệm thu toàn task CP4 xem mục 16.

## 15. CP3 — gate Hội đồng và góp ý sinh viên

Baseline `ba5afa8` (CP2 đã được người dùng review/commit). CP3 dùng nguyên lifecycle, schema, POST/receipt/private DTO và coordinator đã có; không thêm migration, persist ReadyForDefense hoặc đổi StudentGroup/report.

- [ReviewerDefenseEligibilityContract](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/ReviewerDefenseEligibilityContract.cs) đã đăng ký GetAsync/LockForSchedulingAsync thật. Thứ tự reason ở mục 6.3; BatchStatus là thông tin, group/report phải ở trạng thái chuẩn bị phản biện, binding/approval/assignment/evaluation được kiểm tra trước rule. Submitted hỏng nội dung fail closed ContextInvalid, không coi score null là 0. Rule dùng Active hiện tại qua reader/resolver CP1; không hard-code 5.0, không chọn nhánh tùy tiện, không phụ thuộc live account cho giá trị phiếu đã chốt.
- LockForSchedulingAsync yêu cầu transaction đã enlist owner contexts; khóa group/membership/report/file → topic → batch → approval → parent → evaluation → Active config tới commit. Nếu publisher đã retire row đang chờ, owner reader reselect Active bằng SQL mới theo bounded retry có sẵn. Hết retry được bọc thành DefenseCouncilConcurrencyException để caller Abort, HTTP reader map ConcurrencyConflict. Không gọi write guard có live-account flag để xét giá trị phiếu đã chốt.
- [GetReviewerDefenseEligibilityHandler](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/GetReviewerDefenseEligibilityHandler.cs) kiểm tra role + read permission/own active assignment + account Active/Lecturer trước khi xuất gate. Chưa đủ điều kiện chuyên môn trả 200 với reason đầu tiên; DTO 11 field không có raw grade/comment GVPB/GVHD. Student không được đọc gate.
- [GetReviewerFeedbackHandler](../../backend/Modules/DefenseCouncil/Application/ReviewerEvaluations/GetReviewerFeedbackHandler.cs) chỉ chấp nhận nhánh Student và Confirmed membership đúng group, không có override quản trị. ReviewerFeedbackDto chỉ có comment, JsonIgnoreCondition.Never giữ explicit null kể cả serializer global bỏ null. Nháp/legacy/chưa phát hành trả cùng 200/null; không xuất score/decision/status/version/ngưỡng hoặc fallback RevisionFeedback.
- [Publication reader](../../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Repositories/ReviewerFeedbackPublicationReader.cs) dùng projection whitelist trong một query DefenseCouncil, Take(2) phát hiện nhiều nguồn active; chỉ chọn comment Published trên assignment/evaluation nonlegacy và liên kết phiếu GVHD/snapshot hợp lệ. Không select score, defense_approval, revision_feedback, evaluated_by hoặc exclusion_reason. Owner [feedback reader](../../backend/Modules/ThesisManagement/Infrastructure/Persistence/Repositories/ReviewerFeedbackGroupReader.cs) xác minh report/group và file/version/checksum gốc, không dùng current version/status để nối lại góp ý. Góp ý đã phát hành không tự mất khi đổi giai đoạn/đóng đợt/GVPB nghỉ hoặc điểm bị loại tính điểm.
- [Read endpoints](../../backend/Modules/DefenseCouncil/Infrastructure/Endpoints/ReviewerEvaluationReadEndpoints.cs) khai báo hai GET, policies/response/status/route parameter, từ chối mọi query selector; không có body/header idempotency. [Schema transformer](../../backend/host/Api/OpenApi/ReviewerEvaluationSchemaTransformer.cs) tách FeedbackDto khỏi private DTO và mô tả reason canonical; không đổi global JSON setting.

Kiểm thử mới ở [read/role/rule/lifecycle/query](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationReadIntegrationTests.cs), [security/corruption](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationReadSecurityIntegrationTests.cs), [scheduling writer/publisher races](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerDefenseSchedulingIntegrationTests.cs), [bounded churn outcome](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerDefenseChurnIntegrationTests.cs), [real workflow/OpenAPI](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerEvaluationOutputsRealWorkflowIntegrationTests.cs) và [DTO serialization](../../backend/tests/DefenseCouncil.Tests/ReviewerFeedbackSerializationTests.cs).

Writer đại diện ghi CouncilAssignment thật trong coordinator transaction, chỉ khi gate khóa hợp lệ và policy riêng BatchStatus=Open. Test quan sát pg_blocking_pids ở cả hai thứ tự với PublishScoreConfigurationHandler production và các writer sửa owner/parent/evaluation; publisher thắng phải dùng config mới trước khi ghi, gate thắng giữ rule cũ ổn định tới commit. Test no-account-lock xác nhận reviewer nghỉ không ngầm thu hồi giá trị phiếu. Ca exhausted-retry dùng lỗi owner được inject có mục tiêu để kiểm tra mapping/rollback, không được tính thành bằng chứng SQL churn ba lần. Chưa có endpoint scheduling SCRUM-107.

Luồng MinIO thật đi từ nộp v1/v2, khóa/duyệt GVHD, phân công, nháp, chốt GVPB/Admin tới feedback/gate; sinh viên chỉ thấy comment sau chốt, config publish mới đổi gate nhưng không đổi evaluationVersion/comment, quyền tải đúng byte v2 và phiếu GVHD giữ nguyên. OpenAPI được kiểm tra qua HTTP từ host production composition; query-count test xác nhận feedback có số query cố định dù thêm lịch sử assignment.

Lệnh chạy từ root (cần restore trước nếu dùng checkout mới):

```powershell
dotnet build UGTM.sln --no-restore --configuration Release --artifacts-path artifacts/scrum105-cp3 -m:1 -nr:false -p:UseSharedCompilation=false
$cp3Filter = @(
    'FullyQualifiedName~Evaluation_cp3_'
    'FullyQualifiedName~Reviewer_evaluation_foundation_'
    'FullyQualifiedName~Evaluation_cp2_real_report_'
    'FullyQualifiedName~Evaluation_cp2_private_read_role_matrix'
    'FullyQualifiedName~Evaluation_cp2_draft_replace_submit'
    'FullyQualifiedName~Evaluation_cp2_http_save_and_cancel'
    'FullyQualifiedName~Evaluation_cp2_replay_canonicalizes'
    'FullyQualifiedName~Real_reviewer_workflow'
    'FullyQualifiedName~Cancellation_http_blocks_any_evaluation'
    'FullyQualifiedName~Reviewer_reads_has_evaluation'
) -join '|'
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --no-build --no-restore --configuration Release --artifacts-path artifacts/scrum105-cp3 --filter $cp3Filter --logger 'trx;LogFileName=gate-feedback-and-affected-regression.trx' --results-directory artifacts/scrum105-cp3/test-results
dotnet format UGTM.sln --no-restore --verify-no-changes --verbosity quiet
```

DefenseCouncil.Tests/Architecture.Tests dùng cùng options test, bỏ filter để chạy toàn project. Kết quả ngày 30/10/2026:

| Phạm vi | Kết quả mới nhất theo từng test duy nhất | Bằng chứng trong `artifacts/scrum105-cp3/test-results/` |
| --- | --- | --- |
| ThesisManagement filtered: 60 ca CP3 mới + 24 hồi quy liên quan | 84/84 pass | gate-feedback-and-affected-regression.trx + gate-feedback-fixture-retry.trx |
| DefenseCouncil, toàn project (có 2 ca DTO mới) | 92/92 pass | defense-final.trx |
| Architecture, toàn project | 74/74 pass | architecture-final.trx |
| **Tổng phạm vi duy nhất đã chọn** | **250 pass; kết quả mới nhất fail 0/skip 0** | validation-final.log |

Lượt integration đầu có 75/84 pass; 9 ca dừng tại fixture do chọn Single schedule template chưa scope đúng template của batch hoặc thiếu changeReason khi tạo version cấu hình sau version đầu. Đã sửa fixture, giữ nguyên domain/validation/mã nghiệp vụ; chạy lại 11 ca thuộc các method bị ảnh hưởng và pass 11/11. Validation lấy outcome mới nhất theo testName, thay 9 failure cũ và không cộng trùng 2 ca đã pass trong lượt retry. Không cộng kết quả CP1/CP2 hoặc lượt lặp vào tổng CP3. Filter retry local ở `retry-filter.txt`.

Release build solution cuối thành công, 0 error/3 warning nền (CS8602 OpenApiConfiguration source-linked test và MSB3277 EF Core Architecture.Tests); không warning mới của mã CP3. Format verify toàn solution, sáu JSON examples/36 RV IDs, 216 link Markdown local, whitespace/fences và git diff --check pass; index trống. Feedback dùng ≤5 query cố định và một query whitelist DefenseCouncil dù có thêm 12 assignment lịch sử. OpenAPI HTTP export ở `artifacts/scrum105-cp3/bin/UGTM.Modules.ThesisManagement.Tests/release/reviewer-outputs-openapi.json`, gồm feedback schema chỉ comment, bắt buộc nhưng nullable, và gate reason canonical.

Log build/format, filter/TRX, `Validate-Checkpoint.ps1` và plan local đều bị Git ignore. CP3 không sửa lifecycle/schema/POST/receipt của CP1–CP2, không thêm hoặc áp migration DB ứng dụng. Người dùng đã review và commit CP3 tại `0e9dd57`; số liệu mục 15 là kết quả lịch sử CP3, không cộng vào tổng CP4.

## 16. CP4 — nghiệm thu tích hợp và bàn giao

Báo cáo cuối tại [ReviewerEvaluationVerification.md](ReviewerEvaluationVerification.md), gồm từng RV-xx → test thực thi, phạm vi full/filtered, lệnh chạy lại, log/TRX/OpenAPI và giới hạn. Bốn ca bổ sung kiểm tra Draft/Archived và chốt với cấu hình mâu thuẫn/invalid; workflow Kestrel/PostgreSQL/MinIO được mở rộng sửa nháp và khóa sau chốt. CP4 chỉ bổ sung test/tài liệu, không đổi schema hoặc mã nghiệp vụ.

Kết quả trên baseline `0e9dd57` cùng bổ sung test CP4: **782/782 unique selected cases pass, fail 0/skip 0**, mỗi bộ một lượt, không cộng test cũ hoặc lượt lặp. Thesis filtered 441/441 (bao gồm 123 ca integration có tiền tố SCRUM-105), Identity filtered 130/130, AcademicPeriods full 45/45, DefenseCouncil full 92/92, Architecture full 74/74. Release solution cuối 0 error/3 warning nền CS8602/MSB3277; format verify, JSON/links/36 RV IDs ở hai ma trận, OpenAPI HTTP exports và diff pass. Bằng chứng tại `artifacts/scrum105-cp4/` bị Git ignore; không migrate DB ứng dụng, không stage/commit hoặc deploy.
