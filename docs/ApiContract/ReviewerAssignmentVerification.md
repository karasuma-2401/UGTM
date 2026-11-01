# Nghiệm thu backend phân công GVPB — SCRUM-104

Ngày kiểm chứng: 30/10/2026. Baseline CP5: `9ebed88`. Phạm vi CP6: kiểm thử tích hợp và đồng bộ tài liệu; không thêm migration hoặc endpoint chấm điểm. Lượt cuối trên source CP6: **869/869 pass, 0 fail, 0 skip** trong các bộ được ghi dưới đây; không phải toàn bộ test suite backend.

## Luồng nghiệm thu và nguồn thực thi

[Real workflow](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentRealWorkflowIntegrationTests.cs) mở Kestrel với production `AddUgtmModules`/`MapUgtmModules`, coordinator và owner contracts thật. Chỉ authentication và clock được điều khiển; workers nền không chạy. PostgreSQL Testcontainers áp toàn bộ migration, giữ FK; MinIO dùng storage S3 thật và bucket riêng mỗi ca, xóa sau test. Không dùng mock eligibility, account, handler hoặc storage trong hai ca CP6.

| Bước | Bằng chứng được assert |
| --- | --- |
| Nộp/khóa/duyệt | Student/partner nộp PDF v1 và DOCX v2; GVHD khóa v2; GVHD hoặc Admin lưu và đồng ý qua HTTP SCRUM-103. |
| Phân công | Admin tạo sau khi cửa sổ nộp bài đã hết nhưng đợt còn Open. Snapshot trỏ đúng v2/file/phiếu duyệt, assignedBy là Admin thực tế. |
| Quyền GVPB | GET download cấp URL ký S3 TTL 5 phút; tải anonymous bằng URL nhận đúng byte DOCX v2. Yêu cầu v1, lịch sử nhóm và phiếu GVHD bị từ chối. Nhiệm vụ không chứa nhận xét GVHD/object key. |
| Hủy | DELETE JSON hủy chưa chấm, ghi cancelledBy thực tế, giữ bản ghi; xin URL mới bị từ chối. URL vừa được cấp vẫn tải được byte thật: không hứa thu hồi trước TTL. |
| Phân công lại | Hai ca: cùng giảng viên và giảng viên khác. Tạo ID mới, giữ dòng cũ inactive; người cũ mất nhánh quyền phân công khi đổi người. |
| Boundary SCRUM-105 | Writer test dùng coordinator + `LockForEvaluationAsync`, lưu phiếu điểm 0, ẩn và bị loại tính điểm; chưa có grading HTTP API. Đóng đợt rồi Admin hủy nhận 409, không thay xmin/phiếu/receipt/audit. Đợt đóng không tự chặn tải. |
| Bất biến | Một active + một inactive, một phiếu GVPB, ba receipt/audit phân công; group vẫn ReadyForReviewerAssignment, report UnderEvaluation, hai file và hai object còn nguyên. Student/GVHD vẫn tải theo quyền độc lập. |
| OpenAPI thật | GET `/openapi/v1.json` bằng production configuration; kiểm tra UUID header bắt buộc trên POST/DELETE, JSON DELETE body, query của bốn GET và schema list phẳng có assignmentVersion/hasEvaluation/canCancel. JSON sinh ra lưu ở output test `reviewer-openapi.json`. |

## Giao dịch, hồi quy và ranh giới quyền

