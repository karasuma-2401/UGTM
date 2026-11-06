# Kiểm thử Modal với Docker local

Môi trường thử nghiệm tách riêng database UGTM, corpus mẫu và Celery queue khỏi dữ liệu/quy trình cũ. AI API và worker dùng Docker Compose của `UGTM-Plagiarism-AI`; GPU worker chạy trên Modal.

Trong `.env` riêng tư của UGTM cần cấu hình `PLAGIARISM_AI_API_KEY` khớp `AI_API_KEY` của Docker Compose AI. File `docker-compose.modal-test.yml` yêu cầu thêm `UGTM_MODAL_TEST_DB_NAME`, `UGTM_MODAL_TEST_DB_PASSWORD`, `UGTM_MODAL_TEST_CORPUS`; các giá trị thử nghiệm phải trỏ tới database và corpus riêng đã được chuẩn bị. Không commit `.env`.

Trong `.env` riêng tư của AI, `REMOTE_GPU_URL` và `REMOTE_GPU_API_KEY` trỏ tới Modal, còn `CELERY_QUEUE` được đặt thành một tên hàng đợi thử nghiệm riêng. Khi tạo lại container, Compose lấy queue từ `.env`. Việc xóa hoặc đổi `CELERY_QUEUE` về `celery` có thể khiến worker nhận các job cũ; chỉ thực hiện khi chủ sở hữu đồng ý xử lý chúng.

AI local hiện dùng MinIO qua override `compose.minio.yml` và các biến `AI_MINIO_*` trong `.env` riêng. Khi khởi động lại, luôn dùng cả hai file Compose; chỉ chạy `docker compose up -d` ở dự án AI có thể đưa container về local storage và khiến các file mới trong MinIO không đọc được.

Sau khi schema và dữ liệu mẫu của môi trường thử nghiệm đã được chuẩn bị, có thể chạy:

```powershell
cd C:\Projects\UGTM
docker compose -f docker-compose.yml -f docker-compose.modal-test.yml up -d minio

cd C:\Projects\UGTM-Plagiarism-AI
docker compose -f compose.yaml -f compose.minio.yml up -d

cd C:\Projects\UGTM
docker compose -f docker-compose.yml -f docker-compose.modal-test.yml up -d backend
```

Backend chạy với override sẽ dùng database thử nghiệm, không hiển thị dữ liệu của database UGTM thông thường. Khi quay lại môi trường thông thường, bỏ override một cách chủ động và kiểm tra migration/cấu hình của database đó trước; không chạy `docker compose down -v` vì sẽ xóa volume.

Lần gọi GPU và thời gian worker còn hoạt động có thể tiêu thụ credit Modal. Kiểm tra Usage & Billing; việc dừng container GPU không dừng các dịch vụ Docker hoặc phí hạ tầng khác.
