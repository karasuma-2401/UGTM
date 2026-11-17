# Nghiệm thu SCRUM-102 — Báo cáo cuối kỳ

Cập nhật 28/10/2026. Phạm vi CP7: đồng bộ tài liệu và kiểm chứng backend sau CP1–CP6. [API contract](ThesisManagementApiContract.md), [yêu cầu](../Requirements.md) và [data model](../DataModel.md) mô tả cùng quy tắc dưới đây. Chấm điểm/duyệt phản biện thuộc SCRUM-103; hồ sơ lưu chiểu và frontend là phạm vi riêng.

## Quy tắc đã chốt

| Nội dung | Hành vi nghiệm thu |
| --- | --- |
| Người nộp | Role Student, thành viên Confirmed của chính nhóm. Admin/Faculty không nộp thay sinh viên. |
| Điều kiện | Giữa kỳ đã Approved, nhóm InProgress khi nộp lần đầu hoặc FinalReportSubmitted khi nộp lại; đợt Open và trong cửa sổ FINAL_EVALUATION theo lịch/múi giờ đợt. |
| Thời gian | Mặc định tuần 11–18; dùng lịch cấu hình/override, không hard-code số tuần trong handler. Bao gồm đúng thời điểm mở và deadline; thiếu stage hoặc timezone sai không cho nộp. |
| File | PDF/DOC/DOCX, không rỗng, **≤20 MiB = 20,971,520 byte**. Kiểm tra extension và chữ ký nội dung; MIME/checksum do server xác định. Request multipart tối đa 20 MiB + 64 KiB overhead. |
| Phiên bản | Một InitialFinal cho mỗi nhóm; nộp lại giữ ID/loại báo cáo, tăng v1→v2→v3, giữ mọi file/người nộp/thời điểm/audit. Không chuyển thành RevisedAfterReviewer. |
| Quyền đọc/tải | Student Confirmed trong nhóm, GVHD hiện tại, Faculty có thesis.review-gates.review hoặc Admin. Vẫn được xem sau deadline, khi đợt đóng, nhóm dừng hoặc đã bắt đầu chấm. |
| Bắt đầu chấm | GVHD hiện tại hoặc Admin; nhóm FinalReportSubmitted, báo cáo InitialFinal đang Submitted và có file hiện tại. expectedVersionNo phải khớp bản đang xem. Không phụ thuộc deadline nộp. |
| Khóa bản | Chuyển UnderEvaluation, giữ CurrentVersionNo và file ID/checksum; nộp lại bị chặn. Bắt đầu lại trả 409, không ghi audit trùng. |
| Storage | Bucket private, metadata không trả trường objectKey. API tải kiểm tra đúng loại file/hồ sơ/quyền nhóm rồi cấp presigned GET 5 phút; cả bản cũ vẫn tải được. |
| Giao tác | Báo cáo/nhóm, metadata và audit outbox lưu cùng transaction PostgreSQL. xmin kiểm tra thay đổi đồng thời; request thua trả 409. Lỗi save rollback và xóa bù object mới; không xóa file cũ hoặc object đã được database tham chiếu khi mất xác nhận commit. |

Quy tắc PDF/DOC/DOCX ≤20 MiB thay cách ghi PDF/DOCX, “dưới 20 MB” hoặc “<20 MB” trong đặc tả cũ của báo cáo cuối kỳ. Yêu cầu lưu chiểu gồm PDF và DOCX vẫn giữ riêng.

## API bàn giao

| Method | Route | Kết quả chính |
| --- | --- | --- |
| POST | `/api/thesis/final-reports` | Nộp/nộp lại bằng multipart groupId + file; trả mã báo cáo và số phiên bản. |
| GET | `/api/thesis/groups/{groupId}/final-report` | Lịch sử InitialFinal, trạng thái hiện tại và metadata từng phiên bản theo thứ tự tăng dần. |
| GET | `/api/thesis/final-report-files/{fileId}/download` | fileId, downloadUrl, expiresAtUtc cho đúng file đã được kiểm tra quyền. |
| POST | `/api/thesis/final-reports/{finalReportId}/begin-evaluation` | JSON expectedVersionNo; trả UnderEvaluation cùng phiên bản/file đã khóa, actor/thời điểm bắt đầu chấm. |

Đối với SCRUM-103, dùng finalReportId, CurrentVersionNo và file ID/checksum của bản đã khóa. Không tự chọn bản mới khi version khác request; phải đọc lại lịch sử/file trước khi bắt đầu chấm. Quyền đọc của Faculty không đồng nghĩa quyền bắt đầu chấm.

