using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetTopicTrackingDetail;
using UGTM.Modules.TopicCatalog.Application.Topics.PublishTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Endpoints;

public static class TopicEndpoints
{
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/topics")
            .WithTags("TopicCatalog.Topics");

        // 1. GET /api/topics/all-topics - Sinh viên / công khai tra cứu danh mục đề tài đã công bố
        group.MapGet("/all-topics", GetAllTopicsAsync)
            .WithSummary("Tra cứu danh sách đề tài đã được Khoa duyệt và công bố")
            .WithDescription("""
                Lấy danh sách các đề tài KLTN đã được Văn phòng Khoa thẩm định duyệt và công bố chính thức (`IsPublished = true` / `Status = Published`) phục vụ sinh viên tra cứu thông tin cơ bản (Tên đề tài, mục tiêu, yêu cầu, chỉ tiêu).
                - `keyword`: Tìm kiếm theo tên tiếng Việt hoặc tiếng Anh.
                - `batchId`: Lọc theo đợt KLTN.
                - `pageNumber`: Số trang (>= 1, mặc định 1).
                - `pageSize`: Số lượng đề tài trên một trang (1 đến 100, mặc định 10).

                Không yêu cầu quyền đặc thù. Sinh viên chỉ tra cứu được các đề tài đã được duyệt và công bố.
                - 200 OK: Danh sách đề tài phân trang `PagedResult<TopicSummaryDto>`.
                """)
            .Produces<ApiResponse<PagedResult<TopicSummaryDto>>>(StatusCodes.Status200OK);

