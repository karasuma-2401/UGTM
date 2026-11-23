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

public sealed record BatchFinalReportSubmissionWindowContractDto(
    Guid BatchId,
    string BatchStatus,
    bool IsSubmissionWindowOpen,
    DateTime? WindowOpensAtUtc,
    DateTime? WindowDeadlineAtUtc);

public sealed record BatchReviewGateStageWindowContractDto(
    Guid BatchId,
    string BatchStatus,
    bool IsStageWindowOpen,
    DateOnly? StageStartDate,
    DateTime? StageDeadlineAtUtc);

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

    Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<BatchReviewGateStageWindowContractDto?> GetSyllabusSubmissionWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult<BatchReviewGateStageWindowContractDto?>(null);

    Task<BatchReviewGateStageWindowContractDto?> GetMidtermSubmissionWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult<BatchReviewGateStageWindowContractDto?>(null);

    Task<Guid?> GetActiveOpenBatchIdAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<Guid?>(null);
}
