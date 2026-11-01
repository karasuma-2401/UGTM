using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class ReviewerAssignmentReadEndpoints
{
    public static IEndpointRouteBuilder MapReviewerAssignmentReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        Describe<ReviewerAssignmentListItemDto>(endpoints.MapGet("/api/thesis/reviewer-assignments", ListAsync)
            .RequireRead(), "Tra cứu phân công và lịch sử phản biện");
        Describe<ReviewerAssignmentEligibilityItemDto>(endpoints.MapGet("/api/thesis/reviewer-assignment-eligibility", EligibilityAsync)
            .RequireRead(), "Nhóm đủ điều kiện phân công theo đợt đang mở");
        Describe<ReviewerCandidateContractDto>(endpoints.MapGet("/api/thesis/reviewer-candidates", CandidatesAsync)
            .RequireRead(), "Giảng viên Active có Lecturer, khác GVHD hiện tại");
        Describe<ReviewerAssignmentTaskDto>(endpoints.MapGet("/api/thesis/me/reviewer-assignments", TasksAsync)
            .RequireAuthorization(), "Nhiệm vụ phản biện của tài khoản Active có Lecturer hiện tại")
            .WithDescription("""
                Server xác minh tài khoản và Lecturer grant hiện tại, luôn lọc assignment active của chính mình.
                Không nhận reviewerId/includeHistory/isActive. Metadata chỉ của đúng báo cáo đã duyệt;
                canDownloadReport là gợi ý tại lúc đọc, endpoint download kiểm tra lại. Không trả điểm/phiếu,
                lý do hủy, object key hoặc URL tải sẵn. Đợt đóng tự nó không chặn nhiệm vụ/tải báo cáo.
                """);
        return endpoints;
    }

    private static RouteHandlerBuilder Describe<T>(RouteHandlerBuilder builder, string summary)
        => builder.WithTags("DefenseCouncil.ReviewerAssignment").WithSummary(summary)
            .Produces<ApiResponse<PagedResult<T>>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

    private static RouteHandlerBuilder RequireRead(this RouteHandlerBuilder builder)
        => builder.RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
            ReviewerAssignmentAuthorization.CanRead(
                context.User.FindAll(ClaimTypes.Role).Concat(context.User.FindAll("role")).Select(claim => claim.Value),
                context.User.FindAll("permission").Select(claim => claim.Value))));

    private static async Task<IResult> ListAsync(HttpRequest request, ReviewerAssignmentReadHandler handler, CancellationToken token)
    {
        var query = new ReadQuery(request.Query);
        var batch = query.Guid("batchId"); var group = query.Guid("groupId"); var reviewer = query.Guid("reviewerId");
        var active = query.Boolean("isActive"); var history = query.Boolean("includeHistory") ?? false;
        var keyword = query.Text("keyword"); var page = query.Integer("pageNumber", 1); var size = query.Integer("pageSize", 20);
        return query.IsValid ? (await handler.ListAsync(new(batch, group, reviewer, active, history, keyword, page, size), token)).ToHttpResult() : InvalidRequest();
    }

    private static async Task<IResult> EligibilityAsync(HttpRequest request, ReviewerAssignmentReadHandler handler, CancellationToken token)
    {
        var query = new ReadQuery(request.Query);
        var batch = query.Guid("batchId"); var assigned = query.Boolean("includeAssigned") ?? false;
        var keyword = query.Text("keyword"); var page = query.Integer("pageNumber", 1); var size = query.Integer("pageSize", 20);
        return query.IsValid ? (await handler.EligibilityAsync(new(batch ?? System.Guid.Empty, assigned, keyword, page, size), token)).ToHttpResult() : InvalidRequest();
    }

    private static async Task<IResult> CandidatesAsync(HttpRequest request, ReviewerAssignmentReadHandler handler, CancellationToken token)
    {
        var query = new ReadQuery(request.Query);
        var group = query.Guid("groupId"); var keyword = query.Text("keyword");
        var page = query.Integer("pageNumber", 1); var size = query.Integer("pageSize", 20);
        return query.IsValid ? (await handler.CandidatesAsync(new(group ?? System.Guid.Empty, keyword, page, size), token)).ToHttpResult() : InvalidRequest();
    }

    private static async Task<IResult> TasksAsync(HttpRequest request, ReviewerAssignmentReadHandler handler, CancellationToken token)
    {
        var query = new ReadQuery(request.Query);
        if (request.Query.Keys.Any(key => new[] { "reviewerId", "includeHistory", "isActive" }.Contains(key, StringComparer.OrdinalIgnoreCase))) return InvalidRequest();
        var batch = query.Guid("batchId"); var keyword = query.Text("keyword");
        var page = query.Integer("pageNumber", 1); var size = query.Integer("pageSize", 20);
        return query.IsValid ? (await handler.TasksAsync(new(batch, keyword, page, size), token)).ToHttpResult() : InvalidRequest();
    }

    private static IResult InvalidRequest() => Results.BadRequest(ApiResponse<object>.Fail(
        ApiError.Create(ReviewerAssignmentErrors.InvalidRequest.Code, ReviewerAssignmentErrors.InvalidRequest.Message)));

    private sealed class ReadQuery(IQueryCollection query)
    {
        public bool IsValid { get; private set; } = query.All(pair => pair.Value.Count == 1);
        public string? Text(string name) => query.TryGetValue(name, out var value) ? value.ToString() : null;
        public Guid? Guid(string name)
        {
            if (!query.ContainsKey(name)) return null;
            if (System.Guid.TryParse(Text(name), out var value)) return value;
            IsValid = false; return null;
        }
        public bool? Boolean(string name)
        {
            if (!query.ContainsKey(name)) return null;
            if (bool.TryParse(Text(name), out var value)) return value;
            IsValid = false; return null;
        }
        public int Integer(string name, int fallback)
        {
            if (!query.ContainsKey(name)) return fallback;
            if (int.TryParse(Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value;
            IsValid = false; return fallback;
        }
    }
}
