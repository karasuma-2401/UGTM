using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public enum ReviewerFeedbackPublicationOutcome { None, Published, ContextInvalid }
public sealed record ReviewerFeedbackPublication(ReviewerFeedbackPublicationOutcome Outcome, string? Comment = null,
    ReviewerFeedbackReportSnapshotContractDto? Snapshot = null);

public interface IReviewerFeedbackPublicationReader
{
    // Whitelist projection: never loads points, reviewer conclusion, private DTO or RevisionFeedback.
    Task<ReviewerFeedbackPublication> GetAsync(Guid groupId, CancellationToken cancellationToken = default);
}
