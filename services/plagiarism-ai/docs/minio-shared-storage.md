# Lưu file UGTM và AI trên MinIO

UGTM và AI dùng hai bucket private, ví dụ `ugtm-thesis-submissions` và `ugtm-ai`, cùng một MinIO. AI chỉ cần quyền đọc/ghi bucket AI; backend UGTM chỉ cần quyền đọc/ghi bucket UGTM. Không đưa tài khoản quản trị MinIO vào frontend hoặc Git.

Backend UGTM lấy file từ bucket của mình và gửi cho AI; AI lưu bản sao vào bucket AI. Báo cáo quét nằm trong database AI. Việc dùng cùng MinIO không tự loại bỏ bản sao file.

Trong môi trường Docker local đã cấu hình, AI dùng bucket MinIO riêng qua `compose.minio.yml`; backend UGTM dùng bucket riêng của backend. Các file cũ đã được sao chép mà không xóa nguồn. Một số job local cũ lưu đường dẫn tuyệt đối: database chưa bị sửa, file gốc vẫn được giữ và đã có bản sao MinIO theo object key là đường dẫn bỏ dấu `/` đầu tiên. Worker vẫn ưu tiên file tại đường dẫn cũ nếu file đó còn tồn tại.

## Môi trường local

MinIO của UGTM trong `docker-compose.yml` có container `ugtm-minio` trên network `ugtm_default`. AI có thể kết nối network này bằng `compose.minio.yml` sau khi chuẩn bị xong:

1. Tạo bucket AI private riêng và user MinIO có quyền phù hợp chỉ với bucket đó; giữ bucket UGTM hiện có. Lưu access key và secret key tại nơi an toàn.
2. Trong `.env` riêng của AI, đặt `AI_MINIO_BUCKET`, `AI_MINIO_ACCESS_KEY`, `AI_MINIO_SECRET_KEY`. Không gửi các giá trị khóa vào chat. Nếu tên network khác `ugtm_default`, đặt thêm `UGTM_MINIO_NETWORK`.
3. **Trước khi chuyển storage của database đang dùng**, xác định tất cả file được tham chiếu bởi job và dữ liệu nguồn, sao chép từ local storage hoặc R2 tương ứng sang bucket AI, giữ nguyên object key và xác minh checksum. Kiểm tra các object key trùng nhau nhưng nội dung khác nhau và dừng chuyển đổi để xử lý; không ghi đè âm thầm. Cần ngừng tạo job/file mới trong thời gian sao chép và đối soát, và sao lưu dữ liệu trước khi chuyển. Cấu hình storage mới không tự di chuyển file.
4. Xác nhận database và Celery queue là môi trường muốn dùng. Chuyển sang cấu hình này có thể ảnh hưởng khả năng đọc file của các job cũ nếu bước 3 chưa hoàn tất. Chỉ chạy lệnh dưới đây khi đã xác minh, hoặc dùng database và hàng đợi thử nghiệm riêng:

   ```powershell
   docker compose -f compose.yaml -f compose.minio.yml config --quiet
   docker compose -f compose.yaml -f compose.minio.yml up -d api worker
   ```

Các lệnh này dành cho local, không phải cấu hình production. Để thử mà không thay đổi các job cũ, cần database và queue riêng, bucket riêng và file mẫu riêng. Không dùng `docker compose down -v` vì lệnh đó xóa volume.

### Sao chép từ storage S3/R2 đang cấu hình

`scripts/copy_storage_to_minio.py` sao chép object theo nguyên key từ storage S3/R2 được cấu hình của AI sang bucket MinIO đã tạo. Script không xóa nguồn, không tự tạo bucket và không ghi đè object đã tồn tại; nó kiểm tra nội dung bằng SHA-256 và dừng nếu có xung đột. Trong `.env` riêng, đặt `AI_MINIO_ENDPOINT` (địa chỉ truy cập được từ máy chạy script), `AI_MINIO_BUCKET`, `AI_MINIO_ACCESS_KEY`, `AI_MINIO_SECRET_KEY`. Giữ các biến cấu hình nguồn R2 hiện tại cho tới khi sao chép xong.

```powershell
.\.venv\Scripts\python.exe scripts\copy_storage_to_minio.py
.\.venv\Scripts\python.exe scripts\copy_storage_to_minio.py --execute
```

Lệnh đầu chỉ kiểm kê; lệnh sau mới sao chép. Chạy lại sau khi đã dừng nguồn ghi để đối soát và sao chép file mới phát sinh. Script sao chép các object đang có, không sửa database và không tự tìm lại các file đã mất hoặc file tại đường dẫn máy cũ. Cần đối chiếu riêng các tham chiếu trong database trước khi đổi cấu hình hay xử lý job.

## Triển khai VPS

Workflow deploy UGTM hiện tạo lại Compose riêng và chưa deploy MinIO hay AI service. Cần bổ sung cấu hình production phù hợp với VPS, kết nối mạng nội bộ, tài khoản/bucket, lưu dữ liệu bền vững, backup và khôi phục; không dùng trực tiếp override local ở trên. Backend còn cần endpoint MinIO public HTTPS phù hợp cho URL tải file có thời hạn, đồng thời vẫn giữ bucket private.

Kiểm tra ARM64, RAM/dung lượng và migration của hai database trước khi deploy. Sau đó thử nộp, quét, tải lại file và kiểm tra phân quyền với dữ liệu giả lập trước khi chuyển dữ liệu thật.
