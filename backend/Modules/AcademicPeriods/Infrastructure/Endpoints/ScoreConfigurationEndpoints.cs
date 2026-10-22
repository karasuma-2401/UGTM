using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.CreateDraft;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Publish;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Queries;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.UpdateDraft;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Endpoints;

public static class ScoreConfigurationEndpoints
{
    private const string ReadPolicy = "Permission:" + PermissionCodes.AcademicScoreConfigurationsRead;
    private const string ManagePolicy = "Permission:" + PermissionCodes.AcademicScoreConfigurationsManage;

    public static IEndpointRouteBuilder MapScoreConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/academic-periods/batches/{batchId:guid}/score-configurations")
            .WithTags("AcademicPeriods.ScoreConfigurations");

        group.MapPost("/drafts", CreateDraftAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Tạo Draft đầu tiên hoặc clone cấu hình điểm Active")
            .WithDescription("""
                Tạo một version cấu hình điểm ở trạng thái `Draft` cho batch.
                Nếu batch chưa có cấu hình Active, request phải cung cấp `calculationMethod` và `components`
                để tạo version 1. Nếu đã có Active, hệ thống clone thành version kế tiếp; `changeReason`
                là bắt buộc và client có thể gửi method/components mới để thay thế ngay trên bản clone.

                Mỗi batch chỉ được có tối đa một Draft. Phương thức `Percentage` yêu cầu tổng trọng số
                đúng 100; `IntegerCoefficient` chỉ nhận hệ số nguyên không âm với tổng lớn hơn 0.

                Yêu cầu Bearer token và permission `academic.score-config.manage` (`Admin`/`AcademicAffairs`).
                - 200 OK: Draft được tạo thành công.
                - 400 Bad Request: Công thức, component hoặc change reason không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền cấu hình điểm.
                - 404 Not Found: Không tìm thấy batch.
                - 409 Conflict: Batch đã có một Draft.
                """)
            .Produces<ApiResponse<ScoreConfigurationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPut("/drafts/{configurationId:guid}", UpdateDraftAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Cập nhật công thức và thành phần của một Draft")
            .WithDescription("""
                Thay thế toàn bộ phương thức tính và danh sách component của một cấu hình Draft.
                Active/Retired là bất biến và không được sửa trực tiếp. Version lớn hơn 1 bắt buộc
                duy trì `changeReason` để truy vết lý do thay đổi.

                Component code và order phải duy nhất; điểm sàn để trống hoặc nằm trong `0..10`.
                Validation trọng số/hệ số được áp dụng trên toàn bộ cấu hình trước khi lưu.

                Yêu cầu Bearer token và permission `academic.score-config.manage` (`Admin`/`AcademicAffairs`).
                - 200 OK: Draft được cập nhật thành công.
                - 400 Bad Request: Cấu hình không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền cấu hình điểm.
                - 404 Not Found: Không tìm thấy configuration trong batch.
                - 409 Conflict: Configuration không còn ở trạng thái Draft.
                """)
            .Produces<ApiResponse<ScoreConfigurationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPost("/{configurationId:guid}/publish", PublishAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Publish một Draft thành cấu hình điểm Active")
            .WithDescription("""
                Publish configuration đang ở trạng thái `Draft`. Nếu batch đã có một Active,
                version cũ được chuyển sang `Retired` và Draft mới trở thành `Active` trong cùng
                unit of work. Cả sự kiện retire và publish đều được ghi audit outbox.

                Yêu cầu Bearer token và permission `academic.score-config.manage` (`Admin`/`AcademicAffairs`).
                - 200 OK: Version mới được publish thành công.
                - 400 Bad Request: Dữ liệu configuration không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền cấu hình điểm.
                - 404 Not Found: Không tìm thấy configuration trong batch.
                - 409 Conflict: Configuration không ở trạng thái Draft.
                """)
            .Produces<ApiResponse<ScoreConfigurationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapGet("/active", GetActiveAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu cấu hình điểm Active của một batch")
            .WithDescription("""
                Trả về version cấu hình điểm đang được sử dụng cho batch, bao gồm phương thức tính,
                version, metadata publish và toàn bộ component theo `orderIndex`.

                Yêu cầu Bearer token và permission `academic.score-config.read`.
                - 200 OK: Cấu hình Active hiện tại.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền xem cấu hình điểm.
                - 404 Not Found: Batch không tồn tại hoặc chưa có cấu hình Active.
                """)
            .Produces<ApiResponse<ScoreConfigurationDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapGet("/{configurationId:guid}", GetVersionAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu một version cấu hình điểm")
            .WithDescription("""
                Lấy một version cụ thể theo `configurationId`, kể cả Draft, Active hoặc Retired,
                để phục vụ chỉnh sửa có kiểm soát và truy vết lịch sử.

                Yêu cầu Bearer token và permission `academic.score-config.read`.
                - 200 OK: Chi tiết version cấu hình điểm.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền xem cấu hình điểm.
                - 404 Not Found: Batch hoặc configuration không tồn tại.
                """)
            .Produces<ApiResponse<ScoreConfigurationDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapGet("/history", GetHistoryAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu lịch sử version cấu hình điểm")
            .WithDescription("""
                Trả về tất cả version cấu hình điểm của batch theo `versionNo` giảm dần,
                bao gồm Draft hiện tại, Active và các version Retired để audit hoặc tái tạo kết quả.

                Yêu cầu Bearer token và permission `academic.score-config.read`.
                - 200 OK: Danh sách version; có thể là mảng rỗng nếu batch chưa từng cấu hình điểm.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền xem cấu hình điểm.
                - 404 Not Found: Batch không tồn tại.
                """)
            .Produces<ApiResponse<IReadOnlyList<ScoreConfigurationDto>>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateDraftAsync(
        Guid batchId,
        [FromBody] CreateScoreConfigurationDraftRequest request,
        CreateScoreConfigurationDraftHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new CreateScoreConfigurationDraftCommand(
                batchId,
                request.CalculationMethod,
                request.Components,
                request.ChangeReason),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateDraftAsync(
        Guid batchId,
        Guid configurationId,
        [FromBody] UpdateScoreConfigurationDraftRequest request,
        UpdateScoreConfigurationDraftHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new UpdateScoreConfigurationDraftCommand(
                batchId,
                configurationId,
                request.CalculationMethod ?? string.Empty,
                request.Components ?? [],
                request.ChangeReason),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> PublishAsync(
        Guid batchId,
        Guid configurationId,
        PublishScoreConfigurationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new PublishScoreConfigurationCommand(batchId, configurationId),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetActiveAsync(
        Guid batchId,
        GetActiveScoreConfigurationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetActiveScoreConfigurationQuery(batchId),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetVersionAsync(
        Guid batchId,
        Guid configurationId,
        GetScoreConfigurationVersionHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetScoreConfigurationVersionQuery(batchId, configurationId),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetHistoryAsync(
        Guid batchId,
        GetScoreConfigurationHistoryHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetScoreConfigurationHistoryQuery(batchId),
            cancellationToken)).ToHttpResult();

    public sealed record CreateScoreConfigurationDraftRequest(
        string? CalculationMethod,
        IReadOnlyCollection<ScoreComponentInput>? Components,
        string? ChangeReason);

    public sealed record UpdateScoreConfigurationDraftRequest(
        string? CalculationMethod,
        IReadOnlyCollection<ScoreComponentInput>? Components,
        string? ChangeReason);
}