## Data model và migration

- FinalReport có CurrentVersionNo (default 1, check >0), unique (GroupId, VersionType) và concurrency token xmin. xmin là system column PostgreSQL, không tạo/xóa vật lý trong migration.
- SubmissionFile dùng ObjectKey, ContentType và ChecksumSha256 thay FileUrl public. Partial unique (EntityId, VersionNo) WHERE EntityType = FinalReport giữ một file cho mỗi phiên bản cuối kỳ; không áp global unique này cho loại hồ sơ khác. DBML ghi rõ predicate trong Note vì không biểu diễn thành một global unique index.
- EntityId của SubmissionFile là liên kết đa hình không có FK tới hồ sơ; handler kiểm tra EntityType và hồ sơ/nhóm liên quan trước khi đọc/tải.
- Người/thời điểm bắt đầu chấm nằm trong audit event, cùng CurrentVersionNo, FileId và checksum; không có cột StartedBy/StartedAt riêng trên FinalReport.
- Migration nền tảng là `20261027174826_AddFinalReportSubmissionFoundation`; CP7 không thêm migration. Các test migrate database tạm tới bản mới nhất. Migration không tự tạo metadata file còn thiếu của dữ liệu cũ; hồ sơ đó phải được xử lý dữ liệu trước khi bắt đầu chấm.

## Bằng chứng kiểm thử

| Nhóm kiểm tra | Test nguồn |
| --- | --- |
| State machine, giữ loại/ID và khóa bản | [FinalReportTests](../../backend/tests/ThesisManagement.Tests/FinalReportTests.cs) |
| Quyền nộp, Confirmed, giữa kỳ Approved, trạng thái đợt/nhóm | [FinalReportSubmissionEligibilityTests](../../backend/tests/ThesisManagement.Tests/FinalReportSubmissionEligibilityTests.cs) |
| Migrate dữ liệu cũ, rollback/reapply migration, unique/check/xmin | [FinalReportPersistenceIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportPersistenceIntegrationTests.cs) |
| PDF/DOC/DOCX, MIME giả, chữ ký sai, đúng 20 MiB/vượt 1 byte, multipart chunked/malformed | [FinalReportSubmissionHttpIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportSubmissionHttpIntegrationTests.cs) |
| Nộp lại, lịch sử file/audit, race hai sinh viên, rollback và giữ object khi mất commit acknowledgement | [FinalReportResubmissionHttpIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportResubmissionHttpIntegrationTests.cs) và test HTTP nộp báo cáo ở trên |
| Quyền xem/tải, GVHD đổi phân công, sai loại file, bản cũ và đọc sau deadline | [FinalReportReadHttpIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportReadHttpIntegrationTests.cs) |
| expectedVersionNo, chỉ GVHD/Admin, hai lần bắt đầu đồng thời, cả hai thứ tự race nộp lại/chấm, nhóm dừng, rollback audit | [FinalReportEvaluationHttpIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportEvaluationHttpIntegrationTests.cs) |
| Cửa sổ thật theo timezone, override lịch, biên deadline, thiếu stage/timezone sai, đợt không Open | [FinalReportSubmissionWindowIntegrationTests](../../backend/tests/AcademicPeriods.Tests/Integration/FinalReportSubmissionWindowIntegrationTests.cs) |
| PostgreSQL + MinIO + batch/advisor reader + HTTP + unit of work thật | [FinalReportWorkflowIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportWorkflowIntegrationTests.cs) |
| DI, ranh giới module và persistence | [Architecture.Tests](../../backend/tests/Architecture.Tests) |

Test toàn luồng mới dùng Kestrel và ba module AcademicPeriods/TopicCatalog/ThesisManagement được đăng ký thật; seed đầy đủ user/đợt/đề tài/nhóm, **không tắt FK**. PostgreSQL và MinIO là container tạm. Chỉ cơ chế đăng nhập test và đồng hồ nghiệp vụ được điều khiển để kiểm tra các mốc thời gian; đây không phải kiểm chứng đăng nhập JWT hay môi trường đang deploy.

Ba kịch bản toàn luồng:

