using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.ProgressMilestones.Dtos;

internal static class ProgressMilestoneMapper
{
    internal static ProgressMilestoneDto ToDto(ProgressMilestone milestone)
        => new(
            milestone.Id,
            milestone.GroupId,
            milestone.Title,
            milestone.Description,
            milestone.Deadline,
            milestone.CreatedBy,
            milestone.CreatedAt);
}
