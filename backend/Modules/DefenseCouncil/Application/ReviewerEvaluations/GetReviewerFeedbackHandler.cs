using System.Text.Json.Serialization;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed record ReviewerFeedbackDto([property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Comment);

public sealed class GetReviewerFeedbackHandler(IReviewerFeedbackGroupReader groups, IReviewerAssignmentTopicReader topics,
    IReviewerFeedbackPublicationReader publications, ICurrentUser currentUser)
{
    public async Task<Result<ReviewerFeedbackDto>> HandleAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actor || actor == Guid.Empty)
            return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.AuthenticationRequired);
        if (!currentUser.Roles.Contains("Student", StringComparer.OrdinalIgnoreCase))
            return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.AccessDenied);
        if (groupId == Guid.Empty) return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.InvalidRequest);
        var group = await groups.GetAsync(groupId, actor, cancellationToken);
        if (group is null) return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.GroupNotFound);
        if (!group.IsConfirmedMember) return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.AccessDenied);
        var topic = await topics.GetAsync(group.TopicId, cancellationToken);
        if (!group.IsContextValid || topic is null || topic.BatchId != group.BatchId)
            return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.ContextInvalid);
        var publication = await publications.GetAsync(groupId, cancellationToken);
        if (publication.Outcome == ReviewerFeedbackPublicationOutcome.ContextInvalid)
            return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.ContextInvalid);
        if (publication.Outcome == ReviewerFeedbackPublicationOutcome.None) return Result.Success(new ReviewerFeedbackDto(null));
        if (publication.Snapshot is null || !await groups.IsReportSnapshotValidAsync(publication.Snapshot, cancellationToken))
            return Result.Failure<ReviewerFeedbackDto>(ReviewerEvaluationErrors.ContextInvalid);
        return Result.Success(new ReviewerFeedbackDto(publication.Comment));
    }
}