1. Trước giờ mở bị chặn; đúng giờ mở nộp PDF v1, DOC v2, DOCX v3; lấy lịch sử, tải và so sánh byte/checksum từng bản, anonymous GET object bị 403, presigned GET tải được và hết hạn sau 5 phút (đối chiếu mốc ký + thời lượng với đồng hồ host, sai số tối đa 5 giây cho độ chính xác/hiệu chỉnh đồng hồ của SDK; expiresAtUtc của API được kiểm tra đúng bằng đồng hồ nghiệp vụ +5 phút). Sau deadline bị chặn nộp nhưng GVHD vẫn bắt đầu chấm; request dùng version cũ bị 409, version hiện tại được khóa và mọi file vẫn còn.
2. Đóng đợt và thay GVHD bằng dữ liệu thật: nộp lại bị chặn, GVHD cũ mất quyền xem/tải/bắt đầu chấm, GVHD mới được đọc/tải và bắt đầu chấm sau khi đợt đóng.
3. Ép lỗi insert audit trong database container sau khi upload MinIO: response 500, phiên bản/metadata/audit cũ giữ nguyên, object vừa upload bị xóa bù và bản cũ vẫn tải được.

Các test lỗi dùng database/container tạm, không inject lỗi vào database đang sử dụng của ứng dụng.

## Chạy lại nghiệm thu

Yêu cầu .NET 10 và Docker engine chạy được Linux containers. Chạy từ root repository; Testcontainers tự tạo PostgreSQL/MinIO, migrate và dọn tài nguyên, không cần cấu hình tài khoản S3 production. Output riêng giúp tránh khóa DLL của API đang chạy.

```powershell
dotnet build UGTM.sln --artifacts-path artifacts/cp7 --verbosity quiet
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --artifacts-path artifacts/cp7 --no-build --no-restore --logger "trx;LogFileName=thesis-cp7.trx" --results-directory artifacts/cp7/test-results
dotnet test backend/tests/AcademicPeriods.Tests/UGTM.Modules.AcademicPeriods.Tests.csproj --artifacts-path artifacts/cp7 --no-build --no-restore --logger "trx;LogFileName=academic-cp7.trx" --results-directory artifacts/cp7/test-results
dotnet test backend/tests/Architecture.Tests/UGTM.Architecture.Tests.csproj --artifacts-path artifacts/cp7 --no-build --no-restore --logger "trx;LogFileName=architecture-cp7.trx" --results-directory artifacts/cp7/test-results
```

Chạy riêng ba kịch bản PostgreSQL/MinIO thật: thêm `--filter FullyQualifiedName~FinalReportWorkflowIntegrationTests` vào lệnh test ThesisManagement. Kết quả TRX nằm trong `artifacts/cp7/test-results` (gitignored).

## Kết quả CP7

Đã kiểm chứng trên source local ngày 28/10/2026:

| Kiểm tra | Kết quả | Báo cáo |
| --- | --- | --- |
| Build solution, output artifacts/cp7 | Pass, 0 warning / 0 error | dotnet build |
| ThesisManagement | **343 passed, 0 failed, 0 skipped** | thesis-cp7.trx |
| AcademicPeriods | **45 passed, 0 failed, 0 skipped** | academic-cp7.trx |
| Architecture | **74 passed, 0 failed, 0 skipped** | architecture-cp7.trx |
| Định dạng file test mới | Pass | dotnet format whitespace --verify-no-changes |
| Whitespace diff và 13 liên kết local trong hướng dẫn này | Pass | git diff --check và kiểm tra đường dẫn |

Tổng **462 test pass**. Ba kịch bản PostgreSQL/MinIO thật đều Passed trong báo cáo ThesisManagement cuối cùng và đã nằm trong số 343 ở trên. Fixture DOCX cố định timestamp của ZIP entry để kiểm tra checksum/byte tải xuống ổn định giữa các lần chạy.

- [x] Đồng bộ API contract và nhóm mã lỗi FinalReport.
- [x] Đồng bộ phần data model/DBML liên quan đến báo cáo, metadata file, xmin và thành viên Confirmed.
- [x] Thống nhất yêu cầu PDF/DOC/DOCX ≤20 MiB trong Requirements, UseCases và quy tắc file liên quan.
- [x] Chạy ba bộ test yêu cầu và luồng PostgreSQL/MinIO thật, lưu TRX cuối cùng không lỗi.

CP7 chỉ bổ sung tài liệu và kiểm thử; không thay đổi logic API production, không thêm migration. Các kết quả này không thay cho nghiệm thu frontend, đăng nhập JWT hoặc môi trường đã deploy.
