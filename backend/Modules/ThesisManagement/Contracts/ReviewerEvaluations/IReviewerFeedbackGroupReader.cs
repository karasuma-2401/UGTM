namespace UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;

public sealed record ReviewerFeedbackGroupContractDto(Guid GroupId, Guid TopicId, Guid? BatchId,
    bool IsContextValid, bool IsConfirmedMember);
public sealed record ReviewerFeedbackReportSnapshotContractDto(Guid GroupId, Guid FinalReportId, int ReportVersionNo,
    Guid SubmissionFileId, string SubmissionChecksumSha256);

public interface IReviewerFeedbackGroupReader
{
    // No live report/group-stage/reviewer-account guard: published feedback survives later stages.
    Task<ReviewerFeedbackGroupContractDto?> GetAsync(Guid groupId, Guid actorId, CancellationToken cancellationToken = default);
    // Validate the issued snapshot, independent of later current-version/status changes.
    Task<bool> IsReportSnapshotValidAsync(ReviewerFeedbackReportSnapshotContractDto snapshot, CancellationToken cancellationToken = default);
}
