# CI/CD và triển khai production

Tài liệu này tóm tắt hệ thống đã triển khai và cách vận hành thường ngày. Các bước recovery, backup và chẩn đoán chi tiết nằm trong [runbook vận hành](production-operations-runbook.md).

## Kiến trúc

- **GitHub Actions** kiểm tra code, tạo release và điều phối deploy; frontend chạy trên **Vercel**.
- **Azure VM ARM64** chạy .NET API, AI API, Celery worker, reconciler, Redis, MinIO và Caddy bằng Docker Compose. Hai database dùng Neon; Modal cung cấp GPU.
- API dùng `https://ugtm.eastasia.cloudapp.azure.com`; MinIO S3 qua HTTPS cổng `8443`. Console MinIO chỉ bind localhost. Frontend container trên Azure đã được bỏ khỏi Compose production.
- Một VM vẫn là điểm lỗi đơn; quy trình không đảm bảo zero downtime. Backup ngoài VM và auth/GPU smoke cần cấu hình và kiểm tra trước khi bật.

## Các workflow đã triển khai

| Workflow                                                     | Khi chạy                                                      | Chức năng                                                                                                                                                                                               |
| ------------------------------------------------------------ | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [CI](../.github/workflows/ci.yml)                            | PR vào `dev`/`production`, thủ công hoặc được gọi bởi publish | Gọi backend, frontend, AI CI và kiểm tra hợp đồng release. Backend kiểm tra format, secrets, test/schema và image; frontend lint, typecheck, test và build; AI chạy unit/integration và kiểm tra image. |
| [Publish Images](../.github/workflows/publish.yml)           | Push `dev`/`production`, thủ công hoặc theo lịch              | Chạy CI, build hoặc tái sử dụng image API/AI/gateway, scan Trivy theo kiến trúc và tạo release bundle theo digest.                                                                                      |
| [Deploy to VPS](../.github/workflows/deploy.yml)             | Publish từ push `production` thành công hoặc chạy thủ công    | Staging frontend Vercel, kiểm tra candidate trên Azure, promote frontend, smoke test và finalize. Hỗ trợ `prepare`, `deploy` và forward recovery.                                                       |
| [Production Operations](../.github/workflows/operations.yml) | Thủ công hoặc theo lịch khi bật                               | Monitor, rollback/recover, smoke auth/GPU, backup, restore drill và cleanup; một số chức năng cần cấu hình riêng.                                                                                       |
| [Rescan Production Images](../.github/workflows/rescan.yml)  | Thủ công hoặc theo lịch khi `OPS_ENABLED=true`                | Quét lại các digest production đang chạy; không tự nâng image.                                                                                                                                          |
| [Dependabot](../.github/dependabot.yml)                      | Hằng tuần                                                     | Đề xuất cập nhật GitHub Actions qua PR để review và chạy CI.                                                                                                                                            |

GitHub Actions được pin theo commit SHA. CI còn có actionlint, shellcheck, Compose và regression tests cho release/recovery. Workflow có thể được triển khai trong code nhưng chưa được bật hoặc kiểm chứng thực tế: xem trạng thái từng chức năng bên dưới.

## Đọc pipeline Publish Images trên GitHub Actions

Mỗi ô trong sơ đồ là một **job**; một job gồm nhiều **step**. Các đường nối thể hiện job phải chờ job trước. `checks` gọi reusable workflow `CI`, nên các job bên trong có tên dạng `checks / backend / Run Tests`.

| Job hiển thị                                    | Việc thực hiện                                                                                                                                                  |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `checks / backend / Format Check`               | Kiểm tra định dạng mã .NET.                                                                                                                                     |
| `checks / backend / Secret Scanning (Gitleaks)` | Quét secret trong repository.                                                                                                                                   |
| `checks / backend / Run Tests`                  | Build và chạy test backend; kiểm tra gateway và schema gate trên DB tạm.                                                                                        |
| `checks / backend / Docker Build Verification`  | Kiểm tra API image, Compose và scan MinIO khi `verify_image` được bật.                                                                                          |
| `checks / frontend / Lint & Typecheck`          | Cài dependencies, lint, typecheck và chạy frontend tests.                                                                                                       |
| `checks / frontend / Build SPA`                 | Build frontend sau khi lint/typecheck thành công.                                                                                                               |
| `checks / ai / unit`, `integration`             | Chạy unit test và integration test với DB/Redis tạm; migration của test chỉ áp dụng vào DB tạm.                                                                 |
| `checks / ai / docker`                          | Kiểm tra build AI image ARM64 khi `verify_image` được bật.                                                                                                      |
| `checks / release-contracts`                    | Test release/recovery, Compose, cú pháp shell, ShellCheck và actionlint.                                                                                        |
| `plan`                                          | Đối chiếu manifest trước đó để quyết định build mới hoặc tái sử dụng digest và lập danh sách kiến trúc.                                                         |
| `infrastructure-scan`                           | Các job matrix quét riêng Redis và MinIO bằng Trivy trên ARM64.                                                                                                 |
| `build`                                         | Các job matrix xử lý `api`, `ai`, `gateway` theo từng kiến trúc; build mới hoặc dùng lại image, rồi scan đúng digest. Gateway còn được validate cấu hình Caddy. |
| `release`                                       | Chờ `build` và `infrastructure-scan` thành công, ghép manifest image và đóng gói release bundle; không build lại image.                                         |

