using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
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
}
