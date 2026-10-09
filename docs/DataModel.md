# Mô hình dữ liệu UGTM

Đối chiếu source ngày 10/10/2026. Nghiệp vụ theo [Requirements.md](Requirements.md); cấu trúc dưới đây lấy từ metadata mô hình EF Core, không suy diễn từ sơ đồ kiến trúc cũ trong Word.

## 1. Nguồn chuẩn

Sáu DbContext runtime, mỗi context chỉ có entity/schema của module mình; [UGTMDbContext](../backend/Database.Migrations/UGTMDbContext.cs) chỉ tổng hợp mô hình để tạo migration và không được đăng ký DI runtime. Cấu trúc vật lý gồm **34 bảng nghiệp vụ, 6 schema, 50 FK vật lý**. Quy ước snake_case; enum lưu string `varchar(50)`, không phải PostgreSQL enum. Guid dùng cho phần lớn ID; Role/Permission dùng int; UserRole/RolePermission dùng khóa ghép. Bảng lịch sử `public.__ef_migrations_history` không tính trong 34 bảng nghiệp vụ; public là schema kỹ thuật ngoài 6 schema nghiệp vụ.

Nguồn thực thi: [EF configurations tại từng module](../backend/Modules), [migrations](../backend/Database.Migrations/Migrations), [snapshot](../backend/Database.Migrations/Migrations/UGTMDbContextModelSnapshot.cs). [DatabaseScript.txt](DatabaseScript.txt) là DBML đồng bộ theo mô hình này để xem bằng dbdiagram; không chứa comment dư thừa và không phải SQL để update Neon. PK, nullability, kiểu PostgreSQL, SQL default, index, check và FK đều được lấy từ EF metadata. Seed dữ liệu và chi tiết provider vẫn xem migration. Cú pháp theo [DBML chính thức](https://dbml.dbdiagram.io/docs/).

[Sơ đồ schema](PlantUML/MultiSchema_Database.puml); [quyền sở hữu bảng/module và hướng triển khai](Architecture.md#6-migrations-và-neon). Nhóm sinh viên nằm ở thesis; identity giữ audit quản trị; notification có schema notifications riêng. Schema shared đã được loại bỏ. Sáu schema được tổ chức theo chủ sở hữu dữ liệu, không dùng số lượng schema để chứng minh bounded context DDD.

| Schema | Số bảng | Entity |
| --- | --- | --- |
| identity | 6 | AuditLog, Permission, Role, RolePermission, User, UserRole |
| academic | 4 | KltnBatch, ScoreComponentConfig, ScoreWeightConfig, Stage |
| topics | 1 | Topic |
| thesis | 10 | FinalReport, FinalSubmission, MidtermReport, ProgressMilestone, ProgressReport, RenameRequest, StudentGroup, StudentGroupMember, SubmissionFile, Syllabus |
| councils | 12 | AdvisorEvaluation, Council, CouncilAssignment, CouncilMember, CouncilMinute, CouncilScore, EvaluationDetail, GradingCriteria, ReviewerAssignment, ReviewerEvaluation, ScoreSummary, ScoreSummaryComponent |
| notifications | 1 | Notification |

## 2. Thay đổi đáp ứng dữ liệu final

- `ScoreComponentConfig` lưu nguồn/đầu điểm, trường hợp áp dụng, trọng số, điểm sàn và điều kiện duyệt; `ScoreWeightConfig` có phương thức tính và phiên bản. Không dùng RoleId để phân biệt GVHD/GVPB. `RoleScoreWeight` cũ đã được loại bằng EncapsulateModulePersistence vì đầu điểm nghiệp vụ được cấu hình qua ScoreComponentConfig, không gắn theo role hệ thống.
- `ScoreSummaryComponent` lưu điểm/trọng số/nguồn và cờ loại; `ScoreSummary` có tổng trọng số, thời điểm tính và snapshot cấu hình. Snapshot cần được use case ghi cùng kết quả, chưa có API lưu tự động.
- Các nguồn GVHD/GVPB/Hội đồng có cờ loại điểm và lý do; không tự loại điểm do cảnh báo sai biệt. Công thức chia 6/7 và loại cả hệ số đã được xây dựng trong Domain; xem [Architecture.md#6-migrations-và-neon](Architecture.md#6-migrations-và-neon).
- Giữa kỳ dùng AdvisorFeedback, kết luận tiếp tục/dừng, StopReason. `faculty_feedback` cũ được rename sang `advisor_feedback` để giữ dữ liệu; policy GVHD cần triển khai.
- Phân công nhiều phản biện bằng thứ tự phân công; lưu riêng RevisionFeedback để API có thể công khai góp ý mà bảo mật điểm/phiếu.
- Lưu chiểu có metadata Việt/Anh, phiên bản, GVHD xác nhận và phản hồi Khoa; biên bản có người cấp mở khóa.
- Migration seed 6 role hệ thống; không seed tài khoản hoặc mật khẩu. Không thay đổi provider/database bằng việc sửa docs.

## 3. Ràng buộc và giới hạn

Unique `(StudentId, BatchId)` của StudentGroupMember ngăn sinh viên ở hai nhóm cùng đợt. Unique `(GroupId, StudentId)` ngăn thành viên lặp. Unique `(CouncilAssignmentId, CouncilMemberId)` ngăn hai phiếu Hội đồng của cùng người cho cùng phân công. ReviewerAssignment unique `(GroupId, AssignmentOrder)` và `(GroupId, ReviewerId)` cho phép phản biện thứ hai.

Stage có unique `(BatchId, StageIndex)` và index giai đoạn 1–7; chưa tự tạo đủ 7 giai đoạn hoặc kiểm tra toàn bộ deadline. Topic.StudentNumber có check 1–2 khi có giá trị. Aggregate StudentGroup giới hạn 2 thành viên, không trùng và cùng đợt qua AddMember; đăng ký đồng thời, chỉ tiêu đề tài và thời hạn cần Application kiểm soát trong transaction. Các phiếu điểm có check 0–10; precision trong DB không thay quy định nhập một chữ số thập phân ở API.

ScoreWeightConfig vẫn unique BatchId: một cấu hình hiện hành mỗi đợt, VersionNo tăng khi sửa. Lịch sử tính cần snapshot, không giả định bảng này đã hỗ trợ nhiều phiên bản bất biến. Kiểm tra tổng % bằng 100/hệ số nguyên dương tổng, nguồn áp dụng, đủ đầu điểm và điểm sàn cần được use case gọi; check DB chỉ bảo đảm trọng số không âm và điểm sàn 0–10.

Những phần phải thiết kế tiếp trước API:

| Khoảng thiếu | Đề xuất xử lý |
| --- | --- |
| SubmissionFile.EntityId không có FK tới hồ sơ đa hình | Chọn gốc hồ sơ chung hoặc liên kết riêng theo loại; kiểm tra chủ sở hữu và phiên bản |
| EvaluationDetail.EvaluationId chưa có FK tới phiếu | Mô hình liên kết rõ các loại phiếu; không gắn giả vào riêng một loại |
| Quyết định loại điểm mới có cờ/lý do | Lưu người/thời điểm/bằng chứng kết luận và audit; kiểm tra quyền tại use case |
| Mở khóa chưa có thời hạn/phạm vi | Chốt quy tắc cấp quyền và khóa lại, rồi bổ sung mô hình/policy phù hợp |
| Điểm khi có phản biện thứ hai chưa có quy tắc chọn | Nhóm chốt cách kết luận; không mặc định lấy điểm cuối hay trung bình |
| Auth, deadline, khóa điểm, lưu chiểu/công bố chưa có handler | Thực thi ở Application với transaction và policy; entity/enum không tự bảo đảm |

## 4. Từ điển bảng hiện tại

Tên thuộc tính là C#; tên cột và kiểu là PostgreSQL lấy từ EF. Default dưới đây là **SQL default**, khác với giá trị khởi tạo C#. Navigation không phải cột. Runtime model chỉ có navigation nội bộ module; bảng FK ở mục 5 lấy từ mô hình migration tổng hợp, kể cả FK liên module cấu hình qua ID. Enum là string; nullable theo mô hình EF, kể cả thuộc tính tham chiếu.

### academic.kltn_batch — KltnBatch

[Entity](../backend/Modules/AcademicPeriods/Domain/Entities/KltnBatch.cs) · [EF configuration](../backend/Modules/AcademicPeriods/Infrastructure/Persistence/Configurations/KltnBatchConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AcademicYear | academic_year | `String` | `character varying(20)` | Không | — |
| BatchName | batch_name | `String` | `character varying(100)` | Không | — |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| EndDate | end_date | `DateOnly` | `date` | Không | — |
| Semester | semester | `Int32` | `integer` | Không | — |
| StartDate | start_date | `DateOnly` | `date` | Không | — |
| Status | status | `BatchStatus` | `character varying(50)` | Không | 'Draft' |

PK: `id`.

Check: `ck_batch_dates`: `end_date > start_date`; `ck_batch_semester`: `semester > 0`.

Navigation: `ScoreWeightConfig` → ScoreWeightConfig; `Stages` → Stage[].

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

Index: `ix_score_component_config_config_id_component_code` (config_id, component_code) unique.

Check: `ck_score_component_minimum`: `minimum_score IS NULL OR minimum_score BETWEEN 0 AND 10`; `ck_score_component_weight`: `weight >= 0`.

Navigation: `Config` → ScoreWeightConfig.

### academic.score_weight_config — ScoreWeightConfig

[Entity](../backend/Modules/AcademicPeriods/Domain/Entities/ScoreWeightConfig.cs) · [EF configuration](../backend/Modules/AcademicPeriods/Infrastructure/Persistence/Configurations/ScoreWeightConfigConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| CalculationMethod | calculation_method | `ScoreCalculationMethod` | `character varying(50)` | Không | 'IntegerCoefficient' |
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| IsActive | is_active | `Boolean` | `boolean` | Không | true |
| VersionNo | version_no | `Int32` | `integer` | Không | 1 |

PK: `id`.

Index: `ix_score_weight_config_batch_id` (batch_id) unique.

Navigation: `Batch` → KltnBatch; `Components` → ScoreComponentConfig[].

### academic.stage — Stage

[Entity](../backend/Modules/AcademicPeriods/Domain/Entities/Stage.cs) · [EF configuration](../backend/Modules/AcademicPeriods/Infrastructure/Persistence/Configurations/StageConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| DeadlineDate | deadline_date | `DateTime` | `timestamp with time zone` | Không | — |
| StageIndex | stage_index | `Int32` | `integer` | Không | — |
| StageName | stage_name | `String` | `character varying(150)` | Không | — |
| StartDate | start_date | `DateOnly` | `date` | Không | — |

PK: `id`.

Index: `ix_stage_batch_id_stage_index` (batch_id, stage_index) unique.

Check: `ck_stage_index`: `stage_index BETWEEN 1 AND 7`.

Navigation: `Batch` → KltnBatch.

### councils.advisor_evaluation — AdvisorEvaluation

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/AdvisorEvaluation.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/AdvisorEvaluationConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AllowsReviewerAssignment | allows_reviewer_assignment | `Boolean?` | `boolean` | Có | — |
| Comment | comment | `String` | `text` | Có | — |
| EvaluatedAt | evaluated_at | `DateTime` | `timestamp with time zone` | Không | — |
| ExclusionReason | exclusion_reason | `String` | `text` | Có | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| IsIgnoredInCalculation | is_ignored_in_calculation | `Boolean` | `boolean` | Không | false |
| Score | score | `Decimal` | `numeric(4,2)` | Không | — |

PK: `id`.

Index: `ix_advisor_evaluation_group_id` (group_id) unique.

Check: `ck_advisor_evaluation_score`: `score BETWEEN 0 AND 10`.

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

PK: `id`.

Index: `ix_reviewer_assignment_assigned_by` (assigned_by); `ix_reviewer_assignment_reviewer_id` (reviewer_id); `ix_reviewer_assignment_group_id_assignment_order` (group_id, assignment_order) unique; `ix_reviewer_assignment_group_id_reviewer_id` (group_id, reviewer_id) unique.

Navigation: `Evaluation` → ReviewerEvaluation.

### councils.reviewer_evaluation — ReviewerEvaluation

[Entity](../backend/Modules/DefenseCouncil/Domain/Entities/ReviewerEvaluation.cs) · [EF configuration](../backend/Modules/DefenseCouncil/Infrastructure/Persistence/Configurations/ReviewerEvaluationConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| AssignmentId | assignment_id | `Guid` | `uuid` | Không | — |
| Comment | comment | `String` | `text` | Có | — |
| DefenseApproval | defense_approval | `DefenseApprovalStatus` | `character varying(50)` | Không | — |
| EvaluatedAt | evaluated_at | `DateTime` | `timestamp with time zone` | Không | — |
| ExclusionReason | exclusion_reason | `String` | `text` | Có | — |
| IsIgnoredInCalculation | is_ignored_in_calculation | `Boolean` | `boolean` | Không | false |
| IsVisibleToStudent | is_visible_to_student | `Boolean` | `boolean` | Không | false |
| RevisionFeedback | revision_feedback | `String` | `text` | Có | — |
| Score | score | `Decimal` | `numeric(4,2)` | Không | — |

PK: `id`.

Index: `ix_reviewer_evaluation_assignment_id` (assignment_id) unique.

Check: `ck_reviewer_evaluation_score`: `score BETWEEN 0 AND 10`.

Navigation: `Assignment` → ReviewerAssignment.

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
| Action | action | `String` | `character varying(100)` | Không | — |
| EntityId | entity_id | `String` | `character varying(100)` | Không | — |
| EntityType | entity_type | `String` | `character varying(50)` | Không | — |
| NewData | new_data | `String` | `text` | Có | — |
| OldData | old_data | `String` | `text` | Có | — |
| Timestamp | timestamp | `DateTime` | `timestamp with time zone` | Không | — |
| UserId | user_id | `Guid` | `uuid` | Không | — |

PK: `id`.

Index: `ix_audit_log_user_id` (user_id).

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

### identity.role — Role

[Entity](../backend/Modules/Identity/Domain/Entities/Role.cs) · [EF configuration](../backend/Modules/Identity/Infrastructure/Persistence/Configurations/RoleConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Int32` | `integer` | Không | — |
| Description | description | `String` | `character varying(255)` | Có | — |
| RoleName | role_name | `SystemRoleName` | `character varying(50)` | Không | — |

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
| CreatedAt | created_at | `DateTime` | `timestamp with time zone` | Không | — |
| Email | email | `String` | `character varying(150)` | Không | — |
| FullName | full_name | `String` | `character varying(150)` | Không | — |
| IsActive | is_active | `Boolean?` | `boolean` | Có | — |
| PasswordHash | password_hash | `String` | `text` | Không | — |
| PhoneNumber | phone_number | `String` | `character varying(20)` | Có | — |
| UpdatedAt | updated_at | `DateTime?` | `timestamp with time zone` | Có | — |
| UserCode | user_code | `String` | `character varying(20)` | Có | — |

PK: `id`.

Index: `ix_user_email` (email) unique; `ix_user_user_code` (user_code) unique.

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
| Status | status | `FinalReportStatus` | `character varying(50)` | Không | 'Submitted' |
| SubmittedAt | submitted_at | `DateTime` | `timestamp with time zone` | Không | — |
| VersionType | version_type | `FinalReportVersionType` | `character varying(50)` | Không | — |

PK: `id`.

Index: `ix_final_report_group_id` (group_id).

Navigation: `Group` → StudentGroup.

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
| Status | status | `StudentGroupStatus` | `character varying(50)` | Không | 'Registered' |
| TopicId | topic_id | `Guid` | `uuid` | Không | — |

PK: `id`.

Index: `ix_student_group_topic_id` (topic_id).

Navigation: `Members` → StudentGroupMember[].

### thesis.student_group_member — StudentGroupMember

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/StudentGroupMember.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/StudentGroupMemberConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| BatchId | batch_id | `Guid` | `uuid` | Không | — |
| GroupId | group_id | `Guid` | `uuid` | Không | — |
| JoinedAt | joined_at | `DateTime` | `timestamp with time zone` | Không | — |
| StudentId | student_id | `Guid` | `uuid` | Không | — |

PK: `id`.

Index: `ix_student_group_member_batch_id` (batch_id); `ix_student_group_member_group_id_student_id` (group_id, student_id) unique; `ix_student_group_member_student_id_batch_id` (student_id, batch_id) unique.

Navigation: `Group` → StudentGroup.

### thesis.submission_file — SubmissionFile

[Entity](../backend/Modules/ThesisManagement/Domain/Entities/SubmissionFile.cs) · [EF configuration](../backend/Modules/ThesisManagement/Infrastructure/Persistence/Configurations/SubmissionFileConfiguration.cs).

| Thuộc tính | Cột | Kiểu CLR | Kiểu PostgreSQL | Nullable | SQL default |
| --- | --- | --- | --- | --- | --- |
| Id | id | `Guid` | `uuid` | Không | — |
| EntityId | entity_id | `Guid` | `uuid` | Không | — |
| EntityType | entity_type | `SubmissionEntityType` | `character varying(50)` | Không | — |
| FileExtension | file_extension | `String` | `character varying(10)` | Không | — |
| FileName | file_name | `String` | `character varying(255)` | Không | — |
| FileSize | file_size | `Int64` | `bigint` | Không | — |
| FileUrl | file_url | `String` | `character varying(500)` | Không | — |
| UploadedAt | uploaded_at | `DateTime` | `timestamp with time zone` | Không | — |
| UploadedBy | uploaded_by | `Guid` | `uuid` | Không | — |
| VersionNo | version_no | `Int32` | `integer` | Không | 1 |

PK: `id`.

Index: `ix_submission_file_uploaded_by` (uploaded_by); `ix_submission_file_entity_type_entity_id_version_no` (entity_type, entity_id, version_no).

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

## 5. 50 khóa ngoại vật lý

| Bảng/cột nguồn | Bảng/cột đích | Quan hệ | ON DELETE |
| --- | --- | --- | --- |
| academic.score_component_config.config_id | academic.score_weight_config.id | N–1 | cascade |
| academic.score_weight_config.batch_id | academic.kltn_batch.id | 1–1 | restrict |
| academic.stage.batch_id | academic.kltn_batch.id | N–1 | cascade |
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
| identity.audit_log.user_id | identity.user.id | N–1 | restrict |
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
| thesis.student_group_member.student_id | identity.user.id | N–1 | restrict |
| thesis.submission_file.uploaded_by | identity.user.id | N–1 | restrict |
| thesis.syllabus.group_id | thesis.student_group.id | 1–1 | cascade |
| thesis.syllabus.reviewed_by | identity.user.id | N–1 | set null |
| topics.topic.advisor_id | identity.user.id | N–1 | restrict |
| topics.topic.batch_id | academic.kltn_batch.id | N–1 | restrict |

## 6. Kết nối và migration

Xem [Architecture.md#6-migrations-và-neon](Architecture.md#6-migrations-và-neon) để chạy List/Check/Script/Update bằng công cụ pin trong repo. Giữ InitialCreate đã áp dụng; AlignFinalReportFoundation nâng mô hình từ 33 lên 35 bảng; AssignSchemaOwnership chuyển bảng về chủ sở hữu và loại schema shared; EncapsulateModulePersistence loại role_score_weight, còn 34 bảng/50 FK. Việc chuyển entity/configuration về module không đổi các bảng/cột/FK còn lại. CSDL đã chạy bản cũ cần chuyển history một lần bằng MoveMigrationHistory.sql trước khi update; không cần bước này trên CSDL mới. API không tự migrate khi khởi động. Không chạy DatabaseScript.txt như SQL hoặc tạo schema song song với EF.
