# SCRUM-106 — bằng chứng theo checkpoint

Ngày 02/11/2026, baseline CP0 `767b8e8`. [Contract](FinalSubmissionApiContract.md) · [Schema/transaction/handoff](ArchivalScoreWorkflowDesign.md).

## Phạm vi CP1

CP1 có POST `/api/thesis/final-submissions` và sáu GET: `/my-group`, `/submission-eligibility`, `/{id}`, `/{id}/packages`, `/{id}/packages/{packageVersion}/files/{fileRole}/download`, danh sách `/api/thesis/final-submissions`. Selector groupId bắt buộc ở API cá nhân; quyền dựa trên Active/Confirmed membership và role/permission thực trong DB.

Migration `20261102061935_AddFinalSubmissionArchivalWorkflow` tạo đầy đủ package, link hai role, review history, receipt, upload attempt, current-package binding deferred, aggregate xmin/legacy snapshot; phần review dùng ở CP2. Giữ partial unique của FinalReport. Schema từ chối package thiếu file lúc commit và chặn sửa lịch sử. Legacy được giữ nguyên và yêu cầu xmin khi chuyển sang package mới; không giả archive proof. Down không được làm mất dữ liệu workflow hoặc drop system xmin.

S3 upload ngoài transaction nghiệp vụ. Reader group/topic/batch/authority/defense cùng một PostgreSQL transaction sau upload; chốt window bằng clock sau guards. Coordinator chỉ cho ThesisManagement ghi, khôi phục reader connections dùng được sau transaction replay ngắn và transaction commit. Hai save lấy xmin thật rồi ghi DTO receipt/audit cùng commit. Cleanup dùng context mới, row lock/CAS/lease/backoff; giữ object của attempt Committed, sweep lại file đến muộn sau cleanup hết lease.

PUT `confirm-archive`, các hành vi AdvisorConfirm/FacultyConfirm/RequestRevision và producer ArchiveProof thuộc **CP2**, chưa map/đăng ký ở CP1. SCRUM-110 chưa có runtime mới. Không tự stage/commit hoặc migrate DB ứng dụng.

## Kiểm tra

Các ca ở [workflow HTTP/PostgreSQL/MinIO](../../backend/tests/ThesisManagement.Tests/Integration/FinalSubmissionWorkflowIntegrationTests.cs), [đọc và persistence](../../backend/tests/ThesisManagement.Tests/Integration/FinalSubmissionReadAndPersistenceIntegrationTests.cs), [domain/metadata](../../backend/tests/ThesisManagement.Tests/FinalSubmissionWorkflowTests.cs). Fixture dùng production module registration/coordinator/readers, PostgreSQL FK/migration và S3 thật; chỉ authentication/clock cùng hook lỗi/delay storage do test điều khiển.

| Tiêu chí | Bằng chứng CP1 |
| --- | --- |
| FS-01 | Own selector bắt buộc/sai/trùng/IDOR, Student có hai nhóm khác đợt chọn đúng group, quản trị không nộp thay. |
| FS-02–03 | AllScoresEntered/Open/UnlockedByOverride bị chặn; deadline inclusive theo lịch Asia/Ho_Chi_Minh; kiểm tra lại minute/window/batch sau S3. |
| FS-04–05 | PDF + DOCX thật, trim metadata, rune/UTF-16/NUL, strict multipart; request hai file 18 MiB/file đi qua Kestrel vượt default 30 MB. |
| FS-06 | Nộp lại Submitted bằng thành viên khác giữ submission ID, tăng version, giữ file/package/audit; stale xmin và hai lần nộp đầu đồng thời. NeedRevision có domain support, luồng API RequestRevision thuộc CP2. |
| FS-07–09 | CP1 mới có schema review/freeze và domain chặn replacement sau xác nhận; chưa tuyên bố nghiệm thu hai bước xác nhận CP2. |
| FS-10–11 | Batch/membership/role/minute/deadline thay đổi sau upload bị chặn; same-key replay và concurrent first-create; lỗi file thứ hai/receipt rollback, cleanup lỗi được retry, worker claim hết lease chặn commit và dọn file tới muộn. |
| FS-12 | Own/advisor/private presigned download, người không liên quan bị chặn; Faculty/PĐT role + read; Student không dùng management list; Closed/Archived vẫn đọc. MinIO trả attachment với tên gốc tiếng Việt qua Content-Disposition/filename*. SDK ký URL theo độ chính xác giây, test kiểm tra TTL 300 giây với sai số 1 giây và bytes thực tải. |
| FS-13 | Fresh migration, legacy roundtrip/xmin/snapshot, chống Down làm mất workflow, constraints và partial unique cũ được giữ. |
| FS-14 | ArchiveProof producer ở CP2. CP1 kiểm tra Locked-defense owner Lock ngoài shared transaction bị chặn và coordinator không cho reader ghi schema khác. |

