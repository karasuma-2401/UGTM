# Hướng dẫn chạy GPU worker trên Modal

Tài liệu này dành cho môi trường thử nghiệm UGTM. Modal sẽ tự cấp GPU T4 khi nhận request và thu hồi worker sau khi không còn request; việc deploy và gọi thử có thể sử dụng credit hoặc phát sinh phí. Worker ở đây mới được kiểm thử hợp đồng bằng mô hình giả, chưa được xác nhận trên GPU Modal thật.

## 1. Tạo tài khoản và giới hạn chi tiêu

1. Đăng ký tại <https://modal.com/> và chọn gói Starter. Theo tài liệu Modal, GPU yêu cầu phương thức thanh toán hợp lệ ngay cả khi có credit; kiểm tra giá, credit và điều kiện hiện tại trước khi dùng.
2. Mở **Settings → Usage & Billing**. Kiểm tra credit còn lại. Đặt **Workspace budget / usage limit** theo mức tổng sử dụng cho phép *trước khi trừ credit*, và **Spend limit** theo số tiền tối đa bạn chấp nhận trả thêm *sau khi trừ credit*. Xác nhận các giá trị và cách áp dụng trên dashboard; nếu không muốn trả thêm, kiểm tra xem giao diện có cho đặt mức 0 và có áp dụng như mong muốn không.
3. Khi chạm giới hạn, các công việc cần thêm tài nguyên có thể bị dừng. Cấu hình này chỉ giới hạn Workspace Modal, không giới hạn hóa đơn Render, database hay các dịch vụ khác. Xem <https://modal.com/docs/guide/budgets>.

## 2. Cài CLI và đăng nhập

Mở PowerShell tại `C:\Projects\UGTM-Plagiarism-AI`:

```powershell
.\.venv\Scripts\python.exe -m pip install modal
.\.venv\Scripts\python.exe -m modal setup
.\.venv\Scripts\python.exe -m modal token info
```

Lệnh setup mở trình duyệt để xác nhận tài khoản. Nếu `.venv` không chạy, cần sửa môi trường Python của máy trước; không tạo lại hoặc xóa `.venv` nếu chưa lưu ý các phụ thuộc hiện có. Token Modal là bí mật, không gửi vào chat hay đưa lên Git.

## 3. Tạo khóa cho GPU worker

Tạo một chuỗi ngẫu nhiên bằng PowerShell:

```powershell
[Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Trên dashboard Modal, mở **Secrets**, tạo Custom Secret có tên chính xác `ugtm-gpu-worker`, thêm key `GPU_WORKER_API_KEY` với giá trị là chuỗi vừa tạo. Giữ chuỗi trong trình quản lý mật khẩu hoặc nơi riêng tư để cấu hình AI service ở bước 5. Không dùng API key của UGTM hay token Modal làm khóa này. Xem <https://modal.com/docs/guide/secrets>.

## 4. Deploy worker

```powershell
.\.venv\Scripts\python.exe -m modal deploy scripts/modal_gpu_worker.py
```

Lần đầu Modal cần build image và tải hai model, nên có thể mất thời gian và dùng một phần credit CPU/mạng. Sau khi hoàn tất, CLI in URL dạng `https://...modal.run`: sao chép URL gốc, không thêm `/health`. Worker dùng T4, `min_containers=0`, tối đa một container và chờ tối đa 60 giây sau khi rảnh trước khi thu hồi. GPU có thể được tính phí cả lúc khởi động, tải model và trong thời gian chờ.

Không cần Colab hoặc Kaggle chạy trong quá trình này. Endpoint được bảo vệ bằng khóa, nhưng request đến URL công khai vẫn có thể đánh thức container trước khi phần xác thực trong ứng dụng xử lý request: giữ URL và khóa riêng tư, đặt budget và theo dõi Usage. Đừng cấu hình hệ thống ngoài gọi `/health` liên tục.

## 5. Kết nối AI service

Trong file `.env` riêng tư của `UGTM-Plagiarism-AI`, hoặc phần Environment của Render nếu CPU worker chạy trên Render, đặt:

```dotenv
INFERENCE_MODE=remote
REMOTE_GPU_URL=https://YOUR-WORKER-URL.modal.run
REMOTE_GPU_API_KEY=YOUR-GPU-WORKER-SECRET
REMOTE_GPU_TIMEOUT_SECONDS=180
REMOTE_GPU_HEALTH_TIMEOUT_SECONDS=120
```

Thay các giá trị mẫu bằng URL và khóa thật; không commit file `.env`. Khởi động lại CPU worker để nhận cấu hình mới; khởi động lại API service nếu nó cũng dùng cấu hình này. Hai model trong worker đang sử dụng revision `main`; nếu AI service cấu hình revision cụ thể thì phải thống nhất revision thực tế trước khi dùng.

## 6. Kiểm tra

Trên máy có môi trường Python dự án và `.env` vừa cấu hình, chạy:

```powershell
.\.venv\Scripts\python.exe scripts/smoke_test_colab_client.py --health-timeout 120
```

Lệnh sẽ gọi `/health`, `/encode` và `/rerank` bằng câu mẫu. Lần đầu có thể chờ lâu để tải model và sẽ tiêu thụ tài nguyên; nếu đạt sẽ in `PASSED` và kích thước vector `[2, 1024]`. Sau đó quay lại dashboard Modal → Apps/Usage để xem worker được khởi tạo, rồi rảnh và thu hồi, cũng như mức tiêu thụ thực tế. Thử ngắt kết nối và một bài mẫu qua toàn bộ UGTM trước khi dùng dữ liệu thật.

Nếu timeout, xem log worker trong Modal: thời gian cấp GPU, tải model và khởi động có thể vượt timeout của client. Modal Web Functions có quy tắc timeout HTTP riêng; xem <https://modal.com/docs/guide/webhook-timeouts>. Nếu thiếu credit/budget hoặc không có GPU, job có thể chờ và cuối cùng cần retry theo chính sách của hệ thống. Nếu HTTP 401, kiểm tra Secret và cấu hình key có trùng nhau. Không gửi token hoặc file `.env` khi hỏi hỗ trợ.
