# SCRUM-106/110 — schema, proof và transaction contract CP0

Ngày 02/11/2026. **CP1 đã tạo schema lưu chiểu, nộp/đọc package, durable cleanup, coordinator và reader defense/window/authority. Các phần xác nhận/ArchiveProof và bảng điểm vẫn là thiết kế cho CP2–CP4.** Áp dụng cùng [FinalSubmissionApiContract](FinalSubmissionApiContract.md) và [ScoreCalculationPublicationApiContract](ScoreCalculationPublicationApiContract.md). Baseline CP0 `767b8e8`; CP0 chỉ sửa tài liệu. [Bằng chứng CP1](FinalSubmissionVerification.md) phân biệt phần đã có với handoff còn lại.

## 1. Các quyết định dùng chung

- Selector Student là groupId bắt buộc: multipart POST lưu chiểu và query của hai `/my-group` cùng `/submission-eligibility`. Quyền luôn đến từ Student Active + Confirmed membership của actor với đúng group. Batch suy ra từ group/topic/membership phải duy nhất và nhất quán. Không First/Single trên toàn bộ lịch sử nhiều đợt của SV, không fallback nhóm mới nhất.
- Staff read/write kiểm tra role và permission trong endpoint lẫn handler; Admin không giả actor hoặc override nhánh Student. GVHD phải là giảng viên Active có phân công hiện tại; nhánh người tham gia Hội đồng cần vai trò hợp lệ và đúng council. Permission không đúng role không tự cấp quyền.
- Cho nộp lại ở Submitted/NeedRevision với expected xmin hiện tại. AdvisorConfirmed chỉ được nộp lại sau RequestRevision; ArchiveConfirmed chặn mọi ý định sửa mới. Mỗi lần nộp là một package nguyên vẹn, không sửa metadata/file/version đã lưu.
- Xác nhận áp vào package và manifest đã xem. AdvisorConfirmed và ArchiveConfirmed trên aggregate là projection của review rows hợp lệ, không phải hai flag tự do để consumer tin.
- Chỉ tính điểm khi ba nguồn chấm đã hoàn tất, đúng binding/roster, minute Locked và archive proof Succeeded. Không lấy ScoreSummary cũ hoặc null=>0 làm nguồn. Không áp lại ngưỡng đi tiếp của SCRUM-105 làm luật từ chối tính/công bố điểm thấp.
- Cấu hình/nguồn thay đổi trước publish phải tính lại; sau publish đọc revision bất biến, không dựng lại từ live config/điểm. Bỏ nhánh tự ghi summary của SubmitCouncilScore ở CP3.
- Không tự chuyển StudentGroup sang Defended/ScoreLocked/Completed để thay proof hoặc mở thêm ghi cross-module. Task sở hữu bảo vệ quản lý trạng thái bảo vệ; 106 quản lý hồ sơ, 110 quản lý bảng điểm.

## 2. Schema SCRUM-106 — migration CP1, dùng nguyên shape ở CP2

Các tên dưới đây là tên bảng/cột đích; giữ nguyên các cột hiện hữu trừ thay đổi được ghi rõ. Quan hệ nội bộ module có PK/FK thật; actor/advisor/config/batch và group ngoài module chỉ là ID snapshot, được owner contract kiểm tra dưới khóa, không thêm FK xuyên module. Timestamps là timestamptz UTC. `version` ánh xạ system column xmin bằng uint/IsRowVersion, không thêm cột xmin vật lý hoặc dùng sequence giả. Hai aggregate `thesis.final_submission` và `councils.score_summary` đều bổ sung `legacy_snapshot jsonb nullable` để bảo toàn projection cũ theo mục 7. Mọi FK mới dùng Restrict để giữ lịch sử; migration preflight kiểm tra dữ liệu trước khi đổi FK hiện hữu cần bảo vệ lịch sử.

