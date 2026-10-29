using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.DecideAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.GetAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class AdvisorEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapAdvisorEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/thesis/advisor-evaluations", SaveAsync)
            .WithTags("DefenseCouncil.AdvisorEvaluation")
            .RequireAuthorization()
            .Accepts<SaveAdvisorEvaluationRequest>("application/json")
            .WithSummary("Tạo hoặc cập nhật phiếu chấm của GVHD")
            .WithDescription("""
                Chỉ GVHD hiện được phân công cho đề tài hoặc Admin có thể chấm báo cáo InitialFinal
                đang UnderEvaluation. Header Idempotency-Key là UUID bắt buộc: gửi lại cùng key và
                cùng nội dung sẽ trả lại đúng response đã lưu, còn dùng lại key với nội dung khác trả 409.
                Cần gửi expectedReportVersionNo; lần tạo không gửi expectedEvaluationVersion, lần sửa
                phải gửi phiên bản evaluationVersion hiện tại. Phiếu đã được duyệt sẽ bị khóa.
                """)
            .Produces<ApiResponse<AdvisorEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType);
        endpoints.MapGet("/api/thesis/groups/{groupId:guid}/advisor-evaluation", GetAsync)
            .WithTags("DefenseCouncil.AdvisorEvaluation")
            .RequireAuthorization()
            .WithSummary("Xem phiếu chấm của GVHD theo nhóm")
            .WithDescription("""
                Chỉ GVHD đang được phân công cho đề tài hoặc Admin có thể xem phiếu chấm. Kết quả
                chứa evaluationVersion để client dùng khi lưu hoặc quyết định tiếp theo. Đọc phiếu
                không tạo audit hay thay đổi trạng thái.
                """)
            .Produces<ApiResponse<AdvisorEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);
        endpoints.MapPut("/api/thesis/final-reports/{finalReportId:guid}/approval", DecideAsync)
            .WithTags("DefenseCouncil.AdvisorEvaluation")
            .RequireAuthorization()
            .Accepts<DecideAdvisorEvaluationRequest>("application/json")
            .WithSummary("Duyệt hoặc chưa đồng ý chuyển báo cáo sang phản biện")
            .WithDescription("""
                Chỉ GVHD hiện được phân công cho đề tài hoặc Admin có thể quyết định trên phiếu chấm
                chưa bị khóa của báo cáo InitialFinal đang UnderEvaluation. Body phải có
                `expectedReportVersionNo`, `expectedEvaluationVersion` và `allowsReviewerAssignment`.
                Khi chưa đồng ý, `rejectionReason` là bắt buộc. Khi đồng ý, không được gửi lý do từ
                chối; hệ thống khóa phiếu và chuyển nhóm sang ReadyForReviewerAssignment trong cùng
                transaction. Header Idempotency-Key là UUID bắt buộc: gửi lại cùng key và nội dung
                trả đúng response đã lưu, còn dùng lại key với nội dung khác trả 409.
                """)
            .Produces<ApiResponse<AdvisorEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType);
        return endpoints;
    }

    private static async Task<IResult> SaveAsync(
        HttpRequest request,
        SaveAdvisorEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        if (!TryGetIdempotencyKey(request, out var idempotencyKey))
        {
            return InvalidRequest();
        }

        SaveAdvisorEvaluationRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<SaveAdvisorEvaluationRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidRequest();
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge)
                : InvalidRequest();
        }
        catch (IOException)
        {
            return InvalidRequest();
        }

        return (await handler.HandleAsync(new SaveAdvisorEvaluationCommand(
            body?.FinalReportId ?? Guid.Empty,
            body?.ExpectedReportVersionNo ?? 0,
            body?.ExpectedEvaluationVersion,
            body?.Score ?? decimal.MinValue,
            body?.Comment,
            idempotencyKey), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> GetAsync(
        Guid groupId,
        GetAdvisorEvaluationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(groupId, cancellationToken)).ToHttpResult();

    private static async Task<IResult> DecideAsync(
        Guid finalReportId,
        HttpRequest request,
        DecideAdvisorEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        if (!TryGetIdempotencyKey(request, out var idempotencyKey))
        {
            return InvalidRequest();
        }

        DecideAdvisorEvaluationRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<DecideAdvisorEvaluationRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidRequest();
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge)
                : InvalidRequest();
        }
        catch (IOException)
        {
            return InvalidRequest();
        }

        return (await handler.HandleAsync(new DecideAdvisorEvaluationCommand(
            finalReportId,
            body?.ExpectedReportVersionNo ?? 0,
            body?.ExpectedEvaluationVersion,
            body?.AllowsReviewerAssignment,
            body?.RejectionReason,
            idempotencyKey), cancellationToken)).ToHttpResult();
    }

    private static bool TryGetIdempotencyKey(HttpRequest request, out Guid idempotencyKey)
    {
        idempotencyKey = Guid.Empty;
        return request.Headers.TryGetValue("Idempotency-Key", out var values)
               && values.Count == 1
               && Guid.TryParse(values[0], out idempotencyKey)
               && idempotencyKey != Guid.Empty;
    }

    private static IResult InvalidRequest()
        => Results.BadRequest(ApiResponse<AdvisorEvaluationDto>.Fail(ApiError.Create(
            AdvisorEvaluationErrors.InvalidRequest.Code, AdvisorEvaluationErrors.InvalidRequest.Message)));

    public sealed record SaveAdvisorEvaluationRequest(
        Guid FinalReportId,
        int ExpectedReportVersionNo,
        uint? ExpectedEvaluationVersion,
        decimal? Score,
        string? Comment);

    public sealed record DecideAdvisorEvaluationRequest(
        int ExpectedReportVersionNo,
        uint? ExpectedEvaluationVersion,
        bool? AllowsReviewerAssignment,
        string? RejectionReason);
}
