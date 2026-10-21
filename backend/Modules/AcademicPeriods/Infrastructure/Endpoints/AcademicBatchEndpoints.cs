using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Application.Batches.ChangeKltnBatchStatus;
using UGTM.Modules.AcademicPeriods.Application.Batches.CreateKltnBatch;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Batches.Queries;
using UGTM.Modules.AcademicPeriods.Application.Batches.UpdateKltnBatchSchedule;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Endpoints;

public static class AcademicBatchEndpoints
{
    private const string ReadPolicy = "Permission:" + PermissionCodes.AcademicBatchesRead;
    private const string ManagePolicy = "Permission:" + PermissionCodes.AcademicBatchesManage;

    public static IEndpointRouteBuilder MapAcademicBatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/academic-periods/batches")
            .WithTags("AcademicPeriods.Batches");

        group.MapPost("", CreateAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Tạo đợt KLTN và tự sinh lịch 7 giai đoạn")
            .WithDescription("""
                Tạo một đợt khóa luận mới theo năm học, học kỳ và ngày bắt đầu.
                Hệ thống lấy phiên bản schedule template đang hoạt động theo `templateCode`
                (mặc định `UIT_KLTN_STANDARD`), tự tính ngày kết thúc và materialize đúng 7 giai đoạn.

                Yêu cầu Bearer token và permission `academic.batches.manage`.
                Permission này mặc định chỉ được cấp cho `Admin` và `AcademicAffairs` (Phòng Đào tạo).
                - 200 OK: Tạo thành công và trả về chi tiết batch cùng 7 stage.
                - 400 Bad Request: Tên batch, năm học, học kỳ hoặc ngày bắt đầu không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền quản lý đợt KLTN.
                - 409 Conflict: Năm học/học kỳ đã tồn tại hoặc không có template đang hoạt động.
                """)
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu danh sách đợt KLTN")
            .WithDescription("""
                Lấy danh sách đợt khóa luận có phân trang và hỗ trợ lọc theo:
                - `academicYear`: Năm học, ví dụ `2026-2027`.
                - `semester`: Học kỳ, phải là số nguyên dương.
                - `status`: `Draft`, `Open`, `Closed` hoặc `Archived`.
                - `pageNumber`: Số trang, mặc định 1.
                - `pageSize`: Số phần tử mỗi trang, mặc định 20 và tối đa 100.

                Yêu cầu Bearer token và permission `academic.batches.read`.
                Permission đọc được cấp mặc định cho tất cả vai trò hệ thống.
                - 200 OK: Danh sách batch dạng `PagedResult<KltnBatchSummaryDto>`.
                - 400 Bad Request: Bộ lọc học kỳ hoặc trạng thái không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền tra cứu đợt KLTN.
                """)
            .Produces<ApiResponse<PagedResult<KltnBatchSummaryDto>>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{batchId:guid}", GetByIdAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu chi tiết đợt KLTN")
            .WithDescription("""
                Lấy thông tin chi tiết của một đợt khóa luận, bao gồm năm học, học kỳ,
                khoảng thời gian, timezone, trạng thái, schedule template version và toàn bộ 7 stage
                được sắp xếp theo `orderIndex`.

                Yêu cầu Bearer token và permission `academic.batches.read`.
                - 200 OK: Chi tiết batch và lịch 7 giai đoạn.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền tra cứu đợt KLTN.
                - 404 Not Found: Không tìm thấy batch theo `batchId`.
                """)
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapPut("/{batchId:guid}/schedule", UpdateScheduleAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Thay thế nguyên bộ lịch 7 giai đoạn")
            .WithDescription("""
                Thay thế atomically toàn bộ lịch của một đợt KLTN. Request phải gửi đúng 7 stage;
                `orderIndex` phải liên tục từ 1 đến 7, `stageCode` không trùng, các khoảng ngày phải
                phủ kín batch, liền nhau, không chồng lấn và deadline phải thuộc stage tương ứng.

                Batch ở trạng thái `Open` bắt buộc có `changeReason`. Batch `Closed` hoặc `Archived`
                không được thay đổi lịch. Thay đổi thành công được ghi audit outbox cùng transaction.

                Yêu cầu Bearer token và permission `academic.batches.manage` (`Admin`/`AcademicAffairs`).
                - 200 OK: Lịch được cập nhật và trả về chi tiết batch mới nhất.
                - 400 Bad Request: Lịch 7 stage hoặc change reason không hợp lệ.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền quản lý đợt KLTN.
                - 404 Not Found: Không tìm thấy batch.
                - 409 Conflict: Trạng thái batch không cho phép cập nhật lịch.
                """)
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/{batchId:guid}/open", OpenAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Mở đợt KLTN để bắt đầu vận hành")
            .WithDescription("""
                Chuyển batch từ `Draft` sang `Open`. Batch chỉ được mở khi có lịch đầy đủ đúng 7 stage
                và đã có một score configuration `Active` thuộc chính batch đó. Thao tác thành công
                được ghi audit outbox trong cùng transaction.

                Yêu cầu Bearer token và permission `academic.batches.manage` (`Admin`/`AcademicAffairs`).
                - 200 OK: Batch được mở thành công.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền quản lý đợt KLTN.
                - 404 Not Found: Không tìm thấy batch.
                - 409 Conflict: Batch không ở trạng thái Draft hoặc chưa có cấu hình điểm Active.
                """)
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/{batchId:guid}/close", CloseAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Đóng một đợt KLTN đang hoạt động")
            .WithDescription("""
                Chuyển batch từ `Open` sang `Closed`. Chỉ batch đang mở mới được đóng;
                không hỗ trợ chuyển ngược trạng thái. Thao tác thành công được ghi audit outbox
                trong cùng transaction.

                Yêu cầu Bearer token và permission `academic.batches.manage` (`Admin`/`AcademicAffairs`).
                - 200 OK: Batch được đóng thành công.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền quản lý đợt KLTN.
                - 404 Not Found: Không tìm thấy batch.
                - 409 Conflict: Batch không ở trạng thái Open.
                """)
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/{batchId:guid}/archive", ArchiveAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Lưu trữ một đợt KLTN đã đóng")
            .WithDescription("""
                Chuyển batch từ `Closed` sang `Archived`. Đây là bước cuối của lifecycle và không hỗ trợ
                khôi phục hoặc chuyển ngược trạng thái. Thao tác thành công được ghi audit outbox
                trong cùng transaction.

                Yêu cầu Bearer token và permission `academic.batches.manage` (`Admin`/`AcademicAffairs`).
                - 200 OK: Batch được lưu trữ thành công.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền quản lý đợt KLTN.
                - 404 Not Found: Không tìm thấy batch.
                - 409 Conflict: Batch không ở trạng thái Closed.
                """)
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{batchId:guid}/timeline", GetTimelineAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu timeline và trạng thái của 7 giai đoạn")
            .WithDescription("""
                Trả về lịch tổng quát của batch và trạng thái thời gian của từng stage:
                `Upcoming`, `Current` hoặc `Completed`. Trạng thái được tính theo ngày hiện tại tại
                `TimeZoneId` của batch và quy ước khoảng ngày đóng `[StartDate, EndDate]`.

                Yêu cầu Bearer token và permission `academic.batches.read`.
                - 200 OK: Timeline đầy đủ của batch.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền tra cứu đợt KLTN.
                - 404 Not Found: Không tìm thấy batch.
                """)
            .Produces<ApiResponse<KltnBatchTimelineDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapGet("/{batchId:guid}/current-stage", GetCurrentStageAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu giai đoạn hiện tại và thời gian còn lại")
            .WithDescription("""
                Xác định stage hiện tại theo ngày tại timezone của batch. Khi đang trong một stage,
                response trả deadline UTC, cờ `isDeadlinePassed` và `remainingSeconds` được chặn tối thiểu
                ở 0. Nếu ngày hiện tại nằm ngoài toàn bộ lịch, `currentStage` được trả về `null`.

                Yêu cầu Bearer token và permission `academic.batches.read`.
                - 200 OK: Thông tin stage hiện tại hoặc `currentStage: null`.
                - 401 Unauthorized: Chưa xác thực.
                - 403 Forbidden: Không có quyền tra cứu đợt KLTN.
                - 404 Not Found: Không tìm thấy batch.
                """)
            .Produces<ApiResponse<CurrentStageResultDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateKltnBatchRequest request,
        CreateKltnBatchHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new CreateKltnBatchCommand(
                request.BatchName ?? string.Empty,
                request.AcademicYear ?? string.Empty,
                request.Semester,
                request.StartDate,
                request.TemplateCode ?? CreateKltnBatchHandler.DefaultTemplateCode),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetAllAsync(
        string? academicYear,
        int? semester,
        string? status,
        int? pageNumber,
        int? pageSize,
        GetKltnBatchesHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetKltnBatchesQuery(academicYear, semester, status, pageNumber ?? 1, pageSize ?? 20),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetByIdAsync(
        Guid batchId,
        GetKltnBatchByIdHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetKltnBatchByIdQuery(batchId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateScheduleAsync(
        Guid batchId,
        [FromBody] UpdateKltnBatchScheduleRequest request,
        UpdateKltnBatchScheduleHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new UpdateKltnBatchScheduleCommand(
                batchId,
                request.Stages ?? [],
                request.ChangeReason),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> OpenAsync(
        Guid batchId,
        OpenKltnBatchHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new OpenKltnBatchCommand(batchId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> CloseAsync(
        Guid batchId,
        CloseKltnBatchHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new CloseKltnBatchCommand(batchId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> ArchiveAsync(
        Guid batchId,
        ArchiveKltnBatchHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new ArchiveKltnBatchCommand(batchId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetTimelineAsync(
        Guid batchId,
        GetKltnBatchTimelineHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetKltnBatchTimelineQuery(batchId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetCurrentStageAsync(
        Guid batchId,
        GetCurrentStageHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetCurrentStageQuery(batchId), cancellationToken)).ToHttpResult();

    public sealed record CreateKltnBatchRequest(
        string? BatchName,
        string? AcademicYear,
        int Semester,
        DateOnly StartDate,
        string? TemplateCode = null);

    public sealed record UpdateKltnBatchScheduleRequest(
        IReadOnlyCollection<UpdateStageScheduleItem>? Stages,
        string? ChangeReason);
}