| Bảng | Cột/kiểu bắt buộc cho workflow mới | Khóa và ràng buộc |
| --- | --- | --- |
| `thesis.final_submission` | Giữ id/group_id/status/current_version_no và metadata/confirmation columns cũ; thêm is_legacy boolean, current_package_id uuid nullable cho legacy, submitted_by uuid?, submitted_at_utc timestamptz?, version=xmin | Giữ UNIQUE(group_id) và FK group nội bộ; actor là owner-validated ID. Current package FK ghép `(current_package_id,id,current_version_no)` → package `(id,submission_id,version_no)` DEFERRABLE INITIALLY DEFERRED. Workflow mới phải nonlegacy, current version >0, package/file/review hợp lệ với status. Metadata hiện tại là projection cùng transaction với package, không là nguồn lịch sử. |
| `thesis.final_submission_package` | id uuid, submission_id uuid, version_no int, abstract_vi/en text, keywords_vi/en varchar(1000), submitted_by uuid, submitted_at_utc timestamptz, manifest_sha256 char(64) | PK(id); UNIQUE(submission_id,version_no); UNIQUE(id,submission_id,version_no) cho FK ghép; FK submission, actor là owner-validated ID. Version >0, metadata trim/nonempty đúng rune limits, manifest hex lowercase. Insert một lần, không update/delete package đã commit. |
| `thesis.final_submission_package_file` | package_id uuid, submission_id uuid, version_no int, file_role varchar(20), submission_file_id uuid, entity_type varchar(50) cố định FinalSubmission | PK(package_id,file_role); role chỉ ThesisPdf/ThesisDocx; UNIQUE(submission_file_id). FK ghép tới package; FK `(submission_file_id,entity_type,submission_id,version_no)` → SubmissionFile `(id,entity_type,entity_id,version_no)`. Thêm alternate unique tương ứng trên SubmissionFile; **giữ nguyên** partial unique FinalReport và API task cũ. Không thêm role nullable vào mọi loại file chỉ để xử lý hồ sơ lưu chiểu. |
| `thesis.final_submission_review` | id uuid, submission_id uuid, package_id uuid, package_version_no int, action varchar(30), stage varchar(10), actor_id uuid, assigned_advisor_id uuid, decided_at_utc timestamptz, reason text?, manifest_sha256 char(64), defense_proof_snapshot jsonb, idempotency_key uuid, previous_status/result_status varchar(50) | PK(id), FK ghép package/submission/version; actor/advisor là owner-validated IDs. Action AdvisorConfirm/FacultyConfirm/RequestRevision, stage Advisor/Faculty. Confirm không reason; RequestRevision reason 1–1000 runes. Append-only; index(submission_id,package_version_no,decided_at_utc,id). Actor Admin có thể khác assigned_advisor_id; không giả actor. |
| `thesis.final_submission_idempotency` | id uuid, actor_id uuid, operation varchar(64), idempotency_key uuid, fingerprint char(64), response_status int, response_payload jsonb, created_at_utc timestamptz | PK(id), UNIQUE(actor_id,operation,idempotency_key); actor là owner-validated ID. Operation FinalSubmission.Submit hoặc FinalSubmission.Confirm; payload object và status 200/201. Key/fingerprint không rỗng. Không lưu URL ký sẵn trong receipt. |
| `thesis.final_submission_upload_attempt` | id uuid, actor_id uuid, group_id uuid, operation/key/fingerprint như trên, pdf_object_key/docx_object_key varchar(1000), state varchar(20), lease_expires_at_utc timestamptz, committed_package_id uuid?, retry_count int, retry_after_utc timestamptz?, created_at_utc timestamptz, last_error_code varchar(100)? | PK(id), FK group/package nội bộ; actor là owner-validated ID. Object keys duy nhất theo attempt; state Uploading/Committed/CleanupPending/Cleaning/Cleaned. Không unique theo key vì request retry/concurrent attempt có thể dùng object khác nhau; receipt mới là nơi serialize ý định. Không có raw metadata/token trong last_error_code. |

Chốt invariant kiểm tra lúc commit (deferred constraint trigger cho aggregate/package/file links và kiểm tra domain trong cùng transaction):

