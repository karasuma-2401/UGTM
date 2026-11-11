# UGTM — Hệ thống Quản lý Khóa luận Tốt nghiệp

Đồ án môn Công nghệ .NET (SE310.R11), Nhóm 1, UIT. UGTM hỗ trợ quy trình khóa luận từ quản lý đợt, đề tài và đăng ký nhóm đến nộp hồ sơ, phản biện, bảo vệ, lưu chiểu và công bố điểm.

## Công nghệ và kiến trúc

- **Backend:** ASP.NET Core .NET 10, EF Core, PostgreSQL; kiến trúc Modular Monolith, mỗi module có các lớp Domain, Application, Infrastructure và Contracts.
- **Frontend:** React 19, TypeScript, Vite 8, Tailwind CSS, shadcn/Radix và Ant Design.
- **Tích hợp:** JWT và refresh cookie, SMTP/email outbox, lưu file S3/MinIO, dịch vụ kiểm tra tương đồng AI.
- **CI/CD:** GitHub Actions, Docker Compose; môi trường `dev` và `production` được cấu hình riêng.

Dự án có 6 module: **Identity** (tài khoản, phân quyền), **AcademicPeriods** (đợt và cấu hình điểm), **TopicCatalog** (đề tài), **ThesisManagement** (nhóm và hồ sơ khóa luận), **DefenseCouncil** (phản biện, hội đồng và điểm), **Notifications** (gửi thông báo).

Các luồng nghiệp vụ đang tiếp tục được hoàn thiện và nghiệm thu. Xem API contract và tài liệu kiểm chứng để biết phạm vi đã triển khai.

## Cấu trúc source

| Thư mục                       | Nội dung                                 |
| ----------------------------- | ---------------------------------------- |
| `backend/host/Api`            | API host, cấu hình và kết nối các module |
| `backend/Modules`             | Nghiệp vụ của 6 module                   |
| `backend/BuildingBlocks`      | Thành phần dùng chung                    |
| `backend/Database.Migrations` | Quản lý migration database tập trung     |
| `backend/tests`               | Unit, integration và architecture tests  |
| `frontend/src`                | Giao diện và kết nối API                 |
| `docs`, `backend/docs`        | Yêu cầu, API contract và kiến trúc       |
| `scripts`, `docker`           | Script phát triển và cấu hình triển khai |

## Chạy local

Cần **.NET SDK 10.x**, **Node.js 24.x**, **Docker Compose** và một **database PostgreSQL phát triển**. Compose không cung cấp PostgreSQL.

Tại thư mục gốc, dùng PowerShell (chỉ sao chép `.env` ở lần thiết lập đầu):

```powershell
Copy-Item .env.example .env
# Điền DATABASE_CONNECTION_STRING của database phát triển trong .env.
./scripts/GenerateDevelopmentJwtKey.ps1
./scripts/ConfigureDevelopment.ps1
docker compose up -d mailpit minio

dotnet restore UGTM.sln
# Chỉ chạy Update trên database phát triển được chọn.
./scripts/Database.ps1 -Action Update
dotnet run --project backend/host/Api/UGTM.Api.csproj --launch-profile http
```

Mở terminal khác để chạy frontend:

```powershell
cd frontend
npm ci
Copy-Item .env.example .env
npm run dev
```

| Dịch vụ                 | Địa chỉ local                                        |
| ----------------------- | ---------------------------------------------------- |
| Frontend                | http://localhost:5173                                |
| Backend / API Reference | http://localhost:5287 / http://localhost:5287/scalar |
| Mailpit                 | http://localhost:8025                                |
| MinIO console           | http://localhost:9001                                |

API không tự chạy migration hoặc đọc `.env`; chạy lại `ConfigureDevelopment.ps1` sau khi đổi cấu hình. Nếu đặt `SMTP_HOST`, email sẽ gửi qua SMTP đó thay vì Mailpit. Database mới có thể tạo quản trị viên bằng [BootstrapAdmin.ps1](scripts/BootstrapAdmin.ps1). Cấu hình AI xem [hướng dẫn tích hợp](docs/ai-integration-operations.md).

## Kiểm tra trước khi gửi code

AI agent dùng bộ skill FE chung tại `.agents/skills/`. Xem [hướng dẫn thiết lập và kiểm tra](docs/agent-skills.md) cùng [ngữ cảnh sản phẩm](PRODUCT.md) và [thiết kế](DESIGN.md).

```powershell
dotnet build UGTM.sln
dotnet test UGTM.sln
npm --prefix frontend run lint
npm --prefix frontend run build
```

Integration tests dùng Testcontainers cần Docker đang chạy. Không commit `.env`, khóa JWT hoặc thông tin kết nối thật.

## Tài liệu tham khảo

- [Yêu cầu nghiệp vụ](docs/Requirements.md) và [Use Cases](docs/UseCases.md).
- [Kiến trúc backend](backend/docs/Architecture/Architecture.md) và [API contracts](docs/ApiContract/README.md).
- [Cấu hình SMTP](backend/docs/Technical/EmailDelivery.md).
- [Lưu ý triển khai và migration](backend/docs/Technical/DeploymentNotes.md).
- [Mục lục tài liệu](docs/README.md).
