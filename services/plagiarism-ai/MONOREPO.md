# AI trong monorepo UGTM

Source nhập từ repository `UGTM-Plagiarism-AI`, commit `fe21d8d605eecbcc675707dc29836a7b70d62070` (2026-11-06). Chỉ sao chép file được Git theo dõi; không nhập `.git`, `.env`, virtualenv, corpus, model weights, classifier `.joblib`, runtime hoặc `frontend/dist`. Repo gốc được giữ nguyên. Lịch sử Git cũ không được gộp vào lịch sử UGTM; commit nguồn ở trên dùng để đối chiếu.

Từ đây phát triển và phát hành AI tại thư mục này, cùng PR/nhánh `production` của UGTM; repo cũ giữ để tham khảo, tránh tiếp tục sửa hai bản source song song.

Thư mục này là project root Python: chạy lệnh tại đây, không chạy từ root UGTM. Các import `app.*`, `scripts.*`, đường dẫn SQL và cấu hình tương đối vẫn giữ nguyên. Backend .NET tiếp tục gọi AI bằng HTTP; không chia sẻ ORM hoặc database nghiệp vụ. Modal deploy vẫn là một thao tác riêng, không tự kích hoạt khi deploy CPU services.

## Phát triển và kiểm thử

```powershell
cd services/plagiarism-ai
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements/dev.txt
# Chỉ copy khi chưa có .env; điền kết nối cho môi trường phát triển.
Copy-Item .env.example .env
.\.venv\Scripts\python.exe -m pytest -c pytest.unit.ini
```

`pytest.unit.ini` chọn tests tự chứa dữ liệu, không cần Neon, Redis, GPU hoặc private benchmark artifacts. Các test parser dùng tài liệu tổng hợp được tạo trong thư mục tạm. Một số tests lịch sử như `test_documents_block_parser.py`, `test_preprocessing_and_chunking.py`, `test_evaluator.py` và các tests nghiên cứu cần fixture/dataset không được version trong repo cũ; chúng được giữ nguyên, chạy riêng khi đã chuẩn bị dữ liệu. Không xem unit suite là nghiệm thu lại benchmark nghiên cứu.

AI CI chạy thêm `test_queue_lifecycle.py`, `test_job_lifecycle_resilience.py` và `test_commit_enqueue_reconciler.py` trên PostgreSQL pgvector/Redis tạm của runner, không dùng secrets production. Toàn bộ test suite cũ vẫn có thể gọi trực tiếp bằng `python -m pytest tests` khi đã chuẩn bị đúng dữ liệu và hạ tầng **thử nghiệm riêng**. `tests/conftest.py` cũ đọc `.env` tại project root: không đặt credentials production trong `.env` phục vụ tests.

Compose `compose.yaml` và `compose.minio.yml` ở đây được giữ cho môi trường phát triển cũ. Khi đổi project directory, Compose có thể đổi tên project, network và volume. Không chạy chúng trên dữ liệu cũ trước khi kiểm tra `docker compose ls`, `docker volume ls`, các biến `UGTM_MINIO_NETWORK`, queue và storage. Không dùng `down -v`. Hướng dẫn `docs/modal-docker-local-test.md` ở root UGTM vẫn mô tả topology hai repo cũ; không dùng nguyên các tên container/network đó cho production mới.

Dashboard React trong `frontend/` là công cụ AI độc lập được giữ để tham khảo/phát triển, không phải frontend người dùng UGTM, không tự build hay public trong image production. Không đưa service key AI vào frontend Vercel.

## Production

Dùng [runbook chung](../../docs/production-deployment.md) và [Compose production](../../docker/docker-compose.prod.yml). Image AI được build với context chính là thư mục này, chạy non-root UID 10001, không cài torch/CUDA. API, worker và reconciler dùng cùng image nhưng tiến trình riêng. Image đóng gói `scripts/` và `sql/` để kiểm tra schema và chạy migration thủ công.

Classifier được cấp riêng tại `/artifacts`; không tự copy file pickle/joblib từ máy phát triển hoặc tự tải nguồn không tin cậy. Nếu dùng model hiện có, chuyển cả `.joblib` và manifest, kiểm tra checksum/phiên bản scikit-learn và đặt quyền đọc cho UID 10001. Các release manifests trong `artifacts/` là bằng chứng nghiên cứu lịch sử; không sửa hash để giả lập một lần kiểm định mới.

Các tài liệu cũ trong `docs/` có thể chứa liên kết `file:///c:/Projects/UGTM-Plagiarism-AI/...` và hướng dẫn Render/Colab. Đó là ngữ cảnh lịch sử; dùng đường dẫn tương đối trong thư mục hiện tại và runbook chung cho triển khai Azure/Modal.
