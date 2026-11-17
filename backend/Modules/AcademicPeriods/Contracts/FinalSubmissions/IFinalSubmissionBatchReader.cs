namespace UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions;

public sealed record FinalSubmissionBatchContractDto(Guid BatchId, string BatchStatus,
    DateTime? WindowOpensAtUtc, DateTime? WindowDeadlineAtUtc, string ScheduleFingerprint);

public interface IFinalSubmissionBatchReader
{
    Task<FinalSubmissionBatchContractDto?> GetAsync(Guid batchId, CancellationToken token = default);
    Task<FinalSubmissionBatchContractDto?> LockAsync(Guid batchId, CancellationToken token = default);
}
