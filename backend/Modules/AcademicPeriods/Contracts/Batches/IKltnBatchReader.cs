namespace UGTM.Modules.AcademicPeriods.Contracts.Batches;

public sealed record BatchRegistrationWindowContractDto(
    Guid BatchId,
    string BatchStatus,
    bool IsRegistrationStageActive,
    DateTime? StageDeadlineAtUtc);

public interface IKltnBatchReader
{
    Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