Kết quả kiểm tra theo phạm vi tác động, không cộng lại các lượt debug/retry:

| Lượt kiểm tra | Kết quả | Artifact local |
| --- | --- | --- |
| ThesisManagement Release: production FinalReport workflow + CP1 domain/HTTP/persistence/migration | 65/65, không skip; gồm 47 ca CP1 và 18 hồi quy | cp1-release-final.trx |
| Kiểm tra lại phần tải file cuối: archival workflow, storage và đường tải cũ | 6/6, không skip; hai ca lặp từ lượt 65, bốn ca storage bổ sung | cp1-storage-final.trx |
| Architecture Release, toàn project | 74/74, không skip | architecture-release-final.trx |
| Council scoring/application/endpoints Release, filtered | 125/125, không skip | council-release-final.trx |

Tổng **268 ca duy nhất** trong các lượt trên; đây không phải toàn bộ test solution hoặc toàn bộ quy trình 106/110. Build Release toàn solution thành công; còn warning nullable OpenAPI và xung đột patch version EF trong project Architecture.Tests. Format được kiểm tra bằng `dotnet format UGTM.sln --no-restore --verify-no-changes`. Gitleaks v8.24.3 quét candidate cục bộ, không thay kết quả CI hoặc quét lịch sử commit.

Commands/logs/TRX và bảng tổng hợp nằm trong `artifacts/scrum106-110-cp1/` (ignored). Không stage/commit/push; migration chỉ được áp trong các container test tạm.

## Phụ thuộc cần giữ rõ

Các test seed `CouncilMinute.Locked` có actor/time/binding để kiểm chứng consumer. **Chưa có E2E từ API khóa biên bản**; API đó thuộc task Hội đồng. Không thêm endpoint production hoặc trả proof luôn hợp lệ để lấp phụ thuộc này. Xác nhận lưu chiểu, tính và công bố điểm chưa thuộc bằng chứng CP1.

## Phạm vi CP2

Baseline CP1 đã được người dùng review/commit tại `0fa151a`. CP2 bổ sung PUT `/{id}/confirm-archive` với AdvisorConfirm, FacultyConfirm, RequestRevision; reason chuẩn hóa, expected aggregate xmin/package version và Idempotency-Key bắt buộc. GVHD hiện tại/Admin bước 1, Faculty + manage/Admin bước 2, Faculty/PĐT không sửa thay GVHD; Admin ghi actor thật. Faculty có manage nhưng không có read vẫn xác nhận được, PĐT chỉ đọc. Staff ghi khi Open/Closed, không áp hạn SV; Archived chỉ đọc hoặc replay ý định thành công sau kiểm tra quyền.

Xác nhận kiểm tra package/metadata/manifest và đủ hai file, lưu review bất biến gắn đúng version/context và Locked defense snapshot. Khoa so lại snapshot của GVHD; nếu GVHD/biên bản đổi thì từ chối xác nhận, cho RequestRevision để phục hồi. Trả sửa xóa projection xác nhận, giữ review cũ; nộp lại tạo package mới và vượt lại các gate. ArchiveConfirmed chặn mọi ý định sửa mới kể cả Admin.

Get/Lock ArchiveProof là producer thật, trả Succeeded chỉ khi package/two-file/two-review đầy đủ, đúng binding/manifest/actor/time/thứ tự. Proof đã khóa không phụ thuộc quyền/tài khoản/GVHD/minute hiện tại. Consumer SCRUM-110 phải dùng contract nguồn điểm/biên bản riêng để xét dữ liệu hiện tại. Lock cần shared transaction cùng group/context guards của caller, giữ khóa owner aggregate/package/link/file/review tới commit, không lấy lại khóa root muộn và không đổi xmin.

CP2 dùng schema/migration CP1, không có migration mới. Không đổi runtime ScoreSummary hoặc bước chấm Hội đồng; hai phần này thuộc CP3. Không tự commit hoặc migrate DB ứng dụng.

