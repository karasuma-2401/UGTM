using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

public sealed class KltnBatchReader(AcademicPeriodsDbContext db) : IKltnBatchReader
{
    public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default)
        => db.KltnBatches
            .AsNoTracking()
            .AnyAsync(b => b.Id == batchId, cancellationToken);

    public async Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.KltnBatches
            .AsNoTracking()
            .Include(b => b.Stages)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var stage = batch.Stages
            .FirstOrDefault(s => s.StageCode == "REGISTRATION_AND_SYLLABUS" || s.OrderIndex == 2);

        if (stage is null)
        {
            return new BatchRegistrationWindowContractDto(
                batch.Id,
                batch.Status.ToString(),
                false,
                null);
        }

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(batch.TimeZoneId) ? "Asia/Ho_Chi_Minh" : batch.TimeZoneId);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
        var isStageActive = stage.StartDate <= localDate && nowUtc <= stage.DeadlineAtUtc;
        var isRegistrationOpen = batch.Status == BatchStatus.Open && isStageActive;

        return new BatchRegistrationWindowContractDto(
            batch.Id,
            batch.Status.ToString(),
            isRegistrationOpen,
            stage.DeadlineAtUtc);
    }

    public async Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.KltnBatches
            .AsNoTracking()
            .Include(b => b.Stages)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var stage = batch.Stages
            .FirstOrDefault(s => s.StageCode == "TOPIC_PREPARATION" || s.OrderIndex == 1);

        if (stage is null)
        {
            return new BatchTopicPreparationWindowContractDto(
                batch.Id,
                batch.Status.ToString(),
                false,
                null);
        }

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(batch.TimeZoneId) ? "Asia/Ho_Chi_Minh" : batch.TimeZoneId);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
        var isStageActive = stage.StartDate <= localDate && nowUtc <= stage.DeadlineAtUtc;
        var isPreparationOpen = batch.Status == BatchStatus.Open && isStageActive;

        return new BatchTopicPreparationWindowContractDto(
            batch.Id,
            batch.Status.ToString(),
            isPreparationOpen,
            stage.DeadlineAtUtc);
    }

    public async Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.KltnBatches
            .AsNoTracking()
            .Include(b => b.Stages)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var stage3 = batch.Stages
            .FirstOrDefault(s => s.StageCode == "PROGRESS_EXECUTION" || s.OrderIndex == 3);

        var stage4 = batch.Stages
            .FirstOrDefault(s => s.StageCode == "MIDTERM_GATE" || s.OrderIndex == 4);

        if (stage3 is null || stage4 is null)
        {
            return new BatchRenameRequestWindowContractDto(
                batch.Id,
                batch.Status.ToString(),
                false,
                stage3?.StartDate,
                stage4?.DeadlineAtUtc);
        }

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(batch.TimeZoneId) ? "Asia/Ho_Chi_Minh" : batch.TimeZoneId);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
        var isWithinWindow = localDate >= stage3.StartDate && nowUtc <= stage4.DeadlineAtUtc;
        var isRenameOpen = batch.Status == BatchStatus.Open && isWithinWindow;

        return new BatchRenameRequestWindowContractDto(
            batch.Id,
            batch.Status.ToString(),
            isRenameOpen,
            stage3.StartDate,
            stage4.DeadlineAtUtc);
    }
}
