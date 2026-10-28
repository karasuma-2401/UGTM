# API contracts

Mỗi module có một contract riêng. Contract chỉ mô tả endpoint đã triển khai hoặc thiết kế đã được chốt rõ ràng; trạng thái phải được ghi ở đầu file.

| Module | Contract | Trạng thái |
| --- | --- | --- |
| Identity (Auth) | [IdentityApiContract.md](IdentityApiContract.md) | Đã triển khai |
| Identity (Users) | [UserManagementApiContract.md](UserManagementApiContract.md) | Đã triển khai |
| Identity (Role & Permission) | [RolePermissionApiContract.md](RolePermissionApiContract.md) | Đã triển khai |
| TopicCatalog (Topics & Tracking) | [TopicCatalogApiContract.md](TopicCatalogApiContract.md) | Mục 1–4 đã triển khai; mục 5 ghi rõ phần đã có/còn thiếu, thời hạn 24h |
| AcademicPeriods (Batches, Timeline & Score Configuration) | [AcademicPeriodsApiContract.md](AcademicPeriodsApiContract.md) | SCRUM-81 và SCRUM-83 đã triển khai |
| ThesisManagement (Review gates, Progress & Final reports) | [ThesisManagementApiContract.md](ThesisManagementApiContract.md) | SCRUM-88, SCRUM-89 và SCRUM-102 đã triển khai; nghiệm thu SCRUM-102 xem bên dưới |
| Advisor evaluation & reviewer handoff | [AdvisorEvaluationApprovalApiContract.md](AdvisorEvaluationApprovalApiContract.md) | SCRUM-103 CP0 đã chốt thiết kế contract; endpoint chưa triển khai |

Quy ước response, authentication, lỗi và hành vi đặc biệt được ghi trong contract của module đang sở hữu endpoint. Khi thay đổi request, response, status code hoặc error code, cập nhật contract trong cùng pull request.

Báo cáo tích hợp Topic Advising và danh sách API còn thiếu: [TopicAdvisingIntegration.md](TopicAdvisingIntegration.md).

Báo cáo màn hình sinh viên, API đã nối và các trường dữ liệu còn thiếu: [StudentTopicRegistrationIntegration.md](StudentTopicRegistrationIntegration.md).

Nghiệm thu báo cáo cuối kỳ (SCRUM-102), ma trận test và cách kiểm tra PostgreSQL/MinIO: [FinalReportSubmissionVerification.md](FinalReportSubmissionVerification.md).