Các kiểm tra CP2 nằm tại [HTTP/PG/MinIO và concurrency](../../backend/tests/ThesisManagement.Tests/Integration/FinalSubmissionConfirmationIntegrationTests.cs), [validation proof/domain](../../backend/tests/ThesisManagement.Tests/FinalSubmissionProofValidationTests.cs), cùng [OpenAPI và hồi quy persistence](../../backend/tests/ThesisManagement.Tests/Integration/FinalSubmissionReadAndPersistenceIntegrationTests.cs).

| Tiêu chí | Bằng chứng CP2 |
| --- | --- |
| FS-03, FS-07 | Open/Closed sau hạn SV; Draft/Archived chặn ý định mới; quyền live GVHD/Khoa/Admin/PĐT, permission manage độc lập read; không bỏ bước 1, ghi đúng actor. |
| FS-06, FS-08 | Nộp v1 → trả sửa có lý do trim → v2 → hai xác nhận; trả sửa sau AdvisorConfirmed bởi GVHD/Faculty/Admin giữ review cũ và xóa projection; replay reason chuẩn hóa. |
| FS-09 | ArchiveConfirmed khóa upload/ba action mới kể cả Admin; replay trả DTO/xmin gốc sau seal/Archived, mất ownership/permission thì 403. |
| FS-10–11 | Same-key concurrent replay một lần, stale aggregate/package version, FacultyConfirm vs RequestRevision; upload thua xác nhận được cleanup; lỗi receipt/audit rollback toàn bộ state/review/xmin/receipt/audit. |
| FS-12, SP-17 | Strict JSON/header/query/content type/body cap/reason encoding; GET history phản ánh review đúng package; OpenAPI có PUT, JSON schema/action enum và Idempotency-Key. |
| FS-13–14 | Không thay model/migration CP1; thiếu/legacy/unconfirmed proof không trả dữ liệu thành công một phần; validation chống sai file/checksum/MIME/link/version/metadata/review/snapshot; sealed proof không đổi vì live advisor/grants/minute. Kết nối PG độc lập bị lock timeout trên cả năm bảng aggregate/package/link/file/review trong lúc consumer giữ proof; đọc xong commit không tăng xmin/ghi audit, khóa được nhả sau commit. |

| Lượt kiểm tra CP2 | Kết quả | Artifact local |
| --- | --- | --- |
| ThesisManagement Release: toàn bộ Archival + domain/validation proof | 115 ca: 114 đạt, một ca SQL của test giữ khóa lỗi cú pháp sau analyzer fix | cp2-final.trx |
| Chạy lại sau sửa test SQL và hoàn thiện audit manifest: giữ khóa, workflow v1/v2, rollback receipt/audit, OpenAPI | 5/5 đạt, gồm ca giữ khóa đã sửa và bốn ca lặp | cp2-corrections.trx |
| Architecture Release toàn project trên build cuối | 74/74 đạt | architecture-final.trx |

Tổng **189 ca duy nhất có kết quả cuối đạt, không skip**: 115 ThesisManagement (68 ca CP2 mới, 47 hồi quy CP1) + 74 Architecture; không cộng lượt debug hoặc các ca lặp. Không diễn giải lượt 115 là một lần chạy sạch: ca giữ khóa đã được sửa và kiểm tra lại, vẫn giữ TRX gốc để đối chiếu. Lỗi nằm trong test SQL dùng identifier bị analyzer đổi sang tham số, không phải bằng chứng owner không giữ khóa. Test cuối dùng năm câu SQL cố định và kiểm tra mã `55P03` trên kết nối độc lập.

Release solution build cuối thành công (0 errors); còn warning nullable OpenAPI và xung đột patch-version EF ở Architecture.Tests như baseline. Artifacts, script tổng hợp, logs và TRX trong `artifacts/scrum106-110-cp2/` (ignored). Fixture vẫn seed biên bản Locked trong container, không chứng minh API upstream khóa biên bản. Luồng tính/công bố và kiểm tra E2E toàn bộ SCRUM-110 chưa thuộc CP2.

`dotnet format UGTM.sln --no-restore --verify-no-changes` đạt trên source cuối. Script `Validate-Checkpoint.ps1` xác minh 189 kết quả duy nhất, 27 file candidate, 173 relative links, 5 JSON examples, fences/whitespace/scope/index sạch và plan/artifacts ignored. Gitleaks v8.24.3 quét candidate `--no-git --redact=100`: không có finding; đây là quét local, không khẳng định CI hoặc toàn bộ lịch sử commit đã xanh. Không stage/commit/push hoặc áp migration vào DB ứng dụng.
