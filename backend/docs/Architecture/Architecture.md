# Kiến trúc backend UGTM

Đối chiếu source ngày 21/10/2026. Nghiệp vụ theo [Requirements.md](../../../docs/Requirements.md), đã chốt từ báo cáo final do nhóm quản lý ngoài repo. Kiến trúc cũ trong Word không dùng làm hiện trạng triển khai.

## 1. Quyết định kiến trúc

UGTM là **Modular Monolith**: một ASP.NET Core .NET 10 host, sáu module, một PostgreSQL database Neon. Mỗi module có Domain/Application/Contracts/Infrastructure và **DbContext runtime riêng**. Domain không phụ thuộc EF, Database.Migrations, Application hoặc Infrastructure; module khác chỉ giao tiếp qua Contracts/ID/DTO.

`backend/Database.Migrations` là project **điều phối migration**, chứa UGTMDbContext/Factory, CrossModuleForeignKeys và lịch sử migration. UGTMDbContext ghép cấu hình của sáu module để tạo model database đầy đủ; không đăng ký trong DI runtime và từ chối SaveChanges. API chỉ đăng ký các context module. Một lệnh cập nhật database áp dụng schema của toàn hệ thống, không chạy sáu bộ migrations riêng.

Đây là ranh giới kỹ thuật đã được kiểm thử, không phải khẳng định mọi nghiệp vụ DDD đã hoàn thành. StudentGroup là aggregate có kiểm soát thành viên; tính điểm là dịch vụ miền; nhiều entity khác còn là trạng thái nền để xây dựng use case. Domain giữ quy tắc nghiệp vụ và độc lập hạ tầng theo [nguyên tắc DDD của Microsoft](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice).

## 2. Sáu module và quyền sở hữu dữ liệu

| Module | Context runtime | Schema | Phạm vi |
| --- | --- | --- | --- |
| Identity | IdentityDbContext | identity | Tài khoản, phiên, activation/recovery, role/permission, email outbox và audit model |
| AcademicPeriods | AcademicPeriodsDbContext | academic | Đợt, 7 giai đoạn/deadline, cấu hình đầu điểm/trọng số/ngưỡng |
| TopicCatalog | TopicCatalogDbContext | topics | GVHD đề xuất, Khoa thẩm định và công bố danh mục |
| ThesisManagement | ThesisManagementDbContext | thesis | Nhóm/đăng ký, đề cương, tiến độ/giữa kỳ, đổi tên, báo cáo, lưu chiểu |
| DefenseCouncil | DefenseCouncilDbContext | councils | Phiếu GVHD/GVPB, điều kiện bảo vệ, Hội đồng/lịch, điểm/biên bản, tổng hợp/công bố |
| Notifications | NotificationsDbContext | notifications | Thông báo in-app/email, nhắc hạn |

MidtermProgress được gom vào ThesisManagement; FinalReportEvaluation và GradingConsolidation vào DefenseCouncil. Chúng là các bước trong cùng vòng đời hồ sơ hoặc chuỗi đánh giá; khung cũ chỉ có ping, chưa có API nghiệp vụ cần di chuyển. Sáu module là lựa chọn theo tính gắn kết nghiệp vụ của UGTM, không phải số lượng bắt buộc của DDD. DefenseCouncil có thể tách khi Assessment/Results có nhu cầu độc lập thực tế.

41 bảng nghiệp vụ nằm trong sáu schema trên; `public.__ef_migrations_history` là lịch sử kỹ thuật. Không có schema shared. Audit, session và auth challenge thuộc Identity; notification nghiệp vụ thuộc Notifications; nhóm sinh viên thuộc thesis. RoleScoreWeight cũ được loại bằng migration vì RoleId không thể phân biệt GVHD/GVPB cùng là Lecturer; cấu hình mới dùng ScoreComponentConfig có version.

## 3. Tổ chức source và phụ thuộc

