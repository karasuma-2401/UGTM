using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class PlagiarismScanEndpoints
{
    public static IEndpointRouteBuilder MapPlagiarismScanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/thesis/final-reports/{finalReportId:guid}/similarity-report", GetReportAsync)
            .WithTags("ThesisManagement.Plagiarism")
            .RequireAuthorization()
            .WithSummary("Lấy báo cáo đối chiếu đạo văn / biên nhận tương đồng của báo cáo cuối kỳ")
            .WithDescription("""
                Truy xuất kết quả kiểm tra tương đồng học thuật qua AI Service.
                - Sinh viên trong nhóm: Chỉ nhận Biên nhận đối soát (Receipt) che toàn bộ phát hiện trùng lặp và văn bản nguồn.
                - Giảng viên hướng dẫn / Khoa / Admin: Nhận toàn bộ báo cáo phân tích chi tiết (Full Report) bao gồm chỉ số tương đồng và danh sách trùng lặp.
                - Hội đồng đánh giá: Chỉ được xem báo cáo chi tiết khi báo cáo đã chuyển sang giai đoạn chấm (UnderEvaluation).
                400: finalReportId rỗng; 401: chưa đăng nhập; 403: không có quyền xem; 404: không tìm thấy báo cáo/lượt quét; 409: lượt quét chưa hoàn thành.
                """)
            .Produces<ApiResponse<object>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        endpoints.MapPost("/api/thesis/final-reports/{finalReportId:guid}/similarity-scan/retry", RetryScanAsync)
            .WithTags("ThesisManagement.Plagiarism")
            .RequireAuthorization()
            .WithSummary("Yêu cầu quét lại kiểm tra tương đồng cho báo cáo cuối kỳ")
            .WithDescription("""
                Cho phép GVHD hoặc Quản trị viên kích hoạt quét lại lượt kiểm tra đạo văn khi có lỗi hoặc cập nhật nguồn dữ liệu.
                Chỉ thực hiện được khi lượt quét trước đã ở trạng thái Completed, Failed hoặc ComputeUnavailable.
                400: finalReportId rỗng; 401: chưa đăng nhập; 403: không có quyền (chỉ GVHD đề tài/Admin); 404: không tìm thấy; 409: đang xử lý hoặc chưa nộp.
                """)
            .Produces<ApiResponse<object>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        endpoints.MapPost("/api/thesis/similarity-scans/resume-pending", ResumePendingScansAsync)
            .WithTags("ThesisManagement.Plagiarism")
            .RequireAuthorization()
            .Accepts<ResumePendingScansRequest>("application/json")
            .WithSummary("Kích hoạt khôi phục hàng loạt các lượt quét đang chờ tài nguyên tính toán GPU")
            .WithDescription("""
                Khôi phục và kích hoạt quét lại hàng loạt cho các lượt quét đang bị treo hoặc ở trạng thái ComputeUnavailable khi worker GPU (Google Colab) được bật lại.
                Chỉ Quản trị viên hoặc Trưởng khoa có quyền thực hiện.
                401: chưa đăng nhập; 403: không có quyền Quản trị viên/Khoa.
                """)
            .Produces<ApiResponse<ResumePendingPlagiarismScansResult>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> GetReportAsync(
        [FromRoute] Guid finalReportId,
        [FromQuery] int? versionNo,
        [FromServices] GetPlagiarismReportHandler handler,
        CancellationToken cancellationToken)
    {
        if (finalReportId == Guid.Empty)
        {
            return Results.Json(
                ApiResponse<object>.Fail(ApiError.Create("PlagiarismScan.InvalidRequest", "Mã báo cáo cuối kỳ không được rỗng.")),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await handler.HandleAsync(new GetPlagiarismReportQuery(finalReportId, versionNo), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        var data = result.Value.IsReceiptOnly
            ? (object)result.Value.Receipt!
            : result.Value.FullReport!;

        return Results.Ok(ApiResponse<object>.Ok(data));
    }

    private static async Task<IResult> RetryScanAsync(
        [FromRoute] Guid finalReportId,
        [FromQuery] int? versionNo,
        [FromServices] RetryPlagiarismScanHandler handler,
        CancellationToken cancellationToken)
    {
        if (finalReportId == Guid.Empty)
        {
            return Results.Json(
                ApiResponse<object>.Fail(ApiError.Create("PlagiarismScan.InvalidRequest", "Mã báo cáo cuối kỳ không được rỗng.")),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await handler.HandleAsync(new RetryPlagiarismScanCommand(finalReportId, versionNo), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        var data = result.Value.IsReceiptOnly
            ? (object)result.Value.Receipt!
            : result.Value.FullReport!;

        return Results.Ok(ApiResponse<object>.Ok(data));
    }

    private static async Task<IResult> ResumePendingScansAsync(
        [FromBody] ResumePendingScansRequest? request,
        [FromServices] ResumePendingPlagiarismScansHandler handler,
        CancellationToken cancellationToken)
    {
        var limit = request?.Limit ?? 50;
        var result = await handler.HandleAsync(new ResumePendingPlagiarismScansCommand(limit), cancellationToken);
        return result.ToHttpResult();
    }
}

public sealed record ResumePendingScansRequest(int? Limit);
