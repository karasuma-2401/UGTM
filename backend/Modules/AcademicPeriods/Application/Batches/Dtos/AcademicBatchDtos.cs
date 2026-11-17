using UGTM.BuildingBlocks.Application.Responses;

namespace UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;

public sealed record StageDto(
    Guid Id,
    int OrderIndex,
    string StageCode,
    string StageName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime DeadlineAtUtc,
    bool IsOverridden);

public sealed record KltnBatchSummaryDto(
    Guid Id,
    string BatchName,
    string AcademicYear,
    int Semester,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record KltnBatchDetailDto(
    Guid Id,
    string BatchName,
    string AcademicYear,
    int Semester,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    Guid? ScheduleTemplateVersionId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<StageDto> Stages);

public sealed record StageTimelineDto(
    Guid Id,
    int OrderIndex,
    string StageCode,
    string StageName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime DeadlineAtUtc,
    bool IsOverridden,
    string State);

public sealed record KltnBatchTimelineDto(
    Guid BatchId,
    string BatchName,
    string Status,
    DateOnly CurrentLocalDate,
    string TimeZoneId,
    IReadOnlyList<StageTimelineDto> Stages);

public sealed record CurrentStageDto(
    Guid Id,
    int OrderIndex,
    string StageCode,
    string StageName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime DeadlineAtUtc,
    bool IsOverridden,
    bool IsDeadlinePassed,
    long RemainingSeconds);

public sealed record CurrentStageResultDto(
    Guid BatchId,
    string BatchName,
    DateOnly CurrentLocalDate,
    string TimeZoneId,
    CurrentStageDto? CurrentStage);

public interface IAcademicBatchQueryReader
{
    Task<PagedResult<KltnBatchSummaryDto>> GetBatchesAsync(
        string? academicYear,
        int? semester,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<KltnBatchDetailDto?> GetByIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
