using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Application.Users.UpdateUser;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class UserEndpoints
{
    public sealed record CreateUserRequest(
        string Email,
        string FullName,
        string? UserCode,
        string? PhoneNumber,
        IReadOnlyList<string> Roles,
        string? InitialPassword = null,
        bool SendActivationEmail = true);

    public sealed record UpdateUserRequest(
        string FullName,
        string? UserCode,
        string? PhoneNumber,
        IReadOnlyList<string>? Roles);

    public sealed record UpdateUserStatusRequest(string Status);

    public static IEndpointRouteBuilder MapIdentityUserEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users")
            .WithTags("Identity.Users");

        group.MapGet("/", GetUsersAsync)
            .WithSummary("Tra cứu và phân trang danh sách người dùng")
            .WithDescription("""
                Lấy danh sách người dùng có hỗ trợ phân trang, tìm kiếm theo từ khóa (Email, Họ tên, Mã người dùng)
                và lọc theo vai trò (Role), trạng thái tài khoản (Status).

                Yêu cầu Bearer token có quyền `identity.users.read`.
                Thiếu token hoặc token không hợp lệ: 401; không đủ quyền: 403.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersRead}");

        group.MapPost("/", CreateUserAsync)
            .WithSummary("Tạo tài khoản người dùng đơn lẻ")
            .WithDescription("""
                Tạo một tài khoản người dùng mới và gán các vai trò ban đầu.
                - Nếu không cung cấp `initialPassword` và `sendActivationEmail` = true: tài khoản ở trạng thái `PendingActivation`
                  và hệ thống sẽ gửi email Magic Link kích hoạt (thời hạn 24 giờ).
                - Nếu cung cấp `initialPassword`: mật khẩu được kiểm tra độ mạnh, băm và kích hoạt tài khoản `Active` ngay lập tức.

                Yêu cầu Bearer token có quyền `identity.users.manage`.
                Thiếu thông tin bắt buộc: 400 `Identity.InvalidInput`; vai trò sai: 400 `Identity.InvalidRole`;
                số điện thoại không hợp lệ: 400 `Identity.InvalidPhoneNumber`;
                mật khẩu yếu: 400 `Identity.WeakPassword`; trùng email hoặc mã: 409 `Identity.DuplicateAccount`.
                Thiếu token: 401; không đủ quyền: 403.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersManage}");

        group.MapPut("/{id:guid}", UpdateUserAsync)
            .WithSummary("Cập nhật thông tin hồ sơ và vai trò người dùng")
            .WithDescription("""
                Cập nhật họ tên, số điện thoại, mã người dùng và danh sách vai trò cho tài khoản.
                Nếu cập nhật vai trò, Security Stamp sẽ được làm mới để người dùng phải nhận lại claims ở phiên tiếp theo.

                Yêu cầu Bearer token có quyền `identity.users.manage`.
                Họ tên rỗng: 400 `Identity.InvalidInput`; vai trò sai: 400 `Identity.InvalidRole`;
                số điện thoại không hợp lệ: 400 `Identity.InvalidPhoneNumber`;
                không tìm thấy user: 404 `Identity.UserNotFound`; trùng mã người dùng: 409 `Identity.DuplicateAccount`.
                Thiếu token: 401; không đủ quyền: 403.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersManage}");

        group.MapPatch("/{id:guid}/status", UpdateStatusAsync)
            .WithSummary("Cập nhật trạng thái tài khoản (Khóa / Mở khóa / Tạm ngưng)")
            .WithDescription("""
                Chuyển đổi trạng thái tài khoản sang `Active`, `Suspended` hoặc `Disabled`.
                Khi chuyển sang `Suspended` hoặc `Disabled`, Security Stamp tự động thay đổi khiến mọi phiên đăng nhập
                và Access Token hiện tại của tài khoản này bị vô hiệu hóa ngay lập tức.

                Yêu cầu Bearer token có quyền `identity.users.manage`.
                Trạng thái rỗng hoặc sai: 400 `Identity.InvalidStatus`;
                chuyển đổi trạng thái không hợp lệ (ví dụ tài khoản chờ kích hoạt): 400 `Identity.InvalidStatusTransition`;
                không tìm thấy user: 404 `Identity.UserNotFound`.
                Thiếu token: 401; không đủ quyền: 403.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersManage}");

        return endpoints;
    }

    private static async Task<IResult> GetUsersAsync(
        string? keyword,
        string? role,
        string? status,
        int pageNumber = 1,
        int pageSize = 10,
        GetUsersHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUsersQuery(keyword, role, status, pageNumber, pageSize);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        CreateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request.Email,
            request.FullName,
            request.UserCode,
            request.PhoneNumber,
            request.Roles,
            request.InitialPassword,
            request.SendActivationEmail);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid id,
        UpdateUserRequest request,
        UpdateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            id,
            request.FullName,
            request.UserCode,
            request.PhoneNumber,
            request.Roles);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
        UpdateUserStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserStatusCommand(id, request.Status);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }
}
