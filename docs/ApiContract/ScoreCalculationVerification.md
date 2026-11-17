# SCRUM-110 — bằng chứng checkpoint 3

Baseline CP2 được người dùng review/commit tại `3f2fd33`. [Contract điểm](ScoreCalculationPublicationApiContract.md) · [Schema/transaction](ArchivalScoreWorkflowDesign.md) · [Lưu chiểu CP2](FinalSubmissionVerification.md). Plan và artifacts ngoài Git; người dùng tự review/commit.

## Phạm vi đã triển khai

CP3 có POST `/api/thesis/scores/calculate/{groupId}` và ba GET private: `/group/{groupId}`, `/readiness/{groupId}`, danh sách `/api/thesis/scores?batchId=...`. Faculty + manage/Admin tính; Faculty/PĐT + read/Admin đọc, dùng quyền live và không yêu cầu có read để dùng manage. API ghi có JSON/header/query/type/range guards, expected xmin/config version, receipt actor+operation+key, replay sau recheck quyền. Chỉ Open/Closed, không áp hạn SV. Student không đọc private qua token tự nhận Admin hoặc discrepancy.

Migration `20261102141732_AddScoreSummaryWorkflow` tạo revision/components/receipt và đủ published pointer/actor/time cho CP4; bổ sung xmin của summary/assignment/roster/score, không tạo/drop system xmin. FK revision deferred, projection/immutability guards; legacy giữ gốc/components/snapshot, không tự được xác minh. Preflight chặn legacy sai trước DDL; Down chặn workflow data/precision không biểu diễn được ở schema cũ. Permission/grants seed idempotent và giữ tùy chỉnh khi downgrade. Chỉ áp migration trong container test.

Resolver dùng Source/Applicability của đúng một Active config từ owner AcademicPeriods, không match ComponentCode. Đủ Advisor/Reviewer hoàn tất, binding đúng InitialFinal report/file/checksum; đủ roster/scores/details của assignment và Locked minute có metadata hợp lệ. Không áp MinimumScore/RequiresApproval thành gate mới hoặc gán 0 cho nguồn thiếu. Custom/nhiều UV/source overlap fail closed; exclusion có reason vẫn giữ nguồn trong snapshot, loại cả điểm/trọng số. CouncilAverage tính decimal sau loại điểm, không round trung gian; final AwayFromZero hai số.

Calculate dùng `ScoreSummaryInputLoader.LockManyAsync`: group roots trước topics/batches/advisors/authority/archive/grade/config/summary, mỗi pha hoàn tất cho IDs đã sort trước pha sau. ArchiveProof CP2 thật tham gia cùng backend/transaction, không mutate reader schemas. Config bị retire trong lúc chờ được chọn lại bằng statement mới, tối đa ba lần. Save đầu lấy xmin, save sau ghi actual-actor audit/receipt, commit chung; lỗi hoặc hủy request rollback toàn bộ.

Mỗi lần tính tạo revision bất biến: raw/final/weight, formula, source/config snapshots và fingerprint. Snapshot nguồn có prefix `ThesisScoreInputs:v1`, gồm cả tiêu đề để CP4 giữ metadata đã công bố; fingerprint gắn policy v1/AwayFromZero/2 và hai snapshot canonical. GET/history trả active components và từng revision/snapshot riêng; không tạo Pending row hay lưu cờ stale. Danh sách đọc theo lô trước filter/count/page. Config/nguồn/xmin/minute/roster/title đổi trước publish làm stale; Published giữ snapshot cũ.

SubmitCouncilScore chỉ ghi score/details và AllScoresEntered, không còn writer summary/công thức mặc định. Guard production giữ root/grade locks tới commit, chặn Published. Trigger backstop kiểm tra binding OLD/NEW, không cho chuyển nguồn ra khỏi nhóm đã Published. Discrepancy kiểm tra live authority và đúng council scope; chỉ trả finalScore nonlegacy Calculated/Published.

## Kiểm tra theo tiêu chí

Test chính: [resolver](../../backend/tests/DefenseCouncil.Tests/ScoreSummaryResolverTests.cs), [workflow thật](../../backend/tests/ThesisManagement.Tests/Integration/ScoreSummaryWorkflowIntegrationTests.cs), [guards/persistence](../../backend/tests/ThesisManagement.Tests/Integration/ScoreSummaryGuardsIntegrationTests.cs), [concurrency](../../backend/tests/ThesisManagement.Tests/Integration/ScoreSummaryConcurrencyIntegrationTests.cs), [cancellation](../../backend/tests/ThesisManagement.Tests/Integration/ScoreSummaryCancellationIntegrationTests.cs), cùng hồi quy Hội đồng/lưu chiểu và Architecture.

