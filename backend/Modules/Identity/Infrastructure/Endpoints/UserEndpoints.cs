using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Application.Users.ImportUsers;
using UGTM.Modules.Identity.Application.Users.UpdateUser;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class UserEndpoints
{
    private const long MaxImportFileBytes = 5 * 1024 * 1024;

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

        group.MapPost("/import", ImportUsersAsync)
            .WithSummary("Import danh sách Sinh viên hoặc Giảng viên từ Excel")
            .WithDescription("""
                Tạo hàng loạt tài khoản Sinh viên hoặc Giảng viên từ request `multipart/form-data`:
                - `file`: Tệp `.xlsx`, tối đa 5 MB và 5.000 dòng dữ liệu.
                - `role`: Chỉ nhận `Student` hoặc `Lecturer`.
                - Dòng tiêu đề bắt buộc gồm `Email`, `FullName`, `UserCode`, `PhoneNumber`.
                - `UserCode` bắt buộc đối với Sinh viên và có thể để trống đối với Giảng viên.

                Import tuân theo nguyên tắc all-or-nothing. Nếu bất kỳ dòng nào sai định dạng, trùng trong tệp
                hoặc trùng với dữ liệu hiện có, toàn bộ thao tác bị hủy và response chỉ rõ lỗi theo dòng/cột.
                Tài khoản được tạo ở trạng thái `PendingActivation` và không gửi email ngay;
                người dùng chủ động yêu cầu Magic Link qua endpoint `activation/resend`.

                Yêu cầu Bearer token có quyền `identity.users.import` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm tổng số dòng đã xử lý và số tài khoản đã tạo.
                - Tệp rỗng, sai định dạng, vượt giới hạn hoặc vai trò không hỗ trợ: 400 Bad Request.
                - Dữ liệu trong tệp không hợp lệ hoặc bị trùng: 400 Bad Request (`Identity.ImportValidationFailed`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                - Request vượt quá giới hạn kích thước: 413 Payload Too Large.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersImport}")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxImportFileBytes + 64 * 1024))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ApiResponse<ImportUsersResult>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<ImportUsersResult>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        group.MapGet("/import-template", DownloadImportTemplate)
            .WithSummary("Tải file Excel mẫu để import người dùng")
            .WithDescription("""
                Tải tệp Excel mẫu theo loại tài khoản để chuẩn bị dữ liệu cho endpoint import.
                Query `role` chỉ nhận `Student` hoặc `Lecturer`. Tệp `.xlsx` trả về gồm bốn cột
                `Email`, `FullName`, `UserCode`, `PhoneNumber` và sheet hướng dẫn tương ứng với vai trò đã chọn.

                Yêu cầu Bearer token có quyền `identity.users.import` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK với tệp Excel theo đúng cấu trúc import.
                - Thiếu, sai hoặc không hỗ trợ giá trị `role`: 400 Bad Request (`Identity.InvalidImportRole`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityUsersImport}")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .Produces<ApiResponse<ImportUsersResult>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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
                - Cố tình gỡ vai trò Admin của Admin cuối cùng: 400 Bad Request (`Identity.CannotRemoveLastAdminRole`).
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
                - Cố tình tạm ngưng hoặc vô hiệu hóa Admin đang hoạt động cuối cùng: 400 Bad Request (`Identity.CannotDisableLastAdmin`).
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

    private static async Task<IResult> ImportUsersAsync(
        [FromForm(Name = "file")] IFormFile? file,
        [FromForm(Name = "role")] string? role,
        ImportUsersHandler handler,
        CancellationToken cancellationToken)
    {
        if (file is null
            || file.Length == 0
            || file.Length > MaxImportFileBytes
            || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ImportFailure(IdentityErrors.InvalidImportFile, null);
        }

        await using var stream = file.OpenReadStream();
        var result = await handler.HandleAsync(
            new ImportUsersCommand(stream, file.FileName, role ?? string.Empty),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        if (result.Value.Errors.Count > 0)
        {
            var validationErrors = result.Value.Errors
                .GroupBy(error => error.RowNumber == 0
                    ? error.Field
                    : $"rows[{error.RowNumber}].{error.Field}")
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Message).Distinct().ToArray());
            return ImportFailure(IdentityErrors.ImportValidationFailed, validationErrors);
        }

        return result.ToHttpResult();
    }

    private static IResult ImportFailure(
        UGTM.BuildingBlocks.Domain.Error error,
        IDictionary<string, string[]>? validationErrors)
        => Results.Json(
            ApiResponse<ImportUsersResult>.Fail(new ApiError
            {
                Code = error.Code,
                Message = error.Message,
                Errors = validationErrors
            }),
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult DownloadImportTemplate(
        string? role,
        IUserImportTemplateGenerator generator)
    {
        var roleInput = role?.Trim();
        if (int.TryParse(roleInput, out _)
            || !Enum.TryParse<SystemRoleName>(roleInput, ignoreCase: true, out var parsedRole)
            || !Enum.IsDefined(parsedRole)
            || parsedRole is not (SystemRoleName.Student or SystemRoleName.Lecturer))
        {
            return ImportFailure(IdentityErrors.InvalidImportRole, null);
        }

        var content = generator.Generate(parsedRole);
        return Results.File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ugtm-{parsedRole.ToString().ToLowerInvariant()}-import-template.xlsx");
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
