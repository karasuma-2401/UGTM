namespace UGTM.Modules.AcademicPeriods.Contracts.Batches;

public interface IKltnBatchReader
{
    Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default);
}
