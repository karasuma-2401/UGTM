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
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu danh sách đợt KLTN")
            .Produces<ApiResponse<PagedResult<KltnBatchSummaryDto>>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{batchId:guid}", GetByIdAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu chi tiết đợt KLTN")
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapPut("/{batchId:guid}/schedule", UpdateScheduleAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Thay thế nguyên bộ lịch 7 giai đoạn")
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPost("/{batchId:guid}/open", OpenAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Mở đợt KLTN")
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPost("/{batchId:guid}/close", CloseAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Đóng đợt KLTN")
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPost("/{batchId:guid}/archive", ArchiveAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Lưu trữ đợt KLTN")
            .Produces<ApiResponse<KltnBatchDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapGet("/{batchId:guid}/timeline", GetTimelineAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu timeline và trạng thái từng giai đoạn")
            .Produces<ApiResponse<KltnBatchTimelineDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapGet("/{batchId:guid}/current-stage", GetCurrentStageAsync)
            .RequireAuthorization(ReadPolicy)
            .WithSummary("Tra cứu giai đoạn hiện tại và thời gian còn lại")
            .Produces<ApiResponse<CurrentStageResultDto>>()
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
