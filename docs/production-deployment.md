# Phát hành và vận hành UGTM

## Kiến trúc

Azure VM ARM64 chạy .NET API, AI API, một Celery worker, reconciler, Redis AOF, MinIO và Caddy. Vercel phục vụ frontend. Không còn frontend container trên Azure; truy cập trang web qua hostname Azure được chuyển sang Vercel, giữ path/query. Hai Neon DB hiện có và Modal GPU được giữ riêng. Không build ứng dụng trên VM.

API: `https://ugtm.eastasia.cloudapp.azure.com`. S3 API: cùng host qua HTTPS `:8443`. Azure NSG và host firewall cần cho phép 80/443/8443; SSH theo chính sách quản trị. MinIO console chỉ bind `127.0.0.1:9001`. Không public AI, Redis hoặc MinIO 9000.

Một VM vẫn là điểm lỗi đơn. Workflow giảm thao tác thủ công và hỗ trợ khôi phục, không cung cấp high availability hoặc bảo đảm zero downtime. Deploy chờ worker rảnh; khi solo worker bận và không trả lời control, deploy dừng an toàn để thử lại sau.

## Luồng release

1. PR vào `dev`/`production` chạy `CI`: reusable backend/frontend/AI jobs và release contracts. Không lọc đường dẫn ở cấp required workflow để tránh check bị treo vì skip.
2. Push `dev`/`production` chạy `Publish Images`, gọi lại cùng CI bằng `needs`; không còn job polling GitHub API. Các Docker build xác minh riêng không chạy lại trong release CI.
3. Mỗi component API/AI build trên runner native ARM64; AMD64 cũng build native khi repository variable `BUILD_AMD64=true` (mặc định). Repo public hiện dùng được standard ARM64 hosted runner. Không cài runner trên VM production.
4. Image mới được push bằng candidate tag duy nhất, kèm SBOM/provenance. Trivy scan đúng platform digest trên runner tương ứng; các digest Redis/Caddy/MinIO trong policy cũng phải pass scan ARM64. Candidate không được coi là release khi scan chưa pass. SARIF được giữ trong artifact và gửi lên Security tab khi GitHub hỗ trợ. Chính sách giữ HIGH/CRITICAL có bản vá; `ignore-unfixed=true` được giữ từ workflow cũ.
5. Image không thay đổi được tái sử dụng từ manifest thành công cùng branch trong tối đa 7 ngày tính từ lần build thực tế, và vẫn được scan lại. Không có manifest/artifact đã hết hạn thì build toàn bộ. `force_rebuild=true` luôn refresh base images. Lịch thứ Hai rebuild default branch; production không tự phát hành từ lịch này.
6. Ghép manifest từ các digest đã scan, không build lần hai. Bundle `release-bundle-<run-attempt>` lưu commit, run ID/attempt, image/platform digests, source fingerprints, schema fingerprints, rollback policy, deployment scripts và checksums. Artifact giữ 90 ngày; retry tạo artifact mới, không ghi đè bằng chứng của attempt trước.
7. Production publish từ push kích hoạt `Deploy to VPS`. Run thủ công nhận **Publish run ID**, không nhận SHA hoặc floating tag tùy ý. Driver xác minh run thành công, đúng repo/workflow/branch và commit thuộc lịch sử `production`. Auto deploy bỏ qua release đã bị commit mới thay thế.
8. Vercel tạo production deployment với `--skip-domain`. Azure chạy candidate, kiểm tra trong VM rồi kiểm tra từ runner bên ngoài. Sau đó promote frontend, chạy HTTP/auth smoke đã cấu hình, rồi mới cập nhật `current`.

Trình tự này không tạo giao dịch nguyên tử giữa Azure và Vercel. API phải tương thích với frontend trước và sau release trong khoảng chuyển đổi; breaking API changes cần kế hoạch riêng.

## Cấu hình trước lần chuyển sang workflow mới

Giữ các secrets production hiện có: SSH, UGTM/AI database, JWT/HMAC, MinIO, Modal và SMTP. Workflow vẫn dùng đúng tên trong `.github/workflows/deploy.yml`. Không đưa chúng vào Vercel hoặc log.

Thêm các cấu hình sau **trước khi merge vào production**, vì Vercel Git auto deployment được tắt trong source:

