using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetTopicTrackingDetail;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Endpoints;

public static class TopicEndpoints
{
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/topics")
            .WithTags("TopicCatalog.Topics");

        // 1. GET /api/topics/all-topics - Xem danh sách toàn bộ đề tài (công khai / không yêu cầu quyền đặc biệt)
        group.MapGet("/all-topics", GetAllTopicsAsync)
            .WithSummary("Tra cứu danh sách tất cả đề tài trong hệ thống")
            .WithDescription("""
                Lấy danh sách tất cả các đề tài KLTN trong hệ thống phục vụ tra cứu thông tin cơ bản (Tên đề tài, mục tiêu, yêu cầu, chỉ tiêu).
                - `keyword`: Tìm kiếm theo tên tiếng Việt hoặc tiếng Anh.
                - `batchId`: Lọc theo đợt KLTN.
                - `pageNumber`: Số trang (>= 1, mặc định 1).
                - `pageSize`: Số lượng đề tài trên một trang (1 đến 100, mặc định 10).

                Không yêu cầu quyền đặc thù. Tất cả người dùng đều có thể tra cứu danh mục đề tài.
                - 200 OK: Danh sách đề tài phân trang `PagedResult<TopicSummaryDto>`.
                """)
            .Produces<ApiResponse<PagedResult<TopicSummaryDto>>>(StatusCodes.Status200OK);

        // 2. GET /api/topics/my-topics - GVHD theo dõi danh sách đề tài của mình (Admin / Council có thể xem tất cả)
        group.MapGet("/my-topics", GetMyTopicsAsync)
            .WithSummary("GVHD theo dõi danh sách đề tài của mình kèm trạng thái đăng ký của sinh viên")
            .WithDescription("""
                Cho phép GVHD xem danh sách các đề tài mình phụ trách, kèm trạng thái đăng ký hiện tại
                (đã có sinh viên đăng ký chưa, còn trống bao nhiêu chỉ tiêu) và thông tin sinh viên đã đăng ký.
                - `keyword`: Tìm kiếm theo tên đề tài tiếng Việt/tiếng Anh.
                - `batchId`: Lọc theo đợt KLTN.
                - `status`: Lọc theo trạng thái đề tài (`Draft`, `Submitted`, `Approved`, `Published`, `Closed`, v.v.).
                - `registrationStatus`: Lọc theo tình trạng đăng ký (`All`, `Empty` - còn trống, `Registered` - đã có sinh viên, `Full` - đã đủ chỉ tiêu).
                - `advisorId`: Dành riêng cho Quản trị viên (Admin) hoặc Hội đồng (CouncilMember) khi muốn xem đề tài của một GVHD cụ thể.
                - `pageNumber`: Số trang (mặc định 1).
                - `pageSize`: Kích thước trang (1 đến 100, mặc định 10).

                Yêu cầu Bearer token.
                - Vai trò `Lecturer` (GVHD): Xem danh sách đề tài do chính mình hướng dẫn.
                - Vai trò `Admin` hoặc `CouncilMember`: Có quyền xem tất cả đề tài (Bypass) và có thể lọc theo `advisorId`.
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
                - Vai trò `Admin` hoặc `CouncilMember`: Được xem chi tiết mọi đề tài trong hệ thống.
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
}
