using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Contracts.Reminders;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Reminders;

internal sealed class AcademicDeadlineSnapshotReader(AcademicPeriodsDbContext db)
    : IAcademicDeadlineSnapshotReader
{
    public async Task<IReadOnlyList<OpenBatchReminderSnapshotDto>> GetOpenBatchesAsync(
        CancellationToken cancellationToken = default)
    {
        var batches = await db.KltnBatches
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.Open)
            .Include(b => b.Stages)
            .OrderBy(b => b.StartDate)
            .ToListAsync(cancellationToken);

        return batches
            .Select(b => new OpenBatchReminderSnapshotDto(
                b.Id,
                b.BatchName,
                b.TimeZoneId,
                b.Stages
                    .OrderBy(s => s.OrderIndex)
                    .Select(s => new OpenBatchStageReminderDto(
                        s.OrderIndex,
                        s.StageCode,
                        s.StageName,
                        s.DeadlineAtUtc))
                    .ToArray()))
            .ToArray();
    }
}