1. Workflow row nonlegacy luôn có current package thuộc đúng submission/version, đúng hai links với hai role khác nhau, file EntityType/EntityId/VersionNo khớp FK; PDF/DOCX content type, extension, kích thước 1..20 MiB và checksum hợp lệ. Không thể commit Submitted mà chỉ có một file.
2. Metadata projection ở aggregate bằng package; manifest SHA256 tính từ canonical metadata và danh sách role/fileId/size/MIME/checksum của package, sort role cố định. Không chứa URL hoặc object key. Package/file đã commit không được sửa qua các writer workflow; việc đổi một link/file/metadata trái phép phải làm proof invalid.
3. AdvisorConfirmed cần đúng review AdvisorConfirm của current package/manifest, actor/time hợp lệ. ArchiveConfirmed cần cả AdvisorConfirm và FacultyConfirm đúng package/manifest, decidedAt >= submittedAt, AdvisorAt <= FacultyAt; không có RequestRevision sau xác nhận làm mất hiệu lực. Hai review IDs khác nhau; actor có thể cùng là Admin.
4. Nộp package mới reset các confirmation projection trên aggregate, status Submitted; giữ nguyên toàn bộ review/package cũ. NeedRevision không xóa review cũ và không cho dùng chúng xác nhận package mới.
5. Confirmation lưu defense proof đã được kiểm tra dưới khóa và assigned advisor context tại quyết định. FacultyConfirm kiểm tra lại context và proof đã dùng ở bước GVHD; thay đổi giữa hai bước trả 409, không tự tái ký bước GVHD. Sau ArchiveConfirmed, archive proof là bằng chứng lịch sử của package đã seal; thay đổi điểm/minute ở task khác được 110 xét riêng, không làm reader tự đổi hoặc thu hồi package đã seal.

### Upload và compensation

- Xác thực/quyền/selector trước đọc sâu multipart; validate và spool cả hai file cùng metadata trước S3. Sau khi có checksum/fingerprint, transaction ngắn lấy key lock và recheck quyền/receipt để replay mà không upload lại. Không có receipt thì Abort read-only, nhả key rồi mới chạy preflight của ý định mới; deadline/state mới không chặn replay hợp lệ. Mỗi physical attempt có keys riêng từ object-key factory, không dùng tên file/path do client gửi; có thể tái dùng factory hiện hữu với EntityType.FinalSubmission.
- Lưu attempt Uploading/lease trong transaction ngắn, không đổi current package. Upload private S3 ngoài transaction nghiệp vụ. Không giữ group/owner DB locks qua network upload.
- Sau upload, vào coordinator: key lock → recheck quyền → receipt; nếu chưa replay, lấy toàn bộ guards theo mục 5 rồi đọc lại state/xmin/window/minute/membership. Thời điểm nộp quyết định dùng TimeProvider sau khi lấy guards; qua hạn trong lúc upload thì không commit hồ sơ mới.
- Khi hợp lệ, ghi aggregate/package/file links/review projection, save lấy xmin; post-save thêm audit/receipt và mark attempt Committed trỏ đúng package trong **cùng** transaction. Response sau commit, không expose nửa package hoặc AttemptId/object key.
- Khi Abort/lỗi, chỉ cleanup keys của attempt này; uncertain commit phải tra attempt/receipt bằng context mới trước khi xóa. Committed hoặc có package tham chiếu thì không xóa. Lost response retry không sinh thêm version/audit.
- Cleanup thất bại để CleanupPending với retry/backoff. Worker claim Cleaning bằng row lock/CAS, không đụng attempt Committed và không claim lease upload còn hiệu lực. Final commit cũng phải kiểm tra attempt vẫn Uploading và lease hợp lệ, không commit sau khi worker đã claim. Request đã kết thúc upload kiểm tra lại cả attempt Cleaned để dọn file đến muộn; nếu worker còn Cleaning thì đặt CleanupPending cho lượt sweep tiếp, worker cũ không được ghi đè bằng CAS. Worker xóa idempotent rồi mark Cleaned, lỗi được retry; private orphan tạm thời không phải package hợp lệ. Không hứa atomic vật lý DB/S3.

## 3. Schema SCRUM-110 — migration CP3, đủ cho CP4

