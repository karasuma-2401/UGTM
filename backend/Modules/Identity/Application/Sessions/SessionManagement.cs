using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Application.Sessions;

public sealed record SessionDto(
    Guid Id,
    DateTime AuthenticatedAt,
    DateTime LastSeenAt,
    DateTime IdleExpiresAt,
    DateTime AbsoluteExpiresAt,
    string? UserAgentSummary,
    bool IsCurrent);

public sealed record RevokeSessionsResult(
    int RevokedCount,
    bool CurrentSessionRevoked);

public sealed class GetMySessionsHandler(
    ICurrentUser currentUser,
    IAuthenticationSessionRepository sessions,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<SessionDto>>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrent(currentUser, out var userId, out var sessionId))
        {
            return Result.Failure<IReadOnlyList<SessionDto>>(IdentityErrors.InvalidSession);
        }

        var activeSessions = await sessions.GetActiveSessionsAsync(
            userId,
            clock.UtcNow,
            cancellationToken);
        return Result.Success<IReadOnlyList<SessionDto>>(
            activeSessions.Select(session => ToDto(session, sessionId)).ToArray());
    }

    internal static SessionDto ToDto(AuthenticationSession session, Guid? currentSessionId)
        => new(
            session.Id,
            session.AuthenticatedAt,
            session.LastSeenAt,
            session.IdleExpiresAt,
            session.AbsoluteExpiresAt,
            session.UserAgentSummary,
            session.Id == currentSessionId);

    internal static bool TryGetCurrent(
        ICurrentUser currentUser,
        out Guid userId,
        out Guid sessionId)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        sessionId = currentUser.SessionId ?? Guid.Empty;
        return currentUser.IsAuthenticated
               && userId != Guid.Empty
               && sessionId != Guid.Empty;
    }
}

public sealed class RevokeMySessionHandler(
    ICurrentUser currentUser,
    IAuthenticationSessionRepository sessions,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public Task<Result<RevokeSessionsResult>> HandleAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!GetMySessionsHandler.TryGetCurrent(
                currentUser,
                out var userId,
                out var currentSessionId))
        {
            return Task.FromResult(Result.Failure<RevokeSessionsResult>(
                IdentityErrors.InvalidSession));
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var revoked = await sessions.RevokeActiveSessionAsync(
                sessionId,
                userId,
                SessionRevocationReason.UserLogout,
                clock.UtcNow,
                ct);
            if (!revoked)
            {
                return Result.Failure<RevokeSessionsResult>(IdentityErrors.SessionNotFound);
            }

            auditLogs.Add(new AuditEntry(
                "AuthenticationSession",
                sessionId.ToString(),
                AuditActions.SessionRevoked,
                AuditOutcomes.Success,
                SessionRevocationReason.UserLogout.ToString(),
                new { UserId = userId }));
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new RevokeSessionsResult(
                1,
                sessionId == currentSessionId));
        }, cancellationToken);
    }
}

public sealed class RevokeAllMySessionsHandler(
    ICurrentUser currentUser,
    IAuthenticationSessionRepository sessions,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public Task<Result<RevokeSessionsResult>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!GetMySessionsHandler.TryGetCurrent(
                currentUser,
                out var userId,
                out var currentSessionId))
        {
            return Task.FromResult(Result.Failure<RevokeSessionsResult>(
                IdentityErrors.InvalidSession));
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var count = await sessions.RevokeActiveSessionsAsync(
                userId,
                SessionRevocationReason.UserLogoutAll,
                clock.UtcNow,
                ct);
            auditLogs.Add(new AuditEntry(
                "User",
                userId.ToString(),
                AuditActions.UserSessionsRevoked,
                AuditOutcomes.Success,
                SessionRevocationReason.UserLogoutAll.ToString(),
                new { RevokedCount = count }));
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new RevokeSessionsResult(
                count,
                count > 0 && currentSessionId != Guid.Empty));
        }, cancellationToken);
    }
}

public sealed class GetUserSessionsHandler(
    IUserRepository users,
    IAuthenticationSessionRepository sessions,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<SessionDto>>> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(userId, cancellationToken) is null)
        {
            return Result.Failure<IReadOnlyList<SessionDto>>(IdentityErrors.UserNotFound);
        }

        var activeSessions = await sessions.GetActiveSessionsAsync(
            userId,
            clock.UtcNow,
            cancellationToken);
        return Result.Success<IReadOnlyList<SessionDto>>(
            activeSessions.Select(session =>
                    GetMySessionsHandler.ToDto(session, currentUser.SessionId))
                .ToArray());
    }
}

public sealed class RevokeUserSessionHandler(
    IUserRepository users,
    IAuthenticationSessionRepository sessions,
    ICurrentUser currentUser,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public async Task<Result<RevokeSessionsResult>> HandleAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(userId, cancellationToken) is null)
        {
            return Result.Failure<RevokeSessionsResult>(IdentityErrors.UserNotFound);
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var revoked = await sessions.RevokeActiveSessionAsync(
                sessionId,
                userId,
                SessionRevocationReason.AdministratorRevoked,
                clock.UtcNow,
                ct);
            if (!revoked)
            {
                return Result.Failure<RevokeSessionsResult>(IdentityErrors.SessionNotFound);
            }

            auditLogs.Add(new AuditEntry(
                "AuthenticationSession",
                sessionId.ToString(),
                AuditActions.SessionRevoked,
                AuditOutcomes.Success,
                SessionRevocationReason.AdministratorRevoked.ToString(),
                new { UserId = userId }));
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new RevokeSessionsResult(
                1,
                currentUser.SessionId == sessionId));
        }, cancellationToken);
    }
}

public sealed class RevokeAllUserSessionsHandler(
    IUserRepository users,
    IAuthenticationSessionRepository sessions,
    ICurrentUser currentUser,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public async Task<Result<RevokeSessionsResult>> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(userId, cancellationToken) is null)
        {
            return Result.Failure<RevokeSessionsResult>(IdentityErrors.UserNotFound);
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var count = await sessions.RevokeActiveSessionsAsync(
                userId,
                SessionRevocationReason.AdministratorRevoked,
                clock.UtcNow,
                ct);
            auditLogs.Add(new AuditEntry(
                "User",
                userId.ToString(),
                AuditActions.UserSessionsRevoked,
                AuditOutcomes.Success,
                SessionRevocationReason.AdministratorRevoked.ToString(),
                new { RevokedCount = count }));
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new RevokeSessionsResult(
                count,
                userId == currentUser.UserId && count > 0));
        }, cancellationToken);
    }
}
