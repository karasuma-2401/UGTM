using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapIdentityAuditLogEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/audit-logs", GetAuditLogsAsync)
            .WithTags("Identity.AuditLogs")
            .WithSummary("Tra cứu nhật ký hệ thống")
            .WithDescription("""
                Tra cứu Audit Log theo người thực hiện, hành động, loại đối tượng,
                kết quả và khoảng thời gian UTC. Chỉ người dùng có vai trò Admin
                được phép truy cập. `toUtc` là cận trên không bao gồm.
                """)
            .RequireAuthorization("AdminOnly")
            .Produces<ApiResponse<PagedResult<AuditLogDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> GetAuditLogsAsync(
        Guid? actorUserId,
        string? action,
        string? targetType,
        string? outcome,
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber = 1,
        int pageSize = 20,
        GetAuditLogsHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuditLogsQuery(
            actorUserId,
            action,
            targetType,
            outcome,
            fromUtc,
            toUtc,
            pageNumber,
            pageSize);

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }
}