Ví dụ trong hình: **6 job build** tương ứng 3 thành phần × 2 kiến trúc `amd64`/`arm64`; **2 job infrastructure-scan** là Redis và MinIO. Số job build có thể thay đổi theo cấu hình kiến trúc. `plan` và `checks` có thể chạy song song; `build` và `infrastructure-scan` chỉ bắt đầu sau khi cả hai hoàn tất.

Biểu tượng tròn có gạch là **skipped**, cần xem điều kiện của job. Trong lần publish này, `checks` gọi CI với `verify_image=false` để tránh lặp một số bước build image; PR CI có thể chạy các kiểm tra đó. Skipped không tự động có nghĩa là failed, nhưng cũng không chứng minh kiểm tra ấy đã chạy.

Mục **Artifacts** chứa nhiều file phục vụ các job: báo cáo scan, thông tin từng platform và release bundle, cùng test results khi được upload. Số lượng artifacts phụ thuộc các job/step đã chạy; `release-bundle-<attempt>` là gói đầu vào cho deploy. Publish thành công có nghĩa release đã được tạo; kiểm tra workflow **Deploy to VPS** riêng để biết production đã triển khai chưa.

## Đọc Deploy to VPS và Production Operations

Trong job `deploy`, bước **Deploy the verified release** ghi các stage chính sau:

| Stage                                        | Ý nghĩa                                                                                    |
| -------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `SSH transfer and registry authentication`   | Xác minh SSH, chuyển release bundle và đăng nhập registry.                                 |
| `stage_frontend` → `verify_frontend_scope`   | Tạo frontend candidate trên Vercel và kiểm tra đúng scope, trạng thái `READY`.             |
| `backend_candidate` → `external_checks`      | Kiểm tra schema/storage, drain worker, khởi động candidate và kiểm tra từ bên ngoài.       |
| `promote_frontend` → `production_http_smoke` | Đưa frontend candidate lên production và kiểm tra HTTP. Auth smoke chạy thêm khi được bật. |
| `finalize`                                   | Ghi release là `deployed` và cập nhật `current` sau các kiểm tra.                          |
| `rollback_frontend`, `rollback_backend`      | Khôi phục khi deploy lỗi và đủ điều kiện; nếu chưa an toàn, giữ `recovery_required`.       |

**Release timings and recovery summary** và artifact trạng thái giúp xác định stage đã dừng. Với `prepare`, workflow chỉ chuẩn bị; không chạy chuỗi candidate/promote ở trên.

