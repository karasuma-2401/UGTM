using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;

public sealed record GetCouncilMinuteWorkspaceQuery(Guid AssignmentId);

public sealed class GetCouncilMinuteWorkspaceHandler(
    ICouncilRepository councilRepository,
    CouncilMinuteWorkspaceLoader workspaceLoader,
    ICurrentUser currentUser)
{
    public async Task<Result<CouncilMinuteWorkspaceResponseDto>> HandleAsync(
        GetCouncilMinuteWorkspaceQuery query,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<CouncilMinuteWorkspaceResponseDto>(CouncilErrors.Forbidden);
        }

        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(query.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<CouncilMinuteWorkspaceResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        if (!CouncilMinuteAccess.CanView(assignment.Council, currentUser))
        {
            return Result.Failure<CouncilMinuteWorkspaceResponseDto>(CouncilErrors.Forbidden);
        }

        var ctx = await workspaceLoader.LoadAsync(assignment, ct);

        var lockStatus = ctx.Minute?.LockStatus.ToString() ?? "Open";
        var studentFeedback = ctx.Minute?.RequiredRevisions ?? ctx.Minute?.GeneralFeedback;
        var groupCode = $"N{assignment.OrderNumber:D2}";

        var assignmentInfo = new MinuteWorkspaceAssignmentInfoDto(
            assignment.Id,
            assignment.Council.Id,
            assignment.Council.CouncilCode,
            assignment.GroupId,
            groupCode,
            ctx.ChairmanName,
            ctx.SecretaryName,
            lockStatus,
            studentFeedback);

        var (scoreItems, discrepancySummary, calculationPreview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(
            ctx.EvalItems,
            savedSecretaryMarking: null,
            requestedSecretaryMarking: null,
            requestedTargetComponentCode: null,
            overrideReason: ctx.Minute?.OverrideReason,
            configuration: ctx.Configuration);

        return Result.Success(new CouncilMinuteWorkspaceResponseDto(
            assignmentInfo,
            scoreItems,
            discrepancySummary,
            calculationPreview));
    }
}
