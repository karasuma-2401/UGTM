using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AuthenticationSessionRepository(IdentityDbContext db)
    : IAuthenticationSessionRepository
{
    public void AddSession(AuthenticationSession value) => db.AuthenticationSessions.Add(value);
    public void AddRefreshToken(RefreshToken value) => db.RefreshTokens.Add(value);

    public Task<AuthenticationSession?> GetSessionAsync(Guid id, CancellationToken ct)
        => db.AuthenticationSessions.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<RefreshToken?> GetRefreshTokenForUpdateAsync(
        string selector,
        CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM identity.refresh_token WHERE selector = {selector} FOR UPDATE",
            ct);
        return await db.RefreshTokens.Include(x => x.Session).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Selector == selector, ct);
    }

    public async Task<IReadOnlyList<AuthenticationSession>> GetActiveSessionsAsync(
        Guid userId, DateTime now, CancellationToken ct)
        => await db.AuthenticationSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.AbsoluteExpiresAt > now)
            .ToListAsync(ct);

    public Task RevokeAllAsync(
        Guid userId,
        SessionRevocationReason reason,
        DateTime now,
        CancellationToken ct)
        => db.AuthenticationSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.RevocationReason, reason)
                .SetProperty(x => x.ConcurrencyStamp, Guid.NewGuid()), ct);
}