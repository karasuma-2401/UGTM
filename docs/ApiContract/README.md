# API contracts

Mỗi module có một contract riêng. Contract chỉ mô tả endpoint đã triển khai hoặc thiết kế đã được chốt rõ ràng; trạng thái phải được ghi ở đầu file.

| Module | Contract | Trạng thái |
| --- | --- | --- |
| Identity (Auth) | [IdentityApiContract.md](IdentityApiContract.md) | Đã triển khai |
| Identity (Users) | [UserManagementApiContract.md](UserManagementApiContract.md) | Đã triển khai |
| Identity (Role & Permission) | [RolePermissionApiContract.md](RolePermissionApiContract.md) | Đã triển khai |
| TopicCatalog (Topics & Tracking) | [TopicCatalogApiContract.md](TopicCatalogApiContract.md) | Mục 1–4 đã triển khai; mục 5 là yêu cầu bổ sung đăng ký và nhóm hướng dẫn, chưa triển khai |
| AcademicPeriods (Batches, Timeline & Score Configuration) | [AcademicPeriodsApiContract.md](AcademicPeriodsApiContract.md) | SCRUM-81 và SCRUM-83 đã triển khai |
| ThesisManagement (Review gates & Progress reports) | [ThesisManagementApiContract.md](ThesisManagementApiContract.md) | SCRUM-88 và SCRUM-89 đã triển khai |

Quy ước response, authentication, lỗi và hành vi đặc biệt được ghi trong contract của module đang sở hữu endpoint. Khi thay đổi request, response, status code hoặc error code, cập nhật contract trong cùng pull request.
