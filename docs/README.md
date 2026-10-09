# Tài liệu UGTM

Nguồn nghiệp vụ: Báo cáo SE310 (Bản Final).docx (tài liệu nghiệp vụ nhóm quản lý ngoài repo). Ngày đồng bộ: 10/10/2026. Các sơ đồ kiến trúc trong Word chưa cập nhật; tài liệu kiến trúc ở repo mô tả code hiện tại theo xác nhận của nhóm.

| Tài liệu | Nội dung |
| --- | --- |
| [Requirements.md](Requirements.md) | Quy trình 7 giai đoạn, ma trận quyền, 75 FR, yêu cầu phi chức năng và quy tắc final |
| [UseCases.md](UseCases.md) | 12 đặc tả Use Case chính dạng bảng; không có Use Case diagram |
| [DataFlows.md](DataFlows.md) | Biểu mẫu, D1–D6, quy định và thuật toán cho 5 luồng dữ liệu của Word |
| [Sequences.md](Sequences.md) | 5 luồng tuần tự chính; là thiết kế nghiệp vụ cần triển khai |
| [SITEMAP.md](SITEMAP.md) | Route đang có và danh mục màn hình dự kiến theo vai trò |
| [Architecture.md](Architecture.md) | Cấu trúc code, runtime, dữ liệu, xác thực và khác biệt với Word |
| [DataModel.md](DataModel.md) | 34 bảng/6 schema, 50 FK, nguồn migration và khoảng thiếu so với final |
| [DatabaseScript.txt](DatabaseScript.txt) | DBML khớp EF, bỏ comment dư thừa; dùng để xem cấu trúc |

## Quy ước

- **Hiện tại**: có bằng chứng trong source; mô hình EF/migration không đồng nghĩa database đã được migrate hoặc nghiệp vụ đã hoạt động.
- **Yêu cầu/thiết kế nghiệp vụ**: nội dung cần xây dựng theo báo cáo final; không dùng để khẳng định tính năng đã xong.
- **Cần xác nhận**: chi tiết chưa có quy tắc đầy đủ; không thay bằng giả định cũ.
- FR lấy nguyên mã trong Word. Đặc tả chính tham chiếu mục 2.3.2/Bảng 2.8–2.19; danh mục Bảng 2.1–2.7 trong mục lục Word là tham chiếu cũ, không dùng để khẳng định số Use Case.
- 7 nhóm tác nhân nghiệp vụ tương ứng 6 vai trò hệ thống; GVHD/GVPB được phân biệt theo phân công. Không thêm role hệ thống ngoài enum hiện tại.

## Sơ đồ giữ lại

Kiến trúc: [C1](PlantUML/C1_System_Context.puml), [C2](PlantUML/C2_Containers.puml), [C3 tổng quan](PlantUML/C3_Components_HighLevel.puml), [C3 module](PlantUML/C3_Components_ModuleDeepDive.puml), [CSDL](PlantUML/MultiSchema_Database.puml), [xác thực](PlantUML/Security_Authentication_Flow.puml).

Nghiệp vụ: [7 giai đoạn](PlantUML/Gatekeeper_Workflow.puml), [tuần tự](Sequences.md), [sitemap](PlantUML/Sitemap_UGTM.puml). Mỗi sơ đồ ghi rõ trạng thái hiện tại hoặc thiết kế cần triển khai.

Đã bỏ các sơ đồ Conceptual Model/C4 aggregate/CQRS/pipeline/Outbox-EventBus cũ vì lặp nội dung hoặc mô tả các lớp/cơ chế không có trong source. `DatabaseScript.txt` được cập nhật từ EF metadata, không dùng bản DBML nháp làm nguồn độc lập. Không giữ tài liệu module riêng trùng lặp; trách nhiệm 6 module nằm trong Architecture, lý do, ranh giới DDD và hướng dẫn Neon cũng nằm trong Architecture. Kế hoạch sprint cũ cũng được loại khỏi bộ đặc tả vì không phải nội dung nghiệp vụ đã chốt.

Đã bỏ toàn bộ Use Case diagram theo yêu cầu nhóm; giữ đặc tả Use Case dạng bảng để truy vết yêu cầu.
