# KẾ HOẠCH TRIỂN KHAI DỰ ÁN THEO MÔ HÌNH SCRUM
## Final Thesis Management System (KLTN)

---

## I. Chuẩn hóa Định nghĩa & Quy tắc làm việc (Working Agreements)

### 1. Definition of Done (DoD) cho mỗi Task/Sprint
Một User Story hoặc Sprint chỉ được coi là hoàn tất khi thỏa mãn đầy đủ các tiêu chuẩn sau:
- **Kiến trúc & Clean Code:** Source code tuân thủ mô hình Clean Architecture, vượt qua các quy tắc static analysis và linting của dự án.
- **Kiểm thử tự động (Automated Testing):** Viết đầy đủ Unit Test cho các tầng Application Service và Domain Logic (Coverage tối thiểu 70%). Hoàn thành Integration Test cho các luồng API chính.
- **Tài liệu API:** Swagger/OpenAPI documentation được cập nhật đầy đủ request/response schema và mã lỗi chuẩn.
- **Hoàn thiện UI/UX:** Frontend ghép nối API hoàn chỉnh, xử lý toàn diện các trạng thái giao diện: Loading, Empty, Success, và Validation Error.
- **Code Review:** Code phải có ít nhất 1 thành viên (Peer Reviewer) duyệt Pull Request trước khi merge vào branch `develop`.
- **CI/CD & Deployment:** Pipeline CI/CD tự động build, vượt qua toàn bộ test suites và deploy thành công lên môi trường Staging/Dev.

### 2. Quy chuẩn Git Flow & Quản lý Task
- **Quy tắc đặt tên nhánh (Branch Naming):**
  - Feature: `feature/<module>-<task-id>` (Ví dụ: `feature/thesis-submit-midterm`)
  - Bugfix: `bugfix/<issue-id>` (Ví dụ: `bugfix/fix-token-expiry`)
  - Hotfix: `hotfix/<incident-id>`
- **Quy chuẩn Commit (Conventional Commits):**
  - Cú pháp: `<type>(<scope>): <short-description>`
  - Ví dụ: `feat(thesis): add midterm report submission api`, `fix(auth): handle expired refresh token gracefully`
- **Quản lý Task (Jira / GitHub Projects):**
  - Trạng thái luồng công việc: `Backlog` -> `To Do` -> `In Progress` -> `Code Review` -> `Testing` -> `Done`.

---

## II. Sprint 0: Foundation & DevOps Setup (Thời lượng: 1 tuần)

**Mục tiêu Sprint:** Thiết lập toàn bộ khung kiến trúc phần mềm, cấu trúc dữ liệu và pipeline tự động hóa CI/CD nhằm tạo nền tảng vững chắc cho các Sprint nghiệp vụ tiếp theo.

### 1. Phân chia Task chi tiết

#### Backend Dev (Core Architecture & Database Migration)
- [ ] Khởi tạo Solution ASP.NET Core theo mô hình Clean Architecture:
  - `Domain`: Enterprise entities, value objects, domain exceptions.
  - `Application`: Commands, Queries (MediatR/CQRS), DTOs, FluentValidation, Mappings.
  - `Infrastructure`: DbContext, Entity Type Configurations, File Storage, Auth implementations.
  - `Presentation / API`: Controllers, Middleware, Filters, Program configuration.
- [ ] Cấu hình DbContext, Entity Framework Core và sinh Initial Migration cho toàn bộ 26 bảng thuộc 6 schemas (`identity`, `academic`, `topics`, `thesis`, `councils`, `shared`).
- [ ] Xây dựng Global Exception Handling Middleware và chuẩn hóa cấu trúc Base Response Wrapper (`ApiResponse<T>`).
- [ ] Cấu hình Swagger/OpenAPI tích hợp xác thực JWT Bearer token.

#### Frontend Dev (Project Scaffolding & Base UI)
- [ ] Khởi tạo dự án Frontend bằng Vite + React + TypeScript.
- [ ] Cấu hình State Management (Redux Toolkit / Zustand / TanStack Query).
- [ ] Cấu hình Axios Instance tích hợp Interceptor (tự động gắn access token và xử lý refresh token tự động).
- [ ] Cấu hình Route Guard bảo vệ các trang theo vai trò người dùng (Role-based protected routes).
- [ ] Tích hợp CSS framework (Tailwind CSS / UI Component Library) và dựng bộ khung bố cục (Layout Dashboard, Sidebar, Header, Breadcrumbs).

#### DevOps & Shared Tasks
- [ ] Soạn thảo `Dockerfile` tối ưu multi-stage build cho Backend và Frontend.
- [ ] Viết file `docker-compose.yml` chạy local đồng bộ: Database Engine + Backend API + Frontend Client.
- [ ] Thiết lập GitHub Actions CI/CD Workflow:
  - Tự động kích hoạt build và chạy test khi có Pull Request vào `develop`.
  - Tự động build Docker Image và deploy lên môi trường Staging khi merge vào `develop`.