Trong **Production Operations**, chọn action khi chạy thủ công: `monitor` kiểm tra sức khỏe; `rollback` khôi phục release; `recover` xử lý rollback attempt chưa hoàn tất; `smoke-auth` / `smoke-gpu` kiểm tra luồng người dùng; `backup` / `restore-drill` kiểm tra sao lưu; `cleanup` dọn release đủ điều kiện. Forward recovery sang release mới dùng **Deploy to VPS** với `recovery_attempt`, khác action `recover` dùng để rollback. Lịch và điều kiện cụ thể: [runbook](production-operations-runbook.md#lịch-và-cảnh-báo).

## Luồng phát hành

1. Tạo PR vào `dev`, chờ các check; merge lên `dev` rồi tạo PR vào `production`.
2. Push lên mỗi nhánh chạy **Publish Images**. Runner ARM64 build API/AI/gateway; AMD64 cũng được build nếu `BUILD_AMD64=true`. Image không đổi có thể tái sử dụng từ manifest hợp lệ cùng nhánh tối đa 7 ngày, nhưng vẫn phải scan lại. `force_rebuild=true` build lại.
3. Trivy scan đúng platform và digest, gồm Redis/MinIO; bundle chứa manifest, schema fingerprints, rollback policy, checksums và bằng chứng image. Artifact được giữ 90 ngày.
4. Publish thành công từ push `production` kích hoạt **Deploy to VPS**. Frontend được tạo với `--skip-domain`; workflow kiểm tra scope, schema, storage, worker và health, sau đó promote, chạy HTTP smoke và cập nhật `current`.
5. Kiểm tra ứng dụng thực tế. HTTP smoke không kiểm tra đăng nhập hoặc GPU; auth smoke chỉ chạy khi bật `SMOKE_AUTH_ENABLED`.

Vercel Git auto-deploy đã tắt để Actions điều phối release. `prepare` chỉ chuẩn bị, còn `deploy` mới triển khai. Chạy thủ công dùng **ID của Publish Images thành công trên production**, không dùng commit SHA hay publish của `dev`.

## Cấu hình và vận hành

Secrets và variables cấu hình một lần, cập nhật khi thay đổi hoặc hết hạn. Các giá trị chính nằm trong GitHub environment `production`: SSH và `VPS_KNOWN_HOSTS`, `VERCEL_TOKEN`, database, JWT, MinIO, Modal và SMTP; `VERCEL_ORG_ID` / `VERCEL_PROJECT_ID` xác định Vercel project. Không đưa giá trị bí mật vào tài liệu hoặc log.

Vercel dùng Root Directory `frontend`, Node 24, `npm run build`, output `dist` và rewrite `/api/*` tới `API_ORIGIN`. CLI được pin tại `66.2.0` và dùng team scope tường minh. Đợt recovery 21/11/2026 thành công sau khi cập nhật token có scope All Projects của team; nguyên nhân gốc của lỗi token trước đó chưa được xác định chắc chắn.

`OPS_ENABLED` bật các lịch vận hành; `BACKUP_ENABLED` chỉ bật sau khi cấu hình kho ngoài VM, backup và restore drill thành công. `SMOKE_AUTH_ENABLED` chỉ bật sau khi chuẩn bị tài khoản kiểm thử và chạy smoke thành công. Backup cần kho S3 tương thích ngoài VM; cấu hình mẫu ở [backup.example.json](../docker/backup.example.json).

## Redis cache của backend

Backend dùng `cache-redis` riêng, có password, healthcheck và không publish port ra host. `ai-redis` tiếp tục làm Celery broker với AOF và `noeviction`; không dùng broker để chứa cache ứng dụng. Cache có thể bị eviction hoặc mất hoàn toàn mà không mất dữ liệu nghiệp vụ.

Trước khi triển khai thay đổi cache này:

1. Tạo GitHub production secret `REDIS_CACHE_PASSWORD`: 32–128 ký tự ngẫu nhiên trong `A-Z`, `a-z`, `0-9`, `_`, `-` (ví dụ 64 ký tự hex). Giá trị mẫu không dùng cho production.
2. Backup và review/apply migration `20261121120000_AddTransactionalCacheRevision` bằng quy trình migration riêng. Nếu role migration khác role ứng dụng, cấp `SELECT, UPDATE` trên `public.cache_revision` cho role ứng dụng để đọc revision và thực thi trigger. Deploy thông thường không chạy migration và sẽ dừng nếu schema chưa khớp.
3. Kiểm tra ngân sách RAM của VM. Mặc định cache dùng `maxmemory=128mb`, container `192m`; broker dùng `256mb` và container `384m`. Có thể chỉnh qua variables `CACHE_REDIS_MAXMEMORY`, `CACHE_REDIS_MEMORY_LIMIT`, `AI_REDIS_MAXMEMORY`, `AI_REDIS_MEMORY_LIMIT`; luôn chừa RAM cho overhead Redis, AOF và các dịch vụ khác.

Revision `public.cache_revision` thay đổi cùng transaction khi dữ liệu nguồn của read model hội đồng thay đổi. Trigger được deferred đến commit, cập nhật revision một lần mỗi transaction; rollback không đổi revision. Các module nguồn được liệt kê trong migration, gồm điểm/biên bản, phân công phản biện, cấu hình điểm, giữa kỳ, lưu chiểu, đề tài và thông tin người dùng. Khi bổ sung bảng nguồn mới, phải bổ sung trigger bằng migration mới.

Mỗi request chụp revision một lần từ PostgreSQL primary trước khi nạp dữ liệu để cache. Request bắt đầu đọc sau khi mutation commit sử dụng revision mới, kể cả khi Redis đang lỗi hoặc đã phục hồi. Request đang xử lý có thể hoàn tất với revision đã chụp; cache của nó không được request có revision mới sử dụng lại. Production bắt buộc cấu hình Redis và bật database revisions. Scope DI cho `ICacheService` phải giới hạn trong một request/job; không giữ service này trong singleton hay job sống lâu.

Đổi revision hiện vô hiệu hóa toàn bộ read model hội đồng. Đây là lựa chọn ưu tiên tính chính xác; có thêm một truy vấn revision nhỏ mỗi request và một cập nhật row revision ở cuối transaction ghi. Cần đo tải trước khi phân nhỏ revision theo batch/module. Không thêm L1 cache cho revision vì sẽ làm mất bảo đảm cập nhật ngay giữa các API instance.

Payload dùng namespace `cache:v2:`, TTL tối đa 30 phút và jitter giảm tối đa 10%. Nạp lại workspace kết quả học tập và overview hội đồng được gộp giữa các request cùng key trên từng API process; cơ chế này không phải distributed lock giữa các replica. Redis lỗi sẽ được bỏ qua 5 giây trên từng API process. Không đọc được revision thì request bỏ qua cache. Meter `.NET` `UGTM.Cache` cung cấp counters `ugtm.cache.hits`, `misses`, `failures`, `bypasses` cho collector; collector/exporter cần được cấu hình theo hệ thống quan sát đang dùng. Theo dõi RAM, eviction, lỗi và latency Redis khi kiểm thử tải.

Rollback ứng dụng và migration là hai thao tác riêng. Migration Down chỉ bỏ trigger/table revision, không xóa dữ liệu nghiệp vụ; chỉ thực hiện sau khi dừng các API mới đang dùng revision và kiểm tra schema gate. Không xóa volume broker AI khi dọn cache.

## Migration, rollback và recovery

Migration UGTM/AI là thao tác riêng sau backup và review. Deploy chỉ **đọc và kiểm tra** migration history/checksum. Schema không khớp thì dừng; không sửa history hoặc checksum để vượt gate.

Mỗi deploy có attempt riêng; `pending` ngăn release khác chồng lên attempt chưa hoàn tất. Rollback tự động chỉ thực hiện khi release trước đủ bằng chứng và tương thích schema, data contract, cấu hình và storage. Khi không đủ điều kiện, giữ `recovery_required` và kiểm tra trạng thái trước khi thao tác.

Nếu DB đã có migration mới hơn image của attempt đang pending, chọn release production mới có migration đó và chạy **Deploy to VPS → Run workflow** với `deploy_mode=deploy`, `release_run_id` của publish mới và `recovery_attempt` là ID pending đầy đủ. Workflow xác minh trạng thái, schema, cấu hình và worker trước khi chuyển pending. Chi tiết và các trường hợp không được tiếp tục: [runbook migration và rollback](production-operations-runbook.md#migration-và-rollback).

## Sự cố đã gặp

| Dấu hiệu                                             | Cách xử lý đã thực hiện hoặc cần kiểm tra                                                                                   |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| SSH host verification failed                         | Đối chiếu host key từ VM tin cậy và cập nhật `VPS_KNOWN_HOSTS`.                                                             |
| MinIO `:8443` timeout từ ngoài VM                    | Thêm Azure NSG inbound TCP `8443`, rồi kiểm tra lại HTTPS.                                                                  |
| AI migration checksum mismatch                       | Dừng deploy; đối chiếu migration và schema, không tự ghi đè checksum.                                                       |
| Trivy scan fail                                      | Scan đúng kiến trúc, cập nhật MinIO và gateway Caddy với thành phần đã vá.                                                  |
| Vercel config thiếu package hoặc thiếu `destination` | Chuyển config sang type import và rewrite literal có `API_ORIGIN`; thêm kiểm tra trong CI.                                  |
| Vercel `User not found (404)`                        | Thêm team scope; token project vẫn lỗi, token All Projects đã giúp recovery thành công. Chưa kết luận chắc nguyên nhân gốc. |
| `pending` và DB có migration ngoài image             | Dùng release mới chứa migration và forward recovery; không xóa pending hay sửa history.                                     |

Recovery ngày **21/11/2026**: [Publish Images](https://github.com/karasuma-2401/UGTM/actions/runs/44864044629) → [Deploy to VPS #123](https://github.com/karasuma-2401/UGTM/actions/runs/44866138376). Schema UGTM/AI, promote, HTTP smoke và finalize thành công; release `04dd51eb` được ghi nhận `deployed`. Auth smoke chưa chạy.

Chi tiết lệnh, lịch, cấu hình backup, điều kiện rollback và kiểm thử workflow: [runbook vận hành](production-operations-runbook.md).
