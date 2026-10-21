using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Application.Batches.Queries;

public sealed class GetKltnBatchesHandler(
    IAcademicBatchQueryReader queryReader,
    ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<KltnBatchSummaryDto>>> HandleAsync(
        GetKltnBatchesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanReadBatches(currentUser))
        {
            return Result.Failure<PagedResult<KltnBatchSummaryDto>>(AcademicBatchErrors.AccessDenied);
        }

        if (query.Semester is <= 0
            || (!string.IsNullOrWhiteSpace(query.Status)
                && (!Enum.TryParse<BatchStatus>(query.Status.Trim(), true, out var parsedStatus)
                    || !Enum.IsDefined(parsedStatus))))
        {
            return Result.Failure<PagedResult<KltnBatchSummaryDto>>(AcademicBatchErrors.InvalidRequest);
        }

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var result = await queryReader.GetBatchesAsync(
            string.IsNullOrWhiteSpace(query.AcademicYear) ? null : query.AcademicYear.Trim(),
            query.Semester,
            string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            pageNumber,
            pageSize,
            cancellationToken);

        return Result.Success(result);
    }
}

public sealed class GetKltnBatchByIdHandler(
    IAcademicBatchQueryReader queryReader,
    ICurrentUser currentUser)
{
    public async Task<Result<KltnBatchDetailDto>> HandleAsync(
        GetKltnBatchByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanReadBatches(currentUser))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.AccessDenied);
        }

        if (query.BatchId == Guid.Empty)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidRequest);
        }

        var batch = await queryReader.GetByIdAsync(query.BatchId, cancellationToken);
        return batch is null
            ? Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.NotFound)
            : Result.Success(batch);
    }
}

public sealed class GetKltnBatchTimelineHandler(
    IAcademicBatchQueryReader queryReader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<KltnBatchTimelineDto>> HandleAsync(
        GetKltnBatchTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanReadBatches(currentUser))
        {
            return Result.Failure<KltnBatchTimelineDto>(AcademicBatchErrors.AccessDenied);
        }

        var batch = query.BatchId == Guid.Empty
            ? null
            : await queryReader.GetByIdAsync(query.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result.Failure<KltnBatchTimelineDto>(AcademicBatchErrors.NotFound);
        }

        var localDate = CurrentLocalDate(batch.TimeZoneId, timeProvider.GetUtcNow());
        var stages = batch.Stages
            .Select(stage => new StageTimelineDto(
                stage.Id,
                stage.OrderIndex,
                stage.StageCode,
                stage.StageName,
                stage.StartDate,
                stage.EndDate,
                stage.DeadlineAtUtc,
                stage.IsOverridden,
                localDate < stage.StartDate
                    ? "Upcoming"
                    : localDate > stage.EndDate
                        ? "Completed"
                        : "Current"))
            .ToArray();

        return Result.Success(new KltnBatchTimelineDto(
            batch.Id,
            batch.BatchName,
            batch.Status,
            localDate,
            batch.TimeZoneId,
            stages));
    }

    internal static DateOnly CurrentLocalDate(string timeZoneId, DateTimeOffset utcNow)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, timeZone).DateTime);
    }
}

public sealed class GetCurrentStageHandler(
    IAcademicBatchQueryReader queryReader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<CurrentStageResultDto>> HandleAsync(
        GetCurrentStageQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanReadBatches(currentUser))
        {
            return Result.Failure<CurrentStageResultDto>(AcademicBatchErrors.AccessDenied);
        }

        var batch = query.BatchId == Guid.Empty
            ? null
            : await queryReader.GetByIdAsync(query.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result.Failure<CurrentStageResultDto>(AcademicBatchErrors.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        var localDate = GetKltnBatchTimelineHandler.CurrentLocalDate(batch.TimeZoneId, now);
        var stage = batch.Stages.SingleOrDefault(item => localDate >= item.StartDate && localDate <= item.EndDate);
        CurrentStageDto? currentStage = null;
        if (stage is not null)
        {
            var remainingSeconds = Math.Max(0L, (long)Math.Ceiling((stage.DeadlineAtUtc - now.UtcDateTime).TotalSeconds));
            currentStage = new CurrentStageDto(
                stage.Id,
                stage.OrderIndex,
                stage.StageCode,
                stage.StageName,
                stage.StartDate,
                stage.EndDate,
                stage.DeadlineAtUtc,
                stage.IsOverridden,
                now.UtcDateTime > stage.DeadlineAtUtc,
                remainingSeconds);
        }

        return Result.Success(new CurrentStageResultDto(
            batch.Id,
            batch.BatchName,
            localDate,
            batch.TimeZoneId,
            currentStage));
    }
}
