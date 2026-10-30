using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentBatchReader(AcademicPeriodsDbContext context) : IReviewerAssignmentBatchReader
{
    public async Task<ReviewerAssignmentBatchContractDto?> GetAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await context.KltnBatches.AsNoTracking().SingleOrDefaultAsync(value => value.Id == batchId, cancellationToken);
        return batch is null ? null : new(batch.Id, batch.Status == BatchStatus.Open);
    }

    public async Task<ReviewerAssignmentBatchContractDto?> LockAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        var batch = await context.KltnBatches.FromSqlInterpolated($"""
            SELECT b.*, b.xmin FROM academic.kltn_batch b WHERE b.id = {batchId} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return batch is null ? null : new(batch.Id, batch.Status == BatchStatus.Open);
    }
}
