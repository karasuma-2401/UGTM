4# SCRUM-106/110 — nghiệm thu chung checkpoint 5

Baseline CP4: `aa5432e`. Contract: [lưu chiểu](FinalSubmissionApiContract.md), [điểm](ScoreCalculationPublicationApiContract.md), [transaction và handoff](ArchivalScoreWorkflowDesign.md). Kết quả CP1–CP4 là bằng chứng từng giai đoạn, không cộng vào kết quả chạy CP5: [lưu chiểu](FinalSubmissionVerification.md), [tính điểm](ScoreCalculationVerification.md), [công bố](ScorePublicationVerification.md).

Ca [xuyên suốt mới](../../backend/tests/ThesisManagement.Tests/Integration/ArchivalScoreEndToEndIntegrationTests.cs) sử dụng Kestrel, PostgreSQL và MinIO: InitialFinal → nộp lưu chiểu v1 → nộp lại v2 → từ chối duyệt version cũ → GVHD → Khoa → tính → công bố → sinh viên xem snapshot. Role được kiểm tra từ Identity DB; authentication và clock được fixture điều khiển. Điểm GVHD/GVPB/Hội đồng, roster và biên bản Locked được dựng trong container: chưa có E2E từ API khóa biên bản của task Hội đồng.

## Ma trận bằng chứng

Tên trong bảng chỉ các test trong [upload/lifecycle](../../backend/tests/ThesisManagement.Tests/Integration/FinalSubmissionWorkflowIntegrationTests.cs), [xác nhận/proof](../../backend/tests/ThesisManagement.Tests/Integration/FinalSubmissionConfirmationIntegrationTests.cs), [đọc/persistence](../../backend/tests/ThesisManagement.Tests/Integration/FinalSubmissionReadAndPersistenceIntegrationTests.cs), [validation proof](../../backend/tests/ThesisManagement.Tests/FinalSubmissionProofValidationTests.cs), [resolver](../../backend/tests/DefenseCouncil.Tests/ScoreSummaryResolverTests.cs), [score guards](../../backend/tests/ThesisManagement.Tests/Integration/ScoreSummaryGuardsIntegrationTests.cs), [score concurrency](../../backend/tests/ThesisManagement.Tests/Integration/ScoreSummaryConcurrencyIntegrationTests.cs), [publish](../../backend/tests/ThesisManagement.Tests/Integration/ScorePublicationIntegrationTests.cs) và [kiểm tra bổ sung CP5](../../backend/tests/ThesisManagement.Tests/Integration/ArchivalScoreConcurrencyAcceptanceTests.cs). Suffix trong bảng rút gọn tên method nhưng giữ nguyên từ khóa để tìm trong source/TRX.