---

## III. Kế hoạch Lộ trình Scrum (Roadmap 6 Sprints - 2 tuần/Sprint)

### Sprint 1: Identity & Authentication Module
- **Mục tiêu:** Xây dựng hệ thống xác thực tập trung, bảo mật và cơ chế phân quyền RBAC kết hợp quản trị thông tin tài khoản.
- **Phạm vi tính năng:**
  - Đăng nhập (Email trường / Mật khẩu), cấp phát JWT Access Token & Refresh Token, đổi mật khẩu.
  - Quản trị tài khoản người dùng: Tạo mới, cập nhật, kích hoạt/khóa tài khoản (`Active`, `Disabled`, `Locked`).
  - Gán vai trò (`Admin`, `Faculty`, `AcademicAffairs`, `Lecturer`, `Student`).
  - Quản lý thông tin hồ sơ cá nhân (Profile).
- **Phân bổ công việc:**
  - *Backend:* Triển khai Auth Service (JWT, hashing BCrypt/Argon2), CRUD User API, phân quyền `[Authorize(Roles = "...")]`.
  - *Frontend:* Xây dựng giao diện Login, đổi mật khẩu, Profile cá nhân, màn hình quản trị danh sách người dùng.
  - *QA/Test:* Kiểm thử bảo mật (SQL Injection, brute force, token expiration), kiểm thử phân quyền trái phép (401, 403).

### Sprint 2: Academic & Batches Management Module
- **Mục tiêu:** Thiết lập động cơ quản lý đợt KLTN, 7 mốc giai đoạn (Stages) và tỷ lệ trọng số 3 đầu điểm (BR-06).
- **Phạm vi tính năng:**
  - PĐT khởi tạo Đợt KLTN (`KLTNBatches`), thiết lập năm học, học kỳ, ngày bắt đầu và kết thúc.
  - Cấu hình 7 Stage tương ứng 21 tuần kèm `StartDate` và `DeadlineDate`.
  - Cấu hình tỷ lệ trọng số điểm (`ScoreWeightConfigs`: GVHD + GVPB + Hội đồng = 1.00).
  - Logic xác định Stage hiện tại của hệ thống để khóa/mở quyền theo ngữ cảnh thời gian.
- **Phân bổ công việc:**
  - *Backend:* Triển khai API quản lý Batches/Stages, Business Validation cho tỷ lệ điểm (tổng bằng 1.00), Custom Authorization Policy đối soát Deadline.
  - *Frontend:* Giao diện quản lý đợt KLTN cho PĐT/Khoa, Timeline trực quan hiển thị 21 tuần và hạn chót nộp bài.
  - *QA/Test:* Test ràng buộc Check Constraint trọng số điểm, test validation `EndDate > StartDate` và `DeadlineDate >= StartDate`.

### Sprint 3: Topics Bank & Student Groups Module
- **Mục tiêu:** Quản lý quy trình đề xuất, duyệt đề tài và ghép nhóm sinh viên đăng ký thực hiện đề tài (1-2 SV/nhóm).
- **Phạm vi tính năng:**
  - GVHD đề xuất đề tài mới; Khoa phê duyệt / yêu cầu chỉnh sửa / công bố đề tài.
  - Sinh viên tìm kiếm đề tài được công bố, lập nhóm (1-2 sinh viên) và đăng ký đề tài.
  - Ràng buộc nghiệp vụ: Một sinh viên chỉ tham gia 1 đề tài trong cùng 1 đợt, mỗi nhóm tối đa 2 sinh viên.
- **Phân bổ công việc:**
  - *Backend:* Topic CRUD, State Machine duyệt đề tài; API ghép nhóm sinh viên; xử lý Transaction và Concurrency Lock chống tình trạng nhiều nhóm tranh chấp cùng 1 đề tài.
  - *Frontend:* Danh mục đề tài (tìm kiếm, lọc theo chuyên ngành), form nộp đề xuất đề tài cho GVHD, màn hình quản lý nhóm sinh viên.
  - *QA/Test:* Kiểm thử race condition khi nhiều nhóm bấm đăng ký đồng thời cùng 1 đề tài, kiểm thử Trigger chặn thêm sinh viên thứ 3 vào nhóm.

### Sprint 4: Thesis Milestone Tracking Module (Đề cương, Giữa kỳ, ĐTĐT)
- **Mục tiêu:** Vận hành toàn diện quy trình nộp hồ sơ, upload tài liệu, vòng lặp sửa đề cương và luồng duyệt đơn ĐTĐT 3 bên.
- **Phạm vi tính năng:**
  - Sinh viên nộp đề cương chi tiết (Tuần 2); Khoa thẩm định (`NeedRevision` -> Sinh viên nộp Version mới -> `Approved`).
  - Sinh viên nộp báo cáo giữa kỳ (Tuần 10); Khoa thẩm định tiến độ.
  - Luồng duyệt đơn Đổi tên đề tài (ĐTĐT - BR-02): Sinh viên lập đơn -> GVHD xác nhận -> Khoa phê duyệt -> PĐT duyệt chốt.
  - Quản lý tệp tin tải lên (`SubmissionFiles`: giới hạn dung lượng <= 20MB, định dạng PDF/DOC/DOCX).