| Bảng | Cột/kiểu và nguồn dữ liệu | Khóa và ràng buộc |
| --- | --- | --- |
| `councils.score_summary` | Giữ các cột hiện hữu; thêm is_legacy boolean, version=xmin, config_id uuid?, calculated_by uuid?, active_revision_id uuid?, published_revision_id uuid?, input_fingerprint char(64)? | Giữ UNIQUE(group_id); group/config/actor là owner-validated IDs. FK ghép `(active_revision_id,id)` và `(published_revision_id,id)` → revision `(id,summary_id)` deferred. Nonlegacy Calculated cần active revision/config/proof/final score 0..10/total_weight>0; Published cần published revision = active revision, actual published actor/time. Pending/legacy không tự là proof. |
| `councils.score_summary_revision` | id uuid, summary_id uuid, revision_no int, group_id/batch_id uuid, calculated_by uuid, calculated_at_utc timestamptz, config_id uuid, config_version_no int, config_snapshot jsonb, source_snapshot jsonb, input_fingerprint char(64), final_score_raw numeric, final_score numeric(4,2), total_weight numeric(12,4), rounding_mode varchar(30), precision int | PK(id), UNIQUE(summary_id,revision_no), UNIQUE(id,summary_id), FK summary; group/batch/config/actor là owner-validated IDs. RevisionNo >0; rounding AwayFromZero/precision2, 0..10 scores, positive total. Append-only; không tính lại trong row revision cũ. Kiểm tra overflow/range trước persist và trả lỗi cấu hình, không truncate. |
| `councils.score_summary_component` | Giữ cột hiện hữu, thêm revision_id uuid? (null chỉ legacy), source varchar(30)?, source_references jsonb?; score đổi từ numeric(6,4) sang numeric **không scale cố định** | FK ghép `(revision_id,summary_id)` → revision `(id,summary_id)`; new UNIQUE(revision_id,component_code,source_key) cho revision nonnull. Unique cũ chỉ giữ cho revision null/legacy. Score 0..10, weight >=0; raw decimal CouncilAverage không được mất precision vì ghi scale 4. Snapshot giữ giá trị decimal gốc, không dùng component đã round làm đầu vào tính tiếp. |
| `councils.score_summary_idempotency` | id uuid, actor_id uuid, operation varchar(64), idempotency_key uuid, fingerprint char(64), response_status int, response_payload jsonb, created_at_utc timestamptz | PK(id), UNIQUE(actor_id,operation,idempotency_key); actor là owner-validated ID. Operation ScoreSummary.Calculate/ScoreSummary.Publish; payload object, response status 200. Publish receipt lưu đúng selection/result gốc, không tạo receipt riêng từng nhóm để thay atomic request. |

- Current summary là projection của active revision; GET detail mặc định chỉ lấy components của revision đó, history trả các revision bất biến. Không Include tất cả components rồi coi chúng là cùng một phép tính.
- Source snapshot gồm group/topic/batch identity; advisor evaluation ID/xmin/binding/status/score/ignored; reviewer assignment ID/xmin và evaluation ID/xmin/binding/status/score/ignored; council assignment/roster (member ID/user ID/role), minute ID/xmin/Locked proof, từng CouncilScore ID/xmin/score/ignored/conclusion; archive submission/package/version/manifest/two review IDs/file checksums; config ID/version/toàn bộ component definitions và resolution của nhánh.
- Record source ID/version đầy đủ trước khi loại contribution khỏi phép tính, để đổi ignored false→true hoặc true→false, đổi binding/roster hoặc thay phiếu cùng điểm đều bị nhận ra. Chỉ snapshot dữ liệu nghiệp vụ cần cho phép tính; không lấy quyền/account hiện tại của người từng chấm hoặc hạn SV làm grade input.
- SHA256 input fingerprint dùng UTF-8 JSON canonical array, source references/selection sort theo keys cố định, GUID dạng N, checksum lowercase, decimal InvariantCulture G29, enum canonical, timestamp proof UTC O. Không hash JSON tùy field order hoặc thời điểm gọi Calculate hiện tại. Lưu nguyên canonical source/config snapshot để rà soát, không chỉ giữ hash.
- Publish dựng lại current snapshot dưới khóa, yêu cầu đúng một Active config, so cả config ID/version/contents và input fingerprint/revision expected xmin. Thay đổi rồi đổi về giá trị cũ vẫn stale nếu source version đã đổi. Sau Published không chạy stale theo live config nữa; public read lấy published_revision_id và archive package proof gắn vào revision.
- Các summary legacy vẫn giữ final_score/formula/config snapshot/components gốc để quản trị kiểm tra, không trả chúng như điểm đã được hai task mới xác minh. Tính mới từ nguồn hợp lệ tạo revision nonlegacy rõ ràng; không dùng phép tính cũ để lấp nguồn thiếu.

### Resolver và ví dụ làm chuẩn

