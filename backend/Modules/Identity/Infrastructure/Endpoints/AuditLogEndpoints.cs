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
            .WithSummary("Tra cứu và phân trang nhật ký kiểm toán hệ thống")
            .WithDescription("""
                Lấy danh sách Audit Log có hỗ trợ phân trang và kết hợp nhiều điều kiện lọc:
                - `actorUserId`: ID người dùng thực hiện thao tác.
                - `action`: Mã hành động nghiệp vụ, ví dụ `Identity.User.Created`, `Identity.Authentication.Login`
                  hoặc `Identity.Session.RefreshTokenReuseDetected`.
                - `targetType`: Loại đối tượng chịu tác động, ví dụ `User`, `Role`, `AuthenticationSession`
                  hoặc `Authentication` đối với request ẩn danh chưa xác định được tài khoản.
                - `outcome`: Kết quả xử lý, chỉ nhận `Success`, `Denied` hoặc `Failed` và không phân biệt hoa thường.
                - `fromUtc`: Thời điểm bắt đầu theo UTC, được tính trong khoảng kết quả.
                - `toUtc`: Thời điểm kết thúc theo UTC, là cận trên không bao gồm.
                - `pageNumber`: Số trang; giá trị nhỏ hơn 1 được chuẩn hóa thành 1.
                - `pageSize`: Kích thước trang; giá trị nhỏ hơn 1 dùng mặc định 20 và giá trị lớn hơn 100 được giới hạn về 100.

                Chỉ người dùng có vai trò `Admin` được phép truy cập endpoint này.
                - Thành công: 200 OK kèm `PagedResult<AuditLogDto>`.
                - Khoảng thời gian không phải UTC, `fromUtc` lớn hơn `toUtc` hoặc `outcome` không hợp lệ:
                  400 Bad Request (`Identity.InvalidAuditLogFilter`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Người dùng không có vai trò Admin: 403 Forbidden.
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
