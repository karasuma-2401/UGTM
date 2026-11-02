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
