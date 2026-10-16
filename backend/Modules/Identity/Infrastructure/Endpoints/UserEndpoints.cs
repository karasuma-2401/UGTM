using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.Dtos;
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
                - `pageNumber`: Số trang (>= 1, mặc định 1).
                - `pageSize`: Kích thước trang (1 đến 100, mặc định 10).
                - `keyword`: Tìm kiếm không phân biệt hoa thường theo email, họ tên, mã người dùng.
                - `role`: Tên vai trò (`Student`, `Lecturer`, `Admin`, `Faculty`, `AcademicAffairs`, `CouncilMember`). Chuỗi số (ví dụ: '999') hoặc vai trò không hợp lệ sẽ trả về lỗi 400.
                - `status`: Trạng thái (`PendingActivation`, `Active`, `Suspended`, `Disabled`). Chuỗi số (ví dụ: '999') hoặc trạng thái không hợp lệ sẽ trả về lỗi 400.

                Yêu cầu Bearer token có quyền `identity.users.read` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm `PagedResult<UserSummaryDto>`.
                - Tham số lọc không hợp lệ: 400 Bad Request (`Identity.InvalidRole` hoặc `Identity.InvalidStatus`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền (không có quyền `identity.users.read` và không phải Admin): 403 Forbidden.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersRead}")
            .Produces<ApiResponse<PagedResult<UserSummaryDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/", CreateUserAsync)
            .WithSummary("Tạo tài khoản người dùng đơn lẻ")
            .WithDescription("""
                Tạo một tài khoản người dùng mới và gán các vai trò ban đầu.
                - Nếu không cung cấp `initialPassword` và `sendActivationEmail` = true: tài khoản ở trạng thái `PendingActivation`
                  và hệ thống sẽ gửi email Magic Link kích hoạt (thời hạn 24 giờ qua Transactional Outbox).
                - Nếu cung cấp `initialPassword`: mật khẩu được kiểm tra độ mạnh (tối thiểu 12 ký tự, đủ chữ hoa, thường, số, ký tự đặc biệt),
                  băm mật khẩu và kích hoạt tài khoản `Active` ngay lập tức.
                - `roles`: Danh sách tên vai trò hợp lệ. Chuỗi số (ví dụ: '999', '0') hoặc tên vai trò lạ bị từ chối với lỗi 400 `Identity.InvalidRole`.
                - `phoneNumber`: Nếu cung cấp, phải đúng định dạng số điện thoại Việt Nam (10 chữ số).

                Yêu cầu Bearer token có quyền `identity.users.manage` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm `UserSummaryDto`.
                - Thiếu thông tin bắt buộc, sai định dạng email hoặc vượt quá độ dài tối đa: 400 Bad Request (`Identity.InvalidInput`).
                - Vai trò không hợp lệ hoặc chuỗi số: 400 Bad Request (`Identity.InvalidRole`).
                - Số điện thoại sai định dạng: 400 Bad Request (`Identity.InvalidPhoneNumber`).
                - Mật khẩu yếu: 400 Bad Request (`Identity.WeakPassword`).
                - Trùng email hoặc mã người dùng: 409 Conflict (`Identity.DuplicateAccount`).
                - Thiếu token: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersManage}")
            .Produces<ApiResponse<UserSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateUserAsync)
            .WithSummary("Cập nhật thông tin hồ sơ và vai trò người dùng")
            .WithDescription("""
                Cập nhật họ tên, số điện thoại, mã người dùng và danh sách vai trò cho tài khoản.
                Nếu cập nhật vai trò, Security Stamp sẽ được làm mới tự động để thu hồi token cũ và buộc nhận claims mới ở phiên kế tiếp.
                - `roles`: Danh sách tên vai trò hợp lệ. Chuỗi số hoặc vai trò không hợp lệ sẽ trả lỗi 400 `Identity.InvalidRole`.
                - `phoneNumber`: Nếu cung cấp, phải đúng định dạng 10 chữ số.

                Yêu cầu Bearer token có quyền `identity.users.manage` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm `UserSummaryDto`.
                - Họ tên rỗng hoặc vượt quá độ dài tối đa: 400 Bad Request (`Identity.InvalidInput`).
                - Vai trò không hợp lệ hoặc chuỗi số: 400 Bad Request (`Identity.InvalidRole`).
                - Số điện thoại sai định dạng: 400 Bad Request (`Identity.InvalidPhoneNumber`).
                - Không tìm thấy user: 404 Not Found (`Identity.UserNotFound`).
                - Trùng mã người dùng với tài khoản khác: 409 Conflict (`Identity.DuplicateAccount`).
                - Thiếu token: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersManage}")
            .Produces<ApiResponse<UserSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", UpdateStatusAsync)
            .WithSummary("Cập nhật trạng thái tài khoản (Khóa / Mở khóa / Tạm ngưng)")
            .WithDescription("""
                Chuyển đổi trạng thái tài khoản sang `Active`, `Suspended` hoặc `Disabled`.
                Khi chuyển sang `Suspended` hoặc `Disabled`, Security Stamp tự động thay đổi khiến mọi phiên đăng nhập
                và Access Token hiện tại của tài khoản này bị vô hiệu hóa ngay lập tức.
                - `status`: Chỉ chấp nhận 'Active', 'Suspended', 'Disabled'. Chuỗi số (ví dụ: '999') hoặc giá trị không xác định sẽ bị từ chối với lỗi 400 `Identity.InvalidStatus`.

                Yêu cầu Bearer token có quyền `identity.users.manage` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm `UserSummaryDto`.
                - Trạng thái rỗng, chuỗi số hoặc không hợp lệ: 400 Bad Request (`Identity.InvalidStatus`).
                - Chuyển đổi trạng thái không hợp lệ (ví dụ tài khoản đang PendingActivation): 400 Bad Request (`Identity.InvalidStatusTransition`).
                - Không tìm thấy user: 404 Not Found (`Identity.UserNotFound`).
                - Thiếu token: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersManage}")
            .Produces<ApiResponse<UserSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

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
        [FromBody] CreateUserRequest? request,
        CreateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request?.Email ?? string.Empty,
            request?.FullName ?? string.Empty,
            request?.UserCode,
            request?.PhoneNumber,
            request?.Roles ?? [],
            request?.InitialPassword,
            request?.SendActivationEmail ?? true);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid id,
        [FromBody] UpdateUserRequest? request,
        UpdateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            id,
            request?.FullName ?? string.Empty,
            request?.UserCode,
            request?.PhoneNumber,
            request?.Roles);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid id,
        [FromBody] UpdateUserStatusRequest? request,
        UpdateUserStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserStatusCommand(id, request?.Status ?? string.Empty);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }
}
