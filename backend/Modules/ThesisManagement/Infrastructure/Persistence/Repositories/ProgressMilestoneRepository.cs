using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class ProgressMilestoneRepository(ThesisManagementDbContext context)
    : IProgressMilestoneRepository
{
    public async Task<IReadOnlyList<ProgressMilestone>> GetByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
        => await context.ProgressMilestones
            .AsNoTracking()
            .Where(milestone => milestone.GroupId == groupId)
            .OrderBy(milestone => milestone.Deadline)
            .ThenBy(milestone => milestone.CreatedAt)
            .ThenBy(milestone => milestone.Id)
            .ToListAsync(cancellationToken);

    public void Add(ProgressMilestone milestone)
    {
        ArgumentNullException.ThrowIfNull(milestone);
        context.ProgressMilestones.Add(milestone);
    }
}