        // 2. GET /api/topics/my-topics - GVHD theo dõi danh sách đề tài của mình (Khoa / Admin / Hội đồng xem tất cả)
        group.MapGet("/my-topics", GetMyTopicsAsync)
            .WithSummary("GVHD theo dõi đề tài của mình / Văn phòng Khoa xem toàn bộ đề tài kèm trạng thái đăng ký")
            .WithDescription("""
                Cho phép GVHD xem danh sách các đề tài mình phụ trách, kèm trạng thái đăng ký hiện tại
                (đã có sinh viên đăng ký chưa, còn trống bao nhiêu chỉ tiêu) và thông tin sinh viên đã đăng ký.
                Văn phòng Khoa (`Faculty`), Quản trị viên (`Admin`) và Hội đồng (`CouncilMember`) có thể xem và lọc toàn bộ đề tài theo mọi trạng thái để phục vụ thẩm định và công bố.
                - `keyword`: Tìm kiếm theo tên đề tài tiếng Việt/tiếng Anh.
                - `batchId`: Lọc theo đợt KLTN.
                - `status`: Lọc theo trạng thái đề tài (`Draft`, `Submitted`, `NeedRevision`, `Approved`, `Rejected`, `Published`, `Closed`, `Archived`).
                - `registrationStatus`: Lọc theo tình trạng đăng ký (`All`, `Empty` - còn trống, `Registered` - đã có sinh viên, `Full` - đã đủ chỉ tiêu).
                - `advisorId`: Dành cho Văn phòng Khoa (`Faculty`), Quản trị viên (`Admin`) hoặc Hội đồng (`CouncilMember`) khi muốn lọc theo một GVHD cụ thể.
                - `pageNumber`: Số trang (mặc định 1).
                - `pageSize`: Kích thước trang (1 đến 100, mặc định 10).

                Yêu cầu Bearer token.
                - Vai trò `Lecturer` (GVHD): Xem danh sách đề tài do chính mình hướng dẫn.
                - Vai trò `Faculty`, `Admin` hoặc `CouncilMember`: Có quyền xem tất cả đề tài và có thể lọc theo `advisorId`.
                - Các vai trò khác (`Student`, v.v.): Bị từ chối với mã 403 Forbidden.
                - 200 OK: `PagedResult<TopicTrackingSummaryDto>`.
                - 400 Bad Request: Tham số lọc không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không đủ quyền truy cập.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<PagedResult<TopicTrackingSummaryDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // 3. GET /api/topics/{id:guid}/tracking - Xem chi tiết trạng thái theo dõi của 1 đề tài cụ thể
        group.MapGet("/{id:guid}/tracking", GetTopicTrackingDetailAsync)
            .WithSummary("Xem chi tiết trạng thái theo dõi và thông tin sinh viên đăng ký của một đề tài")
            .WithDescription("""
                Xem thông tin chi tiết một đề tài kèm trạng thái đăng ký, nhóm sinh viên và thông tin liên hệ của từng sinh viên trong nhóm.
                - `id`: Mã UUID duy nhất của đề tài.

                Yêu cầu Bearer token.
                - Vai trò `Lecturer`: Chỉ được xem đề tài do chính mình hướng dẫn. Nếu truy cập đề tài của GVHD khác sẽ bị từ chối 403 Forbidden.
                - Vai trò `Faculty`, `Admin` hoặc `CouncilMember`: Được xem chi tiết mọi đề tài trong hệ thống.
                - Các vai trò khác (`Student`, v.v.): Bị từ chối với mã 403 Forbidden.
                - 200 OK: `TopicTrackingDetailDto`.
                - 400 Bad Request: Mã đề tài không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền truy cập đề tài này.
                - 404 Not Found: Không tìm thấy đề tài.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<TopicTrackingDetailDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        // 4. POST /api/topics - GVHD soạn thảo / đề xuất đề tài mới
        group.MapPost("", CreateTopicAsync)
            .WithSummary("GVHD tạo đề tài khóa luận mới (lưu nháp Draft hoặc gửi duyệt Submitted)")
            .WithDescription("""
                Cho phép GVHD tạo mới đề tài khóa luận gồm Tên tiếng Việt, Tên tiếng Anh, Mục tiêu, Yêu cầu công nghệ và Chỉ tiêu sinh viên tối đa (`studentNumber` bắt buộc là 1 hoặc 2).
                - Nếu `submitImmediately = false` (mặc định): Đề tài được lưu ở trạng thái `Draft` (Nháp).
                - Nếu `submitImmediately = true`: Đề tài chuyển ngay sang trạng thái `Submitted` (Chờ Khoa duyệt).
                - Vai trò được phép: `Lecturer` (GVHD) hoặc `Admin`.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<TopicSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        // 5. PUT /api/topics/{id:guid} - GVHD chỉnh sửa đề tài khi ở trạng thái Draft hoặc NeedRevision
        group.MapPut("/{id:guid}", UpdateTopicAsync)
            .WithSummary("GVHD chỉnh sửa đề tài khóa luận (khi ở trạng thái Draft hoặc NeedRevision)")
            .WithDescription("""
                Cho phép GVHD chỉnh sửa thông tin đề tài do mình phụ trách khi đề tài đang ở trạng thái `Draft` (Nháp) hoặc `NeedRevision` (Khoa yêu cầu chỉnh sửa).
                - `studentNumber`: Chỉ nhận giá trị 1 hoặc 2.
                - `submitImmediately`: Nếu `true`, sau khi lưu cập nhật sẽ tự động chuyển trạng thái đề tài sang `Submitted` (Chờ duyệt).
                - Vai trò được phép: `Lecturer` (chính chủ đề tài) hoặc `Admin`.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<TopicSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        // 6. POST /api/topics/{id:guid}/submit - GVHD gửi đề tài lên Văn phòng Khoa thẩm định
        group.MapPost("/{id:guid}/submit", SubmitTopicAsync)
            .WithSummary("GVHD gửi đề tài lên Văn phòng Khoa để thẩm định (Draft / NeedRevision -> Submitted)")
            .WithDescription("""
                Chuyển trạng thái đề tài từ `Draft` (Nháp) hoặc `NeedRevision` (Yêu cầu sửa) sang `Submitted` (Chờ duyệt).
                - Vai trò được phép: `Lecturer` (chính chủ đề tài) hoặc `Admin`.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<TopicSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        // 7. POST /api/topics/{id:guid}/review - Văn phòng Khoa thẩm định đề tài
        group.MapPost("/{id:guid}/review", ReviewTopicAsync)
            .WithSummary("Văn phòng Khoa thẩm định đề tài (Submitted -> Approved / NeedRevision / Rejected)")
            .WithDescription("""
                Chỉ Văn phòng Khoa (`Faculty`) hoặc `Admin` mới có quyền thẩm định đề tài đang ở trạng thái `Submitted`.
                - `decision`: Kết quả thẩm định, chỉ chấp nhận `'Approved'` (Khoa duyệt), `'NeedRevision'` (Yêu cầu sửa), hoặc `'Rejected'` (Từ chối).
                - `reviewNote`: Lý do thẩm định (tối đa 2000 ký tự). Bắt buộc khi `decision` là `'NeedRevision'` hoặc `'Rejected'`; tùy chọn khi `'Approved'`.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<TopicSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        // 8. POST /api/topics/publish - Văn phòng Khoa công bố danh mục đề tài
        group.MapPost("/publish", PublishTopicsAsync)
            .WithSummary("Văn phòng Khoa công bố danh mục đề tài đã duyệt (Approved -> Published / IsPublished = true)")
            .WithDescription("""
                Chỉ Văn phòng Khoa (`Faculty`) hoặc `Admin` mới có quyền bấm lệnh công bố danh mục đề tài.
                - `batchId`: Mã đợt KLTN cần công bố.
                - `topicIds`: Danh sách ID đề tài cụ thể cần công bố (tùy chọn). Nếu bỏ trống hoặc truyền mảng rỗng, hệ thống sẽ công bố toàn bộ các đề tài đang ở trạng thái `Approved` trong đợt KLTN đó.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<PublishTopicsResultDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetAllTopicsAsync(
        string? keyword,
        Guid? batchId,
        int pageNumber = 1,
        int pageSize = 10,
        GetAllTopicsHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllTopicsQuery(keyword, batchId, pageNumber, pageSize);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetMyTopicsAsync(
        string? keyword,
        Guid? batchId,
        string? status,
        string? registrationStatus,
        Guid? advisorId,
        int pageNumber = 1,
        int pageSize = 10,
        GetMyTopicsHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyTopicsQuery(keyword, batchId, status, registrationStatus, advisorId, pageNumber, pageSize);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetTopicTrackingDetailAsync(
        Guid id,
        GetTopicTrackingDetailHandler handler,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTopicTrackingDetailQuery(id);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CreateTopicAsync(
        [FromBody] CreateTopicRequest request,
        CreateTopicHandler handler,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateTopicCommand(
            request.BatchId,
            request.TitleVi ?? string.Empty,
            request.TitleEn,
            request.Description,
            request.Requirements,
            request.StudentNumber,
            request.SubmitImmediately,
            request.AdvisorId);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateTopicAsync(
        Guid id,
        [FromBody] UpdateTopicRequest request,
        UpdateTopicHandler handler,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateTopicCommand(
            id,
            request.TitleVi ?? string.Empty,
            request.TitleEn,
            request.Description,
            request.Requirements,
            request.StudentNumber,
            request.SubmitImmediately);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> SubmitTopicAsync(
        Guid id,
        SubmitTopicHandler handler,
        CancellationToken cancellationToken = default)
    {
        var command = new SubmitTopicCommand(id);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ReviewTopicAsync(
        Guid id,
        [FromBody] ReviewTopicRequest request,
        ReviewTopicHandler handler,
        CancellationToken cancellationToken = default)
    {
        var command = new ReviewTopicCommand(id, request.Decision ?? string.Empty, request.ReviewNote);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> PublishTopicsAsync(
        [FromBody] PublishTopicsRequest request,
        PublishTopicsHandler handler,
        CancellationToken cancellationToken = default)
    {
        var command = new PublishTopicsCommand(request.BatchId, request.TopicIds);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    public sealed record CreateTopicRequest(
        Guid BatchId,
        string? TitleVi,
        string? TitleEn,
        string? Description,
        string? Requirements,
        int? StudentNumber,
        bool SubmitImmediately = false,
        Guid? AdvisorId = null);

    public sealed record UpdateTopicRequest(
        string? TitleVi,
        string? TitleEn,
        string? Description,
        string? Requirements,
        int? StudentNumber,
        bool SubmitImmediately = false);

    public sealed record ReviewTopicRequest(
        string? Decision,
        string? ReviewNote = null);

    public sealed record PublishTopicsRequest(
        Guid BatchId,
        IReadOnlyCollection<Guid>? TopicIds = null);
}
