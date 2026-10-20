using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

public sealed class KltnBatchReader(AcademicPeriodsDbContext db) : IKltnBatchReader
{
    public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default)
        => db.KltnBatchs
            .AsNoTracking()
            .AnyAsync(b => b.Id == batchId, cancellationToken);
}
