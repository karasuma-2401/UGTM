namespace UGTM.Modules.AcademicPeriods.Contracts.Batches;

public sealed record BatchSummaryContractDto(Guid BatchId, string BatchName);

public interface IBatchSummaryReader
{
    Task<IReadOnlyDictionary<Guid, BatchSummaryContractDto>> GetByIdsAsync(
        IEnumerable<Guid> batchIds,
        CancellationToken cancellationToken = default);
}