```text
backend/
  host/Api/                  # Composition root, HTTP, DI
  Modules/<Module>/
    Domain/Entities/          # Model của module; aggregate khi có bất biến
    Domain/Enums/             # Trạng thái/khái niệm thuộc module
    Domain/Repositories/      # Interface repository theo aggregate, khi cần
    Application/              # Use case, DTO adapters và cổng unit of work
    Contracts/                # Hợp đồng công khai, độc lập project/package khác
    Infrastructure/Persistence/
      <Module>DbContext.cs
      Configurations/
      Repositories/
  BuildingBlocks/Infrastructure/Persistence/
                             # Provider/config kết nối, conventions, write guard
  Database.Migrations/       # Context/factory và migrations dùng cho triển khai
```

Domain chỉ tham chiếu BuildingBlocks.Domain; Application tham chiếu Domain, Contracts và BuildingBlocks.Application. Infrastructure tham chiếu nội bộ module và BuildingBlocks.Infrastructure. Module không tham chiếu project Database.Migrations hoặc các layer nội bộ của module khác. Host và project điều phối migration được phép ghép Infrastructure của sáu module; hai project này là composition root, không phải module nghiệp vụ.

Runtime configuration không nhập entity của module khác. Navigation giữa module đã bỏ; giữ scalar ID, như StudentGroup.TopicId và CouncilAssignment.GroupId. Khi xây dựng API, việc xác minh đề tài/giảng viên/đợt phải đi qua Contracts hoặc use case thích hợp, không nạp trực tiếp entity miền của module khác.

Các FK vật lý liên schema vẫn được giữ tại [CrossModuleForeignKeys.cs](../../Database.Migrations/CrossModuleForeignKeys.cs) để bảo đảm dữ liệu trên cùng database. Runtime context chỉ có model nội bộ, còn migration context mô tả các ràng buộc toàn hệ thống. Đây là đánh đổi có chủ ý: có cách ly model/code, chưa có database độc lập để tách deploy từng module.

ModuleDbContext kiểm tra schema của các entity được ghi qua SaveChanges; kiểm thử kiểm tra model chỉ chứa entity của module sở hữu. Đây không phải ranh giới bảo mật cấp database: không dùng raw SQL để ghi schema module khác; nếu cần cách ly mạnh hơn phải bổ sung quyền database. Không gọi Migrate/EnsureCreated trên context runtime.

## 4. Logic miền và repository hiện có

[StudentGroup](../../Modules/ThesisManagement/Domain/Entities/StudentGroup.cs) kiểm soát tối đa hai thành viên, không trùng sinh viên, cùng đợt, chỉ thêm khi Registered và dùng thời gian UTC. Collection chỉ đọc, thay đổi qua AddMember; mapping EF dùng backing field. Unique (StudentId, BatchId) ở database vẫn bảo đảm một sinh viên không vào hai nhóm cùng đợt. Giới hạn chỉ tiêu, đủ điều kiện sinh viên và xử lý tranh chấp cần use case/transaction, chưa tự được bảo đảm bởi aggregate này.

[IStudentGroupRepository](../../Modules/ThesisManagement/Domain/Repositories/IStudentGroupRepository.cs) thuộc Domain; [StudentGroupRepository](../../Modules/ThesisManagement/Infrastructure/Persistence/Repositories/StudentGroupRepository.cs) dùng ThesisManagementDbContext, tải cả thành viên. IThesisUnitOfWork của Application dùng cùng context scoped, cho phép use case ghi nhất quán. Chưa có endpoint đăng ký nghiệp vụ.

[ThesisGradeCalculator](../../Modules/DefenseCouncil/Domain/Grading/ThesisGradeCalculator.cs) tính công thức chia 6/7 đã chốt, loại cả điểm và hệ số, không tự loại vì sai biệt hoặc tự làm tròn. GVPB kiêm ủy viên không nhập điểm Hội đồng riêng, điểm phản biện giữ hệ số 2. [ScoreWeightRules](../../Modules/AcademicPeriods/Domain/Grading/ScoreWeightRules.cs) kiểm tra % tổng 100 và hệ số nguyên không âm có tổng dương. GradeCalculationService/Contracts có DI; chưa có orchestrator lấy phiếu, kiểm tra điểm sàn/lưu chiểu và lưu snapshot kết quả.