- [CP2 transaction tests](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentTransactionIntegrationTests.cs): hai connection thực, quan sát `pg_blocking_pids`, thay đổi guard trước/sau lock, hai request tranh một active, cùng key chờ receipt, rollback cả save/receipt/outbox, lock timeout và từ chối ghi vào schema reader. Các owner guards bao gồm group/report/file, advisor, batch và account/grant.
- [HTTP cancellation races](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentCancellationConcurrencyIntegrationTests.cs): writer phiếu và DELETE giữ cùng parent lock theo cả hai thứ tự; winner quyết định kết quả, không có inactive assignment mang phiếu vừa được tạo bởi writer tuân contract. Writer SCRUM-105 phải giữ khóa tới commit; không gọi read contract rồi ghi ở transaction khác.
- [Persistence/migration](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentWorkflowPersistenceIntegrationTests.cs): partial unique một active, lịch sử same-reviewer, xmin, FK Restrict, legacy, preflight dữ liệu không tương thích và rollback/reapply. Migration không tự sửa/xóa dữ liệu legacy; chỉ áp vào container tạm trong nghiệm thu.
- [Read/access](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentReportAccessHttpIntegrationTests.cs): account/grant hiện tại thắng token cũ; chỉ own active/exact snapshot; legacy/binding/context lỗi không mở quyền; không lộ phiếu GVHD. Management GET cần đúng role + read/manage; write cần manage; Admin bypass permission. GVPB vẫn phải có tài khoản Active/Lecturer.
- [Gateway](../../backend/tests/ThesisManagement.Tests/Integration/ReviewerAssignmentCancellationGatewayIntegrationTests.cs): DELETE Unicode/xuống dòng và replay đi qua Caddy container dùng image/config repository. Đây là proxy local, không phải xác nhận domain/TLS hay frontend đang deploy.
- Quyền Student/GVHD/FacultyGate/Admin theo SCRUM-102 được xét độc lập; việc đồng thời có quyền này có thể cho phép đọc thêm file ngoài nhánh GVPB. Hủy assignment chỉ thu hồi nhánh phân công và cấp URL mới.

## Lệnh chạy lại

