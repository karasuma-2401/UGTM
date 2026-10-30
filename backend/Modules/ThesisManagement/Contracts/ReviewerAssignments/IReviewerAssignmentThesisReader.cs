namespace UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;

public sealed record ReviewerAssignmentThesisContractDto(
    Guid GroupId, Guid TopicId, Guid? BatchId, bool IsReady,
    Guid? FinalReportId, bool IsUnderEvaluation, int? ReportVersionNo,
    Guid? SubmissionFileId, string? SubmissionChecksumSha256,
    ReviewerAssignmentReportFileContractDto? ReportFile = null);

public sealed record ReviewerAssignmentReportFileContractDto(Guid FileId, string FileName, string ContentType,
    long SizeBytes, int VersionNo);
public sealed record ReviewerAssignmentGroupLinkContractDto(Guid GroupId, Guid TopicId);

public interface IReviewerAssignmentThesisReader
{
    Task<IReadOnlyList<ReviewerAssignmentGroupLinkContractDto>> GetGroupLinksAsync(
        IReadOnlyCollection<Guid>? groupIds, IReadOnlyCollection<Guid>? topicIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewerAssignmentThesisContractDto>> GetManyAsync(IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewerAssignmentThesisContractDto>> GetByTopicIdsAsync(IReadOnlyCollection<Guid> topicIds,
        CancellationToken cancellationToken = default);
    Task<ReviewerAssignmentThesisContractDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requires the caller's shared transaction. Locks group, membership, report and bound file
    /// in that order without changing their state or concurrency tokens.
    /// </summary>
    Task<ReviewerAssignmentThesisContractDto?> LockAsync(Guid groupId, CancellationToken cancellationToken = default);
}