- **Phân bổ công việc:**
  - *Backend:* Triển khai File Storage Service (lưu trữ local an toàn / MinIO / Cloud Storage), API nộp/duyệt Đề cương & Giữa kỳ, State Machine duyệt đơn ĐTĐT 3 bên.
  - *Frontend:* Giao diện upload tài liệu kéo thả kèm thanh tiến trình, màn hình chấm/phản hồi của Khoa, giao diện theo dõi tiến trình đơn ĐTĐT.
  - *QA/Test:* Kiểm thử tải lên file độc hại, file vượt dung lượng (25MB), file sai định dạng (.exe), kiểm thử chặn sinh viên nộp bài sau Deadline của Stage.

### Sprint 5: Councils, Reviewers & Dynamic Rubrics Grading Module
- **Mục tiêu:** Tổ chức đánh giá chuyên môn: Phân công GVPB, thành lập Hội đồng, nhập điểm theo barem động và cơ chế khóa biên bản điểm (BR-03, BR-04).
- **Phạm vi tính năng:**
  - Khoa/PĐT cấu hình bộ tiêu chí barem điểm dùng chung (`GradingCriteria`).
  - Khoa phân công GVPB; GVPB nhập nhận xét, điểm số và kết luận đồng ý/không đồng ý ra bảo vệ (`DefenseApproval`).
  - Thiết lập Hội đồng bảo vệ, phân ca/phòng thi; gán nhóm đề tài vào Hội đồng.
  - GVHD, GVPB và từng thành viên Hội đồng chấm điểm chi tiết theo tiêu chí (`EvaluationDetails`).
  - Thư ký Hội đồng thực hiện Khóa biên bản (`LockStatus = 'Locked'`) -> kích hoạt Trigger đóng băng toàn bộ điểm thành viên.
- **Phân bổ công việc:**
  - *Backend:* API quản lý barem điểm động, API phân công GVPB/Hội đồng, API chấm điểm và cộng dồn điểm tổng, Triển khai Trigger/Logic bảo vệ điểm đã khóa (BR-04) và cơ chế Override Unlock của Lãnh đạo Khoa.
  - *Frontend:* Form chấm điểm động render tự động theo danh sách tiêu chí barem, giao diện điều phối Hội đồng & phòng thi, màn hình biên bản Hội đồng của Thư ký có nút bấm Khóa biên bản.
  - *QA/Test:* Kiểm thử chống can thiệp sửa điểm sau khi biên bản đã khóa (kỳ vọng trả về 403 Forbidden), kiểm thử Unique Index chống xếp trùng phòng/ca thi trong cùng một ngày.

### Sprint 6: Final Submission, Score Publishing, Notifications & Audit
- **Mục tiêu:** Hoàn thiện giai đoạn kết thúc khóa luận: nộp lưu chiểu, tính điểm tổng kết, công bố điểm và hoàn tất hệ thống giám sát.
- **Phạm vi tính năng:**
  - Sinh viên nộp báo cáo toàn văn sau chỉnh sửa; Khoa kiểm tra và bấm xác nhận lưu chiểu (`ArchiveConfirmed` - BR-05).
  - Tự động tính toán điểm tổng kết theo công thức trọng số đã cấu hình (`ScoreSummaries` - BR-06).
  - Khoa công bố điểm chính thức cho sinh viên.
  - Cơ chế ghi vết tự động toàn bộ thao tác can thiệp vào bảng `shared.AuditLogs` (đảm bảo tính bất biến).
  - Hệ thống thông báo nội bộ: Nhắc nhở hạn chót (Deadline reminder) và cập nhật trạng thái hồ sơ.
- **Phân bổ công việc:**
  - *Backend:* Service tính toán điểm tổng kết tự động, Interceptor/Trigger tự động ghi Audit Log cho các nghiệp vụ nhạy cảm, Notification Service gửi thông báo qua SignalR / WebSocket.
  - *Frontend:* Màn hình tra cứu bảng điểm chi tiết của Sinh viên, giao diện quản lý hồ sơ lưu chiểu của Khoa, màn hình xem Audit Logs của Admin, component chuông thông báo trên Header.
  - *QA/Test:* End-to-End Test toàn bộ vòng đời đề tài từ Tuần 1 đến Tuần 21, Stress test / Performance test cho các API tổng hợp danh sách, kiểm thử tính bất biến của bảng AuditLog.