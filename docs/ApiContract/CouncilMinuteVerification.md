# Nghiệm thu công thức và transaction biên bản hội đồng

Phần sửa P1 trước đợt này đã được lưu tại commit `2cd1716`. Tài liệu này ghi phạm vi tiếp theo: preview điểm và thao tác lưu/khóa/mở biên bản. Không thay đổi database đang vận hành, không push hoặc deploy.

## Hành vi API

- Workspace và `PUT /api/councils/assignments/{assignmentId}/minute?preview=true` dùng cùng bộ phân giải cấu hình và calculator với tổng kết điểm. Hỗ trợ trọng số, applicability, CouncilAverage và loại điểm; làm tròn cuối bằng `AwayFromZero`.
- Không có cấu hình Active hợp lệ hoặc thiếu/nhập nhằng nguồn điểm: không trả điểm cuối giả định; `finalRoundedScore` là null, `isReadyToLock` là false và có `calculationErrorCode`. Preview trả `configurationId` và `configurationVersion`.
- Preview không ghi dữ liệu. Lưu thật yêu cầu thư ký còn vai trò Lecturer, Admin hoặc AcademicAffairs. Mở khóa yêu cầu Admin hoặc Faculty có quyền `councils.minutes.unlock`; kiểm tra quyền hiện tại trong database.
- Lưu, khóa và mở khóa cùng lấy khóa nhóm rồi khóa nguồn điểm trong transaction tổng kết. Kiểm tra lại quyền và trạng thái công bố sau khi lấy khóa. Nguồn điểm đã công bố bị từ chối với HTTP 409.
- Khi loại điểm hoặc thay đổi cờ loại điểm, body lưu thật bắt buộc có `reason` riêng từ 10–1000 ký tự. `studentFeedback` vẫn là góp ý cho sinh viên. Lý do được lưu trên nguồn điểm bị loại và trong audit; audit có dữ liệu trước/sau. Không lấy góp ý làm lý do thay thế.
- Không chấp nhận marking lạ, đầu điểm chưa chấm hoặc vai trò chỉ tham dự làm mục tiêu loại điểm. Góp ý, cờ loại điểm và audit cùng commit hoặc cùng rollback. Lưu biên bản không tự tạo revision tổng kết mới; gọi workflow tổng kết để tính lại.
- Xác thực lại mật khẩu dùng transaction Identity riêng để lưu được audit và bộ đếm sai mật khẩu. Sau đó workflow khóa biên bản kiểm tra lại quyền và trạng thái dưới khóa dữ liệu.

Ví dụ lưu thật (sau khi biên bản được mở khóa đúng thẩm quyền):

```json
{
  "secretaryMarking": "RejectedAsPerMinute",
  "targetComponentCode": "REVIEWER",
  "studentFeedback": "Bổ sung giải trình phương pháp nghiên cứu.",
  "reason": "Hội đồng xác định phiếu phản biện không hợp lệ."
}
```

## Regression test

Test `DefenseCouncil.Tests` kiểm tra công thức cấu hình so với kết quả chính thức, trường hợp phản biện trong/ngoài hội đồng, tỷ lệ phần trăm, CouncilAverage, loại điểm, midpoint và thiếu cấu hình/nguồn điểm. HTTP test kiểm tra quyền, lý do riêng, marking và mục tiêu loại điểm không hợp lệ.

`CouncilMinuteWorkflowIntegrationTests` dùng composition thực với PostgreSQL và MinIO tạm:

1. Mở khóa → lưu lý do riêng và góp ý → xác thực lại mật khẩu thực → khóa; kiểm tra audit trước/sau.
2. Ép insert audit thất bại bằng CHECK constraint; kiểm tra góp ý và cờ loại điểm rollback.
3. Giữ khóa nhóm, cho request lưu/khóa/mở chờ, công bố điểm rồi nhả khóa; cả ba request trả 409 và không sửa dữ liệu.
4. Gửi token có claim Admin cho tài khoản thực chỉ còn Lecturer; không được mở khóa.

`ScoreSummaryWorkflowIntegrationTests` đối chiếu preview HTTP với tổng kết thực ở cấu hình động và CouncilAverage giữ nguyên độ chính xác decimal.

## Giới hạn nghiệm thu và phần còn lại

Các regression test trên không chứng minh toàn bộ hệ thống đã sẵn sàng production. Các mục sau vẫn cần triển khai hoặc nghiệm thu riêng theo báo cáo gốc:

- AI job claim/lease khi chạy nhiều instance và toàn bộ retry/resume.
- Tính đầy đủ của ràng buộc dữ liệu, liên kết đa hình, cleanup dữ liệu kỹ thuật và invariant còn phân tán.
- Đo hiệu năng phân trang tổng kết, overview hội đồng và bộ nhớ khóa theo nhóm/đề tài.
- Đối chiếu từng FR cho thông báo in-app, nhắc deadline, email công bố, Excel/PDF và transition nhóm sau bảo vệ.
- Readiness dependency, migration/cutover, smoke test, monitoring/alert, rollback và diễn tập backup/restore database, storage, Data Protection keys.
- SMTP/AI thật, VPS, cấu hình domain/cookie trên trình duyệt và tải đồng thời chưa được kiểm chứng bằng bộ test này.

## Kết quả thực chạy ngày 08/11/2026

| Kiểm tra | Kết quả |
| --- | --- |
| DefenseCouncil.Tests, Release | 413/413 đạt |
| Architecture.Tests, Release | 74/74 đạt |
| 6 tình huống integration biên bản và 3 tình huống cấu hình/tổng kết nêu trên | 9/9 đạt, PostgreSQL + MinIO Testcontainers, 5 phút 30 giây |
| API build Release, `--no-restore` | Đạt |
| `dotnet format whitespace UGTM.sln --no-restore --verify-no-changes --include <các file C# thay đổi>` | Đạt |
| `git diff --check` | Đạt |

Build integration vẫn ghi nhận cảnh báo nullable CS8602 tại `OpenApiConfiguration.cs:87`; build architecture còn cảnh báo MSB3277 do phiên bản EF Core khác nhau. Build API cuối chạy incremental đạt 0 warning/0 error; không dùng kết quả này để kết luận các cảnh báo trước đó đã được sửa. Chưa chạy lại toàn bộ integration suite ThesisManagement hoặc xác nhận hạ tầng production trong lượt này.
