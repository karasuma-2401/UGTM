# Lưu ý triển khai và migration

Các lưu ý kỹ thuật được chuyển từ README để giữ hướng dẫn bắt đầu ngắn gọn. Đây là cấu hình và điều kiện cần kiểm tra, không phải xác nhận hạ tầng production đã được nghiệm thu.

## CI/CD và môi trường

- Publish yêu cầu Backend CI và Frontend CI thành công trên cùng SHA. Trivy chặn lỗi HIGH/CRITICAL có bản vá; image có tag `sha-<full SHA>`. Khi CI lỗi hoặc chờ quá 75 phút, chạy lại CI và publish cho cùng commit.
- Tạo GitHub Environment `dev` và `production`; mỗi environment có `DEPLOYMENT_SCOPE` đúng tên, `API_HOST`, `ACME_EMAIL`, cùng secrets VPS, `APP_URL`, database, JWT, SMTP, S3 và AI riêng. Deploy thủ công yêu cầu SHA đầy đủ của image đã publish.
- S3 cần `S3_ENDPOINT`, `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `S3_BUCKET_NAME`. `S3_PUBLIC_ENDPOINT`, `S3_REGION`, `PLAGIARISM_AI_API_KEY` là tùy chọn; `PLAGIARISM_AI_BASE_URL` bắt buộc. `IDENTITY_IP_HASH_KEY` tối thiểu 32 ký tự.
- Không dùng chung database, bucket, Data Protection keys hoặc đích triển khai giữa dev và production. Thư mục, Compose project, container và mạng tách theo environment; gateway cùng dùng cổng 80/443 nên cần VPS hoặc cấu hình gateway riêng.
- Production kiểm tra cấu hình S3 khi khởi động, yêu cầu URL web trong email dùng HTTPS, URL AI không trỏ loopback và partition outbox ThesisManagement được đặt rõ ràng, khác `default`.
- Caddy cấp TLS; DNS `API_HOST` cần trỏ tới VPS và mở TCP 80/443. VPS cần `setfacl` và filesystem hỗ trợ ACL để workflow kiểm tra UID container đọc được PEM. Chuyển từ stack cũ cần kế hoạch cutover vì tên container/project mới không tự thay stack cũ.
- Trước vận hành cần kiểm chứng migration, smoke test, rollback và backup/restore database, object storage, Data Protection keys. Xem [Compose production](../../../docker/docker-compose.prod.yml), [deploy workflow](../../../.github/workflows/deploy.yml) và [checklist Identity](IdentityVerificationChecklist.md).

## Email, storage và cấu hình nghiệp vụ

- `ConfigureDevelopment.ps1` sinh `appsettings.Development.local.json` từ cấu hình phát triển; API chỉ nạp file này trong Development. Environment variables và tham số dòng lệnh có ưu tiên cao hơn; biến terminal không tự truyền vào IDE đang mở.
- Mỗi runtime cần partition outbox phù hợp với Data Protection key ring. Thư mời ghép nhóm được ghi outbox cùng transaction đăng ký; thời hạn xác nhận 24 giờ bắt đầu sau khi worker gửi thư thành công. Giữ, sao lưu key ring và theo dõi `thesis.topic_registration_email`. Lời mời cũ trước migration không được tự tạo lại bản tin outbox.
- Upload đề cương, giữa kỳ và tiến độ có bản ghi `thesis.submission_upload_attempt`. Theo dõi trạng thái `CleanupPending`/`Cleaning` và log worker. Worker quét lại object đã xóa trong 24 giờ; vẫn cần đối soát storage khi upload hoàn tất muộn hơn hoặc worker ngừng dài ngày.
- Barem theo đợt được quản lý qua `GET`/`PUT /api/thesis/batches/{batchId}/grading-rubric`, tổng điểm tối đa bằng 10. Barem khóa sau khi có chi tiết đánh giá; không tự suy diễn tiêu chí cho đợt cũ chưa cấu hình.
- Preview và transaction biên bản: xem [phạm vi nghiệm thu](../../../docs/ApiContract/CouncilMinuteVerification.md). Khi thay đổi loại điểm cần `reason` riêng với góp ý sinh viên.

## Kiểm tra trước migration trên dữ liệu có sẵn

API không tự migrate. Dùng [Database.ps1](../../../scripts/Database.ps1) để List, Check, Script và Update trên database được chọn. Giữ migration đã áp dụng; database dùng history cũ cần kiểm tra bước chuyển history sang `public` theo [hướng dẫn kiến trúc](../Architecture/Architecture.md#6-migrations-và-neon).

Trước migration `CouncilSchedulingUniqueness`, xử lý các hàng trùng; migration thất bại nếu còn trùng:

```sql
SELECT council_id, role_in_council, count(*)
FROM councils.council_member
WHERE role_in_council IN ('Chairman', 'Secretary')
GROUP BY council_id, role_in_council HAVING count(*) > 1;

SELECT council_id, order_number, count(*)
FROM councils.council_assignment
GROUP BY council_id, order_number HAVING count(*) > 1;
```

Trước migration `RenameRequestConcurrency`, xử lý đơn đang chờ bị trùng:

```sql
SELECT group_id, count(*)
FROM thesis.rename_request
WHERE status IN ('Created', 'AdvisorConfirmed', 'FacultyApproved')
GROUP BY group_id HAVING count(*) > 1;
```