| Trường hợp | Kết quả đích |
| --- | --- |
| HD=8, PB=7, CT=9, TK=8; reviewer kiêm UV, hệ số 2/2/1/1 | Raw 47/6, final 7.83; đây là tổng hệ số 6. |
| Thêm UV độc lập=6, hệ số 2/2/1/1/1 | Raw 53/7, final 7.57. |
| Trường hợp trên nhưng UV bị loại có kết luận hợp lệ | Loại score và weight UV, raw 47/6, không gán UV=0. |
| CouncilMember có hai UV độc lập, kể cả một phiếu ignored | AmbiguousSource; không FirstOrDefault hoặc tự tạo quy tắc average UV. |
| CouncilAverage và CouncilChairman/Secretary/Member dùng chồng một phiếu trong cùng nhánh | AmbiguousSource; không tính trùng dưới mã component khác. |
| CouncilAverage được config chọn độc lập | Average từ đúng roster được chấm, loại reviewer kiêm nhiệm và phiếu bị loại trước average; dùng decimal không round trung gian, tập rỗng bị chặn. |
| Custom được áp dụng, có weight >0 hoặc IsRequired nhưng chưa có producer | UnsupportedSource, không bỏ qua/gán 0. Không cần resolve component không áp dụng cho nhánh. |
| Hai config Active, role/source/binding mơ hồ, reviewer là CT/TK, advisor ở council nhóm mình | ConfigurationInvalid/ContextInvalid, không fallback bộ hệ số mặc định. |
| Mọi contribution được loại hoặc chỉ còn weight 0 | NoPositiveWeight, không có final score hợp lệ. |

## 4. Owner contracts và coordinator

Archive proof DTO/interface đầy đủ ở contract SCRUM-106. Không đưa StorageKey, aggregate domain của module khác hoặc EF types vào DTO này. `Outcome.Succeeded` là hậu điều kiện đã được owner xác minh, không phải alias của `Status == ArchiveConfirmed`.

### Defense proof và archival window

Các declaration sau là contract đích phải được tạo ở CP1; reader owner có implementation thật, không trả luôn hợp lệ để lấp API khóa biên bản chưa có.

```csharp
namespace UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions
{
    public enum LockedDefenseOutcome { Succeeded, Missing, NotLocked, ContextInvalid }
    public sealed record LockedDefenseProofContractDto(
        LockedDefenseOutcome Outcome, Guid GroupId, Guid BatchId,
        Guid? CouncilId, Guid? AssignmentId, Guid? MinuteId, uint? MinuteVersion,
        Guid? LockedBy, DateTime? LockedAtUtc, string? ProofSha256);
    public interface ILockedDefenseProofReader
    {
        Task<LockedDefenseProofContractDto> GetAsync(Guid groupId, CancellationToken token = default);
        Task<LockedDefenseProofContractDto> LockAsync(Guid groupId, CancellationToken token = default);
    }
}
namespace UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions
{
    public sealed record FinalSubmissionBatchContractDto(
        Guid BatchId, string BatchStatus, DateTime? WindowOpensAtUtc,
        DateTime? WindowDeadlineAtUtc, string ScheduleFingerprint);
    public interface IFinalSubmissionBatchReader
    {
        Task<FinalSubmissionBatchContractDto?> GetAsync(Guid batchId, CancellationToken token = default);
        Task<FinalSubmissionBatchContractDto?> LockAsync(Guid batchId, CancellationToken token = default);
    }
}
```

- LockedDefense Succeeded cần đúng group/batch/council assignment, minute thực sự Locked, actor/time và xmin hợp lệ; AllScoresEntered/UnlockedByOverride không đạt. Lock giữ owner council/assignment/roster/minute tới commit; thiếu row trả outcome tương ứng, không insert Locked.
- Batch reader lấy cửa sổ ARCHIVAL_AND_PUBLICATION thực tế từ schedule của đợt, gồm điều chỉnh/gia hạn được owner quản lý và chuyển múi giờ sang UTC. Không bịa nguồn gia hạn cá nhân mới hoặc nhận deadline do client gửi. Chưa có window hợp lệ thì không cho SV nộp.
- Lock methods cần ambient shared transaction, giữ batch/stage hoặc defense facts đúng pha mục 5; không lấy thêm group/topic/account ở bên trong reader đã được gọi ở pha sau. Caller lấy TimeProvider sau khi lock guards để đánh giá window inclusive theo bounds. Staff chỉ dùng BatchStatus Open/Closed, không cần trong cửa sổ SV.
- Archive proof Succeeded: aggregate nonlegacy/current package/version/FK/metadata manifest, đủ PDF+DOCX, checksum/size/MIME, hai review đúng package/manifest với actor/time/order và hiệu lực còn nguyên. Legacy status ArchiveConfirmed thiếu một trong các bằng chứng đó trả Legacy/ContextInvalid. Archive reader kiểm tra đầy đủ khi Get lẫn Lock, không chỉ tin snapshot JSON hoặc flag.
- Get không giữ khóa và không cấp proof cho write. Lock ngoài transaction throw InvalidOperationException kể cả group/child không tồn tại; reader không SaveChanges, không cập nhật xmin và không thực hiện S3/network IO trong transaction.

