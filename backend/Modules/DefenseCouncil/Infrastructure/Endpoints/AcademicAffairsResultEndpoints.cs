using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResultDetail;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResults;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class AcademicAffairsResultEndpoints
{
    public static IEndpointRouteBuilder MapAcademicAffairsResultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/academic-affairs/results")
            .WithTags("AcademicAffairs.Results")
            .RequireAuthorization(policy => policy.RequireRole("AcademicAffairs", "Faculty", "Admin"));

        // 1. GET /api/academic-affairs/results
        group.MapGet("/", GetAcademicResultsAsync)
            .WithSummary("Tra cứu và phân trang danh sách kết quả khóa luận toàn trường (Giữa kỳ & Cuối kỳ)")
            .WithDescription("""
                Cung cấp danh sách kết quả khóa luận tốt nghiệp (KLTN) toàn trường có hỗ trợ phân trang, tìm kiếm từ khóa đa trường và bộ lọc trạng thái chuyên sâu. Phục vụ cán bộ Phòng Đào tạo (`AcademicAffairs`) và Văn phòng Khoa (`Faculty`) theo dõi tiến độ, giám sát kết quả và đối soát hồ sơ chấm điểm của sinh viên.
                
                ---
                
                ### 1. Tham số Query Parameters:
                | Tên tham số | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả chi tiết & Ràng buộc |
                | :--- | :--- | :---: | :--- | :--- |
                | `batchId` | `UUID` | **Có** | - | Định danh đợt KLTN cần tra cứu. Phải là GUID hợp lệ và không được rỗng (`00000000-0000-0000-0000-000000000000`). |
                | `phase` | `string` | **Có** | - | Giai đoạn đánh giá: `'Midterm'` (Giữa kỳ) hoặc `'Final'` (Cuối kỳ). Hỗ trợ không phân biệt hoa thường (`midterm`, `final`, `mIdTeRm`). |
                | `keyword` | `string` | Không | `null` | Từ khóa tìm kiếm: Mã nhóm (`N01`, `N02`), Tên đề tài tiếng Việt/tiếng Anh, Tên sinh viên hoặc MSSV (`22520001`). Khoảng trắng thừa được tự động chuẩn hóa. |
                | `status` | `string` | Không | `null` | Bộ lọc trạng thái hồ sơ (xem chi tiết mục 2 bên dưới). Hỗ trợ giá trị `'ALL'` hoặc bỏ trống để lấy toàn bộ. |
                | `pageIndex` | `integer` | Không | `1` | Số thứ tự trang cần lấy (>= 1). Tự động điều chỉnh về 1 nếu truyền số <= 0. |
                | `pageSize` | `integer` | Không | `50` | Kích thước trang (1 đến 100). Tự động giới hạn tối đa 100 bản ghi để bảo vệ hiệu năng hệ thống. |
                
                ---
                
                ### 2. Quy tắc Lọc Trạng thái (`status`) và Nhãn hiển thị (`statusLabel` / `detailLabel`):
                
                #### A. Pha Cuối kỳ (`phase = 'Final'`):
                - `ALL`: Lấy toàn bộ nhóm trong đợt KLTN.
                - `PUBLISHED`: Bảng điểm đã được Khoa chính thức công bố tới sinh viên (`ScoreSummary.Status = PublishedToStudent`).
                  - `statusLabel`: `"Khoa đã công bố"`
                  - `detailLabel`: `"Lưu chiểu đạt"` (nếu đã xác nhận lưu chiểu) hoặc `"Chưa đủ lưu chiểu"`
                - `READY_TO_PUBLISH`: Đã có đầy đủ điểm (GVHD, GVPB, Hội đồng đã khóa biên bản) và đã hoàn tất lưu chiểu đạt (`ArchiveConfirmed`), sẵn sàng để Khoa công bố.
                  - `statusLabel`: `"Chờ công bố"`
                  - `detailLabel`: `"Lưu chiểu đạt"`
                - `MISSING_ARCHIVE`: Đã có đầy đủ điểm số các nguồn nhưng sinh viên chưa hoàn tất nộp hồ sơ lưu chiểu hoặc chưa được Khoa phê duyệt.
                  - `statusLabel`: `"Chưa đủ lưu chiểu"`
                  - `detailLabel`: `"Chưa công bố"`
                - `MISSING_SCORE`: Chưa chấm đủ điểm thành phần (GVHD, GVPB hoặc các thành viên Hội đồng chưa hoàn tất, hoặc Biên bản bảo vệ chưa khóa).
                  - `statusLabel`: `"Chưa đủ điểm"`
                  - `detailLabel`: `"Lưu chiểu đạt"` (nếu đã lưu chiểu) hoặc `"Chưa công bố"`
                
                #### B. Pha Giữa kỳ (`phase = 'Midterm'`):
                - `ALL`: Lấy toàn bộ nhóm trong đợt KLTN.
                - `CONTINUED`: Báo cáo giữa kỳ đã được phê duyệt, nhóm sinh viên đủ điều kiện tiếp tục thực hiện KLTN (`MidtermReport.Status = Approved`).
                  - `statusLabel`: `"Tiếp tục"`
                  - `detailLabel`: `"Đã duyệt giữa kỳ"`
                - `STOPPED`: Nhóm sinh viên bị dừng thực hiện đề tài do không đạt yêu cầu giữa kỳ (`StudentGroup.Status = Stopped` hoặc `MidtermReport.Status = Rejected`).
                  - `statusLabel`: `"Dừng thực hiện"`
                  - `detailLabel`: `"Không đạt giữa kỳ"`
                - `PENDING_MIDTERM`: Nhóm đang chờ xét duyệt báo cáo giữa kỳ.
                  - `statusLabel`: `"Chờ xét duyệt"`
                  - `detailLabel`: `"Chờ Khoa duyệt"` (đã nộp), `"Cần chỉnh sửa"` (yêu cầu nộp lại), hoặc `"Chưa nộp báo cáo"`
                
                > **Lưu ý:** Nếu truyền trạng thái không thuộc pha tương ứng (ví dụ: `phase=Midterm` nhưng truyền `status=PUBLISHED`), hệ thống sẽ trả về lỗi `400 Bad Request` (`AcademicResults.InvalidStatus`).
                
                ---
                
                ### 3. Phân quyền Truy cập (RBAC):
                Yêu cầu Bearer token của tài khoản có một trong các vai trò sau:
                - `AcademicAffairs`: Cán bộ Phòng Đào tạo (Toàn quyền tra cứu và đối soát kết quả toàn trường).
                - `Faculty`: Cán bộ Văn phòng Khoa (Theo dõi và kiểm tra kết quả của các nhóm thuộc Khoa).
                - `Admin`: Quản trị viên hệ thống.
                *(Các vai trò khác như `Student`, `Lecturer`, `CouncilMember` sẽ bị từ chối truy cập với mã `403 Forbidden`).*
                
                ---
                
                ### 4. Cấu trúc Dữ liệu Phản hồi (`AcademicResultListItemDto`):
                - `groupId` (UUID): Định danh duy nhất của nhóm đề tài.
                - `groupCode` (string): Mã hồ sơ nhóm hiển thị trên giao diện (`N01`, `N02`...).
                - `topicId` (UUID): Định danh đề tài KLTN.
                - `topicTitleVi` (string): Tên đề tài tiếng Việt.
                - `topicTitleEn` (string, nullable): Tên đề tài tiếng Anh.
                - `studentNames` (array of string): Danh sách họ tên các sinh viên trong nhóm.
                - `studentCodes` (array of string): Danh sách mã số sinh viên (MSSV) tương ứng.
                - `facultyName` (string): Tên khoa quản lý chuyên môn (mặc định: `"Khoa CNTT"`).
                - `phase` (string): Pha đánh giá (`Midterm` hoặc `Final`).
                - `statusCode` (string): Mã trạng thái kỹ thuật (`PUBLISHED`, `READY_TO_PUBLISH`, `CONTINUED`, ...).
                - `statusLabel` (string): Nhãn trạng thái hiển thị thân thiện trên UI (`"Khoa đã công bố"`, `"Tiếp tục"`...).
                - `detailLabel` (string): Nhãn phụ trạng thái hồ sơ (`"Lưu chiểu đạt"`, `"Đã duyệt giữa kỳ"`...).
                - `finalScore` (decimal, nullable): Điểm tổng kết cuối kỳ (chỉ có giá trị ở pha `Final` khi đã tính; luôn là `null` ở pha `Midterm`).
                
                ---
                
                ### 5. Mã lỗi và Phản hồi HTTP:
                - **`200 OK`**: Tra cứu thành công, trả về danh sách phân trang bọc trong envelope `ApiResponse<PagedResult<AcademicResultListItemDto>>`.
                - **`400 Bad Request`**:
                  - `AcademicResults.InvalidBatchId`: `batchId` bị rỗng hoặc sai định dạng UUID.
                  - `AcademicResults.InvalidPhase`: `phase` không phải là `Midterm` hoặc `Final`.
                  - `AcademicResults.InvalidStatus`: Trạng thái `status` không hợp lệ hoặc không thuộc pha đã chọn.
                - **`401 Unauthorized`**: Người dùng chưa đăng nhập hoặc Bearer token không hợp lệ / hết hạn.
                - **`403 Forbidden`**: Người dùng không có vai trò `AcademicAffairs`, `Faculty` hoặc `Admin` (`AcademicResults.Forbidden`).
                """)
            .Produces<ApiResponse<PagedResult<AcademicResultListItemDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .WithName("AcademicAffairs.Results.GetList");

        // 2. GET /api/academic-affairs/results/export
        group.MapGet("/export", ExportAcademicResultsAsync)
            .WithSummary("Xuất Báo cáo Kết quả Toàn trường ra File Excel (.xlsx) hoặc PDF (.pdf)")
            .WithDescription("""
                Xuất toàn bộ danh sách kết quả khóa luận tốt nghiệp của đợt ra tệp bảng tính Excel (`.xlsx`) hoặc tệp tài liệu PDF (`.pdf`). Hỗ trợ áp dụng đồng bộ bộ lọc tìm kiếm từ khóa (`keyword`) và lọc trạng thái (`status`) từ màn hình tra cứu.
                
                ---
                
                ### 1. Quy tắc Xuất Dữ liệu & Thiết kế Dòng:
                - **Mỗi sinh viên nằm trên một dòng riêng biệt**: Để phục vụ đối soát, thống kê và import điểm theo Mã số sinh viên (MSSV).
                - Với nhóm có 2 sinh viên (ví dụ: nhóm `N01`), hệ thống sẽ xuất **2 dòng liên tiếp**: cùng Mã nhóm, Tên đề tài, Khoa quản lý, Điểm số, Trạng thái; chỉ khác cột `MSSV` và `Họ tên SV`.
                - Pha Giữa kỳ (`Midterm`): Các cột điểm số (GVHD, GVPB, Hội đồng, Tổng kết) được để trống (`""`); cột Trạng thái công bố hiển thị kết quả duyệt giữa kỳ (`"Tiếp tục"`, `"Dừng thực hiện"`, `"Chờ xét duyệt"`).
                - Pha Cuối kỳ (`Final`): Hiển thị đầy đủ điểm thành phần định dạng 2 chữ số thập phân (`8.50`, `7.80`), trạng thái lưu chiểu và công bố.
                
                ---
                
                ### 2. Danh mục 11 Cột trong Tệp Xuất:
                | Cột | Tên cột hiển thị | Căn chỉnh | Nội dung ở pha Cuối kỳ | Nội dung ở pha Giữa kỳ |
                | :---: | :--- | :---: | :--- | :--- |
                | 1 | `MSSV` | Giữa | Mã số sinh viên (ví dụ: `22520001`) | Mã số sinh viên |
                | 2 | `Họ tên SV` | Trái | Họ và tên sinh viên (ví dụ: `Nguyễn Văn An`) | Họ và tên sinh viên |
                | 3 | `Mã nhóm` | Giữa | Mã nhóm đề tài (`N01`, `N02`...) | Mã nhóm đề tài |
                | 4 | `Tên đề tài` | Trái | Tên đề tài khóa luận tiếng Việt | Tên đề tài khóa luận tiếng Việt |
                | 5 | `Khoa quản lý` | Trái | Tên khoa quản lý (ví dụ: `"Khoa CNTT"`) | Tên khoa quản lý |
                | 6 | `Điểm GVHD` | Phải | Điểm số GVHD chấm (ví dụ: `9.00`) | Để trống |
                | 7 | `Điểm GVPB` | Phải | Điểm số GVPB chấm (ví dụ: `8.50`) | Để trống |
                | 8 | `Điểm Hội đồng` | Phải | Điểm trung bình Hội đồng chấm (ví dụ: `8.50`) | Để trống |
                | 9 | `Điểm tổng kết` | Phải | Điểm tổng kết KLTN (ví dụ: `8.65`) | Để trống |
                | 10 | `Trạng thái lưu chiểu` | Giữa | `"Lưu chiểu đạt"` / `"Chưa đủ lưu chiểu"` | Nhãn chi tiết giữa kỳ |
                | 11 | `Trạng thái công bố` | Giữa | `"Khoa đã công bố"` / `"Chờ công bố"` / `"Chưa đủ điểm"` | `"Tiếp tục"` / `"Dừng thực hiện"` / `"Chờ xét duyệt"` |
                
                ---
                
                ### 3. Quy cách Định dạng Tệp Xuất:
                - **File Excel (`format = 'xlsx'`):**
                  - `Content-Type`: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
                  - Tên file đính kèm: `KetQuaKLTN_{phase}_{yyyyMMdd_HHmmss}.xlsx`
                  - Cấu trúc: Sheet `"KetQuaKLTN"`, dòng 1 là tiêu đề in hoa cỡ 16 bold căn giữa, dòng 2 là thời gian xuất UTC; dòng 4 là Header bảng nền màu xanh navy (`#1E3A8A`), chữ trắng bold; toàn bộ ô dữ liệu được kẻ viền (border) và tự động căn chỉnh độ rộng cột (`AdjustToContents`).
                - **File PDF (`format = 'pdf'`):**
                  - `Content-Type`: `application/pdf`
                  - Tên file đính kèm: `KetQuaKLTN_{phase}_{yyyyMMdd_HHmmss}.pdf`
                  - Cấu trúc: Tài liệu chuẩn `%PDF-1.4`, khổ A4 ngang (Landscape, kích thước 842 x 595 pt), tự động phân trang (20 dòng/trang), có tiêu đề báo cáo, số trang (`Trang X/Y`), bảng kẻ dòng xen kẽ và hiển thị tiếng Việt rõ ràng.
                
                ---
                
                ### 4. Tham số Query Parameters:
                - `batchId` (Guid, bắt buộc): Định danh đợt KLTN cần xuất dữ liệu.
                - `format` (string, bắt buộc): Định dạng file xuất: `'xlsx'` hoặc `'pdf'` (hỗ trợ không phân biệt hoa thường).
                - `phase` (string, mặc định = `'Final'`): Giai đoạn đánh giá: `'Midterm'` hoặc `'Final'`.
                - `keyword` (string, tùy chọn): Áp dụng bộ lọc tìm kiếm hiện tại vào tệp xuất.
                - `status` (string, tùy chọn): Áp dụng bộ lọc trạng thái hiện tại vào tệp xuất.
                
                ---
                
                ### 5. Mã lỗi và Phản hồi HTTP:
                - **`200 OK`**: Xuất tệp thành công, trả về File Stream nhị phân kèm đúng Content-Type và Content-Disposition filename.
                - **`400 Bad Request`**:
                  - `AcademicResults.InvalidBatchId`: `batchId` rỗng hoặc không hợp lệ.
                  - `AcademicResults.InvalidFormat`: `format` không phải `xlsx` hoặc `pdf`.
                  - `AcademicResults.InvalidPhase`: `phase` không phải `Midterm` hoặc `Final`.
                  - `AcademicResults.InvalidStatus`: `status` không hợp lệ cho pha đã chọn.
                - **`401 Unauthorized`**: Chưa đăng nhập hoặc token không hợp lệ.
                - **`403 Forbidden`**: Không có quyền `AcademicAffairs`, `Faculty` hoặc `Admin`.
                """)
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .WithName("AcademicAffairs.Results.Export");

        // 3. GET /api/academic-affairs/results/{groupId}
        group.MapGet("/{groupId:guid}", GetAcademicResultDetailAsync)
            .WithSummary("Xem chi tiết kết quả, tiến độ và bảng điểm của 1 nhóm KLTN (Chế độ chỉ đọc)")
            .WithDescription("""
                Xem thông tin chi tiết toàn diện của 1 hồ sơ nhóm sinh viên KLTN ở chế độ chỉ đọc (`Read-Only`), phục vụ cán bộ Phòng Đào tạo hoặc Văn phòng Khoa tra cứu khi bấm vào một dòng trong bảng kết quả.
                
                ---
                
                ### 1. Tham số Request:
                - `groupId` (UUID, bắt buộc trên URL route): Định danh duy nhất của nhóm đề tài (`StudentGroup`).
                - `phase` (string, bắt buộc trên URL query): Giai đoạn đánh giá cần xem: `'Midterm'` hoặc `'Final'`.
                
                ---
                
                ### 2. Dữ liệu Chi tiết theo Giai đoạn:
                
                #### A. Pha Cuối kỳ (`phase = 'Final'`):
                - **Thông tin nhóm & Đề tài**: Mã nhóm (`N01`), Mã đề tài, Tên đề tài tiếng Việt/Anh, GVHD, Khoa quản lý.
                - **Danh sách thành viên**: Danh sách sinh viên với MSSV, Họ tên, Email.
                - **Danh sách nguồn điểm thành phần (`scoreSources`)**:
                  - `ADVISOR` (Giảng viên hướng dẫn): Điểm số, hệ số/trọng số (`0.20`), trạng thái (`Submitted`), nhận xét chi tiết, thời gian chấm.
                  - `REVIEWER` (Giảng viên phản biện): Điểm số, hệ số/trọng số (`0.20`), trạng thái (`Submitted`), nhận xét chi tiết, thời gian chấm.
                  - `COUNCIL_CHAIRMAN` (Chủ tịch Hội đồng): Điểm số, vai trò, kết luận đánh giá, thời gian chấm.
                  - `COUNCIL_SECRETARY` (Thư ký Hội đồng): Điểm số, vai trò, kết luận đánh giá, thời gian chấm.
                  - `COUNCIL_MEMBER` (Ủy viên Hội đồng): Điểm số, vai trò, kết luận đánh giá, thời gian chấm.
                - **Điểm số tổng hợp**: `advisorScore`, `reviewerScore`, `councilScore` (trung bình HĐ), `finalScore` (từ ScoreSummary), và công thức tính (`formulaSnapshot`).
                - **Trạng thái lưu chiểu**: `archiveStatus` (`ArchiveConfirmed` / `Submitted` / `NotSubmitted`), `archiveStatusLabel` (`"Lưu chiểu đạt"` / `"Chưa đủ lưu chiểu"`).
                - **Trạng thái công bố**: `publicationStatus` (`PublishedToStudent` / `Calculated` / `Pending`), `publicationStatusLabel` (`"Khoa đã công bố"` / `"Chờ công bố"` / `"Chưa công bố"`), `publishedAtUtc`.
                - **Thông tin giữa kỳ bổ trợ**: `midtermDetail` chứa trạng thái duyệt và phản hồi lịch sử giữa kỳ (nếu có).
                
                #### B. Pha Giữa kỳ (`phase = 'Midterm'`):
                - Các trường điểm số (`advisorScore`, `reviewerScore`, `councilScore`, `finalScore`, `formulaSnapshot`) đều là `null`.
                - Danh sách nguồn điểm `scoreSources` là mảng rỗng (`[]`).
                - **Thông tin xét duyệt giữa kỳ (`midtermDetail`)**:
                  - `status`: Trạng thái xét duyệt (`Approved`, `Rejected`, `NeedRevision`, `Submitted`).
                  - `statusLabel`: `"Đã duyệt"`, `"Không đạt"`, `"Cần chỉnh sửa"`, `"Đã nộp"`.
                  - `currentVersionNo`: Phiên bản báo cáo giữa kỳ hiện tại.
                  - `facultyFeedback`: Nhận xét, đánh giá của Văn phòng Khoa / Hội đồng thẩm định giữa kỳ.
                  - `reviewedBy`: GUID người phê duyệt.
                  - `reviewerName`: Họ tên cán bộ duyệt báo cáo giữa kỳ.
                  - `reviewedAtUtc`: Thời điểm phê duyệt báo cáo.
                
                ---
                
                ### 3. Phân quyền Truy cập (RBAC):
                Yêu cầu Bearer token có vai trò `AcademicAffairs`, `Faculty` hoặc `Admin`.
                
                ---
                
                ### 4. Mã lỗi và Phản hồi HTTP:
                - **`200 OK`**: Thành công, trả về chi tiết hồ sơ bọc trong `ApiResponse<AcademicResultDetailDto>`.
                - **`400 Bad Request`**: `AcademicResults.InvalidPhase` (thiếu hoặc sai `phase`).
                - **`401 Unauthorized`**: Chưa đăng nhập hoặc token hết hạn.
                - **`403 Forbidden`**: Không có quyền truy cập (`AcademicResults.Forbidden`).
                - **`404 Not Found`**: `AcademicResults.GroupNotFound` (không tìm thấy nhóm đề tài với `groupId` đã cho).
                """)
            .Produces<ApiResponse<AcademicResultDetailDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .WithName("AcademicAffairs.Results.GetDetail");

        return endpoints;
    }

    private static async Task<IResult> GetAcademicResultsAsync(
        [FromQuery] Guid batchId,
        [FromQuery] string phase,
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        [FromServices] GetAcademicResultsHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAcademicResultsQuery(
            batchId,
            phase,
            keyword,
            status,
            pageIndex,
            pageSize);

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetAcademicResultDetailAsync(
        [FromRoute] Guid groupId,
        [FromQuery] string phase,
        [FromServices] GetAcademicResultDetailHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAcademicResultDetailQuery(groupId, phase);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ExportAcademicResultsAsync(
        [FromQuery] Guid batchId,
        [FromQuery] string format,
        [FromQuery] string? phase = "Final",
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        [FromServices] ExportAcademicResultsHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new ExportAcademicResultsQuery(
            batchId,
            format,
            phase,
            keyword,
            status);

        var result = await handler.HandleAsync(query, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        return Results.File(
            result.Value.FileBytes,
            result.Value.ContentType,
            result.Value.FileName);
    }
}
