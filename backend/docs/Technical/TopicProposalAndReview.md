# Topic Proposal, Review & Publication Backend

**Trạng thái:** đã triển khai · **Cập nhật:** 20/10/2026

Tài liệu này giải thích phân hệ Đề xuất, Thẩm định và Công bố Đề tài Khóa luận trong module TopicCatalog: kiến trúc, luồng xử lý, ma trận chuyển trạng thái và phân quyền theo vai trò. Hợp đồng request/response công khai được đặc tả chi tiết tại [Topic Catalog API Contract](../../../docs/ApiContract/TopicCatalogApiContract.md).

---

## 1. Phạm vi hiện tại

Backend phân hệ Topic Catalog hỗ trợ:
- **Tạo đề tài mới (`POST /api/topics`):** Cho phép Giảng viên hướng dẫn (`Lecturer`) hoặc `Admin` tạo đề tài mới với tên tiếng Việt, tên tiếng Anh, mục tiêu, yêu cầu công nghệ và chỉ tiêu tiếp nhận (`studentNumber` chỉ nhận `1` hoặc `2`). Hỗ trợ lưu nháp (`Draft`) hoặc gửi duyệt ngay (`Submitted`). Tự động kiểm tra trùng tên đề tài trong cùng đợt của cùng giảng viên và xác thực sự tồn tại của `batchId` qua `IKltnBatchReader`.
- **Chỉnh sửa đề tài (`PUT /api/topics/{id}`):** Cho phép GVHD chỉnh sửa nội dung đề tài khi đang ở trạng thái `Draft` hoặc `NeedRevision` (Khoa yêu cầu sửa). Chặn chỉnh sửa khi đề tài đã ở `Submitted`, `Approved`, `Published`, `Rejected`.
- **Gửi duyệt đề tài (`POST /api/topics/{id}/submit`):** Cho phép GVHD gửi đề tài từ `Draft` hoặc `NeedRevision` sang `Submitted` để chờ Văn phòng Khoa thẩm định.
- **Thẩm định đề tài (`POST /api/topics/{id}/review`):** Dành riêng cho Văn phòng Khoa (`Faculty`) hoặc `Admin` thẩm định các đề tài đang ở `Submitted`, đưa ra kết luận: `Approved` (Khoa duyệt), `NeedRevision` (Yêu cầu sửa), hoặc `Rejected` (Từ chối).
- **Công bố danh mục đề tài (`POST /api/topics/publish`):** Dành riêng cho Văn phòng Khoa (`Faculty`) hoặc `Admin` công bố toàn bộ hoặc một danh sách chọn lọc các đề tài đã duyệt (`Approved` ➔ `Published` / `isPublished = true`).
- **Tra cứu công khai cho Sinh viên (`GET /api/topics/all-topics`):** Sinh viên chỉ tra cứu được các đề tài đã được Khoa duyệt và công bố chính thức (`status == "Published"` và `isPublished == true`), không bị lộ các đề tài nháp hoặc chưa công bố.
- **Theo dõi đề tài (`GET /api/topics/my-topics` và `GET /api/topics/{id}/tracking`):** Cho phép GVHD theo dõi trạng thái đề tài và nhóm sinh viên của mình; mở quyền đặc quyền (`isPrivileged`) cho Văn phòng Khoa (`Faculty`), Quản trị viên (`Admin`) và Hội đồng (`CouncilMember`) xem và lọc toàn bộ đề tài theo mọi trạng thái phục vụ quản lý và thẩm định.

---

## 2. Vòng đời trạng thái đề tài (Topic Lifecycle)

```text
[Draft] --------(Submit)-------> [Submitted] --------(Review: Approve)-----> [Approved] -----(Publish)-----> [Published]
   ^                                  |
   |----(Submit)--- [NeedRevision] <--+ (Review: NeedRevision)
                          |
                          +-----------> [Rejected] (Review: Reject)
```

---

## 3. Bố cục mã nguồn

```text
backend/
  Modules/AcademicPeriods/
    Contracts/Batches/
      IKltnBatchReader.cs         # Contract kiểm tra đợt KLTN tồn tại
    Infrastructure/Persistence/Repositories/
      KltnBatchReader.cs          # Triển khai đọc KltnBatchs từ DbContext
  Modules/TopicCatalog/
    Domain/
      Entities/
        Topic.cs                  # Entity đề tài: IsPublished, CanBeModified, UpdateDetails, TrySubmit, TryReview, TryPublish
      Enums/
        TopicStatus.cs            # Draft, Submitted, NeedRevision, Approved, Rejected, Published, Closed, Archived
      Repositories/
        ITopicRepository.cs       # Cổng persistence quản lý đề tài
    Application/
      Common/
        TopicErrors.cs            # Mã lỗi chuẩn hóa (InvalidStudentNumber, BatchNotFound, DuplicateTitle, v.v.)
      Persistence/
        ITopicUnitOfWork.cs       # Cổng Unit of Work lưu thay đổi DbContext
      Topics/
        Dtos/TopicDtos.cs         # TopicSummaryDto, TopicTrackingSummaryDto, PublishTopicsResultDto
        CreateTopic/              # Command & Handler tạo đề tài (Draft/Submitted)
        UpdateTopic/              # Command & Handler sửa đề tài (Draft/NeedRevision)
        SubmitTopic/              # Command & Handler gửi duyệt đề tài
        ReviewTopic/              # Command & Handler Khoa thẩm định đề tài
        PublishTopics/            # Command & Handler Khoa công bố danh mục đề tài
        GetAllTopics/             # Query tra cứu đề tài công bố
        GetMyTopics/              # Query GVHD/Khoa theo dõi đề tài
        GetTopicTrackingDetail/   # Query chi tiết đề tài kèm nhóm SV
    Infrastructure/
      Endpoints/
        TopicEndpoints.cs         # Minimal API tại /api/topics
      Persistence/
        TopicCatalogDbContext.cs  # DbContext runtime topics schema & Unit of Work
        Repositories/
          TopicRepository.cs      # Triển khai ITopicRepository
          TopicQueryReader.cs     # Triển khai ITopicQueryReader
```
