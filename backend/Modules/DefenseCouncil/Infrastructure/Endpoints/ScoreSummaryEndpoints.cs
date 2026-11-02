using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class ScoreSummaryEndpoints
{
    public sealed record CalculateRequest(uint? ExpectedSummaryVersion, int? ExpectedActiveConfigVersion);
    public static IEndpointRouteBuilder MapScoreSummaryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/scores").RequireAuthorization().WithTags("DefenseCouncil.ScoreSummary");
        group.MapPost("/calculate/{groupId}", CalculateAsync).Accepts<CalculateRequest>("application/json")
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024))
            .WithSummary("Faculty/Admin tính điểm dự thảo theo cấu hình Active và nguồn đã xác minh")
            .WithDescription("Idempotency-Key UUID bắt buộc. expectedSummaryVersion là xmin, null chỉ khi chưa có summary; expectedActiveConfigVersion tùy chọn. Đợt Open/Closed; đủ chấm, biên bản Locked và ArchiveProof hợp lệ. Lưu revision/provenance bất biến; không fallback công thức cố định. Replay kiểm tra quyền live, trả DTO gốc; Published chặn ý định mới.")
            .Produces<ApiResponse<ScoreSummaryDto>>().Produces<ApiResponse<object>>(400).Produces(401).Produces<ApiResponse<object>>(403).Produces<ApiResponse<object>>(404).Produces<ApiResponse<object>>(409).Produces(413).Produces(415);
        group.MapGet("/group/{groupId}", async (string groupId, HttpRequest request, ReadScoreSummaryHandler handler, CancellationToken token) =>
            !Clean(request, []) || !Id(groupId, out var id) ? Invalid() : (await handler.DetailAsync(id, token)).ToHttpResult())
            .WithSummary("Quản trị đọc bảng điểm private, stale và lịch sử revision").Produces<ApiResponse<ScoreSummaryDto>>();
        group.MapGet("/readiness/{groupId}", async (string groupId, HttpRequest request, ReadScoreSummaryHandler handler, CancellationToken token) =>
            !Clean(request, []) || !Id(groupId, out var id) ? Invalid() : (await handler.ReadinessAsync(id, token)).ToHttpResult())
            .WithSummary("Quản trị đọc điều kiện hiện tại; GET không cấp proof để ghi").Produces<ApiResponse<ScoreReadinessDto>>();
        group.MapGet("", ListAsync).WithSummary("Bảng điểm đợt gồm nhóm Pending; lọc trước phân trang, đọc nguồn theo lô").Produces<ApiResponse<ScorePageDto>>();
        return endpoints;
    }
    private static async Task<IResult> CalculateAsync(string groupId, HttpRequest request, CalculateScoreSummaryHandler handler, CancellationToken token)
    {
        if (await handler.AuthorizeAsync(token) is { } denied) return ResultFailure(denied.Code, denied.Message, denied.Type == BuildingBlocks.Domain.ErrorType.Unauthorized ? 401 : 403);
        if (!Id(groupId, out var id) || request.Query.Count != 0 || request.Headers["Idempotency-Key"].Count != 1 || !Id(request.Headers["Idempotency-Key"], out var key)) return Invalid();
        if (!request.HasJsonContentType()) return Results.StatusCode(415);
        if (request.ContentLength > 16 * 1024) return Results.StatusCode(413);
        uint? expected = null; int? config = null;
        try
        {
            using var body = new MemoryStream(); var buffer = new byte[4096]; int read;
            while ((read = await request.Body.ReadAsync(buffer, token)) > 0) { if (body.Length + read > 16 * 1024) return Results.StatusCode(413); await body.WriteAsync(buffer.AsMemory(0, read), token); }
            using var json = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return Invalid();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in json.RootElement.EnumerateObject())
            {
                if (!names.Add(p.Name)) return Invalid();
                if (p.Name.Equals("expectedSummaryVersion", StringComparison.OrdinalIgnoreCase))
                { if (p.Value.ValueKind != JsonValueKind.Null) { if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetUInt32(out var value) || value == 0) return Invalid(); expected = value; } }
                else if (p.Name.Equals("expectedActiveConfigVersion", StringComparison.OrdinalIgnoreCase))
                { if (p.Value.ValueKind != JsonValueKind.Null) { if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetInt32(out var value) || value <= 0) return Invalid(); config = value; } }
                else return Invalid();
            }
        }
        catch (BadHttpRequestException e) { return e.StatusCode == 413 ? Results.StatusCode(413) : Invalid(); }
        catch (JsonException) { return Invalid(); }
        return (await handler.HandleAsync(new(id, expected, config, key), token)).ToHttpResult();
    }
    private static async Task<IResult> ListAsync(HttpRequest request, ReadScoreSummaryHandler handler, CancellationToken token)
    {
        if (!Clean(request, ["batchId", "status", "isStale", "isReadyForPublication", "pageNumber", "pageSize"]) || !Id(request.Query["batchId"], out var batch)) return Invalid();
        var page = 1; var size = 20;
        if (request.Query.TryGetValue("pageNumber", out var p) && (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1)) return Invalid();
        if (request.Query.TryGetValue("pageSize", out var s) && (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out size) || size is < 1 or > 100)) return Invalid();
        bool? stale = null; bool? ready = null;
        foreach (var name in new[] { "isStale", "isReadyForPublication" }) if (request.Query.TryGetValue(name, out var raw))
        { if (raw.ToString() is not ("true" or "false")) return Invalid(); if (name == "isStale") stale = raw == "true"; else ready = raw == "true"; }
        return (await handler.ListAsync(batch, request.Query["status"].FirstOrDefault(), stale, ready, page, size, token)).ToHttpResult();
    }
    private static bool Id(string? raw, out Guid value) => Guid.TryParse(raw, out value) && value != Guid.Empty;
    private static bool Clean(HttpRequest request, string[] fields) => !(request.ContentLength > 0) && !request.Headers.ContainsKey("Transfer-Encoding") && request.Query.All(p => fields.Contains(p.Key, StringComparer.Ordinal) && p.Value.Count == 1);
    private static IResult Invalid() => ResultFailure(ScoreSummaryErrors.InvalidRequest.Code, ScoreSummaryErrors.InvalidRequest.Message, 400);
    private static IResult ResultFailure(string code, string message, int status) => Results.Json(ApiResponse<object>.Fail(ApiError.Create(code, message)), statusCode: status);
}