Đối chiếu [acceptance matrix RA-01–RA-25](ReviewerAssignmentApiContract.md#10-ma-trận-nghiệm-thu-cp1cp6) với các bộ test được chọn:

| Acceptance | Bộ thực thi |
| --- | --- |
| RA-01–RA-08 | Creation HTTP/handler, candidate/eligibility read và real workflow |
| RA-09–RA-10 | CP2 transaction + creation HTTP: tranh active, request key, replay/mất response |
| RA-11–RA-13 | Cancellation HTTP/handler, persistence và real workflow |
| RA-14–RA-16 | Transaction CP2 + cancellation concurrency: hai thứ tự khóa, guard mutation, rollback/timeout |
| RA-17–RA-18 | Read HTTP: count/filter/page/legacy, SQL count và own-task authorization |
| RA-19–RA-21 | Report access HTTP + real MinIO workflow + hồi quy FinalReport/Advisor |
| RA-22 | Workflow persistence/migration |
| RA-23 | Audit/replay assertions trong creation/cancellation/transaction/read và real workflow |
| RA-24 | Cancellation gateway HTTP qua Caddy local |
| RA-25 | Hai ca Real_reviewer_workflow mới, file nguồn ở trên |

Chạy từ root repository, Docker Desktop Linux engine đang hoạt động, .NET 10 SDK. Artifacts nằm trong `artifacts/scrum104-cp6/` và bị Git ignore. Khi dùng PowerShell phải giữ nguyên dấu nháy quanh logger chứa dấu `;` và filter chứa `|`.

```powershell
dotnet build UGTM.sln --configuration Release --artifacts-path artifacts/scrum104-cp6 -m:1 -nr:false -p:UseSharedCompilation=false -v quiet
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --no-build --no-restore --configuration Release --artifacts-path artifacts/scrum104-cp6 --filter 'FullyQualifiedName~Real_reviewer_workflow' --logger 'trx;LogFileName=real-reviewer-workflow-final.trx' --results-directory artifacts/scrum104-cp6/test-results
```

```powershell
$cp6TestOptions = @('--no-build', '--no-restore', '--configuration', 'Release', '--artifacts-path', 'artifacts/scrum104-cp6', '--results-directory', 'artifacts/scrum104-cp6/test-results')
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj @cp6TestOptions --filter '(FullyQualifiedName~ReviewerAssignment|FullyQualifiedName~FinalReport|FullyQualifiedName~AdvisorEvaluation|FullyQualifiedName~AdvisorWorkflowTransaction)&FullyQualifiedName!~Real_reviewer_workflow' --logger 'trx;LogFileName=reviewer-final-report-advisor-regression.trx'
dotnet test backend/tests/UGTM.Modules.Identity.Tests/UGTM.Modules.Identity.Tests.csproj @cp6TestOptions --filter 'FullyQualifiedName~IdentityPermissionMigrationTests|FullyQualifiedName~RolePermissionHandlerTests|FullyQualifiedName~UserAccountGroupTests|FullyQualifiedName~UsersHandlerTests' --logger 'trx;LogFileName=identity-permission-lookup.trx'
dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj @cp6TestOptions --logger 'trx;LogFileName=defense-council.trx'
dotnet test backend/tests/TopicCatalog.Tests/UGTM.Modules.TopicCatalog.Tests.csproj @cp6TestOptions --logger 'trx;LogFileName=topic-catalog.trx'
dotnet test backend/tests/AcademicPeriods.Tests/UGTM.Modules.AcademicPeriods.Tests.csproj @cp6TestOptions --logger 'trx;LogFileName=academic-periods.trx'
dotnet test backend/tests/Architecture.Tests/UGTM.Architecture.Tests.csproj @cp6TestOptions --logger 'trx;LogFileName=architecture.trx'
dotnet format UGTM.sln --no-restore --verify-no-changes
git diff --check
```

SCRUM-104 không bao gồm UI SCRUM-113, nhập điểm SCRUM-105, phản biện thứ hai, thông báo, công bố/tổng hợp điểm hay triển khai production. Không xác nhận TLS/domain/Neon/S3 production từ các container local.

## Kết quả CP6

| Bộ kiểm chứng | Passed / Total | Skipped | Phạm vi |
| --- | --- | --- | --- |
| PostgreSQL/MinIO workflow mới + OpenAPI | 2 / 2 | 0 | Hai ca CP6, bao gồm GVHD/Admin duyệt và cùng/khác GVPB |
| Reviewer/FinalReport/Advisor trong ThesisManagement | 316 / 316 | 0 | Filter ở trên; gồm 137 ca transaction/HTTP phân công, 10 persistence/migration phân công, 110 HTTP báo cáo/GVHD và các ca domain/persistence/workflow còn lại. Không phải toàn project ThesisManagement. |
| Identity | 130 / 130 | 0 | Filter permission migration, role permission, account grouping và user handlers; lookup Lecturer SQL thật còn được kiểm tra trong CP2/CP5 |
| DefenseCouncil | 55 / 55 | 0 | Toàn project |
| TopicCatalog | 247 / 247 | 0 | Toàn project; owner reviewer contracts SQL thật còn được kiểm tra xuyên module trong CP2/CP5 |
| AcademicPeriods | 45 / 45 | 0 | Toàn project, gồm integration PostgreSQL |
| Architecture | 74 / 74 | 0 | Toàn project |

Build Release toàn solution: 0 error, 4 warning (CS8602 OpenAPI cũ được báo ở cả host và source-link test; MSB3277 phiên bản EF Core trong Architecture.Tests). Lượt đầu source-link OpenAPI thiếu using DI đã sửa; lượt kiểm thử mới đầu tiên 1/2 fail do thiếu header DELETE trong OpenAPI, đã sửa normalize constraint `{id:guid}` và chạy lại source cuối **2/2 pass**. Log/TRX lượt đầu được giữ để đối chiếu, không cộng vào kết quả cuối.

Kiểm tra liên kết local trong các Markdown thay đổi và `git diff --check` đã pass; index Git trống, plan vẫn nằm trong `artifacts/` bị ignore. Chưa stage/commit; người dùng tự review và commit. Format toàn solution `--verify-no-changes` pass. Bộ hồi quy dài hoàn tất trong 17 phút 42 giây, không fail/skip. CP6 sẵn sàng review.

Bằng chứng local: `artifacts/scrum104-cp6/test-results/` chứa `real-reviewer-workflow-final.trx`, `reviewer-final-report-advisor-regression.trx`, `identity-permission-lookup.trx`, `defense-council.trx`, `topic-catalog.trx`, `academic-periods.trx`, `architecture.trx`. Build/format: `build-final.log`, `format-verify.log`; document thực tế: `bin/UGTM.Modules.ThesisManagement.Tests/release/reviewer-openapi.json`. Các kết quả này đều bị Git ignore. Tổng 869 chỉ tính bảy TRX cuối nêu trên, không cộng các lượt thử/sửa trước đó.
