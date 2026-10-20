using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Repositories;

public interface IAuthenticationSessionRepository
{
    void AddSession(AuthenticationSession session);
    void AddRefreshToken(RefreshToken refreshToken);

    Task<AuthenticationSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
    Task<RefreshToken?> GetRefreshTokenForUpdateAsync(
        string selector,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AuthenticationSession>> GetActiveSessionsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken);
    Task<bool> RevokeActiveSessionAsync(
        Guid sessionId,
        Guid userId,
        SessionRevocationReason reason,
        DateTime now,
        CancellationToken cancellationToken);
    Task<int> RevokeActiveSessionsAsync(
        Guid userId,
        SessionRevocationReason reason,
        DateTime now,
        CancellationToken cancellationToken);

    Task RevokeAllAsync(
        Guid userId,
        SessionRevocationReason reason,
        DateTime now,
        CancellationToken cancellationToken);
}
