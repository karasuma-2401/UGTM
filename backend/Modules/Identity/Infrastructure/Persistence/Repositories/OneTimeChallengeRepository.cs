using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class OneTimeChallengeRepository(IdentityDbContext db) : IOneTimeChallengeRepository
{
    public void Add(OneTimeChallenge value) => db.OneTimeChallenges.Add(value);

    public Task<OneTimeChallenge?> GetAsync(
        string selector, ChallengePurpose purpose, CancellationToken ct)
        => db.OneTimeChallenges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Selector == selector && x.Purpose == purpose, ct);

    public async Task<OneTimeChallenge?> GetForUpdateAsync(
        string selector, ChallengePurpose purpose, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM identity.one_time_challenge WHERE selector = {selector} FOR UPDATE",
            ct);
        return await db.OneTimeChallenges.SingleOrDefaultAsync(
            x => x.Selector == selector && x.Purpose == purpose, ct);
    }

    public Task InvalidateOutstandingAsync(
        Guid userId, ChallengePurpose purpose, DateTime now, CancellationToken ct)
        => db.OneTimeChallenges
            .Where(x => x.UserId == userId && x.Purpose == purpose
                                           && x.ConsumedAt == null && x.InvalidatedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.InvalidatedAt, now), ct);
}