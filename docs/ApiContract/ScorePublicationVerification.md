# SCRUM-110 — kiểm tra checkpoint 4

CP4 dùng lại schema, resolver và shared transaction của [CP3](ScoreCalculationVerification.md). API và quy tắc đầy đủ tại [contract điểm](ScoreCalculationPublicationApiContract.md).

- Publish nhận batchId, 1–100 group/version và Idempotency-Key. Kiểm tra tất cả group dưới khóa trước khi thay đổi trạng thái; lỗi trả mã cho từng group trong `error.errors`. Receipt và audit ghi cùng transaction, dùng actor thực tế. Retry cùng ý định trả kết quả gốc sau kiểm tra quyền live.
- Student GET kiểm tra account Active, role Student và membership Confirmed của đúng group trước khi đọc; dùng published revision cùng proof đã lưu trong snapshot. DTO chỉ gồm tiêu đề, kết quả và contribution được công nhận. Không đọc lại điểm/cấu hình hiện tại để dựng kết quả.
- Kiểm thử tại [ScorePublicationIntegrationTests.cs](../../backend/tests/ThesisManagement.Tests/Integration/ScorePublicationIntegrationTests.cs): quyền và selector, dữ liệu thiếu/stale, snapshot và allowlist sinh viên, retry/collision, request đồng thời cùng key, rollback receipt/audit và cạnh tranh sửa biên bản. Hai nhóm đủ điều kiện cùng được công bố; retry đảo thứ tự vẫn nhận receipt gốc. Khi một nhóm sai version hoặc không tồn tại, toàn bộ selection bị từ chối.
- InitialFinal và lưu chiểu được gọi qua API thật với PostgreSQL/MinIO. Điểm đầu vào và biên bản Locked vẫn được dựng trong test do API khóa biên bản là phụ thuộc task khác; vì vậy chưa chứng minh E2E qua producer đó.
- Không áp migration lên DB ứng dụng, không công bố dữ liệu thật. Excel/PDF, email và điều chỉnh/thu hồi sau công bố ngoài phạm vi.
