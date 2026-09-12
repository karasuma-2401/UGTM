# UGTM — Hệ thống Quản lý Khóa luận Tốt nghiệp

Ứng dụng web hỗ trợ quy trình quản lý Khóa luận Tốt nghiệp (KLTN): đăng ký/duyệt đề tài, đề cương, báo cáo giữa kỳ/cuối kỳ, hội đồng chấm, tổng hợp điểm. Xem đặc tả chi tiết tại [`docs/Requirements.md`](docs/Requirements.md) và [`docs/Modules.md`](docs/Modules.md).

## Tech stack

| | |
|---|---|
| Backend | ASP.NET Core (.NET 10), kiến trúc **Modular Monolith + DDD** (tham khảo [kgrzybek/modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd)) |
| Frontend | React 19 + TypeScript + Vite + Ant Design + React Router + TanStack Query |
| Database | SQL Server (dự kiến — chưa cấu hình, xem mục [Trạng thái hiện tại](#trạng-thái-hiện-tại)) |
| Auth | ASP.NET Core Identity + JWT (dự kiến) |

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

## Trạng thái hiện tại

Dự án đang ở giai đoạn khung sườn (scaffolding):
- ✅ Cấu trúc project, dependency graph giữa các layer/module, composition root, Architecture Tests.
- ✅ Frontend SPA cơ bản với routing + gọi API thật.
- ⏳ Chưa có: EF Core/database thật, MediatR/CQRS, ASP.NET Core Identity + JWT, docker-compose, CI.

## Troubleshooting

| Vấn đề | Cách xử lý |
|---|---|
| `dotnet build` báo lỗi version SDK | Kiểm tra `dotnet --version` ≥ 10.0. Cài lại từ trang chủ .NET nếu thiếu. |
| Frontend gọi API bị lỗi CORS | Backend phải chạy đúng port `5287` (http). Nếu đổi port, sửa `WithOrigins(...)` trong `Program.cs`. |
| Port `5287`/`5173` đã bị chiếm | Dừng process đang dùng port, hoặc đổi port khi chạy (`dotnet run --urls ...`, `npm run dev -- --port ...`) và cập nhật `VITE_API_BASE_URL`/CORS tương ứng. |
| `npm install` lỗi trên Windows | Dùng PowerShell hoặc Git Bash (khuyến nghị), tránh `cmd.exe` thuần. |