| Loại | Tên | Nội dung |
| --- | --- | --- |
| Secret production | `VPS_KNOWN_HOSTS` | Dòng OpenSSH known_hosts của VM đã xác minh qua kênh tin cậy; bao gồm host/IP và port tương ứng |
| Secret production | `VERCEL_TOKEN` | Token có quyền deploy/promote/rollback đúng project |
| Variable production | `VERCEL_ORG_ID`, `VERCEL_PROJECT_ID` | IDs từ Vercel project; không phải display name |
| Repository variable | `BUILD_AMD64` | `true` giữ cả hai kiến trúc; `false` chỉ build ARM64 cho Azure |
| Repository variable | `OPS_ENABLED` | Chỉ bật `true` sau một release mới deploy thành công và monitor thủ công pass |
| Variable production | `BACKUP_ENABLED` | Chỉ bật `true` sau cấu hình kho ngoài VM, backup và restore drill thủ công thành công |
| Variable production | `SMOKE_AUTH_ENABLED` | Bật `true` sau khi có tài khoản kiểm thử riêng và `smoke-auth` pass |

Lấy SSH host key trên **SSH/console VM đã tin cậy**:

```bash
cat /etc/ssh/ssh_host_ed25519_key.pub
```

Tạo dòng known_hosts với hostname/IP đúng `VPS_HOST`, ví dụ `104.208.100.45 ssh-ed25519 <public-key>`; port khác 22 dùng `[host]:port`. Đối chiếu fingerprint trước khi lưu secret. Không dùng `StrictHostKeyChecking=no` hoặc tự tin key thu được từ mạng chưa xác minh.

Vercel giữ Root Directory `frontend`, `API_ORIGIN=https://ugtm.eastasia.cloudapp.azure.com`, Node 24, build `npm run build`, output `dist`. Bỏ `VITE_API_BASE_URL` cũ để dùng rewrite `/api/*`. CLI được cố định ở `66.2.0`; cập nhật có review. Source được lấy đúng approved commit, không lấy checkout ngẫu nhiên của workflow điều phối.

Kiểm tra branch protection sau khi đổi sang reusable CI: required checks cần khớp tên check mới xuất hiện trong PR. Giữ review và kiểm tra bắt buộc; không bỏ branch protection để merge. Pin Actions bằng commit SHA; Dependabot gửi cập nhật Actions hằng tuần. Chỉ job build/publish có `packages: write`, deploy có quyền đọc.

## Deployment attempts và trạng thái

Mỗi lần chạy có thư mục:

```text
~/ugtm/production/releases/<sha>-<deploy-run-id>-<run-attempt>/
```

Trong đó có `release.json`, `checksums.json`, `runtime.env` và JWT mode 0600, `public.json`, `status.json`, `deployment.json` và cấu hình Compose. Cấu hình được tạo một lần; chạy lại cùng commit bằng workflow tạo attempt mới. Không `source`, `eval` hoặc in `runtime.env`.

`prepare` chỉ chuẩn bị files/images/key ACL; không truy cập DB hoặc start/stop app. Deploy thật cần một workflow run mới với `deploy_mode=deploy`. Không dùng attempt đã prepare để chạy lại script candidate tại chỗ.

`candidate` kiểm tra disk (tối thiểu 2 GiB), Compose, port, UGTM migration history, AI checksums, bucket/broker, drain worker và health/TLS. File `pending` chặn deploy khác trong giai đoạn chờ kiểm tra bên ngoài. `flock` trên VM bảo vệ cả thao tác SSH thủ công và backup/restore.

`current` chỉ chuyển sau promote và smoke tests; `previous` giữ release trước. Stage timings và trạng thái được lưu trong Actions summary/artifact 90 ngày. Khi lỗi, chỉ thu container status/exit codes; không tự upload application logs, request bodies, signed URLs hoặc env. Muốn xem log phải thao tác trong phiên SSH riêng và lọc dữ liệu nhạy cảm.

Registry token ngắn hạn được lưu trong Docker config riêng của attempt và xóa sau thao tác. Nếu SSH mất trước cleanup, kiểm tra/xóa riêng `.docker/config.json` và `settings.json` của attempt đó; không xóa `runtime.env` khi còn cần khôi phục.

## Migration và rollback

UGTM image có `--check-schema`, chỉ đọc migration history và yêu cầu tập migrations khớp release. AI `scripts.check_schema` giữ kiểm tra checksum nguyên bản. Không chỉnh checksum đã áp dụng hoặc bỏ qua gate để deploy.

Migration vẫn là thao tác riêng sau backup, có SQL review. Dùng `scripts/Database.ps1` cho UGTM và `python -m scripts.migrate status/apply` cho AI với direct connection đúng DB. Dùng expand/contract: thêm schema tương thích, chuyển ứng dụng, dọn schema cũ ở release sau.

Automatic rollback chỉ chạy khi release trước có manifest, schema fingerprints giống nhau, `data_contract` giống nhau, cả hai cho phép rollback và private config fingerprints giống nhau. Thay đổi credentials, bucket/volume hoặc policy hạ tầng sẽ chặn automatic rollback. Đọc `docker/release-policy.json` trong mỗi PR: tăng `data_contract` khi đổi định dạng object/job/API không tương thích; đặt `auto_rollback=false` khi cần recovery có người vận hành.

