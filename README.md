# UGTM — Hệ thống Quản lý Khóa luận Tốt nghiệp

Đồ án Công nghệ .NET, SE310.R11, Nhóm 1, UIT. Phạm vi nghiệp vụ đã chốt theo Báo cáo SE310 (Bản Final).docx (tài liệu nghiệp vụ nhóm quản lý ngoài repo): quy trình 21 tuần, 7 giai đoạn, 2 cổng kiểm soát, từ đăng ký đề tài đến lưu chiểu và công bố điểm.

Source hiện ở giai đoạn nền tảng: 6 module có endpoint ping, EF Core có 34 bảng/6 schema và migrations, DefenseCouncil có service tính điểm đã kiểm thử, frontend có dashboard và trang kiểm tra module. Các API nghiệp vụ trong tài liệu chưa được triển khai đầy đủ.

## Tài liệu

Bắt đầu tại [mục lục docs](docs/README.md). Các tài liệu chính: [yêu cầu](docs/Requirements.md), [đặc tả Use Case](docs/UseCases.md), [kiến trúc hiện tại](docs/Architecture.md), [mô hình dữ liệu](docs/DataModel.md), [đề xuất module/DDD và setup Neon](docs/Architecture.md#6-migrations-và-neon). Đã bỏ toàn bộ Use Case diagram theo yêu cầu nhóm.

## Công nghệ hiện tại

| Phần | Source đang dùng |
| --- | --- |
| Backend | ASP.NET Core .NET 10; 6 module, mỗi module có Domain/Application/Infrastructure/Contracts |
| Dữ liệu | EF Core 10 + Npgsql PostgreSQL; 6 DbContext runtime, 1 context migration; 6 schema nghiệp vụ; history ở public |
| Frontend | React 19, TypeScript, Vite 8, React Router, TanStack Query, Axios |
| Giao diện | Tailwind CSS + các component shadcn/Radix trong layout đang chạy; có Ant Design và `ConfigProvider` |
| Xác thực | Có lớp hỗ trợ token/route guard phía frontend; backend chưa triển khai Identity/JWT/RBAC |
| Hạ tầng | Docker Compose có Mailpit, MinIO, Redis, RabbitMQ; chưa có code tích hợp các dịch vụ này |

Khác biệt với thiết kế kỹ thuật trong Word được ghi tại [Architecture.md](docs/Architecture.md). Docs lấy kiến trúc source hiện tại làm chuẩn theo quyết định của nhóm ngày 10/10/2026.

## Môi trường

- .NET SDK 10.x; Node.js 24.x và npm tương ứng để đồng nhất Docker/CI. Mốc Node 20+ trong Word không phải cấu hình runtime của repo hiện tại.
- Git; Docker Compose nếu chạy container.
- PostgreSQL riêng hoặc Neon với chuỗi kết nối hợp lệ. Compose không tạo container database.

## Chạy backend

Từ thư mục gốc, PowerShell:

```powershell
dotnet restore UGTM.sln
dotnet build UGTM.sln
$env:ConnectionStrings__DefaultConnection = '<chuỗi kết nối PostgreSQL>'
dotnet run --project backend/host/Api/UGTM.Api.csproj --launch-profile http
```

API: `http://localhost:5287`. Swagger trong Development: `http://localhost:5287/swagger`; OpenAPI: `/openapi/v1.json`. Profile `https` sử dụng thêm `https://localhost:7289`.

```powershell
Invoke-RestMethod http://localhost:5287/api/modules/Identity/ping
dotnet test UGTM.sln
```

Ping trả `{"module":"Identity","status":"ok"}` trực tiếp. `Architecture.Tests` kiểm tra ranh giới module/layer, DI và cấu hình database; AcademicPeriods/DefenseCouncil có kiểm thử trọng số và công thức tính điểm; ThesisManagement kiểm thử aggregate nhóm sinh viên. API không tự migrate khi khởi động. API đọc cấu hình môi trường, không tự nạp `.env`; script database bên dưới có nạp các biến kết nối từ `.env`.

### Áp dụng migration trên database phát triển

Dùng công cụ EF cục bộ pin 10.0.12 và chuỗi kết nối của database được chọn (hoặc điền DATABASE_CONNECTION_STRING trong `.env`):

```powershell
$env:DATABASE_CONNECTION_STRING = '<chuỗi kết nối PostgreSQL của database phát triển>'
./scripts/Database.ps1 -Action List
./scripts/Database.ps1 -Action Check
./scripts/Database.ps1 -Action Script
./scripts/Database.ps1 -Action Update
```

Xem [Architecture.md](docs/Architecture.md#6-migrations-và-neon) để biết thứ tự cấu hình, lệnh `Update-Database` và những phần cần triển khai tiếp. Giữ migration InitialCreate; AlignFinalReportFoundation bổ sung dữ liệu final; AssignSchemaOwnership chuyển bảng về module và bỏ shared; EncapsulateModulePersistence loại bảng trọng số theo role cũ. Database đã chạy bản cũ cần bước chuyển history sang public một lần trước List/Update; Neon hiện tại đã chuyển. [DatabaseScript.txt](docs/DatabaseScript.txt) là DBML để xem cấu trúc, không phải SQL chạy trên Neon.

## Chạy frontend

```powershell
Set-Location frontend
npm ci
Copy-Item .env.example .env
npm run dev
```

Mở `http://localhost:5173`. `VITE_API_BASE_URL` mặc định là `http://localhost:5287`. Router hiện có `/` và `/modules/:modulePath`; chưa có màn hình đăng nhập hay các màn hình KLTN. `npm run lint` kiểm tra Oxlint; `npm run build` chạy TypeScript rồi Vite.

## Chạy Docker Compose

Từ thư mục gốc:

```powershell
Copy-Item .env.example .env
# Điền DATABASE_CONNECTION_STRING trong .env.
docker compose up -d backend mailpit minio redis rabbitmq
```

Chạy frontend bằng Vite như trên. `docker compose up -d` cũng khởi chạy frontend Nginx tại `http://localhost:3000`.

| Dịch vụ | Địa chỉ |
| --- | --- |
| Backend | `http://localhost:5287` |
| Frontend Vite / container | `http://localhost:5173` / `http://localhost:3000` |
| Mailpit web / SMTP | `http://localhost:8025` / `localhost:1025` |
| MinIO console / S3 API | `http://localhost:9001` / `http://localhost:9000` |
| RabbitMQ management / AMQP | `http://localhost:15672` / `localhost:5672` |
| Redis | `localhost:6379` |

Backend đọc `Cors:AllowedOrigins`; Compose cấu hình origin 5173 và 3000. Chạy bằng Vite ngoài Compose có fallback origin 5173. Các dịch vụ phụ trợ khởi chạy không đồng nghĩa email/upload/event bus/cache đã hoạt động. Cấu hình tài khoản dịch vụ xem `.env.example`; không đưa credentials thật vào tài liệu.

Dừng môi trường bằng `docker compose down`. File cấu hình production: [docker/docker-compose.prod.yml](docker/docker-compose.prod.yml).

## Cấu trúc và CI

- `backend/host/Api`: composition root, DI, CORS, middleware, Swagger, nạp module qua `IModule`.
- `backend/Modules`: Identity, AcademicPeriods, TopicCatalog, ThesisManagement, DefenseCouncil, Notifications.
- `backend/BuildingBlocks`: Entity/AggregateRoot/Result/Error/IDomainEvent, IModule, ApiResponse/ApiError, middleware.
- `backend/Database.Migrations`: context migration tổng hợp, FK liên module và migrations; entity/configuration thuộc module tương ứng.
- `backend/tests`: kiểm thử kiến trúc và ba module đang có logic miền: AcademicPeriods, DefenseCouncil, ThesisManagement. Thêm project test cho module khác khi có nghiệp vụ cần kiểm thử.
- `frontend`: SPA; [hướng dẫn frontend](frontend/README.md).
- `docs/PlantUML`: sơ đồ kiến trúc, nghiệp vụ, tuần tự, sitemap.

GitHub Actions chạy trên `dev`/`main`: backend format, Gitleaks, build/test, Docker build; frontend lint/typecheck/build và Docker build. Workflow publish đóng gói image, quét Trivy và đẩy GHCR; deploy SSH được bỏ qua khi chưa có secrets VPS. File workflow thể hiện cấu hình tự động hóa, không chứng minh dịch vụ đã được triển khai.
