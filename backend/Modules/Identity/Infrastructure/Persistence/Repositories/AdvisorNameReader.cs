using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AdvisorNameReader(IdentityDbContext db) : IAdvisorNameReader
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IEnumerable<Guid> advisorIds,
        CancellationToken cancellationToken = default)
    {
        var ids = advisorIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
    }
}