| Tiêu chí | Bằng chứng CP3 |
| --- | --- |
| SP-01, SP-15 | DB role/permission thắng token giả; Faculty manage độc lập read; PĐT chỉ đọc; Student/private/discrepancy và Lecturer ngoài roster bị chặn. Discrepancy không trình bày legacy finalScore như kết quả mới. |
| SP-02 | Thiếu archive/minute/advisor/reviewer/details/config, binding sai, future timestamp: không persist một phần. Locked khác AllScoresEntered. |
| SP-03–05 | 47/6 → 7.83, 53/7 → 7.57; hệ số khác mặc định, Percentage/exclusion, source thay vì label; midpoint 7.845 → 7.85. CouncilAverage recurring decimal roundtrip qua numeric không bị cắt scale 4. |
| SP-06–07 | Reviewer kiêm Member không có CouncilScore thứ hai; reviewer Chair/advisor trong council, nhiều UV, duplicate/average-role overlap, Custom, thiếu/invalid config, hết trọng số: fail closed. |
| SP-08–09 | Recalculate/history/legacy; stale cả khi điểm không đổi nhưng xmin mới, config/minute/ignored/roster/title đổi. Published dựng để kiểm tra guards không tự stale vì live config/title, snapshots cũ giữ nguyên. |
| SP-11–13 | Expected xmin/config, same-key concurrent replay/collision, replay sau Archived/mất permission; retire Active trong lúc chờ reselect mới; giữ source locks tới receipt; save/audit/receipt/cancellation/reader mutation rollback. Năm contexts cùng backend. |
| SP-17, FS-14 | OpenAPI JSON/header/private selectors; publish/Student GET chưa map. Dùng Get/Lock ArchiveProof thật; hồi quy share locks/proof/v1-v2/history/download CP2. |

Lượt chính đạt 37/37 workflow PG/MinIO, 162/162 resolver/Hội đồng và 74/74 Architecture. Trên source cuối, lượt bổ sung đạt 47/47 resolver/discrepancy và 20/20 integration (timestamp, precision, cancellation, stale/title và legacy). Tổng hợp theo tên ca, lấy kết quả mới nhất: **281 ca khác nhau đều Passed, không skip**; không cộng smoke hoặc test lặp thành ca mới. Trong đó 66 ca mới CP3 (28 resolver, 38 integration), 215 ca hồi quy.

`dotnet build UGTM.sln -c Release --no-restore` thành công, 0 lỗi; còn 3 warning nền (nullable OpenAPI được compile ở host/test và EF patch-version conflict ở Architecture). `dotnet format UGTM.sln --no-restore --verify-no-changes --verbosity quiet` và `git diff --check` thành công. Kiểm tra 62 candidate files, 188 relative links và 11 ví dụ JSON hợp lệ; index trống, plan/artifacts được ignore. Gitleaks v8.24.3 không phát hiện secret trong candidate files; đây là candidate-only, không thay thế CI/history scan. Kết quả lưu cùng TRX/log trong `artifacts/scrum106-110-cp3/` ngoài Git.

## Giới hạn bàn giao

- PUT publish và GET Student chưa map. Atomic selected publication/public allowlist và nghiệm thu công bố thuộc CP4; schema/pipeline/guards đã sẵn.
- PostgreSQL/MinIO/Kestrel, production registration/coordinator/readers thật. InitialFinal submit/begin và lưu chiểu/xác nhận gọi qua API; Advisor/Reviewer/Council points, roster và Locked minute dùng domain factories/seed trong container. Chưa chứng minh E2E từ API khóa biên bản hoặc toàn bộ producer APIs upstream.
- Published trong test là seed kiểm tra guards/snapshot, không phải endpoint CP4. Writer khóa/mở khóa/loại điểm tương lai vẫn phải lấy root trước grade rows và recheck Published trong shared transaction; trigger là backstop.
- Không commit/push/deploy hoặc migrate DB ứng dụng; Excel/PDF/email và thu hồi/điều chỉnh sau công bố ngoài phạm vi.
