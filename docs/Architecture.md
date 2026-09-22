# KIẾN TRÚC HỆ THỐNG UGTM (UNDERGRADUATE GRADUATION THESIS MANAGEMENT)
## Tham chiếu mô hình: Modular Monolith with DDD (.NET 10)

> Tài liệu này mô tả chi tiết thiết kế kiến trúc của hệ thống **UGTM** dựa trên phương pháp luận **Modular Monolith kết hợp Domain-Driven Design (DDD)** và chuẩn hóa biểu đồ theo **Mô hình C4 (Context, Container, Component, Code)** cùng các biểu đồ tương tác CQRS, Event-Driven Integration, Database Schemas và Security Authentication Flow.

---

## MỤC LỤC

1. [Tổng quan Kiến trúc Cốt lõi](#1-tổng-quan-kiến-trúc-cốt-lõi)
2. [Mô hình Khái niệm Miền (Domain Conceptual Model)](#2-mô-hình-khái-niệm-miền-domain-conceptual-model)
3. [Mô hình C4 (C4 Architecture Model)](#3-mô-hình-c4-c4-architecture-model)
   - [3.1 C1 — System Context Diagram](#31-c1--system-context-diagram)
   - [3.2 C2 — Container Diagram](#32-c2--container-diagram)
   - [3.3 C3 — Component Diagram (High-Level View)](#33-c3--component-diagram-high-level-view)
   - [3.4 C3 — Component Diagram (Module Deep-Dive: Clean Architecture 4 Layer)](#34-c3--component-diagram-module-deep-dive-clean-architecture-4-layer)
   - [3.5 C4 — Code Diagram (Thesis Aggregate Model)](#35-c4--code-diagram-thesis-aggregate-model)
4. [Mô hình Xử lý Yêu cầu qua CQRS](#4-mô-hình-xử-lý-yêu-cầu-qua-cqrs)
   - [4.1 Phân tách Write Model & Read Model (CQRS Architecture)](#41-phân-tách-write-model--read-model-cqrs-architecture)
   - [4.2 Luồng xử lý Command & Query (Sequence Flow)](#42-luồng-xử-lý-command--query-sequence-flow)
   - [4.3 Cross-Cutting Concerns: Pipeline Behaviors / Decorator Pattern](#43-cross-cutting-concerns-pipeline-behaviors--decorator-pattern)
5. [Tích hợp liên Module (Inter-Module Integration via Outbox & Event Bus)](#5-tích-hợp-liên-module-inter-module-integration-via-outbox--event-bus)
6. [Bảo mật & Phân quyền (Security & Authentication Flow)](#6-bảo-mật--phân-quyền-security--authentication-flow)
7. [Kiến trúc Dữ liệu & Phân tách Schema (Multi-Schema Database Architecture)](#7-kiến-trúc-dữ-liệu--phân-tách-schema-multi-schema-database-architecture)
8. [Biểu đồ Luồng Nghiệp vụ 7 Giai đoạn & 2 Cổng Kiểm soát (Gatekeeper Workflow)](#8-biểu-đồ-luồng-nghiệp-vụ-7-giai-đoạn--2-cổng-kiểm-soát-gatekeeper-workflow)

---

> 📂 **Thư mục Mã nguồn PlantUML (`docs/PlantUML/`)**:
> - [Conceptual_Model.puml](PlantUML/Conceptual_Model.puml)
> - [C1_System_Context.puml](PlantUML/C1_System_Context.puml)
> - [C2_Containers.puml](PlantUML/C2_Containers.puml)
> - [C3_Components_HighLevel.puml](PlantUML/C3_Components_HighLevel.puml)
> - [C3_Components_ModuleDeepDive.puml](PlantUML/C3_Components_ModuleDeepDive.puml)
> - [C4_Code_Aggregate.puml](PlantUML/C4_Code_Aggregate.puml)
> - [CQRS_Architecture.puml](PlantUML/CQRS_Architecture.puml)
> - [CQRS_Sequence.puml](PlantUML/CQRS_Sequence.puml)
> - [Pipeline_Decorators.puml](PlantUML/Pipeline_Decorators.puml)
> - [EventBus_Outbox_Integration.puml](PlantUML/EventBus_Outbox_Integration.puml)
> - [Security_Authentication_Flow.puml](PlantUML/Security_Authentication_Flow.puml)
> - [MultiSchema_Database.puml](PlantUML/MultiSchema_Database.puml)
> - [Gatekeeper_Workflow.puml](PlantUML/Gatekeeper_Workflow.puml)

---

## 1. Tổng quan Kiến trúc Cốt lõi

Hệ thống UGTM áp dụng kiến trúc **Modular Monolith**:
- **Tính đóng gói (Encapsulation)**: Mỗi module nghiệp vụ độc lập đại diện cho một **Bounded Context**, sở hữu riêng dữ liệu, logic ứng dụng và quy tắc miền.
- **Ranh giới nghiêm ngặt (Strict Boundaries)**: Các module **tuyệt đối không** tham chiếu trực tiếp tầng `Domain`, `Application` hay `Infrastructure` của nhau. Mọi giao tiếp đồng bộ hoặc bất đồng bộ chỉ thông qua tầng `Contracts` (DTOs, Integration Events). Ranh giới này được kiểm soát tự động bởi bộ kiểm thử `backend/tests/Architecture.Tests`.
- **Tách bạch Read/Write (CQRS)**: Tách riêng luồng ghi (`Commands` xử lý qua Domain Model và Transaction) và luồng đọc (`Queries` trả về DTOs tối ưu hiệu năng).
- **Tính tự chủ dữ liệu (Logical Data Isolation)**: Tất cả module dùng chung một Database SQL Server nhưng phân tách thành các schema độc lập (`identity`, `academic`, `topics`, `thesis`, `councils`,...), không dùng Foreign Key vật lý liên schema.

---

## 2. Mô hình Khái niệm Miền (Domain Conceptual Model)

> 📄 **Mã nguồn PlantUML:** [Conceptual_Model.puml](PlantUML/Conceptual_Model.puml)

Mô hình khái niệm mô tả các khái niệm nghiệp vụ cốt lõi và mối quan hệ giữa chúng qua các Bounded Contexts trong hệ thống UGTM:

```mermaid
flowchart TB
    subgraph Identity["Identity (User Access)"]
        User["User"] -->|has| UserRole["User Role"]
        UserRole -->|has| Permission["Permission"]
    end

    subgraph Academic["Academic Periods"]
        Period["Academic Period"] -->|has| PeriodStatus["Period Status<br/>Draft, Active, Completed"]
    end

    subgraph Topics["Topic Catalog"]
        Proposal["Topic Proposal"] -->|for| Period
        Proposal -->|has| ProposalStatus["Proposal Status<br/>Draft, Submitted, Approved, Rejected"]
    end

    subgraph Thesis["Thesis Management"]
        Reg["Thesis Registration"] -->|registers| Proposal
        Reg -->|consists of| Member["Student Member (1..3)"]
        Reg -->|contains| Outline["Thesis Outline (0..1)"]
        Outline -->|has| OutlineStatus["Outline Status<br/>PendingReview, Approved, NeedRevision"]
    end

    subgraph Progress["Midterm Progress"]
        MidReport["Midterm Report"] -->|for| Reg
        MidReport -->|evaluated by GVHD| MidDecision["Gatekeeper 2 Decision<br/>AllowedToContinue, Stopped"]
        TitleChange["Topic Change Request"] -->|modifies title for| Reg
    end

    subgraph Eval["Final Report & Evaluation"]
        Final["Final Thesis Report"] -->|for| Reg
        SupEval["Supervisor Assessment"] -->|evaluates| Final
        RevEval["Reviewer Assessment"] -->|reviews| Final
        RevEval -->|concludes| RevDecision["Reviewer Conclusion<br/>AllowedForDefense, NotAllowed"]
    end

    subgraph Council["Defense Council"]
        CouncilGroup["Defense Council"] -->|schedules| Session["Council Session"]
        Session -->|evaluated by 3..5 members| MemberEval["Council Member Evaluation"]
        Session -->|defense of| Final
    end

    subgraph Grading["Grading Consolidation"]
        Result["Consolidated Result"] -->|for| Reg
        Result -->|calculated using| Strategy["Grading Strategy Pattern"]
        Result -->|includes score| SupEval
        Result -->|includes score| RevEval
        Result -->|includes score| MemberEval
    end

    style Identity fill:#f3e8f8,stroke:#ab47bc
    style Academic fill:#ffeddb,stroke:#ffa726
    style Topics fill:#e8f5e9,stroke:#66bb6a
    style Thesis fill:#e1f5fe,stroke:#42a5f5
    style Progress fill:#fbe9e7,stroke:#ff7043
    style Eval fill:#ede7f6,stroke:#7e57c2
    style Council fill:#fffde7,stroke:#ffee58
    style Grading fill:#e0f2f1,stroke:#26a69a
```

---

## 3. Mô hình C4 (C4 Architecture Model)

### 3.1 C1 — System Context Diagram

> 📄 **Mã nguồn PlantUML:** [C1_System_Context.puml](PlantUML/C1_System_Context.puml)

Biểu đồ ngữ cảnh hệ thống mô tả vị trí của UGTM trong môi trường trường đại học, tương tác với 7 nhóm tác nhân (Actors) và các hệ thống bên ngoài:

```mermaid
flowchart TB
    title["<b>C1 — System Context Diagram: Hệ thống Quản lý KLTN (UGTM)</b>"]
    style title fill:transparent,stroke:none,font-size:16px

    subgraph Users["Các Tác Nhân Người Dùng (Actors)"]
        SV["👤 <b>Sinh viên</b><br/>Tra cứu, đăng ký đề tài, nộp đề cương,<br/>báo cáo tiến độ, xin ĐTĐT, nộp lưu chiểu, xem điểm"]
        GVHD["👨‍🏫 <b>Giảng viên hướng dẫn (GVHD)</b><br/>Đề xuất đề tài, duyệt nhóm SV, đánh giá giữa kỳ<br/>(Cổng dừng/tiếp tục), duyệt ĐTĐT, chấm điểm cuối kỳ"]
        GVPB["🔍 <b>Giảng viên phản biện (GVPB)</b><br/>Tiếp nhận phân công, đánh giá báo cáo cuối kỳ,<br/>kết luận điều kiện ra bảo vệ, chấm điểm phản biện"]
        KHOA["🏢 <b>Văn phòng Khoa</b><br/>Thẩm định đề tài, duyệt đề cương, điều phối quy trình,<br/>phân công GVPB, lập hội đồng, xác nhận lưu chiểu, công bố điểm"]
        PDT["🏛️ <b>Phòng Đào Tạo</b><br/>Phê duyệt đơn ĐTĐT, ban hành quyết định<br/>thành lập Hội đồng, xuất báo cáo toàn trường xét tốt nghiệp"]
        HD["⚖️ <b>Hội đồng bảo vệ</b><br/>Xem hồ sơ, chấm điểm thành phần trực tiếp<br/>trong buổi bảo vệ, lập và khóa biên bản điểm"]
        ADMIN["⚙️ <b>Quản trị viên (Admin)</b><br/>Quản lý tài khoản, phân quyền RBAC,<br/>cấu hình đợt 21 tuần, tra cứu Audit Log"]
    end

    subgraph SystemBoundary["Ranh Giới Hệ Thống"]
        UGTM["🎓 <b>Hệ thống Quản lý KLTN (UGTM)</b><br/><i>[Phần mềm Modular Monolith .NET 10 & React SPA]</i><br/>Hỗ trợ số hóa toàn diện quy trình KLTN 21 tuần<br/>theo chuẩn đào tạo UIT"]
    end

    subgraph ExternalSystems["Hệ Thống Ngoại Vi"]
        EMAIL["📧 <b>Hệ thống Email / SMTP</b><br/>Gửi email thông báo, nhắc hạn nộp báo cáo<br/>và cảnh báo trễ hạn"]
        STORAGE["📁 <b>File Storage Service</b><br/>Lưu trữ tệp tin bản mềm (PDF, Docx &lt;= 20MB):<br/>đề cương, báo cáo tiến độ, quyết định"]
    end

    SV -->|HTTPS / Web Browser| UGTM
    GVHD -->|HTTPS / Web Browser| UGTM
    GVPB -->|HTTPS / Web Browser| UGTM
    KHOA -->|HTTPS / Web Browser| UGTM
    PDT -->|HTTPS / Web Browser| UGTM
    HD -->|HTTPS / Web Browser| UGTM
    ADMIN -->|HTTPS / Web Browser| UGTM

    UGTM -->|SMTP / Background Task| EMAIL
    UGTM -->|Lưu và tải tệp tin bản mềm| STORAGE

    style UGTM fill:#1168bd,stroke:#0b4884,color:#ffffff,stroke-width:2px
    style SV fill:#08427b,stroke:#052e56,color:#ffffff
    style GVHD fill:#08427b,stroke:#052e56,color:#ffffff
    style GVPB fill:#08427b,stroke:#052e56,color:#ffffff
    style KHOA fill:#08427b,stroke:#052e56,color:#ffffff
    style PDT fill:#08427b,stroke:#052e56,color:#ffffff
    style HD fill:#08427b,stroke:#052e56,color:#ffffff
    style ADMIN fill:#08427b,stroke:#052e56,color:#ffffff
    style EMAIL fill:#7f8c8d,stroke:#576574,color:#ffffff
    style STORAGE fill:#7f8c8d,stroke:#576574,color:#ffffff
    style Users fill:#f5f7fa,stroke:#d0d7de,stroke-width:1px
    style SystemBoundary fill:#eef6fc,stroke:#85bbf0,stroke-width:2px
    style ExternalSystems fill:#f9f9f9,stroke:#cccccc,stroke-width:1px
```

---

### 3.2 C2 — Container Diagram

> 📄 **Mã nguồn PlantUML:** [C2_Containers.puml](PlantUML/C2_Containers.puml)

Biểu đồ Container mô tả các khối runtime triển khai:

```mermaid
flowchart TB
    title["<b>C2 — Container Diagram: Các thành phần triển khai của UGTM</b>"]
    style title fill:transparent,stroke:none,font-size:16px

    USER["👤 <b>Người dùng hệ thống</b><br/>Sinh viên, Giảng viên, Khoa, PĐT, Admin"]

    subgraph FE["Frontend"]
        SPA["💻 <b>Single Page Application (SPA)</b><br/><i>[React 19, TypeScript, Vite, Ant Design]</i><br/>Cung cấp giao diện responsive, tra cứu đề tài,<br/>nộp tài liệu, theo dõi tiến độ, nhập điểm"]
    end

    subgraph BackendMonolith["UGTM Backend Monolith (.NET 10 Runtime)"]
        API["🚀 <b>Host API (UGTM.Api)</b><br/><i>[ASP.NET Core Web API]</i><br/>Composition root: Routing, JWT Auth, CORS,<br/>chuyển giao Commands/Queries tới các module"]
        BUS["⚡ <b>In-Memory Event Bus</b><br/><i>[MediatR / Channel / Outbox]</i><br/>Vận chuyển Integration Events bất đồng bộ<br/>giữa các module nội bộ phi ghép nối"]
        BG["⏱️ <b>Background Worker Service</b><br/><i>[IHostedService / Quartz.NET]</i><br/>Quét bảng Outbox định kỳ, gửi Email nhắc hạn,<br/>kiểm tra quá hạn deadline"]
    end

    subgraph DataStorage["Lưu trữ Dữ liệu"]
        SQL[("🗄️ <b>Database</b><br/><i>[Microsoft SQL Server]</i><br/>9 Schemas riêng biệt: identity, academic,<br/>topics, thesis, progress, evaluation,<br/>councils, grading, notifications")]
        FILES[("📁 <b>Tài liệu lưu trữ (File Storage)</b><br/><i>[Local Disk / Volume Mount]</i><br/>Lưu trữ tệp đính kèm bản mềm:<br/>đề cương, báo cáo giữa/cuối kỳ, đơn ĐTĐT (&lt;= 20MB)")]
    end

    USER -->|HTTPS / Trình duyệt| SPA
    SPA -->|HTTPS / REST API - JWT Bearer Token| API
    API -->|Publish / Subscribe Integration Events| BUS
    BG -->|Quét định kỳ và Dispatch| BUS
    API -->|EF Core / Dapper / TCP 1433| SQL
    BG -->|Đọc Outbox / Cập nhật trạng thái| SQL
    API -->|Lưu và đọc file Stream I/O| FILES

    style USER fill:#08427b,stroke:#052e56,color:#ffffff
    style SPA fill:#438dd5,stroke:#2e6295,color:#ffffff,stroke-width:2px
    style API fill:#1168bd,stroke:#0b4884,color:#ffffff,stroke-width:2px
    style BUS fill:#2b8a3e,stroke:#1b5e20,color:#ffffff,stroke-width:2px
    style BG fill:#e67e22,stroke:#d35400,color:#ffffff,stroke-width:2px
    style SQL fill:#2c3e50,stroke:#1a252f,color:#ffffff,stroke-width:2px
    style FILES fill:#7f8c8d,stroke:#576574,color:#ffffff,stroke-width:2px
    style BackendMonolith fill:#eef6fc,stroke:#85bbf0,stroke-width:2px
    style DataStorage fill:#fcf8e3,stroke:#faebcc,stroke-width:1px
    style FE fill:#f5f7fa,stroke:#d0d7de,stroke-width:1px
```

---

### 3.3 C3 — Component Diagram (High-Level View)

> 📄 **Mã nguồn PlantUML:** [C3_Components_HighLevel.puml](PlantUML/C3_Components_HighLevel.puml)

Biểu đồ thành phần cấp cao thể hiện 9 module nghiệp vụ bên trong khối Backend Modular Monolith và mối liên kết với `BuildingBlocks`:

```mermaid
flowchart TB
    subgraph Host["Host API: UGTM.Api (Composition Root)"]
        Program["Program.cs\n(Route Mapping, DI, CORS, JWT Auth)"]
    end

    subgraph BuildingBlocks["BuildingBlocks (Dùng chung)"]
        BBDomain["BuildingBlocks.Domain\n(Entity, AggregateRoot, Result, Error, IDomainEvent)"]
        BBApp["BuildingBlocks.Application\n(IModule, ICommand, IQuery, IExecutionContextAccessor)"]
        BBInfra["BuildingBlocks.Infrastructure\n(EventBus, OutboxProcessor, DomainEventsDispatcher)"]
    end

    subgraph Modules["9 Business Modules (Độc lập về mặt nghiệp vụ)"]
        M1["1. Identity Module\n(Tài khoản, Xác thực JWT, RBAC)"]
        M2["2. AcademicPeriods Module\n(Cấu hình 21 tuần, Mốc Deadline)"]
        M3["3. TopicCatalog Module\n(Đề xuất, Thẩm định & Công bố Đề tài)"]
        M4["4. ThesisManagement Module\n(Đăng ký Đề tài, Ghép nhóm, Duyệt Đề cương)"]
        M5["5. MidtermProgress Module\n(Báo cáo giữa kỳ, Cổng dừng/tiếp tục, Đơn ĐTĐT 3 bên)"]
        M6["6. FinalReportEvaluation Module\n(Nộp Báo cáo cuối kỳ, Chấm GVHD, Phân công & Chấm GVPB)"]
        M7["7. DefenseCouncil Module\n(Lập Hội đồng, Xếp lịch, Quyết định PĐT, Chấm điểm & Khóa biên bản)"]
        M8["8. GradingConsolidation Module\n(Tổng hợp điểm 3 nguồn, Cấu hình trọng số, Công bố & Xuất báo cáo)"]
        M9["9. Notifications Module\n(Hệ thống thông báo, Email nhắc hạn)"]
    end

    subgraph Storage["Cơ sở dữ liệu (Microsoft SQL Server)"]
        DB1[("schema: identity")]
        DB2[("schema: academic_periods")]
        DB3[("schema: topic_catalog")]
        DB4[("schema: thesis_management")]
        DB5[("schema: midterm_progress")]
        DB6[("schema: final_report")]
        DB7[("schema: defense_councils")]
        DB8[("schema: grading_consolidation")]
        DB9[("schema: notifications")]
    end

    Program -->|Khởi tạo qua IModule| Modules
    Modules -.->|Kế thừa và Triển khai| BuildingBlocks
    
    M1 --- DB1
    M2 --- DB2
    M3 --- DB3
    M4 --- DB4
    M5 --- DB5
    M6 --- DB6
    M7 --- DB7
    M8 --- DB8
    M9 --- DB9

    style Host fill:#e1f5fe,stroke:#0288d1,stroke-width:2px
    style BuildingBlocks fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    style Modules fill:#e8f5e9,stroke:#388e3c,stroke-width:2px
    style Storage fill:#fff3e0,stroke:#f57c00,stroke-width:2px
```

---

### 3.4 C3 — Component Diagram (Module Deep-Dive: Clean Architecture 4 Layer)

> 📄 **Mã nguồn PlantUML:** [C3_Components_ModuleDeepDive.puml](PlantUML/C3_Components_ModuleDeepDive.puml)

Mỗi module trong hệ sinh thái UGTM được chuẩn hóa thành 4 layer riêng biệt:

```mermaid
flowchart TD
    subgraph OtherModules["Các Module khác"]
        OtherCaller["Module khác"]
    end

    subgraph SingleModule["Cấu trúc bên trong 1 Module (Ví dụ: ThesisManagement)"]
        
        subgraph ContractsLayer["Layer 1: Contracts (Công khai cho toàn Solution)"]
            IModuleAPI["IThesisManagementModule\n(ExecuteCommand, ExecuteQuery)"]
            IntegrationEvents["Integration Events\n(ThesisRegisteredIntegrationEvent,...)"]
            ContractDTOs["Public DTOs / Requests"]
        end

        subgraph ApplicationLayer["Layer 2: Application (Requests Processing & Use Cases)"]
            CommandHandlers["Command Handlers\n(RegisterThesisCommandHandler,...)"]
            QueryHandlers["Query Handlers\n(GetThesisDetailsQueryHandler,...)"]
            DomainEventHandlers["Domain Event Handlers\n(ThesisSubmittedDomainEventHandler,...)"]
            AppValidators["FluentValidators / Behaviors"]
        end

        subgraph DomainLayer["Layer 3: Domain (Nghiệp vụ cốt lõi - Bounded Context)"]
            AggregateRoots["Aggregate Root\n(Thesis, Outline)"]
            Entities["Entities\n(StudentMember, Revision)"]
            ValueObjects["Value Objects\n(ThesisStatus, AcademicPeriodId)"]
            DomainEvents["Domain Events\n(ThesisCreatedDomainEvent,...)"]
            DomainRules["Business Rules / Invariants\n(BR-01, BR-02,...)"]
            RepositoryInterfaces["IRepository Interfaces\n(IThesisRepository)"]
        end

        subgraph InfrastructureLayer["Layer 4: Infrastructure (Truy cập dữ liệu & Kỹ thuật)"]
            ModuleClass["ThesisManagementModule : IModule\n(RegisterServices, MapEndpoints)"]
            DbContext["ThesisManagementDbContext\n(Chỉ map schema thesis_management)"]
            RepositoryImpl["ThesisRepository : IThesisRepository"]
            OutboxTable["OutboxMessages Table & InboxMessages"]
            FileStorageAdapter["LocalFileStorageService"]
        end
    end

    subgraph HostAPI["Host API (UGTM.Api)"]
        Endpoints["Minimal API Endpoints\nMapGet, MapPost"]
    end

    Endpoints -->|Gửi Command / Query| IModuleAPI
    OtherCaller -.->|Chỉ được phép tham chiếu| ContractsLayer

    IModuleAPI --> CommandHandlers
    IModuleAPI --> QueryHandlers

    CommandHandlers --> DomainLayer
    CommandHandlers --> RepositoryInterfaces
    CommandHandlers --> OutboxTable

    RepositoryInterfaces -.->|Thực thi bởi| RepositoryImpl
    RepositoryImpl --> DbContext
    DbContext --> OutboxTable

    ModuleClass --> Endpoints

    style ContractsLayer fill:#e8eaf6,stroke:#3f51b5,stroke-width:2px
    style ApplicationLayer fill:#e0f2f1,stroke:#00796b,stroke-width:2px
    style DomainLayer fill:#fffde7,stroke:#fbc02d,stroke-width:2px
    style InfrastructureLayer fill:#fbe9e7,stroke:#d84315,stroke-width:2px
```

---

### 3.5 C4 — Code Diagram (Thesis Aggregate Model)

> 📄 **Mã nguồn PlantUML:** [C4_Code_Aggregate.puml](PlantUML/C4_Code_Aggregate.puml)

Biểu đồ lớp mã nguồn mô tả mô hình Thực thể Miền (Domain Aggregate) đại diện cho đề tài khóa luận trong phân hệ đăng ký & đề cương:

```mermaid
flowchart TB
    subgraph AggregateRoot["AggregateRoot &lt;&lt;abstract&gt;&gt;"]
        AR_Content["+ Guid Id<br/>+ List~IDomainEvent~ DomainEvents<br/># AddDomainEvent(domainEvent)<br/>+ ClearDomainEvents()"]
    end

    subgraph Entity["Entity &lt;&lt;abstract&gt;&gt;"]
        E_Content["+ Guid Id<br/>+ bool Equals(obj)<br/>+ int GetHashCode()"]
    end

    subgraph Thesis["Thesis (Aggregate Root)"]
        T_Content["- Guid _topicId<br/>- Guid _academicPeriodId<br/>- Guid _supervisorId<br/>- List~StudentMember~ _members<br/>- ThesisOutline _outline<br/>- ThesisStatus _status<br/>------------------------------------------------<br/>+ AddMember(studentId, fullName): Result<br/>+ SubmitOutline(fileUrl, fileName, fileSize): Result<br/>+ ReviewOutline(isApproved, remarks, reviewerId): Result<br/>+ ChangeStatus(newStatus): Result"]
    end

    subgraph StudentMember["StudentMember (Entity)"]
        SM_Content["+ Guid StudentId<br/>+ string FullName<br/>+ bool IsLeader<br/>+ DateTime JoinedAt"]
    end

    subgraph ThesisOutline["ThesisOutline (Value Object)"]
        TO_Content["+ string FileUrl<br/>+ string FileName<br/>+ int Version<br/>+ DateTime SubmittedAt<br/>+ string ReviewStatus<br/>+ string Remarks"]
    end

    subgraph ThesisStatus["ThesisStatus (Enum)"]
        TS_Content["• Registered<br/>• OutlinePendingReview<br/>• OutlineNeedRevision<br/>• OutlineApproved<br/>• InProgress<br/>• StoppedAtMidterm<br/>• ReadyForDefense<br/>• Completed"]
    end

    AggregateRoot -->|kế thừa| Thesis
    Entity -->|kế thừa| StudentMember
    Thesis -->|sở hữu 1..3 thành viên| StudentMember
    Thesis -->|chứa 0..1 đề cương| ThesisOutline
    Thesis -->|trạng thái| ThesisStatus

    style AggregateRoot fill:#f3e5f5,stroke:#7b1fa2,stroke-width:1px
    style Entity fill:#f3e5f5,stroke:#7b1fa2,stroke-width:1px
    style Thesis fill:#e1f5fe,stroke:#0288d1,stroke-width:2px
    style StudentMember fill:#e8f5e9,stroke:#388e3c,stroke-width:1px
    style ThesisOutline fill:#fff9c4,stroke:#fbc02d,stroke-width:1px
    style ThesisStatus fill:#fff3e0,stroke:#f57c00,stroke-width:1px
```

---

## 4. Mô hình Xử lý Yêu cầu qua CQRS

### 4.1 Phân tách Write Model & Read Model (CQRS Architecture)

> 📄 **Mã nguồn PlantUML:** [CQRS_Architecture.puml](PlantUML/CQRS_Architecture.puml)

Tách biệt rạch ròi giữa tác vụ ghi dữ liệu (Write Model tuân thủ Domain Model, Outbox, Transaction) và tác vụ đọc dữ liệu (Read Model dùng Direct SQL / Dapper tối ưu hóa tốc độ):

```mermaid
flowchart LR
    Client["Client / User"] --> API["Host API Endpoint"]
    API --> IModule["IModule Interface"]

    subgraph WriteSide["Write Model (Commands)"]
        Cmd["Command"] --> Pipeline["Pipeline Behaviors"]
        Pipeline --> CmdHandler["Command Handler"]
        CmdHandler --> Domain["Domain Model (Aggregates)"]
        Domain --> Repo["Repository (EF Core)"]
        Domain -.-> Outbox["Outbox Table"]
    end

    subgraph ReadSide["Read Model (Queries)"]
        Query["Query"] --> QueryHandler["Query Handler"]
        QueryHandler --> SQL["Direct SQL / Dapper"]
        SQL --> DTO["DTO Result"]
    end

    IModule -->|Write| Cmd
    IModule -->|Read| Query

    Repo --> DB[("SQL Server (Module Schema)")]
    Outbox --> DB
    SQL -->|No-Tracking SELECT| DB

    DTO --> Client

    style WriteSide fill:#e8f5e9,stroke:#2e7d32
    style ReadSide fill:#e1f5fe,stroke:#0277bd
```

---

### 4.2 Luồng xử lý Command & Query (Sequence Flow)

> 📄 **Mã nguồn PlantUML:** [CQRS_Sequence.puml](PlantUML/CQRS_Sequence.puml)

```mermaid
sequenceDiagram
    autonumber
    actor Client as Client / SPA
    participant Host as Host API (Minimal API)
    participant Module as IModule Interface
    participant Pipeline as MediatR Pipeline (Logging / Validation)
    participant Handler as Command Handler
    participant Domain as Domain Aggregate (Thesis)
    participant Repo as IRepository
    participant Outbox as Outbox Table
    participant DB as SQL Server (Module Schema)

    Note over Client, DB: LUỒNG GHI (WRITE COMMAND FLOW)
    Client->>Host: POST /api/theses/{id}/submit-outline (FormData + Bearer Token)
    Host->>Module: ExecuteCommandAsync(SubmitOutlineCommand)
    Module->>Pipeline: MediatR.Send(command)
    Pipeline->>Pipeline: Validate DTO (FluentValidation)
    Pipeline->>Handler: Handle(SubmitOutlineCommand)
    Handler->>Repo: GetByIdAsync(id)
    Repo->>DB: Query Aggregate Data
    DB-->>Repo: Raw Table Rows
    Repo-->>Handler: Thesis (Aggregate Root)
    
    Handler->>Domain: thesis.SubmitOutline(fileUrl, fileName, fileSize)
    Domain->>Domain: Kiểm tra Rule: Deadline nộp bài còn hiệu lực?
    Domain->>Domain: Cập nhật trạng thái sang OutlinePendingReview
    Domain->>Domain: AddDomainEvent(OutlineSubmittedDomainEvent)

    Handler->>Outbox: Add Integration Event (nếu có thông báo liên module)
    Handler->>Repo: UnitOfWork.CommitAsync()
    Repo->>DB: BEGIN TRANSACTION -> UPDATE Tables -> INSERT Outbox -> COMMIT
    DB-->>Handler: Transaction Success
    Handler-->>Module: Result.Success()
    Module-->>Host: 200 OK
    Host-->>Client: 200 OK (JSON Response)

    Note over Client, DB: LUỒNG ĐỌC (READ QUERY FLOW)
    Client->>Host: GET /api/theses/{id}
    Host->>Module: ExecuteQueryAsync(GetThesisDetailsQuery)
    Module->>Handler: Handle(GetThesisDetailsQuery)
    Handler->>DB: Direct Projection Query (SELECT ... FROM thesis_management.Theses)
    DB-->>Handler: DTO Result
    Handler-->>Host: ThesisDetailsDto
    Host-->>Client: 200 OK (JSON Data)
```

---

### 4.3 Cross-Cutting Concerns: Pipeline Behaviors / Decorator Pattern

> 📄 **Mã nguồn PlantUML:** [Pipeline_Decorators.puml](PlantUML/Pipeline_Decorators.puml)

Mọi Command trước khi đến được Command Handler đều đi qua chuỗi Pipeline Decorators để đảm bảo tính toàn vẹn và ghi vết:

```mermaid
sequenceDiagram
    autonumber
    actor Caller as API Caller
    participant Logging as LoggingDecorator
    participant Validation as ValidationDecorator
    participant UoW as UnitOfWorkDecorator
    participant Handler as ActualCommandHandler
    participant DB as SQL Server

    Caller->>Logging: Handle(Command)
    Logging->>Logging: Ghi log bắt đầu Command (Payload, UserId)
    Logging->>Validation: Handle(Command)
    Validation->>Validation: Kiểm tra FluentValidation Rules
    Validation->>UoW: Handle(Command)
    UoW->>DB: BEGIN TRANSACTION
    UoW->>Handler: Handle(Command)
    Handler->>Handler: Xử lý nghiệp vụ cốt lõi
    Handler-->>UoW: Result.Success()
    UoW->>DB: COMMIT TRANSACTION
    UoW-->>Validation: Hoàn tất Transaction
    Validation-->>Logging: Hoàn tất Validation
    Logging->>Logging: Ghi log hoàn thành (ExecutionTimeMs)
    Logging-->>Caller: 200 OK / Result
```

---

## 5. Tích hợp liên Module (Inter-Module Integration via Outbox & Event Bus)

> 📄 **Mã nguồn PlantUML:** [EventBus_Outbox_Integration.puml](PlantUML/EventBus_Outbox_Integration.puml)

Để các module hoàn toàn phân rã và không phụ thuộc chéo vào nhau, khi có sự kiện nghiệp vụ quan trọng, hệ thống sử dụng **Transactional Outbox Pattern** kết hợp với **In-Memory Event Bus**:

```mermaid
flowchart LR
    subgraph ModuleA["Module A (Nguồn sự kiện - Ví dụ: MidtermProgress)"]
        CmdA["Command: ApproveStopThesisCommand"] --> AggA["Aggregate Root: Cập nhật dừng KLTN"]
        AggA --> EvtA["Domain Event: ThesisStoppedDomainEvent"]
        EvtA --> OutboxA[("Outbox Table\n(midterm_progress.OutboxMessages)")]
    end

    subgraph Infra["Hạ tầng Event Bus & Dispatcher"]
        Job["Quartz Job / Background Worker"] -->|Quét định kỳ mỗi 2s| OutboxA
        Job -->|Đánh dấu Đã xử lý và Publish| Bus["In-Memory Event Bus\n(MediatR / Channels)"]
    end

    subgraph ModuleB["Module B (Đích sự kiện - Ví dụ: Notifications)"]
        Bus --> HandlerB["Integration Event Handler\n(ThesisStoppedIntegrationEventHandler)"]
        HandlerB --> InboxB[("Inbox Table\n(chống xử lý trùng)")]
        HandlerB --> SendMail["Kích hoạt gửi Email thông báo tới Sinh viên"]
    end

    style ModuleA fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px
    style Infra fill:#fff8e1,stroke:#f57f17,stroke-width:2px
    style ModuleB fill:#e1f5fe,stroke:#0277bd,stroke-width:2px
```

---

## 6. Bảo mật & Phân quyền (Security & Authentication Flow)

> 📄 **Mã nguồn PlantUML:** [Security_Authentication_Flow.puml](PlantUML/Security_Authentication_Flow.puml)

Xác thực phi trạng thái (Stateless) bằng ASP.NET Core Identity + JWT Bearer Token kết hợp phân quyền RBAC và Policy-Based:

```mermaid
sequenceDiagram
    autonumber
    actor Client as Người dùng / SPA
    participant Host as Host API
    participant JWT as JWT Middleware
    participant AuthFilter as Authorization Policy Filter
    participant Context as ExecutionContext Accessor
    participant Module as Module Application
    participant DB as identity.Users

    Note over Client, DB: GIAI ĐOẠN ĐĂNG NHẬP
    Client->>Host: POST /api/identity/login (Email, Password)
    Host->>DB: Kiểm tra tài khoản & mã hóa mật khẩu
    DB-->>Host: Hợp lệ (Kèm Roles: Sinh viên, Khoa...)
    Host->>Host: Ký JWT Bearer Token + Refresh Token
    Host-->>Client: 200 OK { token, refreshToken, roles }

    Note over Client, DB: GIAI ĐOẠN GỌI TÁC VỤ CẦN QUYỀN
    Client->>Host: Gửi Request + Header: Authorization: Bearer <token>
    Host->>JWT: Xác thực chữ ký số & hạn dùng token
    JWT->>Host: Trích xuất Claims (UserId, Roles)
    Host->>Context: Thiết lập User Context (UserId, CorrelationId)
    Host->>AuthFilter: Kiểm tra quyền hạn [Authorize(Roles = "...")]
    AuthFilter->>Module: Chuyển giao Command/Query cùng User Context
    Module-->>Host: Kết quả xử lý
    Host-->>Client: 200 OK (Response Data)
```

---

## 7. Kiến trúc Dữ liệu & Phân tách Schema (Multi-Schema Database Architecture)

> 📄 **Mã nguồn PlantUML:** [MultiSchema_Database.puml](PlantUML/MultiSchema_Database.puml)

Nhằm đảm bảo quy tắc DDD và tính sẵn sàng tách thành Microservices trong tương lai:
- **1 Database duy nhất**: `UGTM_Db`
- **Mỗi Module sở hữu Schema riêng biệt**:
  - `identity.*`: Quản lý người dùng, Roles, JWT refresh tokens, Audit log.
  - `academic.*`: Đợt KLTN, mốc 21 tuần, cấu hình deadline từng giai đoạn.
  - `topics.*`: Ngân hàng đề tài, đề xuất từ GVHD, duyệt đề tài của Khoa.
  - `thesis.*`: Đăng ký đề tài, thành viên nhóm, nộp & thẩm định đề cương.
  - `progress.*`: Báo cáo giữa kỳ, đơn xin đổi tên đề tài (ĐTĐT) 3 bên.
  - `evaluation.*`: Báo cáo cuối kỳ, bảng điểm thành phần GVHD, GVPB.
  - `councils.*`: Hội đồng chấm KLTN, quyết định thành lập của PĐT, biên bản điểm bảo vệ.
  - `grading.*`: Bảng tổng hợp điểm 3 nguồn, công thức trọng số Strategy, xuất dữ liệu.
  - `notifications.*`: Hàng đợi thông báo, mẫu email, nhật ký gửi tin.
- **Ràng buộc khóa ngoại**: Không dùng Foreign Key vật lý qua lại giữa các schema. Quan hệ liên module được duy trì qua các khóa logic định danh (GUIDs).

```mermaid
flowchart TB
    subgraph S_Identity["Schema: identity"]
        T_Users["<b>identity.Users</b><br/>• Id: guid [PK]<br/>• Email: string [UK]<br/>• PasswordHash: string<br/>• Role: string<br/>• IsActive: bool"]
    end

    subgraph S_Academic["Schema: academic"]
        T_Periods["<b>academic.AcademicPeriods</b><br/>• Id: guid [PK]<br/>• AcademicYear: string<br/>• Semester: int<br/>• StartDate: datetime<br/>• EndDate: datetime<br/>• Status: string"]
    end

    subgraph S_Topics["Schema: topics"]
        T_Topics["<b>topics.TopicProposals</b><br/>• Id: guid [PK]<br/>• AcademicPeriodId: guid [Logic Ref]<br/>• SupervisorId: guid [Logic Ref]<br/>• TitleVietnamese: string<br/>• TitleEnglish: string<br/>• MaxStudents: int<br/>• Status: string"]
    end

    subgraph S_Thesis["Schema: thesis"]
        T_Thesis["<b>thesis.ThesisRegistrations</b><br/>• Id: guid [PK]<br/>• TopicId: guid [Logic Ref]<br/>• AcademicPeriodId: guid [Logic Ref]<br/>• SupervisorId: guid [Logic Ref]<br/>• CurrentStatus: string"]
    end

    subgraph S_Progress["Schema: progress"]
        T_Midterm["<b>progress.MidtermReports</b><br/>• Id: guid [PK]<br/>• ThesisId: guid [Logic Ref]<br/>• ReportFileUrl: string<br/>• SupervisorDecision: string"]
        T_Change["<b>progress.TopicChangeRequests</b><br/>• Id: guid [PK]<br/>• ThesisId: guid [Logic Ref]<br/>• NewTitle: string<br/>• SupervisorStatus: string<br/>• FacultyStatus: string<br/>• AcademicAffairsStatus: string"]
    end

    subgraph S_Councils["Schema: councils"]
        T_Councils["<b>councils.DefenseCouncils</b><br/>• Id: guid [PK]<br/>• AcademicPeriodId: guid [Logic Ref]<br/>• CouncilName: string<br/>• Room: string<br/>• DefenseDate: datetime<br/>• IsLocked: bool"]
    end

    subgraph S_Grading["Schema: grading"]
        T_Grading["<b>grading.GradingResults</b><br/>• Id: guid [PK]<br/>• ThesisId: guid [Logic Ref]<br/>• SupervisorScore: decimal<br/>• ReviewerScore: decimal<br/>• CouncilScore: decimal<br/>• FinalScore: decimal<br/>• IsPublished: bool"]
    end

    T_Topics -.->|Logic Ref: PeriodId| T_Periods
    T_Thesis -.->|Logic Ref: TopicId| T_Topics
    T_Midterm -.->|Logic Ref: ThesisId| T_Thesis
    T_Change -.->|Logic Ref: ThesisId| T_Thesis
    T_Grading -.->|Logic Ref: ThesisId| T_Thesis
    T_Councils -.->|Logic Ref: PeriodId| T_Periods

    style S_Identity fill:#f3e8f8,stroke:#ab47bc,stroke-width:1px
    style S_Academic fill:#ffeddb,stroke:#ffa726,stroke-width:1px
    style S_Topics fill:#e8f5e9,stroke:#66bb6a,stroke-width:1px
    style S_Thesis fill:#e1f5fe,stroke:#42a5f5,stroke-width:1px
    style S_Progress fill:#fbe9e7,stroke:#ff7043,stroke-width:1px
    style S_Councils fill:#ede7f6,stroke:#7e57c2,stroke-width:1px
    style S_Grading fill:#e0f2f1,stroke:#26a69a,stroke-width:1px
```

---

## 8. Biểu đồ Luồng Nghiệp vụ 7 Giai đoạn & 2 Cổng Kiểm soát (Gatekeeper Workflow)

> 📄 **Mã nguồn PlantUML:** [Gatekeeper_Workflow.puml](PlantUML/Gatekeeper_Workflow.puml)

Biểu đồ trạng thái và tương tác của toàn bộ quy trình chuẩn 21 tuần theo đặc tả nghiệp vụ:

```mermaid
stateDiagram-v2
    [*] --> GiaiDoan1 : Tuần 0 - 1
    
    state GiaiDoan1 {
        [*] --> GVHD_SoanDeTai
        GVHD_SoanDeTai --> Khoa_ThamDinhDeTai
        Khoa_ThamDinhDeTai --> Khoa_CongBoDeTai : Đạt
        Khoa_ThamDinhDeTai --> GVHD_SoanDeTai : Yêu cầu sửa
    }

    GiaiDoan1 --> GiaiDoan2 : Tuần 2 (Mở đăng ký)
    
    state GiaiDoan2 {
        [*] --> SV_DangKy_GhepNhom
        SV_DangKy_GhepNhom --> NopDeCuongChiTiet
        NopDeCuongChiTiet --> CongKiemSoat1_KhoaDuyetDeCuong
        
        state CongKiemSoat1_KhoaDuyetDeCuong <<choice>>
        CongKiemSoat1_KhoaDuyetDeCuong --> NeedRevision : Không đạt / Cần sửa
        NeedRevision --> NopDeCuongChiTiet : Nộp lại trước deadline
        CongKiemSoat1_KhoaDuyetDeCuong --> Approved_DeCuong : Đạt
    }

    GiaiDoan2 --> GiaiDoan3 : Tuần 3 - 10 (Thực hiện KLTN)

    state GiaiDoan3 {
        [*] --> ThucHienKLTN
        ThucHienKLTN --> DonXinDoiTenDeTai : Nếu có thay đổi
        state DonXinDoiTenDeTai {
            SV_TaoDon --> GVHD_XacNhan
            GVHD_XacNhan --> Khoa_Duyet
            Khoa_Duyet --> PDT_DuyetQuyetDinh
            PDT_DuyetQuyetDinh --> CapNhatDeCuongMoi
        }
        ThucHienKLTN --> NopBaoCaoGiuaKy : Tuần 10
    }

    GiaiDoan3 --> GiaiDoan4 : Tuần 10 (Cổng kiểm soát 2)

    state GiaiDoan4 {
        [*] --> CongKiemSoat2_GVHD_DanhGia
        state CongKiemSoat2_GVHD_DanhGia <<choice>>
        CongKiemSoat2_GVHD_DanhGia --> DungKLTN : Không đồng ý tiếp tục
        CongKiemSoat2_GVHD_DanhGia --> TiepTucKLTN : Đồng ý tiếp tục
    }

    DungKLTN --> [*] : Chấm dứt quy trình KLTN

    TiepTucKLTN --> GiaiDoan5 : Tuần 11 - 18

    state GiaiDoan5 {
        [*] --> NopBaoCaoCuoiKy
        NopBaoCaoCuoiKy --> GVHD_ChamDiemThanhPhan
        GVHD_ChamDiemThanhPhan --> Khoa_PhanCongGVPB
        Khoa_PhanCongGVPB --> GVPB_NhanXetVaChamDiem
        
        state GVPB_DanhGia <<choice>>
        GVPB_NhanXetVaChamDiem --> GVPB_DanhGia
        GVPB_DanhGia --> KhongChoBaoVe : Không đồng ý
        GVPB_DanhGia --> ChoBaoVe : Đồng ý cho bảo vệ
        ChoBaoVe --> SV_SuaBaoCaoTheoGVPB
    }

    KhongChoBaoVe --> [*] : Không đủ điều kiện ra Hội đồng

    GiaiDoan5 --> GiaiDoan6 : Tuần 19 - 20 (Bảo vệ Hội đồng)

    state GiaiDoan6 {
        [*] --> Khoa_LapDuThaoHoiDong
        Khoa_LapDuThaoHoiDong --> PDT_BanHanhQuyetDinhHD
        PDT_BanHanhQuyetDinhHD --> BuoiBaoVeTrucTiep
        BuoiBaoVeTrucTiep --> HoiDong_ChamDiemVaLapBienBan
        HoiDong_ChamDiemVaLapBienBan --> KhoaBienBanDiem : Khóa biên bản (Read-only)
        KhoaBienBanDiem --> NopLuuChieuBaoCaoFinal : SV sửa theo HĐ
        NopLuuChieuBaoCaoFinal --> Khoa_XacNhanLuuChieu
    }

    GiaiDoan6 --> GiaiDoan7 : Tuần 21 (Tổng hợp & Công bố)

    state GiaiDoan7 {
        [*] --> TongHopDiem3Nguon : GVHD, GVPB, Hội đồng
        TongHopDiem3Nguon --> ApDungTrongSoStrategy
        ApDungTrongSoStrategy --> Khoa_CongBoDiemChinhThuc
        Khoa_CongBoDiemChinhThuc --> XuatBaoCaoExcelPDF
        XuatBaoCaoExcelPDF --> PDT_XetCongNhanTotNghiep
    }

    GiaiDoan7 --> [*] : Kết thúc kỳ KLTN thành công
```