### Coordinator declarations

Hai interface/record dưới đây được tạo trong Contracts của module ghi; host triển khai bằng pattern coordinator hiện hữu. Owner proof reader không nhận một `ITransactionCoordinator` chưa định nghĩa hoặc tham chiếu host/EF. Các result types được định nghĩa ngay trong cùng namespace, không có generic result chưa xác định.

```csharp
namespace UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions
{
    public sealed record FinalSubmissionTransactionResult<T>(T Value, bool ShouldCommit)
    {
        public static FinalSubmissionTransactionResult<T> Commit(T value) => new(value, true);
        public static FinalSubmissionTransactionResult<T> Abort(T value) => new(value, false);
    }
    public interface IFinalSubmissionWorkflowTransactionCoordinator
    {
        Task<T> ExecuteWithPostSaveAsync<T>(
            Func<CancellationToken, Task<FinalSubmissionTransactionResult<T>>> workflow,
            Func<T, CancellationToken, Task<T>> postSave, CancellationToken token = default);
    }
}
namespace UGTM.Modules.DefenseCouncil.Contracts.ScoreSummaries
{
    public sealed record ScoreSummaryTransactionResult<T>(T Value, bool ShouldCommit)
    {
        public static ScoreSummaryTransactionResult<T> Commit(T value) => new(value, true);
        public static ScoreSummaryTransactionResult<T> Abort(T value) => new(value, false);
    }
    public interface IScoreSummaryWorkflowTransactionCoordinator
    {
        Task<T> ExecuteWithPostSaveAsync<T>(
            Func<CancellationToken, Task<ScoreSummaryTransactionResult<T>>> workflow,
            Func<T, CancellationToken, Task<T>> postSave, CancellationToken token = default);
    }
}
```

- Host enlist cùng Npgsql connection/ReadCommitted transaction trước callback. 106 chỉ ghi ThesisManagement; DefenseCouncil/TopicCatalog/AcademicPeriods/Identity là reader. 110 chỉ ghi DefenseCouncil; ThesisManagement/TopicCatalog/AcademicPeriods/Identity là reader. Contexts phải sạch, restore connections sau transaction, từ chối reader ChangeTracker.HasChanges.
- Execute callback Commit → save lần 1 lấy xmin; postSave tạo DTO/audit/receipt đúng token → save lần 2 → commit. Abort/exception/cancellation/save/outbox/receipt lỗi rollback tất cả, clear tracking. Map PostgreSQL deadlock/serialization/lock timeout thành conflict; không retry một phần save bên trong kết quả đã công bố.
- Không S3 IO trong callbacks. Get cho management/readiness không tự dùng coordinator để giữ locks qua HTTP response.

## 5. Lock protocol và bảo vệ writer

Các thứ tự này là ràng buộc triển khai, không phải khẳng định mọi writer baseline đã tuân thủ. CP1 đưa producer 106 vào protocol; CP3 retrofit writer điểm hiện hữu; task sở hữu khóa/mở khóa biên bản phải tham gia cùng protocol trước nghiệm thu toàn luồng API thật.

1. **Key:** authorize/parse ý định → begin transaction → advisory transaction lock theo actor+operation+key → recheck quyền và receipt. Không lấy key lock sau khi đã giữ business row locks. Replay hợp lệ kết thúc bằng receipt gốc, không mutate.
2. **Group roots:** lấy tất cả group IDs của request theo thứ tự UUID ổn định, FOR UPDATE, trước khi lấy owner facts. Khóa dependent memberships/reports/file facts cần thiết của từng group theo thứ tự giống reader SCRUM-104/105. Group root bảo vệ cả lần tạo aggregate/child chưa tồn tại.
3. **Context:** topic IDs tăng dần FOR SHARE; batch IDs/stages cần cho policy tăng dần FOR SHARE; current advisor approval facts trước account/grants theo protocol SCRUM-103/104/105. Không mang quyền/accessToken vào grade fingerprint; quyền là guard của request hiện tại.
4. **Authority:** account/grant rows cần kiểm tra thao tác hiện tại theo thứ tự ổn định; dùng lại cùng thứ tự user/role/permission owner readers hiện hữu.
5. **Archival:** aggregate rồi current package, links/files, review rows theo keys ổn định. 106 khóa aggregate FOR UPDATE; 110 FOR SHARE. Receipt replay đã hoàn tất trước pha này. Archive reader không quay lại lấy topic/batch/group locks ở bên trong.
6. **Grade facts:** active reviewer assignment rồi evaluation theo thứ tự parent/child hiện hữu; council root, assignment, roster, minute rồi scores/details theo ID ổn định. 106 chỉ cần defense proof; 110 cần đầy đủ grade facts. Advisor đã được khóa ở pha context, không lấy account/approval ngược thứ tự.
7. **Config:** chọn đúng một Active config và khóa FOR SHARE; nếu publisher retire row trong lúc chờ, dùng statement mới chọn/nạp lại, tối đa 3 lần, hết lượt Abort/409. Bounded reselect không được nhả group/source locks rồi coi proof cũ là mới.
8. **Summary:** FOR UPDATE summary rows theo group order; thiếu row được bảo vệ bởi group root + unique(group_id). Source writer nếu chỉ kiểm tra Published có thể bỏ pha config nhưng không được bỏ group root/grade locks, và đọc Published guard lại ngay trước mutation trong cùng transaction.

