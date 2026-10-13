# Tài liệu backend

Tài liệu trong thư mục này mô tả kiến trúc và cách vận hành backend theo source hiện tại. Nghiệp vụ, use case, mô hình dữ liệu và API contract dùng chung với frontend nằm tại [docs](../../docs/README.md).

| Tài liệu | Nội dung |
| --- | --- |
| [Architecture](Architecture/Architecture.md) | Modular Monolith, ranh giới module, persistence, runtime và migrations |
| [Sequences](Architecture/Sequences.md) | Năm luồng tuần tự nghiệp vụ cần triển khai |
| [Authentication](Technical/Authentication.md) | Thiết kế, bảo mật, cấu hình và vận hành authentication backend |
| [User Management](Technical/UserManagement.md) | Thiết kế, luồng dữ liệu, phân trang và bảo mật quản lý người dùng |
| [PlantUML](Architecture/PlantUML) | Sơ đồ kiến trúc, database, authentication và luồng nghiệp vụ |

API công khai của từng module được quản lý tại [API contracts](../../docs/ApiContract/README.md).
