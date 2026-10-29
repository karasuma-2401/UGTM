using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.GetAdvisorEvaluation;

/// <summary>
/// Retrieves an advisor evaluation only for the currently assigned advisor or an administrator.
/// This operation is deliberately side-effect free: it does not create audit or idempotency rows.
/// </summary>
public sealed class GetAdvisorEvaluationHandler(
    IAdvisorEvaluationRepository evaluationRepository,
    IAdvisorEvaluationWorkflowContract thesisWorkflow,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    ICurrentUser currentUser)
{
    public async Task<Result<AdvisorEvaluationDto>> HandleAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId || actorId == Guid.Empty)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.AuthenticationRequired);
        }
        if (groupId == Guid.Empty)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.InvalidRequest);
        }

        var group = await thesisWorkflow.GetGroupContextAsync(groupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.GroupNotFound);
        }

        if (!IsAdministrator(currentUser))
        {
            var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
            if (assignment?.AdvisorId != actorId)
            {
                return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.AccessDenied);
            }
        }

        var evaluation = await evaluationRepository.GetByGroupIdAsync(groupId, cancellationToken);
        if (evaluation is null)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.NotFound);
        }
        if (evaluation.IsLegacy)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.LegacyRecordNeedsResolution);
        }

        return Result.Success(AdvisorEvaluationDtoMapper.ToDto(evaluation));
    }

    private static bool IsAdministrator(ICurrentUser user)
        => user.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
}
