namespace UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;

public enum ArchiveProofOutcome { Succeeded, Missing, Legacy, NotConfirmed, ContextInvalid }

public sealed record ArchiveFileProofContractDto(Guid FileId, Guid PackageId, int PackageVersionNo, string FileRole,
    string ContentType, long SizeBytes, string ChecksumSha256);

public sealed record FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome Outcome, Guid GroupId, Guid BatchId,
    Guid? SubmissionId, uint? SubmissionVersion, Guid? PackageId, int? PackageVersionNo, string? ManifestSha256,
    Guid? AdvisorReviewId, Guid? FacultyReviewId, Guid? AdvisorConfirmedBy, DateTime? AdvisorConfirmedAtUtc,
    Guid? ArchivedConfirmedBy, DateTime? ArchivedConfirmedAtUtc, IReadOnlyList<ArchiveFileProofContractDto> Files);

public interface IFinalSubmissionArchiveProofContract
{
    Task<FinalSubmissionArchiveProofContractDto> GetArchiveProofAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinalSubmissionArchiveProofContractDto>> GetManyArchiveProofsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default);

    /// <summary>Requires an enlisted shared transaction and caller-held group/context guards.
    /// Retains owner package/file/review share locks until the consumer commits. Does not reacquire earlier root locks.</summary>
    Task<FinalSubmissionArchiveProofContractDto> LockArchiveProofForCalculationAsync(Guid groupId, CancellationToken cancellationToken = default);
}