| ID | Bằng chứng và giới hạn |
| --- | --- |
| FS-01 | `Archival_own_endpoints_require...`, `Archival_student_with_two_batches...`, management reads không cấp Student upload. |
| FS-02 | `Archival_unlocked_or_incomplete_minutes...`, kiểm tra lại minute khi upload; Locked được seed. |
| FS-03 | `Archival_effective_timezone_window_is_inclusive`, `Archival_staff_confirmation_ignores_student_deadline...`, AcademicPeriods window tests. |
| FS-04 | `Archival_strict_multipart_validation...`, `Metadata_counts_runes...`, package/proof validation. |
| FS-05 | `Archival_two_large_valid_files_exceed_default_30MB_body_limit` qua Kestrel và MinIO; không kiểm tra reverse proxy triển khai thực tế. |
| FS-06 | `Archival_real_package_replacement...`, concurrent first submissions, stale version và ca xuyên suốt v1/v2. |
| FS-07 | `Archival_confirmation_two_steps...`, live authority và `Archival_faculty_manage_permission...`. |
| FS-08 | `Archival_confirmation_two_steps_revision...`, `Archival_revision_after_advisor_confirmation...`. |
| FS-09 | `Archival_sealed_proof_survives...`, replay sau seal và domain `Archival_sealed_domain_rejects...`. |
| FS-10 | upload thua confirm, replacement thắng confirm, faculty confirm vs revision, `Archival_changes_after_S3...`. |
| FS-11 | lỗi file thứ hai/delete, receipt rollback, expired attempt/cleanup; không có cam kết transaction vật lý DB/S3. |
| FS-12 | `Archival_real_package_replacement...` kiểm tra file tải thật, tên, TTL; Student/lecturer ngoài phạm vi bị chặn. |
| FS-13 | `Archival_migration_roundtrip...`, `Archival_migration_rejects_malformed_legacy...`, schema/fresh migration. |
| FS-14 | `Archival_archive_proof_share_locks...`, missing/legacy proof và `Archival_proof_fails_closed...`. |
| SP-01 | `ScoreSummary_private_reads_and_writes...`, `ScoreSummary_manage_without_read...`, `ScorePublication_rejects_whole_selection...`. |
| SP-02 | `ScoreSummary_missing_prerequisites...`, resolver incomplete/ambiguous và ca xuyên suốt dùng ArchiveProof thật. |
| SP-03 | `ScoreSummary_resolver_uses_source_applicability...` và HTTP `ScoreSummary_real_archival_to_dynamic...` cho cả 47/6 và 53/7. |
| SP-04 | resolver source/applicability, dynamic nondefault và percentage normalization. |
| SP-05 | resolver percentage/exclusion, raw average, midpoint AwayFromZero; numeric roundtrip trong PostgreSQL. |
| SP-06 | `ScoreSummary_resolver_fails_closed_on_incomplete_or_ambiguous_inputs`, reviewer trong/ngoài Hội đồng. |
| SP-07 | resolver fail-closed Custom/ambiguous, `ScoreSummary_missing_prerequisites...` thiếu hoặc invalid config. |
| SP-08 | tính lại hai revisions, `ScoreSummary_legacy_recalculation...` giữ snapshot legacy. |
| SP-09 | `ScoreSummary_read_stale...`, `ScorePublication_rejects_whole_selection...` với config/title; hai thứ tự config/publish trong kiểm tra CP5. |
| SP-10 | `ScorePublication_publishes_two_valid_groups_atomically...`: cả hai thành công; một xmin sai chặn cả hai, group không tồn tại cũng chặn. |
| SP-11 | calculate/publish same-key đồng thời, key collision, replay và đảo thứ tự selection; không audit/receipt đôi. |
| SP-12 | config thay trước/sau publish trong kiểm tra CP5; `ScorePublication_holds_source_locks...`, calculate config reselect. Chưa có writer API khóa/mở khóa biên bản thật để chạy race qua endpoint đó. |
| SP-13 | lỗi save/audit/receipt, `ScoreSummary_cancelled_request...`, cancellation publish CP5 và reader-schema mutation rollback. |
| SP-14 | Student own group trước/sau publish, ngoài nhóm, selector trùng, hai nhóm và hai đợt; ca hai đợt có một nhóm công bố và một nhóm chưa công bố. |
| SP-15 | discrepancy/private reads kiểm tra quyền live, reviewer feedback chỉ comment, Student DTO allowlist và ignored component. |
| SP-16 | `ScorePublication_publishes_verified_snapshot...` đổi tiêu đề và chuyển Archived sau publish; config thay sau publish trong kiểm tra CP5. |
| SP-17 | `Archival_openapi_describes...`, `ScoreSummary_strict_HTTP_and_OpenAPI...`: schemas, selector/header, giới hạn selection; đây không phải kiểm thử mọi combination HTTP. |
| SP-18 | Ca xuyên suốt v1/v2 đến Student snapshot, với giới hạn seed upstream nêu trên. |

## Kết quả và giới hạn

Ngày 03/11/2026, sau khi sửa deadlock hủy/chấm phân công GVPB, chạy `dotnet test UGTM.sln -c Release --no-build --no-restore -m:1` trên build Release của source hiện tại. **1.676/1.676 ca đạt, không skip**: Architecture 74, AcademicPeriods 45, DefenseCouncil 245, ThesisManagement 845, Identity 220 và TopicCatalog 247. Trong đó có năm ca `ArchivalScore_` CP5, gồm E2E và hai thứ tự cạnh tranh cấu hình/công bố. Đây là một lượt toàn solution; không cộng lượt tái hiện 9 ca trước và sau sửa vào tổng.

Trước sửa, test tập trung tái hiện đúng bốn ca fail của GitHub Actions (5/9 đạt); sau sửa cả 9/9 đạt và bốn ca đó cũng đạt trong lượt toàn solution. Build Release và `dotnet format UGTM.sln --no-restore --verify-no-changes` thành công; build còn warning nullable OpenAPI và xung đột patch version EF ở Architecture.Tests. Gitleaks v8.24.3 quét các file sửa lỗi cục bộ không phát hiện secret; kết quả này không thay thế CI GitHub trên commit tiếp theo.

TRX/log của lượt toàn solution và tái hiện nằm trong `artifacts/ci-reviewer-deadlock/`, ngoài Git. Chỉ migrate DB trong container test; không migrate DB ứng dụng, không tự commit/push/deploy. Biên bản Locked và các đầu điểm upstream được seed như nêu trên; chưa xác minh E2E qua API khóa biên bản. Không gồm export, email hoặc thu hồi/điều chỉnh điểm sau công bố.
