# UGTM — Hệ thống Quản lý Khóa luận Tốt nghiệp

Ứng dụng web hỗ trợ quy trình quản lý Khóa luận Tốt nghiệp (KLTN): đăng ký/duyệt đề tài, đề cương, báo cáo giữa kỳ/cuối kỳ, hội đồng chấm, tổng hợp điểm. Xem đặc tả chi tiết tại [`docs/Requirements.md`](docs/Requirements.md), [`docs/Modules.md`](docs/Modules.md) và thiết kế kiến trúc tại [`docs/Architecture.md`](docs/Architecture.md).

## Tech stack

| | |
|---|---|
| Backend | ASP.NET Core (.NET 10), kiến trúc **Modular Monolith + DDD** (tham khảo [kgrzybek/modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd)) |
| Frontend | React 19 + TypeScript + Vite + Ant Design + React Router + TanStack Query |
| Database | Neon Serverless PostgreSQL 17 (AWS Singapore) |
| Auth | ASP.NET Core Identity + JWT |

## Yêu cầu môi trường

- [.NET SDK 10](https://dotnet.microsoft.com/download) trở lên (đang phát triển với `10.0.303`)
- [Node.js](https://nodejs.org/) 20+ và npm 10+ (đang phát triển với Node `24.x` / npm `11.x`)
- Git

Kiểm tra nhanh:

```bash
dotnet --version
node --version
npm --version
```

## Cấu trúc thư mục

```
UGTM/
├── UGTM.sln                   # Solution .NET (mở bằng Visual Studio/Rider nếu muốn)
├── backend/
│   ├── host/Api/               # ASP.NET Core host — composition root, chạy ứng dụng
│   ├── BuildingBlocks/          # Domain/Application/Infrastructure dùng chung mọi module
│   ├── Modules/                 # 9 module nghiệp vụ, mỗi module có 4 layer:
│   │                            #   Domain / Application / Infrastructure / Contracts
│   └── tests/
│       ├── Architecture.Tests/ # Test enforce ranh giới module (bắt buộc phải xanh)
│       └── {Module}.Tests/     # Unit test theo từng module
├── frontend/                   # React SPA
├── docs/                       # Đặc tả nghiệp vụ (Requirements.md, Modules.md)
└── docker/                     # (đang trống — sẽ bổ sung docker-compose sau)
```

9 module nghiệp vụ hiện có: `AcademicPeriods`, `Identity`, `TopicCatalog`, `ThesisManagement`, `MidtermProgress`, `FinalReportEvaluation`, `DefenseCouncil`, `GradingConsolidation`, `Notifications`.

## Setup lần đầu

### 1. Clone

```bash
git clone <repo-url>
cd UGTM
```

### 2. Backend

Chạy từ thư mục gốc repo:

```bash
dotnet restore UGTM.sln
dotnet build UGTM.sln
```

Chạy API:

```bash
dotnet run --project backend/host/Api/UGTM.Api.csproj
```

Mặc định lắng nghe tại:
- HTTP: `http://localhost:5287`
- HTTPS: `https://localhost:7289`

Kiểm tra API đã chạy đúng (mỗi module có sẵn endpoint `ping` để verify wiring):

```bash
curl http://localhost:5287/api/modules/Identity/ping
# => {"module":"Identity","status":"ok"}
```

Chạy toàn bộ test:

```bash
dotnet test UGTM.sln
```

> Hiện tại chỉ `Architecture.Tests` có test thật (enforce ranh giới giữa các module/layer). Các project `*.Tests` khác đang trống, sẽ có nội dung khi từng module được code nghiệp vụ.

### 3. Frontend

```bash
cd frontend
npm install
```

Tạo file môi trường từ mẫu:

```bash
# bash / git bash
cp .env.example .env
```

```powershell
# PowerShell
Copy-Item .env.example .env
```

Mặc định `.env` trỏ `VITE_API_BASE_URL=http://localhost:5287` — chỉnh lại nếu backend chạy ở port khác.

Chạy dev server:

```bash
npm run dev
```

Mở trình duyệt tại `http://localhost:5173`.

Các lệnh khác:

```bash
npm run build     # build production
npm run lint      # oxlint
```

### 4. Kiểm tra end-to-end

Chạy song song backend (bước 2) và frontend (bước 3), sau đó mở `http://localhost:5173`, vào menu bất kỳ module nào — trang sẽ gọi `GET /api/modules/{module}/ping` và hiển thị phản hồi từ backend. Nếu thấy lỗi CORS trong console, kiểm tra backend đang chạy đúng port `5287` (policy CORS trong `backend/host/Api/Program.cs` đang chỉ cho phép origin `http://localhost:5173`).

## Kiến trúc backend (Modular Monolith + DDD)

- Mỗi module gồm 4 project: `Domain`, `Application`, `Infrastructure`, `Contracts`.
- `Domain` chỉ được phụ thuộc `BuildingBlocks.Domain` và `Contracts` của chính module đó.
- Một module **không được** tham chiếu trực tiếp `Domain`/`Application`/`Infrastructure` của module khác — chỉ được giao tiếp qua `Contracts`. Ranh giới này được enforce tự động bởi `backend/tests/Architecture.Tests` (dựa trên `Assembly.GetReferencedAssemblies()`), build/test sẽ đỏ ngay nếu vi phạm.
- Host (`UGTM.Api`) là composition root: mỗi module implement interface `IModule` (`RegisterModule` để đăng ký DI, `MapEndpoints` để khai báo route), được liệt kê tường minh trong `Program.cs` — không dùng reflection scan.

## Chạy nhanh với Docker Compose (Dev)

Tạo file `.env` từ `.env.example` và điền chuỗi kết nối Neon PostgreSQL:

```bash
cp .env.example .env
```

Khởi chạy hệ thống gồm Backend API và Frontend SPA (kết nối trực tiếp tới Neon PostgreSQL):

```bash
docker compose up -d --build
```

- Frontend: `http://localhost:3000`
- Backend API: `http://localhost:5287`
- Database: Neon Serverless PostgreSQL Cloud

Dừng các container:

```bash
docker compose down
```

## CI/CD Pipeline (GitHub Actions)

Dự án thiết lập quy trình tự động hóa:

1. **Continuous Integration (CI)**:
   - `backend-ci.yml`: Chạy `dotnet format --verify-no-changes`, quét lộ secret `gitleaks`, build Release, chạy toàn bộ Unit & Architecture tests, verify build Docker image API.
   - `frontend-ci.yml`: Chạy `oxlint`, `tsc -b`, `npm run build`, verify build Docker image Frontend Nginx.
2. **Publish Image**:
   - `publish.yml`: Đóng gói Docker image đa kiến trúc (`linux/amd64,linux/arm64`) cho API và Frontend, quét an toàn container với **Trivy**, đẩy image lên GitHub Container Registry (`ghcr.io/karasuma-2401/ugtm-*`).
3. **Continuous Deployment (CD)**:
   - `deploy.yml`: SSH vào VPS máy chủ, pull image mới nhất và restart container thông qua `docker compose -f docker-compose.prod.yml up -d`.

