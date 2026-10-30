# Mô hình dữ liệu UGTM

Đối chiếu source ngày 29/10/2026. Nghiệp vụ theo [Requirements.md](Requirements.md); cấu trúc dưới đây lấy từ metadata mô hình EF Core, không suy diễn từ sơ đồ kiến trúc cũ trong Word.

Phạm vi SCRUM-102/103 được đồng bộ ngày 29/10/2026. SCRUM-104 (`ReviewerAssignment`, receipt idempotency và FK phiếu phản biện) được đồng bộ ngày 30/10/2026 theo migration `20261030022105_AddReviewerAssignmentWorkflowFoundation`; xem [nghiệm thu phân công](ApiContract/ReviewerAssignmentVerification.md). Các thống kê tổng số bảng/FK dưới đây giữ mốc đối chiếu cũ, không bao gồm các bảng kỹ thuật outbox/receipt mới; không dùng chúng làm tổng số hiện tại của database. Xem thêm [SCRUM-102](ApiContract/FinalReportSubmissionVerification.md) và [SCRUM-103](ApiContract/AdvisorEvaluationApprovalVerification.md).

Phạm vi SCRUM-105 CP1 được đồng bộ ngày 30/10/2026: lifecycle/metadata/xmin của ReviewerEvaluation và receipt; các HTTP endpoint còn ở checkpoint sau.

## 1. Nguồn chuẩn

Sáu DbContext runtime, mỗi context chỉ có entity/schema của module mình; [UGTMDbContext](../backend/Database.Migrations/UGTMDbContext.cs) chỉ tổng hợp mô hình để tạo migration và không được đăng ký DI runtime. Thống kê lịch sử trước các bảng kỹ thuật outbox/receipt SCRUM-103/104: **41 bảng nghiệp vụ, 6 schema, 55 FK vật lý**. Quy ước snake_case; enum lưu string `varchar(50)`, không phải PostgreSQL enum. Guid dùng cho phần lớn ID; Role/Permission dùng int; UserRole/RolePermission dùng khóa ghép. Bảng lịch sử `public.__ef_migrations_history` không tính trong 41 bảng nghiệp vụ; public là schema kỹ thuật ngoài 6 schema nghiệp vụ.