AcademicPeriods đã triển khai CQRS/API quản lý batch, lịch bảy giai đoạn, timeline/current stage và cấu hình điểm versioned. Lịch batch được materialize từ `ScheduleTemplateVersion`, giữ timezone IANA `Asia/Ho_Chi_Minh`; cấu hình điểm có vòng đời `Draft → Active → Retired`. Partial unique index và `xmin` xử lý tranh chấp ở PostgreSQL; lỗi đồng thời được trả về `409`. Quyền quản lý batch/cấu hình chỉ cấp mặc định cho Admin và PĐT (`AcademicAffairs`); quyền đọc batch cấp cho các vai trò hệ thống, còn quyền đọc cấu hình điểm chỉ cấp Admin/PĐT. Mọi mutation tạo audit integration event trong `academic.audit_outbox_message` cùng transaction dữ liệu.

Giao dịch xuyên module phải được thiết kế theo use case; nhiều DbContext không tự tạo transaction chung. Ghi nhóm và thành viên dùng một context/transaction. Các bước cần nhất quán xuyên module phải phối hợp rõ ràng; không tự giả định mọi SaveChanges cùng database là nguyên tử. Identity đã có outbox và worker riêng cho email activation/recovery; chưa có event bus hoặc outbox dùng chung giữa các module.

## 5. Trạng thái runtime/frontend

[ModuleRegistration.cs](../../host/Api/Modules/ModuleRegistration.cs) đăng ký sáu IModule. Mỗi module có endpoint ping; Identity cung cấp API authentication/quản trị, TopicCatalog có API đề tài và AcademicPeriods có 15 endpoint batch/cấu hình điểm dưới `/api/academic-periods/batches`. Các model/repository/domain services còn lại không đồng nghĩa toàn bộ FR đã được triển khai.

Frontend React 19/TypeScript/Vite 8 có dashboard `/` và trang ping `/modules/:modulePath`. Router dùng AppLayout với Tailwind/shadcn/Radix; App/ModulePingPage còn dùng Ant Design. AuthContext/ProtectedRoute và các màn hình auth chưa được tích hợp. Backend đã có refresh endpoint nhưng frontend chưa nối theo contract mới.

Backend dùng JWT RSA có session validation, refresh rotation, CSRF, lockout, activation/recovery, rate limit và permission policy động. Chi tiết tại [Authentication.md](../Technical/Authentication.md), contract HTTP tại [IdentityApiContract.md](../../../docs/ApiContract/IdentityApiContract.md). Policy theo phân công, deadline, trạng thái, quyền sửa điểm, khóa/mở biên bản và điều kiện công bố/lưu chiểu vẫn phải được xây tại module nghiệp vụ tương ứng.

CORS đọc `Cors:AllowedOrigins`, fallback origin 5173; Compose cấu hình 5173/3000. Identity gửi email qua SMTP/Mailpit bằng transactional outbox. Redis, RabbitMQ và MinIO chưa có client tích hợp. HTTPS production, backup, SMTP production và SLA cần được cấu hình theo môi trường triển khai.

## 6. Migrations và Neon

Runtime dùng ConnectionStrings:DefaultConnection rồi DATABASE_CONNECTION_STRING. Factory ưu tiên ConnectionStrings:MigrationConnection rồi MIGRATION_CONNECTION_STRING, sau đó các nguồn runtime. [DatabaseOptions](../../BuildingBlocks/Infrastructure/Persistence/DatabaseOptions.cs) dùng Npgsql/snake_case, command timeout 60 giây và history public. Factory không tự đọc .env; [Database.ps1](../../../scripts/Database.ps1) nạp riêng hai biến kết nối từ .env nếu môi trường chưa cung cấp. Không commit credentials.

