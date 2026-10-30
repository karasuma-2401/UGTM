namespace UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;

public sealed record ReviewerAssignmentThesisContractDto(
    Guid GroupId, Guid TopicId, Guid? BatchId, bool IsReady,
    Guid? FinalReportId, bool IsUnderEvaluation, int? ReportVersionNo,
    Guid? SubmissionFileId, string? SubmissionChecksumSha256);

public interface IReviewerAssignmentThesisReader
{
    Task<ReviewerAssignmentThesisContractDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requires the caller's shared transaction. Locks group, membership, report and bound file
    /// in that order without changing their state or concurrency tokens.
    /// </summary>
    Task<ReviewerAssignmentThesisContractDto?> LockAsync(Guid groupId, CancellationToken cancellationToken = default);
}
