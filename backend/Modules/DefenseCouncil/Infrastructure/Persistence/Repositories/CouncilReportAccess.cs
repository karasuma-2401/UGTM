using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Contracts.Councils;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class CouncilReportAccess(DefenseCouncilDbContext db) : ICouncilReportAccess, ICouncilAssignedGroupReader
{
    public Task<bool> IsAssignedAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        db.CouncilAssignments.AsNoTracking().AnyAsync(
            a => a.GroupId == groupId && a.Council.Members.Any(m => m.UserId == userId), cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetAssignedGroupIdsAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.CouncilAssignments.AsNoTracking()
            .Where(a => a.Council.Members.Any(m => m.UserId == userId))
            .Select(a => a.GroupId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
}
