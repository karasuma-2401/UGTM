using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class ReviewerEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapReviewerEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/thesis/reviewer-evaluations", SaveAsync)
            .WithTags("DefenseCouncil.ReviewerEvaluation")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                ReviewerEvaluationAuthorization.CanWrite(Roles(context.User))))
            .WithSummary("Lưu nháp hoặc chốt phiếu phản biện")
            .WithDescription("""
                GVPB Active/Lecturer của phân công hoặc Admin được lưu toàn bộ nội dung nháp/chốt khi đợt Open.
                Không phụ thuộc hạn nộp bài sinh viên hoặc cấu hình ngưỡng Hội đồng. Submitted khóa cả Approved và Rejected.
                Idempotency-Key là UUID bắt buộc mọi môi trường; retry cùng key và ý định trả status/phiếu gốc.
                Lần đầu trả 201; sửa/chốt nháp trả 200. Hai expected version chống ghi đè; đọc lại GET cho ý định mới.
                Không nhận query, field lạ/lặp, enum số hoặc numeric string; comment công khai sau chốt, tối đa 2.000 rune.
                """)
            .Produces<ApiResponse<ReviewerEvaluationDto>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<ReviewerEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status413PayloadTooLarge).Produces(StatusCodes.Status415UnsupportedMediaType);
        endpoints.MapGet("/api/thesis/reviewer-assignments/{assignmentId:guid}/evaluation", GetAsync)
            .WithTags("DefenseCouncil.ReviewerEvaluation")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                ReviewerEvaluationAuthorization.CanRead(Roles(context.User), context.User.FindAll("permission").Select(claim => claim.Value))))
            .WithSummary("Xem phiếu phản biện đầy đủ của phân công")
            .WithDescription("""
                GVPB Active/Lecturer đọc phiếu của mình; Admin hoặc Faculty/PĐT có thesis.reviewer-evaluations.read được tra cứu.
                Đọc không tạo nháp/audit; không yêu cầu đợt Open hoặc điều kiện ghi còn hợp lệ. Chưa có phiếu trả 404, legacy trả 409.
                Không có query selector. DTO không có điểm/nhận xét GVHD, object key, URL tải hoặc kết quả gate động.
                """)
            .Produces<ApiResponse<ReviewerEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);
        return endpoints;
    }

    private static IEnumerable<string> Roles(ClaimsPrincipal principal)
        => principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("role")).Select(claim => claim.Value);

    private static async Task<IResult> GetAsync(Guid assignmentId, HttpRequest request, GetReviewerEvaluationHandler handler, CancellationToken token)
        => request.Query.Count != 0 ? InvalidRequest() : (await handler.HandleAsync(assignmentId, token)).ToHttpResult();

    private static async Task<IResult> SaveAsync(HttpRequest request, SaveReviewerEvaluationHandler handler, CancellationToken token)
    {
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (request.Query.Count != 0 || !request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1
            || !Guid.TryParse(keys[0], out var key) || key == Guid.Empty) return InvalidRequest();
        SaveReviewerEvaluationCommand? command;
        try
        {
            using var json = await JsonDocument.ParseAsync(request.Body, new JsonDocumentOptions { MaxDepth = 16 }, token);
            command = Parse(json.RootElement, key);
        }
        catch (JsonException) { return InvalidRequest(); }
        catch (InvalidOperationException) { return InvalidRequest(); }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge) : InvalidRequest();
        }
        catch (IOException) { return InvalidRequest(); }
        if (command is null) return InvalidRequest();
        var result = await handler.HandleAsync(command, token);
        return result.IsFailure ? result.ToHttpResult()
            : Results.Json(ApiResponse<ReviewerEvaluationDto>.Ok(result.Value.Evaluation), statusCode: result.Value.StatusCode);
    }

    private static SaveReviewerEvaluationCommand? Parse(JsonElement root, Guid key)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!new[] { "assignmentId", "action", "expectedAssignmentVersion", "expectedEvaluationVersion", "score", "comment", "defenseApproval" }
                    .Contains(property.Name, StringComparer.OrdinalIgnoreCase) || !fields.TryAdd(property.Name, property.Value)) return null;
        }
        if (!fields.TryGetValue("assignmentId", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var assignmentId)
            || !fields.TryGetValue("action", out var action) || !TryEnum<ReviewerEvaluationAction>(action, out var parsedAction)
            || !fields.TryGetValue("expectedAssignmentVersion", out var version) || version.ValueKind != JsonValueKind.Number
            || !version.TryGetUInt32(out var assignmentVersion)) return null;
        uint? evaluationVersion = null;
        if (fields.TryGetValue("expectedEvaluationVersion", out var ev) && ev.ValueKind != JsonValueKind.Null)
        {
            if (ev.ValueKind != JsonValueKind.Number || !ev.TryGetUInt32(out var value)) return null;
            evaluationVersion = value;
        }
        decimal? score = null;
        if (fields.TryGetValue("score", out var sc) && sc.ValueKind != JsonValueKind.Null)
        {
            if (sc.ValueKind != JsonValueKind.Number || !sc.TryGetDecimal(out var value)) return null;
            score = value;
        }
        string? comment = null;
        if (fields.TryGetValue("comment", out var co) && co.ValueKind != JsonValueKind.Null)
        {
            if (co.ValueKind != JsonValueKind.String) return null;
            comment = co.GetString();
        }
        DefenseApprovalStatus? decision = null;
        if (fields.TryGetValue("defenseApproval", out var de) && de.ValueKind != JsonValueKind.Null)
        {
            if (!TryEnum<DefenseApprovalStatus>(de, out var value)) return null;
            decision = value;
        }
        return new(assignmentId, parsedAction, assignmentVersion, evaluationVersion, score, comment, decision, key);
    }

    private static bool TryEnum<T>(JsonElement value, out T parsed) where T : struct, Enum
    {
        parsed = default;
        return value.ValueKind == JsonValueKind.String && Enum.GetNames<T>().Contains(value.GetString()?.Trim(), StringComparer.OrdinalIgnoreCase)
            && Enum.TryParse(value.GetString()!.Trim(), true, out parsed);
    }
    private static IResult InvalidRequest() => Results.BadRequest(ApiResponse<object>.Fail(
        ApiError.Create(ReviewerEvaluationErrors.InvalidRequest.Code, ReviewerEvaluationErrors.InvalidRequest.Message)));

}