Rollback kiểm tra schema của image cũ trước, drain worker rồi khôi phục Compose/env của chính release cũ và kiểm tra health. Không restore DB, xóa object hoặc volume. Khi không đủ điều kiện, giữ `pending` và trạng thái `recovery_required`; deploy kế tiếp dừng để tránh chồng sự cố.

**Lần đầu chuyển từ workflow cũ**: release cũ chỉ có SHA/env, chưa có manifest/config fingerprint, nên không đủ bằng chứng để auto rollback. Giữ thư mục/volume cũ. Nếu lần chuyển đầu thất bại sau mutation, dùng Compose và `runtime.env` của release cũ để khôi phục sau khi xác minh schema; không xóa network/volume hoặc dùng `down -v`. Sau một release mới thành công, các release tiếp theo có đầy đủ baseline rollback.

Workflow `Production Operations`:

| Action | Ý nghĩa |
| --- | --- |
| `monitor` | Kiểm tra public HTTP/TLS, container, DB/broker, RAM/disk/queue/job age |
| `rollback` | Kiểm tra release hiện tại có thể rollback, khôi phục Vercel và backend về baseline đã lưu |
| `recover` | Rollback attempt chưa hoàn tất, nhập đầy đủ attempt ID; thao tác idempotent |
| `smoke-auth` | Đăng nhập, me, refresh, logout và fixture kiểm tra quyền/download đã cấu hình |
| `smoke-gpu` | Có chủ đích upload DOCX tổng hợp qua UGTM và đợi report; có thể phát sinh Modal credit |
| `backup` / `restore-drill` | Backup ngoài VM / restore trong container cô lập |
| `cleanup` | Giữ current/previous và ít nhất 5 release thành công gần nhất; chỉ xóa release hoàn tất ngoài tham chiếu, cũ hơn 30 ngày, khi có backup thành công trong 36 giờ |

Nếu rollback Vercel hoặc backend thất bại một phần, đọc summary và trạng thái thực tế của cả hai phía trước khi tiếp tục. Mất SSH sau finalize được đối chiếu `status.json` trước khi quyết định rollback. Nếu mất kết nối không thể xác định trạng thái, xử lý như sự cố chưa xác nhận, không tự tiếp tục release mới.

## Smoke test riêng

Thiết lập `SMOKE_USER` / `SMOKE_PASSWORD` (secrets) cho tài khoản Student kiểm thử riêng. Không dùng admin hoặc tài khoản người dùng thật. `SMOKE_DENIED_PATH=/api/users` kiểm tra 403 với tài khoản này; `SMOKE_FILE_ID` là fixture được phép tải, `SMOKE_FILE_SHA256` là checksum mong đợi. Token/cookies chỉ giữ trong RAM, không ghi artifact.

`smoke-gpu` cần thêm `SMOKE_GROUP_ID` của nhóm kiểm thử riêng, đủ điều kiện nộp trong cửa sổ FINAL_EVALUATION. Test tạo phiên bản báo cáo và scan thật, không chạy mỗi deploy hoặc tự xóa lịch sử. Không chạy trên nhóm luận văn thật. Nếu chưa có test data hợp lệ thì giữ smoke GPU thủ công, không báo đã kiểm thử end-to-end.

HTTP smoke mỗi deploy không gọi GPU. Auth smoke chỉ chạy mỗi deploy sau khi bật `SMOKE_AUTH_ENABLED=true`; khi chưa bật, release summary không phải bằng chứng xác thực đầy đủ luồng đăng nhập.

Dev/test cần database, bucket và queue riêng. Queue production khác tên không cô lập dữ liệu nếu vẫn dùng chung DB: reconciler AI đọc job toàn DB. Giữ worker/producer phát triển ngừng truy cập DB production trong quá trình chuyển đổi.

## Backup S3 ngoài VM

Chưa bật sẵn vì cần chọn nơi lưu. Dùng bucket/repository riêng ngoài VM production, access key riêng và mật khẩu mã hóa độc lập. Cất mật khẩu restic và credentials ở nơi khôi phục được khi mất VM.

