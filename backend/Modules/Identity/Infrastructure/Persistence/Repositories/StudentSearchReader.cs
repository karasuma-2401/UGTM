using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class StudentSearchReader(IdentityDbContext db) : IStudentSearchReader
{
    public async Task<IReadOnlyList<Guid>> FindIdsByNameOrCodeAsync(
        string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return Array.Empty<Guid>();
        var term = keyword.Trim().ToLowerInvariant();
        // Membership is checked by the consuming module, like GetStudentsByIdsAsync.
        // Historical submissions must remain searchable if an account's role/status changes.
        return await db.Users.AsNoTracking()
            .Where(user => user.FullName.ToLower().Contains(term)
                || (user.UserCode != null && user.UserCode.ToLower().Contains(term)))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }
}
