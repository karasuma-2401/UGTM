# Deploy UGTM monorepo: Azure, Vercel, Neon, MinIO và Modal

## Kiến trúc và phạm vi

Nhánh phát hành: `production`. Azure VM hiện tại chạy .NET API, FastAPI, Celery worker (concurrency 1), reconciler, Redis AOF, MinIO và Caddy. Frontend React chính chạy Vercel; frontend container cũ được giữ làm fallback. Modal hiện có tiếp tục chạy GPU, không redeploy tự động. Hai database Neon hiện có được giữ riêng. Không tạo PostgreSQL trong Compose production.

API: `https://ugtm.eastasia.cloudapp.azure.com`; MinIO S3 API: cùng hostname qua HTTPS `:8443`. Bucket private, tài khoản UGTM/AI riêng. Caddy giữ Host/path/query để chữ ký presigned URL hợp lệ. AI/Redis không có port public; MinIO console chỉ bind `127.0.0.1:9001`. Mở Azure NSG/host firewall cho TCP 80, 443, 8443 và SSH theo chính sách hiện có. Không đưa service key AI/GPU, DB hoặc MinIO credentials vào Vercel.

Compose chính là `docker/docker-compose.prod.yml`; workflow chuyển đúng file và script từ commit phát hành lên VM, không sinh một bản Compose khác bằng heredoc. Image `ugtm-api`, `ugtm-frontend`, `ugtm-ai` dùng cùng tag `sha-<full-commit>`. Hiện mỗi release build cả ba image để có một bộ phiên bản nhất quán; chưa tối ưu bỏ build image không đổi. CI cho pull request có lọc đường dẫn; trên push `dev`/`production`, cả ba CI luôn tạo kết quả để release gate không chờ vô hạn một workflow bị skip.

## Secrets và variables trên GitHub

Environment `production` hoặc repository secrets, theo tên workflow `.github/workflows/deploy.yml`:

| Nhóm | Secrets |
| --- | --- |
| SSH | `VPS_HOST`, `VPS_USERNAME`, `VPS_SSH_KEY`, tùy chọn `VPS_PORT` |
| UGTM | `DATABASE_CONNECTION_STRING` (Npgsql), `APP_URL`, `JWT_PRIVATE_KEY_PEM_BASE64`, `IDENTITY_IP_HASH_KEY` |
| AI | `AI_POSTGRES_DSN` (pooled URI), `AI_POSTGRES_DIRECT_DSN` (direct URI), `PLAGIARISM_AI_API_KEY` |
| Modal | `REMOTE_GPU_URL` (URL gốc), `REMOTE_GPU_API_KEY` |
| MinIO quản trị | `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD` |
| Bucket UGTM | `S3_ACCESS_KEY`, `S3_SECRET_KEY` |
| Bucket AI | `AI_S3_ACCESS_KEY`, `AI_S3_SECRET_KEY` |
| SMTP | `SMTP_HOST`, `SMTP_FROM_ADDRESS`, `SMTP_USERNAME`, `SMTP_PASSWORD`; tùy chọn `SMTP_PORT`, `SMTP_FROM_NAME`, `SMTP_SECURITY`, `SMTP_TIMEOUT_SECONDS` |

Giữ nguyên khóa JWT/HMAC cũ nếu tiếp tục dùng dữ liệu và phiên đăng nhập hiện tại. `PLAGIARISM_AI_API_KEY` phải ít nhất 32 ký tự; Compose tự truyền cùng giá trị vào backend và AI. Khóa Modal là khóa riêng. Mật khẩu trong URI PostgreSQL phải percent-encode đúng; giá trị Npgsql dùng định dạng key/value riêng của Npgsql. Không gửi giá trị thật vào chat hoặc log.

Variables: `DEPLOYMENT_SCOPE=production`, `API_HOST`, `ACME_EMAIL`, `PLAGIARISM_AI_DEFAULT_CORPUS` (khớp corpus đã ingest). Tùy chọn `S3_BUCKET_NAME`, `AI_S3_BUCKET_NAME`, `AI_CLASSIFIER_ENABLED`, `MINIO_IMAGE`, `MINIO_MC_IMAGE`, `DATA_PROTECTION_VOLUME`, `CADDY_DATA_VOLUME`, `CADDY_CONFIG_VOLUME`, `MINIO_DATA_VOLUME`. Mặc định MinIO và MC được cố định theo digest đã kiểm tra, manifest có AMD64/ARM64; MC dùng variant có shell cho thao tác provisioning. Không dùng `minio/mc` trên Docker Hub vì image này không pull được khi kiểm tra. `S3_ENDPOINT`, `S3_PUBLIC_ENDPOINT`, `PLAGIARISM_AI_BASE_URL` cũ không còn được đọc trong deployment monorepo: endpoint nội bộ và HTTPS 8443 được cấu hình tập trung trong Compose.

Mẫu đầy đủ cho thao tác thủ công: `docker/.env.production.example`. Mỗi release có `runtime.env` mode 0600 và khóa JWT riêng; không chạy `docker compose config` không có `--quiet` vì có thể in credentials. Không `source` hoặc `eval` file env.