Từ thư mục gốc, PowerShell:

```powershell
./scripts/Database.ps1 -Action List
./scripts/Database.ps1 -Action Check
./scripts/Database.ps1 -Action Script -SqlOutput artifacts/migrations.sql
./scripts/Database.ps1 -Action Update
```

Sau khi thay model của module, tạo migration tại project điều phối:

```powershell
./scripts/Database.ps1 -Action Add -MigrationName DescribeTheChange
```

Review migration/SQL trước Update. Công cụ EF cục bộ pin 10.0.12; chỉ dùng context UGTMDbContext cho CLI migrations:

```powershell
dotnet tool restore
dotnet ef database update --project backend/Database.Migrations/UGTM.Database.Migrations.csproj --startup-project backend/host/Api/UGTM.Api.csproj --context UGTMDbContext
```

Visual Studio Package Manager Console, sau khi cung cấp kết nối môi trường:

```powershell
Update-Database -Project UGTM.Database.Migrations -StartupProject UGTM.Api -Context UGTMDbContext
```

Giữ các migration đã áp dụng và snapshot. Database đã chạy phiên bản dùng history shared cần chuyển một lần bằng [MoveMigrationHistory.sql](../../../scripts/MoveMigrationHistory.sql) trước khi chạy code mới; Neon hiện tại đã chuyển. Database mới không cần bước đó. Script báo lỗi nếu history đồng thời tồn tại ở shared và public.

Không chạy DatabaseScript.txt như SQL: đó là DBML đồng bộ từ EF, dùng xem cấu trúc. Chi tiết bảng/ràng buộc/khoảng thiếu tại [DataModel.md](../../../docs/DataModel.md).

## 7. Khác biệt với Word và phần còn cần triển khai

| Nội dung | Word | Source hiện tại |
| --- | --- | --- |
| Provider | SQL Server | PostgreSQL/Npgsql/Neon |
| Context | Module sở hữu riêng | Sáu context runtime; một context chỉ điều phối migrations |
| FK liên module | Mô tả không có FK vật lý | Có FK tại model điều phối migration; Domain/runtime không có navigation liên module |
| UI | Ant Design | Layout Tailwind/shadcn; App/ping dùng Ant Design |
| DDD | Mô tả nghiệp vụ đầy đủ | Ranh giới module/model được kiểm thử; aggregate nhóm và calculator có quy tắc; chưa đầy đủ use case |
| Authentication | Identity + JWT | Đã có JWT/session, activation/recovery, CSRF, permission policy và rate limit; frontend chưa tích hợp |

SubmissionFile.EntityId và EvaluationDetail.EvaluationId vẫn là liên kết đa hình chưa có FK tới hồ sơ/phiếu. Quyết định loại điểm cần người/thời điểm/bằng chứng/audit; mở khóa cần chốt phạm vi/thời hạn/khóa lại; điểm khi có phản biện thứ hai chưa có quy tắc chọn đầy đủ. Không tự suy diễn các nội dung này. Metadata lưu chiểu, snapshot cấu hình, trọng số động đã có model nhưng cần orchestrator/policy.

## 8. Sơ đồ

[C1](PlantUML/C1_System_Context.puml) · [C2](PlantUML/C2_Containers.puml) · [C3 tổng quan](PlantUML/C3_Components_HighLevel.puml) · [C3 module](PlantUML/C3_Components_ModuleDeepDive.puml) · [Database](PlantUML/MultiSchema_Database.puml) · [Xác thực](PlantUML/Security_Authentication_Flow.puml). Sơ đồ ghi rõ hiện trạng hoặc thiết kế nghiệp vụ chưa triển khai. Use Case diagram đã bỏ; giữ [12 đặc tả](../../../docs/UseCases.md).
