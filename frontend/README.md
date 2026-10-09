# UGTM frontend

React 19 + TypeScript + Vite 8. Router hiện có dashboard `/` và trang kiểm tra 6 module `/modules/:modulePath`. Layout đang chạy dùng Tailwind/shadcn/Radix; `App.tsx` vẫn dùng Ant Design ConfigProvider.

## Chạy phát triển

Dùng Node.js 24.x như Docker và CI:

```powershell
Set-Location frontend
npm ci
Copy-Item .env.example .env
npm run dev
```

Mở `http://localhost:5173`. API mặc định `http://localhost:5287`, cấu hình bằng `VITE_API_BASE_URL` trong `.env`. Backend đọc Cors:AllowedOrigins, fallback 5173; Compose có cả 5173/3000. Xem [README dự án](../README.md) để chạy API/Docker.

```powershell
npm run lint
npm run build
npm run preview
```

`build` chạy `tsc -b` và Vite. Biến `VITE_*` được đưa vào bundle khi build, không đổi bằng biến môi trường runtime của container Nginx.

## Các phần hiện có

- `src/app/router.tsx`: route thực tế; `queryClient.ts`: TanStack Query.
- `src/shared/config/modules.ts`: danh mục 6 module để kiểm tra endpoint ping.
- `src/shared/layout/AppLayout.tsx`: layout được sử dụng.
- `src/shared/api`: Axios, token storage, event 401 và kiểu dữ liệu.
- `src/shared/auth`: AuthContext/useAuth/ProtectedRoute; chưa tích hợp vào router/app.
- `src/components/ui`: component giao diện dùng chung.

API refresh `/api/identity/auth/refresh` mà interceptor dự kiến gọi chưa được backend triển khai. Chưa có route đăng nhập, trang 403 hoặc màn hình nghiệp vụ. Kiểu response frontend hiện khác `ApiResponse` backend; cần thống nhất trước khi ghép tính năng. Trang ping xử lý object `module/status` trực tiếp.

Danh mục màn hình cần xây dựng tại [SITEMAP.md](../docs/SITEMAP.md); nghiệp vụ final tại [Requirements.md](../docs/Requirements.md). Quyền giao diện cần đi cùng policy kiểm tra phía API.