1. Cài `restic` trên VM theo nguồn package tin cậy; kiểm tra phiên bản.
2. Sao chép `docker/backup.example.json` thành `~/ugtm/production/backup.json`, mode 0600. Điền hai direct PostgreSQL URIs, kho S3 HTTPS, hai bucket chính xác, key-ring/TLS volume đã inspect và image digests. File này không được commit/upload Actions.
3. Tạo MinIO user chỉ có ListBucket/GetObject cho hai bucket; không dùng tài khoản root. Quyền PG backup phải đọc được toàn bộ dữ liệu cần khôi phục, bao gồm quyền liên quan RLS nếu có. Không tự đổi quyền DB trong deploy.
4. Khởi tạo repository restic một lần bằng `python3 ~/ugtm/production/current/scripts/production_backup.py init`. Kiểm tra đúng bucket/prefix riêng trước khi chạy; lệnh lấy cấu hình từ file JSON private, không cần `source` hoặc đưa mật khẩu vào arguments.
5. Chạy `backup`, sau đó `restore-drill` thủ công. Chỉ bật `BACKUP_ENABLED` sau hai thao tác pass.

Backup lấy hai custom-format DB dumps, objects theo ETag với SHA-256, archive key ring/TLS/classifier artifacts và release/env cần khôi phục. Object key không được dùng làm filesystem path. Restic mã hóa trước khi đưa ngoài VM, kiểm tra dữ liệu theo phân đoạn sau mỗi backup. Giữ 7 daily / 4 weekly / 3 monthly snapshots với tag riêng và 2 bản local; retention không xóa volume hoặc image.

Đây là logical backup trong lúc hệ thống chạy, không phải snapshot nguyên tử xuyên hai DB và storage. Khi cần mốc khôi phục nhất quán cho migration/cutover, ngừng ghi/đợi job hoàn tất rồi backup. Không coi dump/list kiểm tra được là đã restore thành công.

Restore drill tải snapshot từ kho ngoài VM, kiểm SHA-256, restore hai DB trong PostgreSQL/pgvector 18 tạm, khôi phục objects vào MinIO tạm và đọc lại checksum, kiểm archive TLS. Network internal, không publish port, không gắn volume/DSN production. Xóa container/anonymous volumes/network tạm sau test. Cần ít nhất 1800 MiB RAM rảnh và 4 GiB disk; có giới hạn CPU/RAM cho container. Nếu VM không đủ chỗ, drill dừng; thực hiện trên máy cô lập đủ tài nguyên thay vì tăng tải production.

## Lịch và cảnh báo

Sau khi bật repository variable `OPS_ENABLED=true`:

- Monitor mỗi 30 phút (phút 13/43). Đây là kiểm tra vận hành cơ bản; GitHub schedules có thể chậm và không thay thế uptime monitoring chuyên dụng.
- Backup 19:23 UTC, tức 02:23 giờ Việt Nam ngày kế tiếp, khi `BACKUP_ENABLED=true`.
- Restore drill 20:37 UTC ngày 1 mỗi tháng, tức 03:37 ngày 2 ở Việt Nam, khi backup đã bật.
- Re-scan API/AI và các digest hạ tầng đang chạy vào thứ Hai, 03:47 UTC. Scan lỗi tạo Actions run thất bại, không tự nâng image trên production.

Ngưỡng ban đầu: disk rảnh <2 GiB, RAM available <256 MiB, queue >50, job chưa hoàn tất >1800 giây, container lỗi, backup >36 giờ hoặc restore drill >40 ngày. Điều chỉnh theo số liệu thực, không coi các giá trị này là SLO đã kiểm thử tải.

Bật notifications cho workflow failures theo tài khoản GitHub của người vận hành. Không tự gửi Slack/email, tạo issue hoặc cấu hình dịch vụ cảnh báo ngoài repo. Khi có uptime/SLO nghiêm ngặt, cần thêm nơi nhận cảnh báo và cơ chế theo dõi độc lập.

## Kiểm tra thay đổi workflow

```powershell
$env:UGTM_PYTHON = 'C:\Projects\UGTM-Plagiarism-AI\.venv\Scripts\python.exe'
.\scripts\Invoke-SkillPython.ps1 -m unittest discover -s scripts/tests -p 'test_*.py' -v
.\scripts\Invoke-SkillPython.ps1 scripts/tests/check_production_deployment.py
```

State machine Bash cần Linux (CI hoặc container tạm). Test dùng Docker/network giả lập, không truy cập Neon/MinIO/Modal thật. Kiểm tra thêm actionlint, shellcheck, backend build/schema preflight và AI unit tests. Việc test local không thay thế một lần chạy GitHub/Vercel/Azure thật sau khi cấu hình secrets và merge được duyệt.

## Nguồn kỹ thuật

- [GitHub hosted runners](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
- [Docker multi-platform CI](https://docs.docker.com/build/ci/github-actions/multi-platform/)
- [Vercel staged deployment](https://vercel.com/docs/cli/deploy)
- [Vercel promotion](https://vercel.com/docs/cli/promote)
- [restic backup](https://restic.readthedocs.io/en/stable/040_backup.html)
