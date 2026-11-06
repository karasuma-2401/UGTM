using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Contracts.Councils;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class CouncilReportAccess(DefenseCouncilDbContext db) : ICouncilReportAccess
{
    public Task<bool> IsAssignedAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        db.CouncilAssignments.AsNoTracking().AnyAsync(
            a => a.GroupId == groupId && a.Council.Members.Any(m => m.UserId == userId), cancellationToken);
}
