# Nghiệm thu SCRUM-103 — Phiếu chấm GVHD và bàn giao phản biện

Cập nhật 29/10/2026. Tài liệu này là bằng chứng tích hợp cho endpoint và contract trong [AdvisorEvaluationApprovalApiContract.md](AdvisorEvaluationApprovalApiContract.md). Phạm vi gồm phiếu GVHD, quyết định chuyển phản biện và điều kiện bàn giao cho SCRUM-104; không gồm UI SCRUM-112, ghi phân công GVPB SCRUM-104, chấm GVPB SCRUM-105, xét bảo vệ hay công bố điểm.

## Quy tắc đã nghiệm thu

| Nội dung | Hành vi |
| --- | --- |
| Chủ thể | Chỉ GVHD hiện được phân công theo TopicCatalog hoặc Admin đọc/ghi phiếu. Student, Faculty chỉ có quyền đọc báo cáo theo SCRUM-102 và GVHD cũ nhận 403 với phiếu. |
| Bản báo cáo | Phiếu lưu snapshot `FinalReportId`, phiên bản, file và SHA-256 của bản `InitialFinal` đã khóa `UnderEvaluation`; server không tự chọn bản mới. |
| Điểm và phiếu | Điểm nhận từ 0 đến 10 trước khi làm tròn `AwayFromZero` hai chữ số thập phân; nhận xét là bắt buộc. GVHD/Admin được sửa trước khi đồng ý, với `evaluationVersion` để phát hiện ghi đồng thời. |
| Chưa đồng ý | Bắt buộc có lý do; báo cáo giữ khóa, nhóm giữ `FinalReportSubmitted`, SCRUM-104 không nhận eligibility. GVHD/Admin có thể sửa và xét lại. |
| Đồng ý | Trong một transaction PostgreSQL, khóa điểm/nhận xét/quyết định, chuyển nhóm sang `ReadyForReviewerAssignment`, lưu idempotency và audit ở hai schema. Không có API thu hồi trong SCRUM-103. |
| Eligibility | `IReviewerAssignmentEligibilityContract` chỉ trả binding khi phiếu đã đồng ý và khóa, nhóm sẵn sàng, báo cáo/version/file/checksum còn khớp. SCRUM-104 phải kiểm tra lại ngay trước khi ghi phân công. |
| Audit | Actor là người thao tác thực tế, vì vậy Admin thao tác thay vẫn ghi Admin. Audit quyết định không chứa nhận xét hay lý do từ chối; chỉ ghi cờ có lý do khi cần. |

## Bằng chứng test

| Phạm vi | Test nguồn | Bằng chứng |
| --- | --- | --- |
| Toàn luồng với host production | [FinalReportWorkflowIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportWorkflowIntegrationTests.cs) | Kestrel map `AddUgtmModules`/`MapUgtmModules`; PostgreSQL và MinIO thật, seed AcademicPeriods, TopicCatalog, ThesisManagement và DefenseCouncil với FK hoạt động. Kịch bản nộp v1/v2, khóa v2, Admin chấm 8.345 thành 8.35, đổi GVHD, từ chối/xét lại/đồng ý, khóa phiếu và eligibility được kiểm tra end-to-end. |
| Quyền, validation, retry và state machine HTTP | [AdvisorEvaluationApprovalHttpIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/AdvisorEvaluationApprovalHttpIntegrationTests.cs) và [AdvisorEvaluationReadHttpIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/AdvisorEvaluationReadHttpIntegrationTests.cs) | Kiểm tra quyền GVHD/Admin, Student/Faculty/GVHD cũ bị chặn, lý do bắt buộc, optimistic concurrency, idempotent replay, không thu hồi sau đồng ý và GET không ghi audit. |
| Atomicity và rollback cross-schema | [AdvisorWorkflowTransactionIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/AdvisorWorkflowTransactionIntegrationTests.cs) | PostgreSQL thật kiểm tra commit đồng thời report binding, group transition và hai audit; ép lỗi update Thesis để xác nhận rollback cả quyết định, nhóm và audit. |
| Regression SCRUM-102 | [FinalReportWorkflowIntegrationTests](../../backend/tests/ThesisManagement.Tests/Integration/FinalReportWorkflowIntegrationTests.cs) | Giữ các kịch bản version/file/MinIO, đổi GVHD và lỗi database của báo cáo cuối kỳ; fixture dùng cùng hạ tầng với luồng mới. |

Fixture toàn luồng chỉ thay thế authentication và clock để kiểm soát chủ thể/thời điểm; không giả lập PostgreSQL, MinIO, migration, module registration, FK, transaction hoặc API route. Các container tạm do Testcontainers tạo và dọn, không đụng dữ liệu đang chạy.

## Chạy lại

Yêu cầu .NET 10 và Docker Desktop ở Linux containers. Chạy từ root repository. Dùng `artifacts/cp6` để tránh khóa DLL của API đang chạy:

```powershell
dotnet build UGTM.sln --configuration Release --artifacts-path artifacts/cp6
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --configuration Release --artifacts-path artifacts/cp6 --no-build --no-restore
dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj --configuration Release --artifacts-path artifacts/cp6 --no-build --no-restore
dotnet test backend/tests/Architecture.Tests/UGTM.Architecture.Tests.csproj --configuration Release --artifacts-path artifacts/cp6 --no-build --no-restore
```

Chạy riêng toàn luồng: thêm `--filter FullyQualifiedName~FinalReportWorkflowIntegrationTests` vào lệnh ThesisManagement. Chạy rollback cross-schema: dùng `--filter FullyQualifiedName~AdvisorWorkflowTransactionIntegrationTests`. Output `artifacts/` không được đưa vào Git.

## Kết quả CP6

| Kiểm tra | Kết quả |
| --- | --- |
| Kịch bản production-host PostgreSQL/MinIO: `Real_workflow_hands_off_only_an_approved_locked_evaluation_across_all_modules` | **1 passed, 0 failed** |
| Giao dịch cross-schema PostgreSQL: `AdvisorWorkflowTransactionIntegrationTests` | **2 passed, 0 failed** |
| API HTTP chấm, xét lại/duyệt, quyền đọc và eligibility | **10 passed, 0 failed** với PostgreSQL Testcontainers; gồm validation, concurrent version, idempotent replay, rollback audit, đổi GVHD, riêng tư Student/Faculty và eligibility. |
| Build Release toàn solution | **Pass: 0 error**. Có 1 warning EF Core `10.0.4`/`10.0.12` đã tồn tại tại `UGTM.Architecture.Tests`. |
| DefenseCouncil regression | **24 passed, 0 failed** |
| Architecture regression | **74 passed, 0 failed** |
| Format | `dotnet format UGTM.sln --no-restore --verify-no-changes`: **pass**. |
| Toàn bộ suite ThesisManagement | Chưa có kết quả hoàn tất: lượt chạy đầy đủ không sinh kết quả hay TRX thêm sau hơn 10 phút và đã được dừng để giải phóng Docker. Các suite mục tiêu ở trên đã pass và bao phủ luồng SCRUM-103. |