- Tất cả owner rows giữ tới cả hai save/audit/receipt commit. Không làm từng group trọn chuỗi rồi mới lấy group root tiếp theo trong bulk request; không chọn batch-first đối nghịch group-first của writer khác.
- Revalidate context/batch/window/minute/membership/state/xmin sau S3 dưới guards trước commit 106. FacultyConfirm recheck AdvisorConfirm còn áp dụng đúng package/context. Preflight/readiness ngoài transaction không đủ.
- Check chỉ đọc Published trước transaction không bảo vệ rescore/unlock đang bay. SubmitCouncilScore, writer loại điểm và writer mở khóa phải dùng root lock, re-read Published và Abort khi đã published; không chỉ thêm if ở đầu handler. Reader-schema mutation bị coordinator từ chối.
- Group reader thực hiện pha root/context như hợp đồng owner; reader proof ở pha sau chỉ đọc context đã khóa để xác minh liên kết, không tái khóa root. Read-only GET không được lấy khóa FOR SHARE rồi upgrade trong writer.

## 6. Idempotency, lỗi và audit

- Fingerprint ý định: Submit = groupId + expected aggregate xmin/null + metadata trim + hai role/checksum/size/MIME; Confirm = submissionId + action + expected aggregate/package version + reason/null; Calculate = groupId + expected summary/config version; Publish = batchId + sorted groupId/expected summary versions. Không hash object key, attempt ID hoặc giờ server do mỗi retry sẽ khác nhau.
- Receipt namespace theo actor+operation+key, SHA256 canonical như mục 3. Cùng UUID có thể độc lập giữa các operation; Confirm dùng cùng operation cho ba action nên đổi action với cùng key là collision. Publish reorder selections được cùng ý định nhưng duplicate group bị 400.
- Kiểm tra authentication/role/ownership trước đọc receipt và kiểm tra lại sau chờ key. Replay được phép trả DTO gốc sau thay đổi deadline/status/batch nếu vẫn còn quyền, vì không phải mutation mới; không hạ current state về Submitted/Calculated. Mất role/membership thì không trả nội dung receipt. GET mới là current state.
- Outbox dùng cơ chế audit hiện hữu. Action đích: ThesisManagement.FinalSubmission.Submitted/AdvisorConfirmed/ArchiveConfirmed/RevisionRequested; DefenseCouncil.ScoreSummary.Calculated/Published. Metadata chỉ IDs/version/action/actor/time và các hash cần đối chiếu, không nguyên abstract/keywords/score/comment/URL. Không phát email trong task này.

