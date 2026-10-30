namespace UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;

public sealed record ReviewerFeedbackGroupContractDto(Guid GroupId, Guid TopicId, Guid? BatchId,
    bool IsContextValid, bool IsConfirmedMember);

public interface IReviewerFeedbackGroupReader
{
    // No live report/group-stage/reviewer-account guard: published feedback survives later stages.
    Task<ReviewerFeedbackGroupContractDto?> GetAsync(Guid groupId, Guid actorId, CancellationToken cancellationToken = default);
}