## Lần chuyển đổi đầu tiên

1. Bật VM Azure, kiểm tra IP/DNS/SSH, disk, RAM, Docker Compose và `setfacl` (`acl` trên Ubuntu). VM là ARM64; pipeline build cả ARM64 và AMD64. Không build tại VM 4 GB RAM.
2. Backup cả hai Neon DB bằng direct connection; backup MinIO và key ring Data Protection. Tạm dừng producer/worker phát triển cùng dùng hai DB trước cutover. AI reconciler đọc job trong toàn DB, nên queue production riêng không tự cô lập dữ liệu/job phát triển.
3. Kiểm tra `docker compose ls`, `docker ps -a`, `docker volume ls`, `docker network ls`. Bản cũ có thể ở `~/ugtm`, còn bản mới ở `~/ugtm/production`. Dùng `docker inspect <container>` để xác định volume thực tế. Đặt variables `DATA_PROTECTION_VOLUME`, `CADDY_DATA_VOLUME`, `CADDY_CONFIG_VOLUME` theo tên volume cũ muốn dùng lại. Không đoán tên; không `down -v`. Volume MinIO trên máy local không tự xuất hiện trên Azure.
4. Đưa thay đổi đã review vào `production` khi được phép. `Backend CI`, `Frontend CI`, `AI CI` phải thành công; `Publish Images` scan rồi publish cả ba image. Auto deploy có thể dừng ở kiểm tra schema/storage nếu chưa chuẩn bị dữ liệu; nó không tự apply migration hoặc tạo bucket. Không bỏ qua gate này.
5. Chạy workflow **Deploy to VPS** thủ công với full commit SHA đã publish và `deploy_mode=prepare`. Thao tác này chép file, pull image, chuẩn bị env/JWT; không start/stop container, không truy cập DB, không đổi symlink `current`. Nếu phát hiện bản cũ mà chưa chỉ rõ volume key ring thì chuẩn bị volume mapping trước.

Trên VM, dùng đúng SHA đã prepare:

```bash
RELEASE_SHA=<FULL_COMMIT_SHA_DA_PUBLISH>
cd "$HOME/ugtm/production/releases/$RELEASE_SHA"
dc() { docker compose -p ugtm-production --env-file "$PWD/runtime.env" -f "$PWD/docker/docker-compose.prod.yml" "$@"; }
dc config --quiet
```

Chỉ sau backup, khởi động MinIO và Redis mới, provision tài khoản/bucket bằng thao tác có chủ đích:

```bash
dc up -d minio ai-redis
# Đợi MinIO sẵn sàng; lệnh này tạo/cập nhật hai service users và bucket policies.
dc --profile tools run --rm minio-init
```

`minio-init` không xóa object, đặt bucket private, tạo hai tài khoản và policy giới hạn theo bucket. Dùng riêng tên user cho UGTM/AI; không dùng tài khoản root cho ứng dụng. Nếu volume đã có user/policy khác, kiểm tra trước vì đây là lệnh provisioning có thể cập nhật credentials/policy. Nếu VM đang có MinIO cũ, xác định đúng volume và quyền sở hữu trước khi chạy thêm container, tránh hai instance ghi chung một volume.

Copy object từ MinIO đang dùng sang Azure, giữ object key/bucket names khớp metadata DB, đối chiếu số lượng và checksum. Không xóa nguồn sau copy cho tới khi nghiệm thu. Với AI có thể dùng `services/plagiarism-ai/scripts/copy_storage_to_minio.py` từ máy nguồn: chạy dry run trước, chỉ thêm `--execute` sau backup, cấu hình endpoint đích qua SSH tunnel tới S3 API nếu chưa có gateway. Script chỉ hỗ trợ nguồn S3/MinIO, không chuyển local filesystem backend. Copy bucket UGTM bằng công cụ S3 phù hợp, cũng giữ nguyên key. Cần đưa lên đủ report/object cũ mà DB còn tham chiếu.

MinIO S3 API không bind public 9000. Để copy trước khi gateway chuyển đổi, có thể tạo SSH tunnel tới IP nội bộ container MinIO được kiểm tra trên VM, hoặc dùng một công cụ S3 trong cùng Docker network. Console có thể truy cập qua `ssh -L 9001:127.0.0.1:9001 ...`; console không thay thế endpoint S3 API trong script copy.

Kiểm tra/apply schema AI sau backup bằng direct connection:

```bash
dc run --rm --no-deps ai-api python -m scripts.migrate status
# Đọc SQL, điều tra checksum mismatch; không dùng apply để bỏ qua mismatch.
dc run --rm --no-deps ai-api python -m scripts.migrate apply
dc run --rm --no-deps ai-api python -m scripts.check_schema
```

Image chứa nguyên các SQL migration; check_schema chỉ SELECT, chặn pending/mismatch và database có migration ngoài release. `migrate apply` là công cụ cũ, không tự chặn checksum mismatch; cần xem status và điều tra trước. Đối chiếu UGTM DB bằng `scripts/Database.ps1 -Action List/Script`, áp migration đã review bằng `-Action Update` với `MIGRATION_CONNECTION_STRING` direct đúng DB. Backend không tự migrate; workflow cũng không tự apply schema UGTM. Các backfill có thể tạo scan cho báo cáo cũ, cần dự tính credit Modal và ngừng worker dev.

