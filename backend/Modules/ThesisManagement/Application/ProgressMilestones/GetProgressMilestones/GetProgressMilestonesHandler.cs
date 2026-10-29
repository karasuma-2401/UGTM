using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.ProgressMilestones.GetProgressMilestones;

public sealed class GetProgressMilestonesHandler(
    IStudentGroupRepository groupRepository,
    IProgressMilestoneRepository milestoneRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ProgressMilestoneDto>>> HandleAsync(
        GetProgressMilestonesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<IReadOnlyList<ProgressMilestoneDto>>(ProgressMilestoneErrors.AccessDenied);
        }

        if (query.GroupId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ProgressMilestoneDto>>(ProgressMilestoneErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<IReadOnlyList<ProgressMilestoneDto>>(ProgressMilestoneErrors.GroupNotFound);
        }

        var isMember = group.Members.Any(member => member.StudentId == currentUser.UserId.Value);
        if (!isMember && !ThesisAuthorization.IsAdministrator(currentUser))
        {
            var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
            if (assignment?.AdvisorId != currentUser.UserId.Value)
            {
                return Result.Failure<IReadOnlyList<ProgressMilestoneDto>>(ProgressMilestoneErrors.AccessDenied);
            }
        }

        var milestones = await milestoneRepository.GetByGroupIdAsync(group.Id, cancellationToken);
        return Result.Success<IReadOnlyList<ProgressMilestoneDto>>(
            milestones.Select(ProgressMilestoneMapper.ToDto).ToArray());
    }
}
