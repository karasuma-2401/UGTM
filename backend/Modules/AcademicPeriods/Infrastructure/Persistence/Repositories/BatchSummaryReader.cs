using UGTM.Modules.AcademicPeriods.Contracts.Batches;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

internal sealed class BatchSummaryReader(AcademicPeriodsDbContext db) : IBatchSummaryReader
{
    public async Task<IReadOnlyDictionary<Guid, BatchSummaryContractDto>> GetByIdsAsync(
        IEnumerable<Guid> batchIds,
        CancellationToken cancellationToken = default)
    {
        var ids = batchIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, BatchSummaryContractDto>();

        return await db.KltnBatches.AsNoTracking()
            .Where(batch => ids.Contains(batch.Id))
            .Select(batch => new BatchSummaryContractDto(batch.Id, batch.BatchName))
            .ToDictionaryAsync(batch => batch.BatchId, cancellationToken);
    }
}
