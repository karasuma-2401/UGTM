using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class CouncilUserInfoReader(IdentityDbContext db) : ICouncilUserInfoReader
{
    public async Task<CouncilUserInfoContractDto?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CouncilUserInfoContractDto(
                user.Id,
                user.FullName,
                user.Email,
                user.UserCode,
                user.Status == AccountStatus.Active))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, CouncilUserInfoContractDto>();

        return await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new CouncilUserInfoContractDto(
                user.Id,
                user.FullName,
                user.Email,
                user.UserCode,
                user.Status == AccountStatus.Active))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }
}
