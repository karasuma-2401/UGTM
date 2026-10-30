using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class ReviewerAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapReviewerAssignmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/thesis/reviewer-assignments", CreateAsync)
            .WithTags("DefenseCouncil.ReviewerAssignment")
            .RequireManagement()
            .Accepts<CreateReviewerAssignmentRequest>("application/json")
            .WithSummary("Phân công giảng viên phản biện cho nhóm đã được duyệt")
            .WithDescription("""
                Faculty/PĐT có quyền quản lý hoặc Admin được tạo một phân công active cho mỗi nhóm.
                Đợt phải Open và báo cáo phải khớp binding GVHD đã duyệt; GVPB là Lecturer đang hoạt động,
                khác GVHD hiện tại. Không phụ thuộc giai đoạn hoặc hạn nộp bài của sinh viên.
                Idempotency-Key là UUID bắt buộc ở mọi môi trường. Retry giữ cùng key/body để nhận receipt gốc.
                """)
            .Produces<ApiResponse<ReviewerAssignmentDto>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType);
        endpoints.MapDelete("/api/thesis/reviewer-assignments/{id:guid}", CancelAsync)
            .WithTags("DefenseCouncil.ReviewerAssignment")
            .RequireManagement()
            .Accepts<CancelReviewerAssignmentRequest>("application/json")
            .WithSummary("Hủy mềm phân công phản biện chưa có phiếu chấm")
            .WithDescription("""
                Faculty/PĐT có quyền quản lý hoặc Admin được hủy phân công active chưa có bất kỳ phiếu chấm nào.
                Body JSON trên DELETE phải có expectedAssignmentVersion hiện tại và reason từ 1–1000 ký tự sau trim.
                Idempotency-Key là UUID bắt buộc. Retry cùng key/body trả receipt gốc; key mới trên dòng đã hủy trả 409.
                Giữ lịch sử, snapshot và người/thời điểm tạo; ghi người/thời điểm hủy cùng audit trong một transaction.
                Không yêu cầu đợt còn Open hoặc điều kiện tạo còn hợp lệ. Phân công lại bằng POST mới, không bật lại dòng cũ.
                """)
            .Produces<ApiResponse<ReviewerAssignmentDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType);
        return endpoints;
    }

    private static RouteHandlerBuilder RequireManagement(this RouteHandlerBuilder endpoint)
        => endpoint.RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
            ReviewerAssignmentAuthorization.CanManage(
                context.User.FindAll(ClaimTypes.Role).Concat(context.User.FindAll("role")).Select(claim => claim.Value),
                context.User.FindAll("permission").Select(claim => claim.Value))));

    private static bool TryGetIdempotencyKey(HttpRequest request, out Guid key)
    {
        key = Guid.Empty;
        return request.Headers.TryGetValue("Idempotency-Key", out var keys) && keys.Count == 1
            && Guid.TryParse(keys[0], out key) && key != Guid.Empty;
    }

    private static async Task<IResult> CreateAsync(HttpRequest request, CreateReviewerAssignmentHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (!TryGetIdempotencyKey(request, out var key))
            return InvalidRequest();

        CreateReviewerAssignmentRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<CreateReviewerAssignmentRequest>(cancellationToken);
        }
        catch (JsonException) { return InvalidRequest(); }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge) : InvalidRequest();
        }
        catch (IOException) { return InvalidRequest(); }

        var result = await handler.HandleAsync(new(body?.GroupId ?? Guid.Empty, body?.ReviewerId ?? Guid.Empty,
            body?.ExpectedAdvisorEvaluationId ?? Guid.Empty, body?.ExpectedReportVersionNo ?? 0, key), cancellationToken);
        return result.IsSuccess
            ? Results.Json(ApiResponse<ReviewerAssignmentDto>.Ok(result.Value), statusCode: StatusCodes.Status201Created)
            : result.ToHttpResult();
    }

    private static async Task<IResult> CancelAsync(Guid id, HttpRequest request, CancelReviewerAssignmentHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (!TryGetIdempotencyKey(request, out var key)) return InvalidRequest();
        CancelReviewerAssignmentRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<CancelReviewerAssignmentRequest>(cancellationToken);
        }
        catch (JsonException) { return InvalidRequest(); }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge) : InvalidRequest();
        }
        catch (IOException) { return InvalidRequest(); }

        return (await handler.HandleAsync(new(id, body?.ExpectedAssignmentVersion ?? 0, body?.Reason, key),
            cancellationToken)).ToHttpResult();
    }

    private static IResult InvalidRequest() => Results.BadRequest(ApiResponse<ReviewerAssignmentDto>.Fail(
        ApiError.Create(ReviewerAssignmentErrors.InvalidRequest.Code, ReviewerAssignmentErrors.InvalidRequest.Message)));

    public sealed record CreateReviewerAssignmentRequest(Guid GroupId, Guid ReviewerId,
        Guid ExpectedAdvisorEvaluationId,
        [property: JsonNumberHandling(JsonNumberHandling.Strict)] int ExpectedReportVersionNo);

    public sealed record CancelReviewerAssignmentRequest(
        [property: JsonNumberHandling(JsonNumberHandling.Strict)] uint ExpectedAssignmentVersion, string? Reason);
}