Nguồn thực thi: [EF configurations tại từng module](../backend/Modules), [migrations](../backend/Database.Migrations/Migrations), [snapshot](../backend/Database.Migrations/Migrations/UGTMDbContextModelSnapshot.cs). [DatabaseScript.txt](DatabaseScript.txt) là DBML đồng bộ theo mô hình này để xem bằng dbdiagram; không chứa comment dư thừa và không phải SQL để update Neon. PK, nullability, kiểu PostgreSQL, SQL default, index, check và FK đều được lấy từ EF metadata. Seed dữ liệu và chi tiết provider vẫn xem migration. Cú pháp theo [DBML chính thức](https://dbml.dbdiagram.io/docs/).

[Sơ đồ schema](../backend/docs/Architecture/PlantUML/MultiSchema_Database.puml); [quyền sở hữu bảng/module và hướng triển khai](../backend/docs/Architecture/Architecture.md#6-migrations-và-neon). Nhóm sinh viên nằm ở thesis; identity giữ audit quản trị; notification có schema notifications riêng. Schema shared đã được loại bỏ. Sáu schema được tổ chức theo chủ sở hữu dữ liệu, không dùng số lượng schema để chứng minh bounded context DDD.

| Schema | Số bảng (mốc cũ) | Entity |
| --- | --- | --- |
| identity | 10 | AuditLog, AuthenticationSession, IdentityOutboxMessage, OneTimeChallenge, Permission, RefreshToken, Role, RolePermission, User, UserRole |
| academic | 7 | AuditOutboxMessage, KltnBatch, ScheduleTemplateVersion, ScoreComponentConfig, ScoreWeightConfig, Stage, StageTemplateDefinition |
| topics | 1 | Topic |
| thesis | 10 | FinalReport, FinalSubmission, MidtermReport, ProgressMilestone, ProgressReport, RenameRequest, StudentGroup, StudentGroupMember, SubmissionFile, Syllabus |
| councils | 12 | AdvisorEvaluation, Council, CouncilAssignment, CouncilMember, CouncilMinute, CouncilScore, EvaluationDetail, GradingCriteria, ReviewerAssignment, ReviewerEvaluation, ScoreSummary, ScoreSummaryComponent |
| notifications | 1 | Notification |

## 2. Thay đổi đáp ứng dữ liệu final

- `ScoreComponentConfig` lưu nguồn/đầu điểm, trường hợp áp dụng, trọng số, điểm sàn và điều kiện duyệt; `ScoreWeightConfig` có phương thức tính và phiên bản. Không dùng RoleId để phân biệt GVHD/GVPB. `RoleScoreWeight` cũ đã được loại bằng EncapsulateModulePersistence vì đầu điểm nghiệp vụ được cấu hình qua ScoreComponentConfig, không gắn theo role hệ thống.
- `ScoreSummaryComponent` lưu điểm/trọng số/nguồn và cờ loại; `ScoreSummary` có tổng trọng số, thời điểm tính và snapshot cấu hình. Snapshot cần được use case ghi cùng kết quả, chưa có API lưu tự động.
- Các nguồn GVHD/GVPB/Hội đồng có cờ loại điểm và lý do; không tự loại điểm do cảnh báo sai biệt. Công thức chia 6/7 và loại cả hệ số đã được xây dựng trong Domain; xem [Architecture.md#6-migrations-và-neon](../backend/docs/Architecture/Architecture.md#6-migrations-và-neon).
- Giữa kỳ dùng AdvisorFeedback, kết luận tiếp tục/dừng, StopReason. `faculty_feedback` cũ được rename sang `advisor_feedback` để giữ dữ liệu; policy GVHD cần triển khai.
- SCRUM-104 chỉ cho một phân công phản biện active/nhóm, AssignmentOrder mới luôn 1. Giữ cột này cho dữ liệu legacy và task phản biện thứ hai về sau; không cho tạo phản biện thứ hai qua API hiện tại. RevisionFeedback vẫn thuộc mô hình phiếu điểm của task tiếp theo.
- Lưu chiểu có metadata Việt/Anh, phiên bản, GVHD xác nhận và phản hồi Khoa; biên bản có người cấp mở khóa.
- `AdvisorEvaluation` mới luôn gắn bằng snapshot với `FinalReport`, phiên bản, `SubmissionFile` và checksum đã khóa. Các giá trị này nằm ở schema DefenseCouncil nhưng được kiểm tra chéo trong transaction với ThesisManagement; không thêm FK xuyên module. Bản ghi cũ không đủ binding mang cờ `IsLegacy`, không được dùng cho workflow SCRUM-103.
- Migration seed 6 role hệ thống; không seed tài khoản hoặc mật khẩu. Không thay đổi provider/database bằng việc sửa docs.

## 3. Ràng buộc và giới hạn

Unique `(StudentId, BatchId)` của StudentGroupMember ngăn sinh viên ở hai nhóm cùng đợt. Unique `(GroupId, StudentId)` ngăn thành viên lặp. Unique `(CouncilAssignmentId, CouncilMemberId)` ngăn hai phiếu Hội đồng của cùng người cho cùng phân công. ReviewerAssignment dùng partial unique `GroupId WHERE is_active = true`; hai unique lịch sử `(GroupId, AssignmentOrder)`/`(GroupId, ReviewerId)` đã được bỏ để cho phép hủy rồi tạo lại cùng giảng viên/thứ tự bằng dòng mới.

AcademicPeriods bảo đảm một đợt cho mỗi `(AcademicYear, Semester)`, đúng bảy stage liên tục và deadline UTC sinh từ lịch mẫu theo `Asia/Ho_Chi_Minh`. Lịch mẫu và cấu hình điểm đều có version bất biến sau khi publish; partial unique index chỉ cho phép một `Active` và một `Draft` phù hợp. `xmin` bảo vệ cập nhật đồng thời của batch/cấu hình điểm, còn audit outbox được ghi cùng transaction nghiệp vụ. Topic.StudentNumber có check 1–2 khi có giá trị. Aggregate StudentGroup giới hạn 2 thành viên, không trùng và cùng đợt qua AddMember; đăng ký đồng thời, chỉ tiêu đề tài và thời hạn cần Application kiểm soát trong transaction. Các phiếu điểm có check 0–10; precision trong DB không thay quy định nhập một chữ số thập phân ở API.

Những phần phải thiết kế tiếp trước API:

| Khoảng thiếu | Đề xuất xử lý |
| --- | --- |
| SubmissionFile.EntityId không có FK tới hồ sơ đa hình | Chọn gốc hồ sơ chung hoặc liên kết riêng theo loại; kiểm tra chủ sở hữu và phiên bản |
| EvaluationDetail.EvaluationId chưa có FK tới phiếu | Mô hình liên kết rõ các loại phiếu; không gắn giả vào riêng một loại |
| Quyết định loại điểm mới có cờ/lý do | Lưu người/thời điểm/bằng chứng kết luận và audit; kiểm tra quyền tại use case |
| Mở khóa chưa có thời hạn/phạm vi | Chốt quy tắc cấp quyền và khóa lại, rồi bổ sung mô hình/policy phù hợp |
| Điểm khi có phản biện thứ hai chưa có quy tắc chọn | Nhóm chốt cách kết luận; không mặc định lấy điểm cuối hay trung bình |
| Deadline, khóa điểm, lưu chiểu/công bố chưa có handler | Thực thi ở Application với transaction và policy; entity/enum không tự bảo đảm |

## 4. Từ điển bảng hiện tại

Tên thuộc tính là C#; tên cột và kiểu là PostgreSQL lấy từ EF. Default dưới đây là **SQL default**, khác với giá trị khởi tạo C#. Navigation không phải cột. Runtime model chỉ có navigation nội bộ module; bảng FK ở mục 5 lấy từ mô hình migration tổng hợp, kể cả FK liên module cấu hình qua ID. Enum là string; nullable theo mô hình EF, kể cả thuộc tính tham chiếu.

### academic.audit_outbox_message — AuditOutboxMessage

Audit sự kiện tích hợp của AcademicPeriods; worker chuyển sang `identity.audit_log` theo cơ chế at-least-once.

| Thuộc tính | Cột | Kiểu PostgreSQL | Nullable |
| --- | --- | --- | --- |
| Id | id | `uuid` | Không |
| Action | action | `character varying(100)` | Không |
| AttemptCount | attempt_count | `integer` | Không |
| CreatedAt | created_at | `timestamp with time zone` | Không |
| LastError | last_error | `character varying(1000)` | Có |
| NextAttemptAt | next_attempt_at | `timestamp with time zone` | Không |
| PayloadJson | payload_json | `jsonb` | Không |
| ProcessedAt | processed_at | `timestamp with time zone` | Có |

PK: `id`. Index: `ix_academic_audit_outbox_pending` (`processed_at`, `next_attempt_at`).

### academic.kltn_batch — KltnBatch

[Entity](../backend/Modules/AcademicPeriods/Domain/Entities/KltnBatch.cs) · [EF configuration](../backend/Modules/AcademicPeriods/Infrastructure/Persistence/Configurations/KltnBatchConfiguration.cs).

| Thuộc tính | Cột | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- |
| Id | id | `uuid` | Không | — |
| AcademicYear | academic_year | `character varying(20)` | Không | — |
| BatchName | batch_name | `character varying(100)` | Không | — |
| CreatedAt / CreatedBy | created_at / created_by | `timestamp with time zone` / `uuid` | Không / Có | — |
| EndDate / StartDate | end_date / start_date | `date` | Không | — |
| ScheduleTemplateVersionId | schedule_template_version_id | `uuid` | Có | — |
| Semester | semester | `integer` | Không | — |
| Status | status | `character varying(50)` | Không | `'Draft'` |
| TimeZoneId | time_zone_id | `character varying(100)` | Không | `'Asia/Ho_Chi_Minh'` |
| UpdatedAt / UpdatedBy | updated_at / updated_by | `timestamp with time zone` / `uuid` | Có | — |
| Version | xmin | `xid` | Không | PostgreSQL quản lý |

PK: `id`. Unique: `ix_kltn_batch_academic_year_semester` (`academic_year`, `semester`). Concurrency token: `xmin`.

Check: `ck_batch_dates`: `end_date > start_date`; `ck_batch_semester`: `semester > 0`.

Navigation: `ScheduleTemplateVersion` → ScheduleTemplateVersion; `ScoreConfigurations` → ScoreWeightConfig[]; `Stages` → Stage[].

### academic.schedule_template_version — ScheduleTemplateVersion

| Cột | Kiểu PostgreSQL | Nullable |
| --- | --- | --- |
| id | `uuid` | Không |
| template_code | `character varying(100)` | Không |
| version_no | `integer` | Không |
| name | `character varying(200)` | Không |
| time_zone_id | `character varying(100)` | Không |
| status | `character varying(50)` | Không |
| created_by / published_by | `uuid` | Có |
| created_at | `timestamp with time zone` | Không |
| published_at / retired_at | `timestamp with time zone` | Có |

PK: `id`. Unique `(template_code, version_no)`; partial unique `template_code WHERE status = 'Active'`. Seed hiện tại là `UIT_KLTN_STANDARD` version 1.

### academic.score_component_config — ScoreComponentConfig

[Entity](../backend/Modules/AcademicPeriods/Domain/Entities/ScoreComponentConfig.cs) · [EF configuration](../backend/Modules/AcademicPeriods/Infrastructure/Persistence/Configurations/ScoreComponentConfigConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| Applicability | applicability | `ScoreComponentApplicability` | `character varying(50)` | Không | — |
| ComponentCode | component_code | `String` | `character varying(100)` | Không | — |
| ComponentName | component_name | `String` | `character varying(200)` | Không | — |
| ConfigId | config_id | `Guid` | `uuid` | Không | — |
| IsRequired | is_required | `Boolean` | `boolean` | Không | — |
| MinimumScore | minimum_score | `Decimal?` | `numeric(4,2)` | Có | — |
| OrderIndex | order_index | `Int32` | `integer` | Không | — |
| RequiresApproval | requires_approval | `Boolean` | `boolean` | Không | — |
| Source | source | `ScoreComponentSource` | `character varying(50)` | Không | — |
| Weight | weight | `Decimal` | `numeric(9,4)` | Không | — |

PK: `id`.

Index unique: `(config_id, component_code)` và `(config_id, order_index)`.

Check: điểm sàn 0–10 hoặc null, trọng số không âm, thứ tự lớn hơn 0.

Navigation: `Config` → ScoreWeightConfig.

### academic.score_weight_config — ScoreWeightConfig

[Entity](../backend/Modules/AcademicPeriods/Domain/Entities/ScoreWeightConfig.cs) · [EF configuration](../backend/Modules/AcademicPeriods/Infrastructure/Persistence/Configurations/ScoreWeightConfigConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| CalculationMethod | calculation_method | `ScoreCalculationMethod` | `character varying(50)` | Không | 'IntegerCoefficient' |
| ChangeReason | change_reason | `String` | `character varying(1000)` | Có | — |
| CreatedAt / CreatedBy | created_at / created_by | — | — | — |
| PublishedAt / PublishedBy | published_at / published_by | — | — | — |
| RetiredAt / UpdatedAt | retired_at / updated_at | — | — | — |
| Status | status | `ConfigurationVersionStatus` | `character varying(50)` | Không | 'Draft' |
| Version | xmin | `UInt32` | `xid` | Không | PostgreSQL quản lý |
| VersionNo | version_no | `Int32` | `integer` | Không | 1 |

PK: `id`.

Index unique: `(batch_id, version_no)`; partial unique `batch_id WHERE status = 'Active'`; partial unique `batch_id WHERE status = 'Draft'`. Concurrency token: `xmin`.

Navigation: `Batch` → KltnBatch; `Components` → ScoreComponentConfig[].

### academic.stage — Stage

[Entity](../backend/Modules/AcademicPeriods/Domain/Entities/Stage.cs) · [EF configuration](../backend/Modules/AcademicPeriods/Infrastructure/Persistence/Configurations/StageConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| DeadlineAtUtc | deadline_at_utc | `DateTime` | `timestamp with time zone` | Không | — |
| EndDate | end_date | `DateOnly` | `date` | Không | — |
| IsOverridden | is_overridden | `Boolean` | `boolean` | Không | false |
| OrderIndex | order_index | `Int32` | `integer` | Không | — |
| StageCode | stage_code | `String` | `character varying(100)` | Không | — |
| StageName | stage_name | `String` | `character varying(150)` | Không | — |
| StartDate | start_date | `DateOnly` | `date` | Không | — |

PK: `id`.

Index unique: `(batch_id, order_index)` và `(batch_id, stage_code)`.

Check: `order_index BETWEEN 1 AND 7`; `end_date >= start_date`.

Navigation: `Batch` → KltnBatch.

### academic.stage_template_definition — StageTemplateDefinition

| Cột | Kiểu PostgreSQL | Nullable |
| --- | --- | --- |
| id | `uuid` | Không |
| template_version_id | `uuid` | Không |
| stage_code | `character varying(100)` | Không |
| stage_name | `character varying(150)` | Không |
| order_index | `integer` | Không |
| start_offset_days / end_offset_days | `integer` | Không |
| default_deadline_local_time | `time without time zone` | Không |
| deadline_adjustment_policy | `character varying(50)` | Không |

PK: `id`. Unique: `(template_version_id, order_index)` và `(template_version_id, stage_code)`. Check: thứ tự 1–7, offset bắt đầu không âm và kết thúc không nhỏ hơn bắt đầu.

### councils.advisor_evaluation — AdvisorEvaluation

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/AdvisorEvaluation.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/AdvisorEvaluationConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AllowsReviewerAssignment | allows_reviewer_assignment | `Boolean?` | `boolean` | Có | — |
| Comment | comment | `String` | `text` | Có | — |
| DecisionAt | decision_at | `DateTime?` | `timestamp with time zone` | Có | — |
| DecisionBy | decision_by | `Guid?` | `uuid` | Có | — |
| EvaluatedAt | evaluated_at | `DateTime` | `timestamp with time zone` | Không | — |
| EvaluatedBy | evaluated_by | `Guid?` | `uuid` | Có | — |
| ExclusionReason | exclusion_reason | `String` | `text` | Có | — |
| FinalReportId | final_report_id | `Guid?` | `uuid` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| IsIgnoredInCalculation | is_ignored_in_calculation | `Boolean` | `boolean` | Không | false |
| IsLegacy | is_legacy | `Boolean` | `boolean` | Không | true |
| RejectionReason | rejection_reason | `String` | `text` | Có | — |
| ReportVersionNo | report_version_no | `Int32?` | `integer` | Có | — |
| Score | score | `Decimal` | `numeric(4,2)` | Không | — |
| SubmissionChecksumSha256 | submission_checksum_sha256 | `String` | `character varying(64)` | Có | — |
| SubmissionFileId | submission_file_id | `Guid?` | `uuid` | Có | — |
| Version | xmin | `UInt32` | `xid` | Không | PostgreSQL quản lý |

PK: `id`.

Index: `ix_advisor_evaluation_group_id` (group_id) unique; `ix_advisor_evaluation_final_report_id` (final_report_id); `ix_advisor_evaluation_submission_file_id` (submission_file_id); `ix_advisor_evaluation_evaluated_by` (evaluated_by); `ix_advisor_evaluation_decision_by` (decision_by).

Check: `ck_advisor_evaluation_score`: `score BETWEEN 0 AND 10`. Với `is_legacy = false`, `ck_advisor_evaluation_workflow_binding` bắt buộc final report, số phiên bản dương, submission file, SHA-256 64 ký tự, actor chấm và nhận xét đã trim 1–2.000 ký tự. `ck_advisor_evaluation_reviewer_assignment_decision` buộc quyết định chưa đồng ý có lý do trim 1–1.000 ký tự, actor/thời điểm quyết định; quyết định đồng ý không có lý do. `Version` là concurrency token `xmin`; `IsLocked` là thuộc tính suy ra từ `!IsLegacy && AllowsReviewerAssignment == true`, không phải cột.

### councils.council — Council

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/Council.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/CouncilConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| ApprovedAt | approved_at | `DateTime?` | `timestamp with time zone` | Có | — |
| ApprovedBy | approved_by | `Guid?` | `uuid` | Có | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| CouncilCode | council_code | `String` | `character varying(50)` | Không | — |
| DecisionFileUrl | decision_file_url | `String` | `character varying(500)` | Có | — |
| DecisionNumber | decision_number | `String` | `character varying(100)` | Có | — |
| DefenseDate | defense_date | `DateOnly` | `date` | Không | — |
| Room | room | `String` | `character varying(50)` | Không | — |
| SignedDate | signed_date | `DateOnly?` | `date` | Có | — |
| Status | status | `CouncilStatus` | `character varying(50)` | Không | 'Draft' |
| TimeSlot | time_slot | `String` | `character varying(50)` | Không | — |

PK: `id`.

Index: `ix_council_approved_by` (approved_by); `ix_council_batch_id` (batch_id).

Navigation: `Assignments` → CouncilAssignment[]; `Members` → CouncilMember[].

### councils.council_assignment — CouncilAssignment

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/CouncilAssignment.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/CouncilAssignmentConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CouncilId | council_id | `Guid` | `uuid` | Không | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| OrderNumber | order_number | `Int32` | `integer` | Không | 1 |

PK: `id`.

Index: `ix_council_assignment_council_id` (council_id); `ix_council_assignment_group_id` (group_id) unique.

Navigation: `Council` → Council; `Minute` → CouncilMinute; `Scores` → CouncilScore[].

### councils.council_member — CouncilMember

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/CouncilMember.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/CouncilMemberConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CouncilId | council_id | `Guid` | `uuid` | Không | — |
| RoleInCouncil | role_in_council | `CouncilMemberRole` | `character varying(50)` | Không | — |
| UserId | user_id | `Guid` | `uuid` | Không | — |

PK: `id`.

Index: `ix_council_member_user_id` (user_id); `ix_council_member_council_id_user_id` (council_id, user_id) unique.

Navigation: `Council` → Council; `Scores` → CouncilScore[].

### councils.council_minute — CouncilMinute

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/CouncilMinute.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/CouncilMinuteConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CouncilAssignmentId | council_assignment_id | `Guid` | `uuid` | Không | — |
| GeneralFeedback | general_feedback | `String` | `text` | Có | — |
| LockStatus | lock_status | `CouncilMinuteLockStatus` | `character varying(50)` | Không | 'Open' |
| LockedAt | locked_at | `DateTime?` | `timestamp with time zone` | Có | — |
| LockedBy | locked_by | `Guid?` | `uuid` | Có | — |
| OverrideReason | override_reason | `String` | `text` | Có | — |
| RequiredRevisions | required_revisions | `String` | `text` | Có | — |
| UnlockedAt | unlocked_at | `DateTime?` | `timestamp with time zone` | Có | — |
| UnlockedBy | unlocked_by | `Guid?` | `uuid` | Có | — |

PK: `id`.

Index: `ix_council_minute_council_assignment_id` (council_assignment_id) unique; `ix_council_minute_locked_by` (locked_by); `ix_council_minute_unlocked_by` (unlocked_by).

Navigation: `Assignment` → CouncilAssignment.

### councils.council_score — CouncilScore

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/CouncilScore.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/CouncilScoreConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CouncilAssignmentId | council_assignment_id | `Guid` | `uuid` | Không | — |
| CouncilMemberId | council_member_id | `Guid` | `uuid` | Không | — |
| EvaluatedAt | evaluated_at | `DateTime` | `timestamp with time zone` | Không | — |
| ExclusionReason | exclusion_reason | `String` | `text` | Có | — |
| Feedback | feedback | `String` | `text` | Có | — |
| IsIgnoredInCalculation | is_ignored_in_calculation | `Boolean` | `boolean` | Không | false |
| Score | score | `Decimal` | `numeric(4,2)` | Không | — |

PK: `id`.

Index: `ix_council_score_council_member_id` (council_member_id); `ix_council_score_council_assignment_id_council_member_id` (council_assignment_id, council_member_id) unique.

Check: `ck_council_score_score`: `score BETWEEN 0 AND 10`.

Navigation: `Assignment` → CouncilAssignment; `CouncilMember` → CouncilMember.

### councils.evaluation_detail — EvaluationDetail

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/EvaluationDetail.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/EvaluationDetailConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CriteriaId | criteria_id | `Guid` | `uuid` | Không | — |
| EvaluationId | evaluation_id | `Guid` | `uuid` | Không | — |
| Score | score | `Decimal` | `numeric(4,2)` | Không | — |

PK: `id`.

Index: `ix_evaluation_detail_criteria_id_evaluation_id` (criteria_id, evaluation_id) unique.

Navigation: `Criteria` → GradingCriteria.

### councils.grading_criteria — GradingCriteria

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/GradingCriteria.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/GradingCriteriaConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| CriteriaCode | criteria_code | `String` | `character varying(20)` | Không | — |
| CriteriaName | criteria_name | `String` | `character varying(250)` | Không | — |
| MaxScore | max_score | `Decimal` | `numeric(4,2)` | Không | — |
| OrderIndex | order_index | `Int32` | `integer` | Không | 1 |

PK: `id`.

Index: `ix_grading_criteria_batch_id_criteria_code` (batch_id, criteria_code) unique.

### councils.reviewer_assignment — ReviewerAssignment

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/ReviewerAssignment.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/ReviewerAssignmentConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AssignedAt | assigned_at | `DateTime` | `timestamp with time zone` | Không | — |
| AssignedBy | assigned_by | `Guid` | `uuid` | Không | — |
| AssignmentOrder | assignment_order | `Int32` | `integer` | Không | 1 |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| IsActive | is_active | `Boolean` | `boolean` | Không | true |
| ReviewerId | reviewer_id | `Guid` | `uuid` | Không | — |
| AdvisorIdAtAssignment | advisor_id_at_assignment | `Guid?` | `uuid` | Có | — |
| AdvisorEvaluationId | advisor_evaluation_id | `Guid?` | `uuid` | Có | — |
| FinalReportId | final_report_id | `Guid?` | `uuid` | Có | — |
| ReportVersionNo | report_version_no | `Int32?` | `integer` | Có | — |
| SubmissionFileId | submission_file_id | `Guid?` | `uuid` | Có | — |
| SubmissionChecksumSha256 | submission_checksum_sha256 | `String?` | `character varying(64)` | Có | — |
| IsLegacy | is_legacy | `Boolean` | `boolean` | Không | true |
| CancelledBy | cancelled_by | `Guid?` | `uuid` | Có | — |
| CancelledAt | cancelled_at | `DateTime?` | `timestamp with time zone` | Có | — |
| CancellationReason | cancellation_reason | `String?` | `text` | Có | — |
| Version | xmin | `UInt32` | `xid` (system column) | Không | PostgreSQL |

PK: `id`.

Index: `ix_reviewer_assignment_assigned_by` (assigned_by); `ix_reviewer_assignment_reviewer_id` (reviewer_id); `ix_reviewer_assignment_group_id_assigned_at` (group_id, assigned_at); **`ux_reviewer_assignment_active_group` (group_id) unique WHERE is_active = true**.

`ck_reviewer_assignment_workflow_binding`: dòng mới (`!IsLegacy`) bắt buộc các UUID không rỗng, snapshot đầy đủ, version dương, checksum 64 hex thường, ReviewerId khác AdvisorIdAtAssignment và AssignmentOrder = 1. `ck_reviewer_assignment_cancellation`: active không có metadata hủy; inactive mới phải có actor/time (không trước AssignedAt), reason đã trim 1–1000 ký tự. Legacy inactive được giữ nguyên metadata null; không bịa binding/actor. `xmin` bảo vệ optimistic concurrency. Snapshot không thêm FK xuyên module; các FK cũ group/reviewer/assigned_by vẫn giữ. Migration preflight từ chối dữ liệu nhiều active hoặc phiếu mồ côi, không tự xóa/chọn dòng.

DELETE nghiệp vụ là hủy mềm. Application khóa parent và kiểm tra bất kỳ phiếu chấm nào trước hủy; writer chấm phải dùng `LockForEvaluationAsync` trong coordinator tới commit. DB CHECK/FK không thay cho giao thức khóa này. Không chuyển nhóm khỏi ReadyForReviewerAssignment, không mở lại báo cáo.

Navigation: `Evaluation` → ReviewerEvaluation.

### councils.reviewer_assignment_idempotency — ReviewerAssignmentIdempotencyRecord

| Thuộc tính | Cột | Kiểu PostgreSQL | Nullable |
| --- | --- | --- | --- |
| Id | id | uuid (PK) | Không |
| ActorId | actor_id | uuid | Không |
| Operation | operation | character varying(100) | Không |
| IdempotencyKey | idempotency_key | uuid | Không |
| RequestFingerprint | request_fingerprint | character varying(64) | Không |
| ResponseStatusCode | response_status_code | integer | Không |
| ResponsePayloadJson | response_payload_json | jsonb | Không |
| CreatedAtUtc | created_at_utc | timestamp with time zone | Không |

Unique `ux_reviewer_assignment_idempotency_actor_operation_key` trên (actor_id, operation, idempotency_key). CHECK status IN (200,201), fingerprint 64 hex thường, operation trim 1–100. Không có FK receipt. Chỉ lưu receipt thành công cùng assignment/audit; sau save lấy xmin rồi hoàn thiện JSON trước commit. Replay không chạy lại mutation hoặc ghi thêm audit. Operations `ReviewerAssignment.Create`/`ReviewerAssignment.Cancel` có scope riêng, gắn actor thực tế.

### councils.reviewer_evaluation — ReviewerEvaluation

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/ReviewerEvaluation.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/ReviewerEvaluationConfiguration.cs). SCRUM-105 CP1, migration `20261030113753_AddReviewerEvaluationWorkflowFoundation`; HTTP chấm/gate/feedback chưa triển khai. [Contract đầy đủ](ApiContract/ReviewerEvaluationApiContract.md).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AssignmentId | assignment_id | `Guid` | `uuid` | Không | — |
| Comment | comment | `String?` | `text` | Có | — |
| Score | score | `Decimal?` | `numeric(4,2)` | Có | — |
| DefenseApproval | defense_approval | `DefenseApprovalStatus?` | `character varying(50)` | Có | — |
| Status | status | `ReviewerEvaluationStatus?` | `character varying(50)` | Có (legacy) | — |
| IsLegacy | is_legacy | `Boolean` | `boolean` | Không | true |
| CreatedBy | created_by | `Guid?` | `uuid` | Có (legacy) | — |
| CreatedAtUtc | created_at_utc | `DateTime?` | `timestamp with time zone` | Có (legacy) | — |
| EvaluatedBy | evaluated_by | `Guid?` | `uuid` | Có (legacy) | — |
| EvaluatedAt | evaluated_at | `DateTime` | `timestamp with time zone` | Không | — |
| SubmittedBy | submitted_by | `Guid?` | `uuid` | Có | — |
| SubmittedAtUtc | submitted_at_utc | `DateTime?` | `timestamp with time zone` | Có | — |
| IsVisibleToStudent | is_visible_to_student | `Boolean` | `boolean` | Không | false |
| IsIgnoredInCalculation | is_ignored_in_calculation | `Boolean` | `boolean` | Không | false |
| ExclusionReason | exclusion_reason | `String?` | `text` | Có | — |
| RevisionFeedback | revision_feedback | `String?` | `text` | Có | — |
| Version | xmin | `UInt32` | `xid` (system column) | Không | PostgreSQL |

Unique `ix_reviewer_evaluation_assignment_id`; FK `fk_reviewer_evaluation_reviewer_assignment_assignment_id` đến assignment dùng **Restrict**. Binding report/version/file/checksum đi qua assignment bất biến, không nhân đôi snapshot hoặc thêm FK xuyên module cho actor mới. Mọi phiếu, kể cả Draft/điểm 0/ẩn/bị loại tính điểm, đều chặn hủy phân công.

CHECK score 0–10 khi có giá trị; status null/Draft/Submitted và decision null/Approved/Rejected. `ck_reviewer_evaluation_workflow` buộc dòng mới có actor/time/UUID đầy đủ, created ≤ evaluated, comment trim/rune tối đa 2.000. Draft cho phép thiếu nội dung và visibility=false/submission metadata null. Submitted phải đủ score/comment/decision, submitted actor/time bằng lần lưu cuối và visibility=true. `IsLocked` suy ra từ !IsLegacy && Status=Submitted, không phải cột; không có API mở khóa.

CreatedBy/At giữ nguyên khi sửa, EvaluatedBy/At là người/lúc lưu gần nhất, SubmittedBy/At là người/lúc chốt. Comment là góp ý công khai sau Submit; RevisionFeedback giữ dữ liệu cũ, không fallback để xuất góp ý. Cờ loại điểm không được sửa qua luồng chấm. Domain trim Unicode/đếm rune, làm tròn AwayFromZero hai số sau khi kiểm tra khoảng; DB btrim/char_length bảo vệ persistence. `xmin` ngăn lost update; parent assignment lock serialize với hủy.

Legacy giữ nguyên điểm/nhận xét/cờ công khai/cờ loại/thời gian cũ, IsLegacy=true và metadata workflow null; không tự chốt/công khai qua API mới. Down preflight chặn workflow rows/receipt/null score-decision; không xóa phiếu hoặc điền 0 để downgrade. Permission read seed giữ custom metadata/grants cả khi rollback/reapply.

### councils.reviewer_evaluation_idempotency — ReviewerEvaluationIdempotencyRecord

| Thuộc tính | Cột | Kiểu PostgreSQL | Nullable |
| --- | --- | --- | --- |
| Id | id | uuid (PK) | Không |
| ActorId | actor_id | uuid | Không |
| Operation | operation | character varying(100) | Không |
| IdempotencyKey | idempotency_key | uuid | Không |
| RequestFingerprint | request_fingerprint | character varying(64) | Không |
| ResponseStatusCode | response_status_code | integer | Không |
| ResponsePayloadJson | response_payload_json | jsonb | Không |
| CreatedAtUtc | created_at_utc | timestamp with time zone | Không |

Unique `ux_reviewer_evaluation_idempotency_actor_operation_key` trên (actor_id, operation, idempotency_key). CHECK success status IN (200,201), fingerprint SHA-256 64 hex thường và operation trim 1–100 ký tự. Không có FK cascade receipt. Phiếu/receipt/audit cùng transaction; save đầu lấy xmin rồi hoàn thiện receipt/audit trước commit. Payload không có gate động; resolver dùng cấu hình Active hiện tại, không persist ReadyForDefense vào phiếu hoặc StudentGroup.
### councils.score_summary — ScoreSummary

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/ScoreSummary.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/ScoreSummaryConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AdvisorScore | advisor_score | `Decimal?` | `numeric(4,2)` | Có | — |
| CalculatedAt | calculated_at | `DateTime?` | `timestamp with time zone` | Có | — |
| CalculationConfigSnapshot | calculation_config_snapshot | `String` | `jsonb` | Có | — |
| CalculationFormula | calculation_formula | `String` | `character varying(200)` | Có | — |
| ConfigVersionNo | config_version_no | `Int32?` | `integer` | Có | — |
| CouncilScore | council_score | `Decimal?` | `numeric(4,2)` | Có | — |
| FinalScore | final_score | `Decimal?` | `numeric(4,2)` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| PublishedAt | published_at | `DateTime?` | `timestamp with time zone` | Có | — |
| PublishedBy | published_by | `Guid?` | `uuid` | Có | — |
| ReviewerScore | reviewer_score | `Decimal?` | `numeric(4,2)` | Có | — |
| Status | status | `ScoreSummaryStatus` | `character varying(50)` | Không | 'Pending' |
| TotalWeight | total_weight | `Decimal?` | `numeric(12,4)` | Có | — |

PK: `id`.

Index: `ix_score_summary_group_id` (group_id) unique; `ix_score_summary_published_by` (published_by).

Navigation: `Components` → ScoreSummaryComponent[].

### councils.score_summary_component — ScoreSummaryComponent

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/ScoreSummaryComponent.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/ScoreSummaryComponentConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| ComponentCode | component_code | `String` | `character varying(100)` | Không | — |
| ExclusionReason | exclusion_reason | `String` | `text` | Có | — |
| IsIgnoredInCalculation | is_ignored_in_calculation | `Boolean` | `boolean` | Không | — |
| Score | score | `Decimal` | `numeric(6,4)` | Không | — |
| SourceKey | source_key | `String` | `character varying(150)` | Không | — |
| SummaryId | summary_id | `Guid` | `uuid` | Không | — |
| Weight | weight | `Decimal` | `numeric(9,4)` | Không | — |

PK: `id`.

Index: `ix_score_summary_component_summary_id_component_code_source_key` (summary_id, component_code, source_key) unique.

Check: `ck_summary_component_score`: `score BETWEEN 0 AND 10`; `ck_summary_component_weight`: `weight >= 0`.

Navigation: `Summary` → ScoreSummary.

### identity.audit_log — AuditLog

[Entity](../backend/Modules/Identity/Domain/Entities/AuditLog.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Int64` | `bigint` | Không | — |
| ActorSessionId | actor_session_id | `Guid?` | `uuid` | Có | — |
| ActorUserId | actor_user_id | `Guid?` | `uuid` | Có | — |
| Action | action | `String` | `character varying(100)` | Không | — |
| MetadataJson | metadata_json | `String` | `jsonb` | Có | — |
| Outcome | outcome | `String` | `character varying(50)` | Không | — |
| ReasonCode | reason_code | `String` | `character varying(100)` | Có | — |
| RequestId | request_id | `String` | `character varying(100)` | Không | — |
| TargetId | target_id | `String` | `character varying(100)` | Không | — |
| TargetType | target_type | `String` | `character varying(100)` | Không | — |
| Timestamp | timestamp | `DateTime` | `timestamp with time zone` | Không | — |

PK: `id`.

Index: `ix_audit_log_actor_session_id` (actor_session_id); `ix_audit_log_actor_user_id` (actor_user_id); `ix_audit_log_request_id` (request_id); `ix_audit_log_timestamp` (timestamp).

### identity.authentication_session — AuthenticationSession

[Entity](../backend/Modules/Identity/Domain/Entities/AuthenticationSession.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/AuthenticationSessionConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| UserId | user_id | `Guid` | `uuid` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| AuthenticatedAt | authenticated_at | `DateTime` | `timestamp with time zone` | Không | — |
| LastSeenAt | last_seen_at | `DateTime` | `timestamp with time zone` | Không | — |
| IdleExpiresAt | idle_expires_at | `DateTime` | `timestamp with time zone` | Không | — |
| AbsoluteExpiresAt | absolute_expires_at | `DateTime` | `timestamp with time zone` | Không | — |
| SecurityStampSnapshot | security_stamp_snapshot | `Guid` | `uuid` | Không | — |
| RevokedAt | revoked_at | `DateTime?` | `timestamp with time zone` | Có | — |
| RevocationReason | revocation_reason | `SessionRevocationReason?` | `character varying(50)` | Có | — |
| UserAgentSummary | user_agent_summary | `String` | `character varying(300)` | Có | — |
| IpAddressHash | ip_address_hash | `String` | `character varying(128)` | Có | — |
| ConcurrencyStamp | concurrency_stamp | `Guid` | `uuid` | Không | — |

PK: `id`.

Index: `ix_authentication_session_user_id_revoked_at_absolute_expires_` (user_id, revoked_at, absolute_expires_at).

Navigation: `User` → User; `RefreshTokens` → RefreshToken[].

### identity.outbox_message — IdentityOutboxMessage

[Entity](../backend/Modules/Identity/Domain/Entities/IdentityOutboxMessage.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/IdentityOutboxMessageConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| Type | type | `String` | `character varying(100)` | Không | — |
| ProtectedPayload | protected_payload | `String` | `text` | Không | — |
| ProtectionPurpose | protection_purpose | `String` | `character varying(200)` | Không | — |
| SchemaVersion | schema_version | `Int32` | `integer` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| ProcessedAt | processed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| AttemptCount | attempt_count | `Int32` | `integer` | Không | — |
| NextAttemptAt | next_attempt_at | `DateTime` | `timestamp with time zone` | Không | — |
| LastError | last_error | `String` | `character varying(1000)` | Có | — |

PK: `id`.

Index: `ix_outbox_message_processed_at_next_attempt_at` (processed_at, next_attempt_at).

### identity.one_time_challenge — OneTimeChallenge

[Entity](../backend/Modules/Identity/Domain/Entities/OneTimeChallenge.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/OneTimeChallengeConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| UserId | user_id | `Guid` | `uuid` | Không | — |
| Purpose | purpose | `ChallengePurpose` | `character varying(50)` | Không | — |
| Selector | selector | `String` | `character varying(64)` | Không | — |
| SecretHash | secret_hash | `Byte[]` | `bytea` | Không | — |
| DestinationSnapshot | destination_snapshot | `String` | `character varying(150)` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| ExpiresAt | expires_at | `DateTime` | `timestamp with time zone` | Không | — |
| ConsumedAt | consumed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| InvalidatedAt | invalidated_at | `DateTime?` | `timestamp with time zone` | Có | — |
| AttemptCount | attempt_count | `Int32` | `integer` | Không | — |
| MaxAttempts | max_attempts | `Int32` | `integer` | Không | — |
| LastSentAt | last_sent_at | `DateTime?` | `timestamp with time zone` | Có | — |
| SendCount | send_count | `Int32` | `integer` | Không | — |

PK: `id`.

Index: `ix_one_time_challenge_selector` (selector) unique; `ix_one_time_challenge_user_id_purpose_expires_at` (user_id, purpose, expires_at).

Navigation: `User` → User.

### identity.permission — Permission

[Entity](../backend/Modules/Identity/Domain/Entities/Permission.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/PermissionConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Int32` | `integer` | Không | — |
| Module | module | `String` | `character varying(50)` | Có | — |
| PermissionCode | permission_code | `String` | `character varying(100)` | Không | — |
| PermissionName | permission_name | `String` | `character varying(150)` | Có | — |

PK: `id`.

Index: `ix_permission_permission_code` (permission_code) unique.

Navigation: `RolePermissions` → RolePermission[].

### identity.refresh_token — RefreshToken

[Entity](../backend/Modules/Identity/Domain/Entities/RefreshToken.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| SessionId | session_id | `Guid` | `uuid` | Không | — |
| Selector | selector | `String` | `character varying(64)` | Không | — |
| ValidatorHash | validator_hash | `Byte[]` | `bytea` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| ExpiresAt | expires_at | `DateTime` | `timestamp with time zone` | Không | — |
| ConsumedAt | consumed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| ReplacedByTokenId | replaced_by_token_id | `Guid?` | `uuid` | Có | — |
| RevokedAt | revoked_at | `DateTime?` | `timestamp with time zone` | Có | — |
| RevocationReason | revocation_reason | `SessionRevocationReason?` | `character varying(50)` | Có | — |

PK: `id`.

Index: `ix_refresh_token_replaced_by_token_id` (replaced_by_token_id); `ix_refresh_token_selector` (selector) unique; `ix_refresh_token_session_id` (session_id).

Navigation: `Session` → AuthenticationSession.

### identity.role — Role

[Entity](../backend/Modules/Identity/Domain/Entities/Role.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/RoleConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Int32` | `integer` | Không | — |
| Description | description | `String` | `character varying(255)` | Có | — |
| RoleName | role_name | `String` | `character varying(50)` | Không | — |

PK: `id`.

Index: `ix_role_role_name` (role_name) unique.

Navigation: `RolePermissions` → RolePermission[]; `UserRoles` → UserRole[].

### identity.role_permission — RolePermission

[Entity](../backend/Modules/Identity/Domain/Entities/RolePermission.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/RolePermissionConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| RoleId | role_id | `Int32` | `integer` | Không | — |
| PermissionId | permission_id | `Int32` | `integer` | Không | — |
| AssignedAt | assigned_at | `DateTime` | `timestamp with time zone` | Không | — |

PK: `role_id, permission_id`.

Index: `ix_role_permission_permission_id` (permission_id).

Navigation: `Permission` → Permission; `Role` → Role.

### identity.user — User

[Entity](../backend/Modules/Identity/Domain/Entities/User.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/UserConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AccessFailedCount | access_failed_count | `Int32` | `integer` | Không | 0 |
| ActivatedAt | activated_at | `DateTime?` | `timestamp with time zone` | Có | — |
| ConcurrencyStamp | concurrency_stamp | `Guid` | `uuid` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| Email | email | `String` | `character varying(150)` | Không | — |
| EmailVerifiedAt | email_verified_at | `DateTime?` | `timestamp with time zone` | Có | — |
| FullName | full_name | `String` | `character varying(150)` | Không | — |
| LastLoginAt | last_login_at | `DateTime?` | `timestamp with time zone` | Có | — |
| LockoutEndAt | lockout_end_at | `DateTime?` | `timestamp with time zone` | Có | — |
| NormalizedEmail | normalized_email | `String` | `character varying(150)` | Không | — |
| PasswordChangedAt | password_changed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| PasswordHash | password_hash | `String` | `text` | Có | — |
| PhoneNumber | phone_number | `String` | `character varying(20)` | Có | — |
| SecurityStamp | security_stamp | `Guid` | `uuid` | Không | — |
| Status | status | `AccountStatus` | `character varying(50)` | Không | — |
| UpdatedAt | updated_at | `DateTime?` | `timestamp with time zone` | Có | — |
| UserCode | user_code | `String` | `character varying(20)` | Có | — |

PK: `id`.

Index: `ux_user_normalized_email` (normalized_email) unique; `ux_user_user_code` (user_code) unique.

Navigation: `UserRoles` → UserRole[].

### identity.user_role — UserRole

[Entity](../backend/Modules/Identity/Domain/Entities/UserRole.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/UserRoleConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| UserId | user_id | `Guid` | `uuid` | Không | — |
| RoleId | role_id | `Int32` | `integer` | Không | — |
| AssignedAt | assigned_at | `DateTime` | `timestamp with time zone` | Không | — |

PK: `user_id, role_id`.

Index: `ix_user_role_role_id` (role_id).

Navigation: `Role` → Role; `User` → User.

### notifications.notification — Notification

[Entity](../backend/Modules/Notifications/Domain/Entities/Notification.cs) · [EF configuration](../backend/Modules/Notifications/Infrastructure/Persistence/Configurations/NotificationConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| Content | content | `String` | `text` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| IsRead | is_read | `Boolean` | `boolean` | Không | false |
| NotificationType | notification_type | `String` | `character varying(50)` | Không | — |
| RecipientId | recipient_id | `Guid` | `uuid` | Không | — |
| Title | title | `String` | `character varying(200)` | Không | — |

PK: `id`.

Index: `ix_notification_recipient_id` (recipient_id).

### thesis.final_report — FinalReport

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/FinalReport.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/FinalReportConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| CurrentVersionNo | current_version_no | `Int32` | `integer` | Không | 1 |
| Status | status | `FinalReportStatus` | `character varying(50)` | Không | 'Submitted' |
| SubmittedAt | submitted_at | `DateTime` | `timestamp with time zone` | Không | — |
| VersionType | version_type | `FinalReportVersionType` | `character varying(50)` | Không | — |
| Version | xmin | `UInt32` | `xid` | Không | PostgreSQL quản lý |

PK: `id`.

Index unique: `ux_final_report_group_id_version_type` (`group_id`, `version_type`). Check: `ck_final_report_current_version_no`: `current_version_no > 0`. `xmin` là system column/concurrency token, không phải cột được tạo/xóa thủ công trong migration.

Navigation: `Group` → StudentGroup.

SCRUM-102 dùng một hồ sơ `InitialFinal` cho mỗi nhóm: `CurrentVersionNo` khởi tạo 1, tăng khi nộp lại; không đổi `VersionType` thành `RevisedAfterReviewer`. `SubmittedAt` là thời điểm UTC của phiên bản hiện tại. File của mỗi lần nộp nằm tại `SubmissionFile` với `EntityType = FinalReport`, `EntityId = FinalReport.Id`, `VersionNo = CurrentVersionNo`; mọi phiên bản cũ được giữ.

`Submitted → UnderEvaluation` chỉ qua thao tác bắt đầu chấm của GVHD hiện tại/Admin với số phiên bản đã xem. Khi `UnderEvaluation`, không cho nộp lại/tăng phiên bản. Không có cột riêng cho người/thời điểm bắt đầu chấm: thông tin này, file ID và checksum được lưu trong audit action `ThesisManagement.FinalReport.EvaluationStarted`. SCRUM-103 chụp các giá trị đó vào `councils.advisor_evaluation` và chỉ khi duyệt đồng ý mới chuyển nhóm `FinalReportSubmitted → ReadyForReviewerAssignment`; không đổi trạng thái `FinalReport` khỏi `UnderEvaluation`. Nộp/nộp lại ghi action `ThesisManagement.FinalReport.Submitted`; trạng thái, metadata và audit outbox lưu cùng transaction. `xmin` của cả nhóm và báo cáo được kiểm tra khi nộp lại/bắt đầu chấm/duyệt. Xem [API contract](ApiContract/ThesisManagementApiContract.md) và [contract phiếu GVHD](ApiContract/AdvisorEvaluationApprovalApiContract.md).

Migration nền tảng: `20261027174826_AddFinalReportSubmissionFoundation`; CP2–CP7 không thêm migration mới. Migration đặt số phiên bản của hồ sơ cũ thành 1, không tự tạo file cho hồ sơ cũ thiếu metadata. Handler trả `FileNotFound` khi không có file để bắt đầu chấm; cần xử lý dữ liệu cũ trước khi sử dụng hồ sơ đó.

### thesis.final_submission — FinalSubmission

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/FinalSubmission.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/FinalSubmissionConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AbstractEn | abstract_en | `String` | `text` | Có | — |
| AbstractVi | abstract_vi | `String` | `text` | Có | — |
| AdvisorConfirmedAt | advisor_confirmed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| AdvisorConfirmedBy | advisor_confirmed_by | `Guid?` | `uuid` | Có | — |
| ArchivedConfirmedAt | archived_confirmed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| ArchivedConfirmedBy | archived_confirmed_by | `Guid?` | `uuid` | Có | — |
| CurrentVersionNo | current_version_no | `Int32` | `integer` | Không | 1 |
| FacultyFeedback | faculty_feedback | `String` | `text` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| KeywordsEn | keywords_en | `String` | `character varying(1000)` | Có | — |
| KeywordsVi | keywords_vi | `String` | `character varying(1000)` | Có | — |
| Status | status | `FinalSubmissionStatus` | `character varying(50)` | Không | 'NotSubmitted' |

PK: `id`.

Index: `ix_final_submission_advisor_confirmed_by` (advisor_confirmed_by); `ix_final_submission_archived_confirmed_by` (archived_confirmed_by); `ix_final_submission_group_id` (group_id) unique.

Navigation: `Group` → StudentGroup.

### thesis.midterm_report — MidtermReport

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/MidtermReport.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/MidtermReportConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AdvisorFeedback | advisor_feedback | `String` | `text` | Có | — |
| AllowedToContinue | allowed_to_continue | `Boolean?` | `boolean` | Có | — |
| CurrentVersionNo | current_version_no | `Int32` | `integer` | Không | 1 |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| ReviewedAt | reviewed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| ReviewedBy | reviewed_by | `Guid?` | `uuid` | Có | — |
| Status | status | `MidtermReportStatus` | `character varying(50)` | Không | 'Submitted' |
| StopReason | stop_reason | `String` | `text` | Có | — |

PK: `id`.

Index: `ix_midterm_report_group_id` (group_id) unique; `ix_midterm_report_reviewed_by` (reviewed_by).

Navigation: `Group` → StudentGroup.

### thesis.progress_milestone — ProgressMilestone

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/ProgressMilestone.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/ProgressMilestoneConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| CreatedBy | created_by | `Guid` | `uuid` | Không | — |
| Deadline | deadline | `DateTime` | `timestamp with time zone` | Không | — |
| Description | description | `String` | `text` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| Title | title | `String` | `character varying(250)` | Không | — |

PK: `id`.

Index: `ix_progress_milestone_created_by` (created_by); `ix_progress_milestone_group_id` (group_id).

Navigation: `Group` → StudentGroup; `ProgressReports` → ProgressReport[].

### thesis.progress_report — ProgressReport

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/ProgressReport.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/ProgressReportConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AdvisorFeedback | advisor_feedback | `String` | `text` | Có | — |
| CurrentVersionNo | current_version_no | `Int32` | `integer` | Không | 1 |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| MilestoneId | milestone_id | `Guid` | `uuid` | Không | — |
| ReviewedAt | reviewed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| Status | status | `ProgressReportStatus` | `character varying(50)` | Không | 'Submitted' |
| StudentNote | student_note | `String` | `text` | Có | — |
| SubmittedAt | submitted_at | `DateTime` | `timestamp with time zone` | Không | — |

PK: `id`.

Index: `ix_progress_report_group_id` (group_id); `ix_progress_report_milestone_id_group_id` (milestone_id, group_id) unique.

Navigation: `Group` → StudentGroup; `Milestone` → ProgressMilestone.

### thesis.rename_request — RenameRequest

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/RenameRequest.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/RenameRequestConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AaApprovedAt | aa_approved_at | `DateTime?` | `timestamp with time zone` | Có | — |
| AdvisorConfirmedAt | advisor_confirmed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| FacultyApprovedAt | faculty_approved_at | `DateTime?` | `timestamp with time zone` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| NewTitleEn | new_title_en | `String` | `character varying(350)` | Có | — |
| NewTitleVi | new_title_vi | `String` | `character varying(300)` | Không | — |
| OldTitleEn | old_title_en | `String` | `character varying(350)` | Có | — |
| OldTitleVi | old_title_vi | `String` | `character varying(300)` | Không | — |
| Reason | reason | `String` | `text` | Không | — |
| Status | status | `RenameRequestStatus` | `character varying(50)` | Không | 'Created' |

PK: `id`.

Index: `ix_rename_request_group_id` (group_id).

Navigation: `Group` → StudentGroup.

### thesis.student_group — StudentGroup

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/StudentGroup.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/StudentGroupConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| AllowSyllabusResubmission | allow_syllabus_resubmission | `Boolean` | `boolean` | Không | false |
| Status | status | `StudentGroupStatus` | `character varying(50)` | Không | 'Registered' |
| TopicId | topic_id | `Guid` | `uuid` | Không | — |
| Version | xmin | `UInt32` | `xid` | Không | PostgreSQL quản lý |

PK: `id`.

Index: `ix_student_group_topic_id` (topic_id).

Concurrency token: `xmin`. SCRUM-102 chuyển `InProgress → FinalReportSubmitted` khi nộp lần đầu. Nộp lại và bắt đầu chấm giữ nguyên trạng thái nhưng vẫn ghi UPDATE để kiểm tra xmin, tránh lưu yêu cầu dựa trên nhóm vừa bị dừng.

Navigation: `Members` → StudentGroupMember[].

### thesis.student_group_member — StudentGroupMember

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/StudentGroupMember.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/StudentGroupMemberConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| ConfirmationStatus | confirmation_status | `MemberConfirmationStatus` | `character varying(50)` | Không | 'Confirmed' |
| ConfirmationTokenHash | confirmation_token_hash | `String` | `character varying(128)` | Có | — |
| ConfirmationExpiresAtUtc | confirmation_expires_at_utc | `DateTime?` | `timestamp with time zone` | Có | — |
| ConfirmedAtUtc | confirmed_at_utc | `DateTime?` | `timestamp with time zone` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| JoinedAt | joined_at | `DateTime` | `timestamp with time zone` | Không | — |
| InvitedByStudentId | invited_by_student_id | `Guid?` | `uuid` | Có | — |
| StudentId | student_id | `Guid` | `uuid` | Không | — |

PK: `id`.

Index: `ix_student_group_member_batch_id` (batch_id); `ix_student_group_member_group_id_student_id` (group_id, student_id) unique; `ix_student_group_member_student_id_batch_id` (student_id, batch_id) unique.

Index bổ sung: `ix_student_group_member_confirmation_token_hash` (confirmation_token_hash); `ix_student_group_member_status_expires` (confirmation_status, confirmation_expires_at_utc); index FK `ix_student_group_member_invited_by_student_id` (invited_by_student_id) trong mô hình migration tổng hợp. SCRUM-102 chỉ chấp nhận Student có ConfirmationStatus = Confirmed để nộp hoặc đọc theo quyền thành viên; không suy ra Confirmed chỉ từ việc row thành viên đã tồn tại.

Navigation: `Group` → StudentGroup.

### thesis.submission_file — SubmissionFile

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/SubmissionFile.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/SubmissionFileConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| EntityId | entity_id | `Guid` | `uuid` | Không | — |
| EntityType | entity_type | `SubmissionEntityType` | `character varying(50)` | Không | — |
| ChecksumSha256 | checksum_sha256 | `String` | `character varying(64)` | Có | — |
| ContentType | content_type | `String` | `character varying(100)` | Không | 'application/octet-stream' |
| FileExtension | file_extension | `String` | `character varying(10)` | Không | — |
| FileName | file_name | `String` | `character varying(255)` | Không | — |
| FileSize | file_size | `Int64` | `bigint` | Không | — |
| ObjectKey | object_key | `String` | `character varying(1000)` | Không | — |
| UploadedAt | uploaded_at | `DateTime` | `timestamp with time zone` | Không | — |
| UploadedBy | uploaded_by | `Guid` | `uuid` | Không | — |
| VersionNo | version_no | `Int32` | `integer` | Không | 1 |

PK: `id`.

Index: `ix_submission_file_uploaded_by` (uploaded_by); `ix_submission_file_entity_type_entity_id_version_no` (entity_type, entity_id, version_no).

Partial unique: `ux_submission_file_final_report_entity_id_version_no` (`entity_id`, `version_no`) **WHERE `entity_type = 'FinalReport'`**: mỗi phiên bản báo cáo cuối kỳ chỉ có một file, không áp ràng buộc này cho loại hồ sơ khác. `EntityId` vẫn là liên kết đa hình không có FK tới hồ sơ; Application kiểm tra loại file, hồ sơ và quyền trên đúng nhóm trước khi cấp URL tải.

File nộp SCRUM-102 chấp nhận PDF/DOC/DOCX **≤20 MiB = 20,971,520 byte**, kiểm tra chữ ký nội dung và extension; MIME/checksum do server xác định. `ObjectKey` trỏ tới bucket private, thay cho URL public; lịch sử chỉ trả metadata, tải file phải xin presigned URL 5 phút. `UploadedBy`/`UploadedAt` lưu người/thời điểm của từng bản; nộp lại không ghi đè row/object cũ. Object storage không nằm trong transaction PostgreSQL: lỗi upload/save có cơ chế xóa bù object mới, giữ object đã được database tham chiếu khi kết quả commit chưa rõ.

### thesis.syllabus — Syllabus

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/Syllabus.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/SyllabusConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| CurrentVersionNo | current_version_no | `Int32` | `integer` | Không | 1 |
| FacultyFeedback | faculty_feedback | `String` | `text` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| ReviewedAt | reviewed_at | `DateTime?` | `timestamp with time zone` | Có | — |
| ReviewedBy | reviewed_by | `Guid?` | `uuid` | Có | — |
| Status | status | `SyllabusStatus` | `character varying(50)` | Không | 'Submitted' |

PK: `id`.

Index: `ix_syllabus_group_id` (group_id) unique; `ix_syllabus_reviewed_by` (reviewed_by).

Navigation: `Group` → StudentGroup.

### topics.topic — Topic

[Entity](../backend/Modules/TopicCatalog/Domain/Entities/Topic.cs) · [EF configuration](../backend/Modules/TopicCatalog/Infrastructure/Persistence/Configurations/TopicConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AdvisorId | advisor_id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| Description | description | `String` | `text` | Có | — |
| Requirements | requirements | `String` | `text` | Có | — |
| Status | status | `TopicStatus` | `character varying(50)` | Không | 'Draft' |
| StudentNumber | student_number | `Int32?` | `integer` | Có | — |
| TitleEn | title_en | `String` | `character varying(350)` | Có | — |
| TitleVi | title_vi | `String` | `character varying(300)` | Không | — |

PK: `id`.

Index: `ix_topic_advisor_id` (advisor_id); `ix_topic_batch_id` (batch_id).

Check: `ck_topic_student_number`: `student_number IS NULL OR student_number BETWEEN 1 AND 2`.

## 5. 55 khóa ngoại vật lý

| Bảng/cột nguồn | Bảng/cột đích | Quan hệ | ON DELETE |
| --- | --- | --- | --- |
| academic.kltn_batch.schedule_template_version_id | academic.schedule_template_version.id | N–1 | restrict |
| academic.score_component_config.config_id | academic.score_weight_config.id | N–1 | cascade |
| academic.score_weight_config.batch_id | academic.kltn_batch.id | N–1 | restrict |
| academic.stage.batch_id | academic.kltn_batch.id | N–1 | cascade |
| academic.stage_template_definition.template_version_id | academic.schedule_template_version.id | N–1 | cascade |
| councils.advisor_evaluation.group_id | thesis.student_group.id | 1–1 | cascade |
| councils.council.approved_by | identity.user.id | N–1 | set null |
| councils.council.batch_id | academic.kltn_batch.id | N–1 | restrict |
| councils.council_assignment.council_id | councils.council.id | N–1 | cascade |
| councils.council_assignment.group_id | thesis.student_group.id | 1–1 | cascade |
| councils.council_member.council_id | councils.council.id | N–1 | cascade |
| councils.council_member.user_id | identity.user.id | N–1 | restrict |
| councils.council_minute.council_assignment_id | councils.council_assignment.id | 1–1 | cascade |
| councils.council_minute.locked_by | identity.user.id | N–1 | set null |
| councils.council_minute.unlocked_by | identity.user.id | N–1 | set null |
| councils.council_score.council_assignment_id | councils.council_assignment.id | N–1 | cascade |
| councils.council_score.council_member_id | councils.council_member.id | N–1 | cascade |
| councils.evaluation_detail.criteria_id | councils.grading_criteria.id | N–1 | restrict |
| councils.grading_criteria.batch_id | academic.kltn_batch.id | N–1 | restrict |
| councils.reviewer_assignment.assigned_by | identity.user.id | N–1 | restrict |
| councils.reviewer_assignment.group_id | thesis.student_group.id | N–1 | cascade |
| councils.reviewer_assignment.reviewer_id | identity.user.id | N–1 | restrict |
| councils.reviewer_evaluation.assignment_id | councils.reviewer_assignment.id | 1–1 | cascade |
| councils.score_summary.group_id | thesis.student_group.id | 1–1 | cascade |
| councils.score_summary.published_by | identity.user.id | N–1 | set null |
| councils.score_summary_component.summary_id | councils.score_summary.id | N–1 | cascade |
| identity.authentication_session.user_id | identity.user.id | N–1 | restrict |
| identity.one_time_challenge.user_id | identity.user.id | N–1 | cascade |
| identity.refresh_token.replaced_by_token_id | identity.refresh_token.id | N–1 | restrict |
| identity.refresh_token.session_id | identity.authentication_session.id | N–1 | cascade |
| identity.role_permission.permission_id | identity.permission.id | N–1 | cascade |
| identity.role_permission.role_id | identity.role.id | N–1 | cascade |
| identity.user_role.role_id | identity.role.id | N–1 | cascade |
| identity.user_role.user_id | identity.user.id | N–1 | cascade |
| notifications.notification.recipient_id | identity.user.id | N–1 | cascade |
| thesis.final_report.group_id | thesis.student_group.id | N–1 | cascade |
| thesis.final_submission.advisor_confirmed_by | identity.user.id | N–1 | set null |
| thesis.final_submission.archived_confirmed_by | identity.user.id | N–1 | set null |
| thesis.final_submission.group_id | thesis.student_group.id | 1–1 | cascade |
| thesis.midterm_report.group_id | thesis.student_group.id | 1–1 | cascade |
| thesis.midterm_report.reviewed_by | identity.user.id | N–1 | set null |
| thesis.progress_milestone.created_by | identity.user.id | N–1 | restrict |
| thesis.progress_milestone.group_id | thesis.student_group.id | N–1 | cascade |
| thesis.progress_report.group_id | thesis.student_group.id | N–1 | cascade |
| thesis.progress_report.milestone_id | thesis.progress_milestone.id | N–1 | cascade |
| thesis.rename_request.group_id | thesis.student_group.id | N–1 | cascade |
| thesis.student_group.topic_id | topics.topic.id | N–1 | restrict |
| thesis.student_group_member.batch_id | academic.kltn_batch.id | N–1 | restrict |
| thesis.student_group_member.group_id | thesis.student_group.id | N–1 | cascade |
| thesis.student_group_member.invited_by_student_id | identity.user.id | N–1 | set null |
| thesis.student_group_member.student_id | identity.user.id | N–1 | restrict |
| thesis.submission_file.uploaded_by | identity.user.id | N–1 | restrict |
| thesis.syllabus.group_id | thesis.student_group.id | 1–1 | cascade |
| thesis.syllabus.reviewed_by | identity.user.id | N–1 | set null |
| topics.topic.advisor_id | identity.user.id | N–1 | restrict |
| topics.topic.batch_id | academic.kltn_batch.id | N–1 | restrict |

## 6. Kết nối và migration

Xem [Architecture.md#6-migrations-và-neon](../backend/docs/Architecture/Architecture.md#6-migrations-và-neon) để chạy List/Check/Script/Update bằng công cụ pin trong repo. Giữ các migration đã áp dụng; chuỗi migration AcademicPeriods bổ sung lịch mẫu có version, audit outbox, vòng đời batch/cấu hình điểm, partial unique index và optimistic concurrency. CSDL đã chạy bản cũ cần chuyển history một lần bằng `MoveMigrationHistory.sql` trước khi update; không cần bước này trên CSDL mới. API không tự migrate khi khởi động. Không chạy `DatabaseScript.txt` như SQL hoặc tạo schema song song với EF.