| HTTP | Code chuẩn | Điều kiện |
| --- | --- | --- |
| 400 | FinalSubmission.InvalidRequest / ScoreSummary.InvalidRequest | Selector/body/key/version/enum/type/duplicate/unknown field không hợp lệ. |
| 400 | FinalSubmission.InvalidFile / InvalidMetadata | Sai signature/role/định dạng/rune limits. File vượt cap có thể 413 do host; không dựa riêng Content-Type client. |
| 401 / 403 | AuthenticationRequired / AccessDenied dưới prefix owner | Anonymous; hoặc role/permission/membership/owner scope không hợp lệ. Middleware có thể không có envelope. |
| 404 | FinalSubmission.NotFound / ScoreSummary.NotFound | Reader đã được authorize nhưng không có row; readiness của group hợp lệ vẫn trả 200 với Missing reason. |
| 409 | FinalSubmission.BatchNotWritable / SubmissionWindowClosed / DefenseNotLocked / ContextInvalid | Guard hiện tại không cho nộp/xác nhận. Staff bỏ window guard, vẫn kiểm tra batch Open/Closed. |
| 409 | FinalSubmission.VersionConflict / Locked / Legacy | Stale xmin/package; AdvisorConfirmed chặn nộp mới, ArchiveConfirmed khóa; legacy không đủ proof/không được suy diễn xác nhận. |
| 409 | ScoreSummary.ConfigurationInvalid / UnsupportedSource / AmbiguousSource / MissingRequiredSource / NoPositiveWeight | Thiếu/invalid nguồn hoặc config, công thức không xác định. Không trả kết quả một phần hoặc fallback. |
| 409 | ScoreSummary.NotEligible / Stale / VersionConflict / AlreadyPublished / Legacy | Defense/archive chưa đủ; snapshot/xmin không khớp; không ghi đè Published. |
| 409 | IdempotencyConflict / ConcurrencyConflict dưới prefix owner | Cùng key khác ý định; timeout/deadlock/churn. Không để một nhóm đã Published khi selection thất bại. |
| 403 | ScoreSummary.NotYetPublished / ArchivalNotConfirmed | Student own group chưa đủ quyền xem kết quả; không đính kèm điểm trong error/diagnostics. |
| 413 / 415 | Host/body/content-type policy | Cap riêng upload 40 MiB+256 KiB và từng file 20 MiB; auth trước parse body. |

Các code rút gọn cùng hàng được thêm prefix `FinalSubmission.` hoặc `ScoreSummary.` đúng owner. Bulk publish failure có danh sách groupId/error codes chỉ sau authorization quản trị; không có điểm hoặc nội dung snapshot trong error. Permission seed ở migration tương ứng: Faculty có `thesis.final-submissions.read/manage` và `councils.score-summaries.read/manage/publish`; AcademicAffairs chỉ có hai permission read; Admin có tất cả. Ký hiệu dấu `/` là các permission cùng prefix riêng biệt. Giữ custom grants/metadata/assigned_at đã có, không tạo thêm permission archive.

## 7. Legacy, migration và nghiệm thu CP0

- Migration thêm is_legacy default true cho row có sẵn; factory workflow mới đặt false. Không tạo package/review/revision giả từ các cột metadata/confirmation/score cũ. Legacy ArchiveConfirmed hoặc PublishedToStudent không đủ snapshot không được public endpoints mới phát hành; quản trị vẫn đọc bản gốc với nhãn legacy.
- Preflight group/file/enum/FK/unique bất thường phải fail với thông tin vị trí, không xóa hoặc chọn một row trùng. Nếu có row NotSubmitted trống thì API gửi expected xmin khi tạo package đầu tiên; nếu chuyển legacy chưa xác nhận sang workflow, phải validate fresh guards/full package và lưu bản snapshot legacy trước khi thay projection. Legacy đã ArchiveConfirmed giữ immutable, không tự reset để nộp lại; sửa dữ liệu kiểu này là quy trình migration riêng.
- Snapshot legacy aggregate lưu trong cột `legacy_snapshot jsonb` bổ sung trên final_submission/score_summary trước khi workflow mới ghi projection; giữ nguyên metadata/confirmation/score/components cũ, không giả một review hoặc revision hợp lệ. Workflow mới không tính từ legacy_snapshot.
- Down phải từ chối khi còn package/review/revision/receipt/attempt liên quan workflow hoặc giá trị không thể biểu diễn lại kiểu cũ. Đặc biệt numeric raw component không được round về scale 4 khi downgrade. Fresh/legacy roundtrip và transaction rollback là test bắt buộc CP1/CP3.
- Interface/schema ở tài liệu này được triển khai theo CP, không coi code block là source đã tồn tại. Task khóa biên bản là phụ thuộc thật: seed Locked trong container chỉ chứng minh consumer, không có API production tạo Locked giả để qua điều kiện.
- CP0 kiểm tra JSON examples, links/anchors cần thiết, FS-01–14/SP-01–18 duy nhất, Mermaid fences, consistency schema/proof/selector/lock order, whitespace/diff và Gitleaks candidate. Không chạy build/test ứng dụng để chứng minh những API/migration chưa có.
