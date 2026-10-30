using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed class GetReviewerEvaluationHandler(IReviewerAssignmentRepository assignments, IReviewerEvaluationRepository evaluations,
    ReviewerEvaluationAuthorization authorization, IReviewerFeedbackGroupReader groups, IReviewerAssignmentTopicReader topics, ICurrentUser currentUser)
{
    public async Task<Result<ReviewerEvaluationDto>> HandleAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actor || actor == Guid.Empty)
            return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.AuthenticationRequired);
        if (!ReviewerEvaluationAuthorization.CanRead(currentUser.Roles, currentUser.Permissions))
            return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.AccessDenied);
        if (assignmentId == Guid.Empty) return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.InvalidRequest);
        var assignment = await assignments.GetByIdAsync(assignmentId, cancellationToken);
        if (assignment is null) return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.AssignmentNotFound);
        if (!await authorization.CanAccessAsync(assignment, false, cancellationToken)) return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.AccessDenied);
        if (assignment.IsLegacy) return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.LegacyRecordNeedsResolution);
        var evaluation = await evaluations.GetByAssignmentAsync(assignmentId, cancellationToken);
        if (evaluation is null) return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.NotFound);
        if (evaluation.IsLegacy) return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.LegacyRecordNeedsResolution);
        // History reads do not reuse the write guard (Open, group stage, reviewer activity).
        var group = await groups.GetAsync(assignment.GroupId, actor, cancellationToken);
        var topic = group is null ? null : await topics.GetAsync(group.TopicId, cancellationToken);
        if (group is not { IsContextValid: true, BatchId: not null } || topic is null || topic.BatchId != group.BatchId)
            return Result.Failure<ReviewerEvaluationDto>(ReviewerEvaluationErrors.ContextInvalid);
        return Result.Success(ReviewerEvaluationDtoMapper.ToDto(evaluation, assignment, topic.TopicId, topic.BatchId));
    }
}
