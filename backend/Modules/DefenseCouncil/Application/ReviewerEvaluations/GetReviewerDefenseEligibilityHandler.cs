using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed record ReviewerDefenseEligibilityDto(Guid GroupId, Guid? BatchId, string? BatchStatus, Guid? AssignmentId,
    Guid? EvaluationId, uint? EvaluationVersion, Guid? ScoreConfigurationId, int? ScoreConfigurationVersionNo,
    decimal? MinimumReviewerScore, bool CanProceed, string? ReasonCode);

public sealed class GetReviewerDefenseEligibilityHandler(IReviewerDefenseEligibilityContract eligibility,
    IReviewerFeedbackGroupReader groups, IReviewerAssignmentRepository assignments, ReviewerEvaluationAuthorization authorization, ICurrentUser currentUser)
{
    public async Task<Result<ReviewerDefenseEligibilityDto>> HandleAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actor || actor == Guid.Empty)
            return Result.Failure<ReviewerDefenseEligibilityDto>(ReviewerEvaluationErrors.AuthenticationRequired);
        if (!ReviewerEvaluationAuthorization.CanRead(currentUser.Roles, currentUser.Permissions))
            return Result.Failure<ReviewerDefenseEligibilityDto>(ReviewerEvaluationErrors.AccessDenied);
        if (groupId == Guid.Empty) return Result.Failure<ReviewerDefenseEligibilityDto>(ReviewerEvaluationErrors.InvalidRequest);
        if (!ReviewerEvaluationAuthorization.CanReadManagement(currentUser.Roles, currentUser.Permissions))
        {
            if (await groups.GetAsync(groupId, actor, cancellationToken) is null)
                return Result.Failure<ReviewerDefenseEligibilityDto>(ReviewerEvaluationErrors.GroupNotFound);
            var assignment = await assignments.GetActiveByGroupAsync(groupId, cancellationToken);
            if (assignment is null || !await authorization.CanAccessAsync(assignment, false, cancellationToken))
                return Result.Failure<ReviewerDefenseEligibilityDto>(ReviewerEvaluationErrors.AccessDenied);
        }
        try
        {
            var value = await eligibility.GetAsync(groupId, cancellationToken);
            return value is null ? Result.Failure<ReviewerDefenseEligibilityDto>(ReviewerEvaluationErrors.GroupNotFound)
                : Result.Success(new ReviewerDefenseEligibilityDto(value.GroupId, value.BatchId, value.BatchStatus, value.AssignmentId,
                    value.EvaluationId, value.EvaluationVersion, value.ScoreConfigurationId, value.ScoreConfigurationVersionNo,
                    value.MinimumReviewerScore, value.CanProceed, value.ReasonCode?.ToString()));
        }
        catch (DefenseCouncilConcurrencyException) { return Result.Failure<ReviewerDefenseEligibilityDto>(ReviewerEvaluationErrors.ConcurrencyConflict); }
    }
}
