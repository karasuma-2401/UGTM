namespace UGTM.Modules.AcademicPeriods.Contracts.Batches;

public sealed record BatchRegistrationWindowContractDto(
    Guid BatchId,
    string BatchStatus,
    bool IsRegistrationStageActive,
    DateTime? StageDeadlineAtUtc);

public sealed record BatchTopicPreparationWindowContractDto(
    Guid BatchId,
    string BatchStatus,
    bool IsTopicPreparationStageActive,
    DateTime? StageDeadlineAtUtc);

public sealed record BatchRenameRequestWindowContractDto(
    Guid BatchId,
    string BatchStatus,
    bool IsRenameWindowOpen,
    DateOnly? WindowStartDate,
    DateTime? WindowDeadlineAtUtc);

public interface IKltnBatchReader
{
    Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