Nếu dùng classifier hiện có, cấp `.joblib` cùng manifest vào `~/ugtm/production/ai-artifacts`, chỉ đọc được từ UID 10001; bật `AI_CLASSIFIER_ENABLED=true` sau khi kiểm tra checksum và compatibility. Mặc định false, không tự import model riêng tư của máy phát triển. Deploy preflight kiểm tra load model nếu bật, không gọi GPU.

Cuối cùng dừng **đúng stack/gateway cũ đã xác định**, giữ lại volume; xử lý network cũ nếu trùng subnet `172.29.0.0/24`, không xóa network đang có container. Chạy lại workflow với `deploy_mode=deploy`. Script từ chối nếu cổng 80/443/8443 đang thuộc container khác, kiểm tra schema và AI bucket/broker trước khi thay app, kiểm tra worker ping/API/TLS/MinIO sau khi start. Không tự dừng stack cũ hoặc tự chuyển dữ liệu.

## Vercel và kiểm tra sau deploy

Vercel project: Root Directory `frontend`, framework Vite, Node 24.x, install `npm ci`, build `npm run build`, output `dist`, production branch `production`. Set Production env `API_ORIGIN=https://ugtm.eastasia.cloudapp.azure.com`; bỏ `VITE_API_BASE_URL` cũ để dùng rewrite `/api/*` cùng origin. Không thêm `/api` vào API_ORIGIN. `frontend/vercel.ts` chỉ bật auto Git deploy cho `production`. Nếu Vercel build trước khi backend mới sẵn sàng, chỉ chuyển người dùng sang bản mới sau khi kiểm tra backend; có thể cần rollback/promote deployment Vercel riêng.

Nghiệm thu: đăng nhập/refresh session, tải file cũ qua URL ký HTTPS 8443, upload PDF/DOCX mới, scan đi qua queue/Modal tới report hoàn tất, quyền sinh viên/giảng viên, restart giữ file và kết quả. `/healthz` AI chỉ là liveness, không xác nhận GPU/DB/bucket. Deployment không tự gọi Modal; chạy smoke test GPU có chủ đích bằng `python scripts/smoke_test_colab_client.py --health-timeout 120` trong môi trường đúng cấu hình, sau đó thử một tài liệu qua UGTM. Theo dõi RAM/disk/CPU, queue depth và credit Modal; 4 GB RAM chưa có bằng chứng kiểm thử tải production.

## Deploy tiếp theo và rollback

Push/merge `production` chỉ thực hiện khi được phép. CI thành công → publish ba image cùng SHA → deploy. Mỗi release lưu file/env dưới `~/ugtm/production/releases/<sha>`. Symlink `current` chỉ được cập nhật sau các health checks; `previous` giữ release thành công trước. Lỗi sau `up` không tự rollback: container có thể đã đổi dù `current` chưa đổi, cần kiểm tra `docker compose ps` và log ngay.

Rollback chỉ sau khi xác minh schema/object format còn tương thích với release trước. Dùng workflow thủ công với SHA của release monorepo trước đã publish và cấu hình phù hợp; hoặc trên VM chạy Compose của thư mục `previous` cùng `runtime.env` của chính release đó. Workflow dùng secrets hiện tại, còn rollback Compose thủ công dùng env đã lưu. Không dùng SHA trước monorepo vì chưa có đủ image/file mới. Không tự rollback DB, không xóa volume/object để chữa lỗi và không phục hồi DB cũ đè lên dữ liệu mới nếu chưa có kế hoạch bảo toàn dữ liệu.

## Tài liệu nền

Kiểm tra local khi tích hợp (2026-11-17): 93 unit tests AI và 24 integration tests pass trên DB/Redis Docker tạm; build image AMD64 và khởi động API/worker/reconciler, kiểm tra schema/bucket/broker và worker ping thành công. Đã kiểm tra provisioning hai tài khoản MinIO, từ chối cross-bucket và tải URL ký qua proxy HTTPS tạm; actionlint, shellcheck, Compose/Caddy validation và test chuẩn bị deploy/literal dotenv/schema failure đều pass. Frontend lint/build pass, còn warnings hiện có. 36 file `app/` và `sql/` được đối chiếu byte với repo AI gốc, không đổi. Chưa build/chạy AI trên ARM64 local, gọi GPU Modal thật, kiểm thử tải hoặc deploy Azure/Vercel; các bước này vẫn phải nghiệm thu khi phát hành. Container/volume kiểm thử tạm đã được dọn, không dùng dữ liệu Neon/MinIO hiện có.

- [Azure public IP](https://learn.microsoft.com/en-us/azure/virtual-network/ip-services/public-ip-addresses).
- [Neon pooling và direct connection](https://neon.com/docs/connect/connection-pooling).
- [Vercel Git deployment configuration](https://vercel.com/docs/project-configuration/git-configuration).
- [Caddy reverse proxy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy).
- [Modal cold start](https://modal.com/docs/guide/cold-start).
