using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Sessions;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class SessionManagementHandlerTests
{
    private static readonly DateTime Now =
        new(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetMySessions_marks_only_current_session()
    {
        var user = CreateUser();
        var current = CreateSession(user, "Chrome / Windows");
        var other = CreateSession(user, "Safari / iPhone");
        var handler = new GetMySessionsHandler(
            new FakeCurrentUser(user.Id, current.Id),
            new FakeSessionRepository([current, other]),
            new FakeClock());

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.True(result.Value.Single(value => value.Id == current.Id).IsCurrent);
        Assert.False(result.Value.Single(value => value.Id == other.Id).IsCurrent);
    }

    [Fact]
    public async Task RevokeMySession_cannot_revoke_session_owned_by_another_user()
    {
        var currentUser = CreateUser();
        var otherUser = CreateUser();
        var currentSession = CreateSession(currentUser, "Current device");
        var otherSession = CreateSession(otherUser, "Other device");
        var auditLogs = new FakeAuditLogWriter();
        var handler = new RevokeMySessionHandler(
            new FakeCurrentUser(currentUser.Id, currentSession.Id),
            new FakeSessionRepository([currentSession, otherSession]),
            new DirectUnitOfWork(),
            auditLogs,
            new FakeClock());

        var result = await handler.HandleAsync(otherSession.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.SessionNotFound", result.Error.Code);
        Assert.Null(otherSession.RevokedAt);
        Assert.Empty(auditLogs.Entries);
    }

    [Fact]
    public async Task RevokeMySession_revokes_current_session_and_writes_audit_log()
    {
        var user = CreateUser();
        var session = CreateSession(user, "Current device");
        var auditLogs = new FakeAuditLogWriter();
        var handler = new RevokeMySessionHandler(
            new FakeCurrentUser(user.Id, session.Id),
            new FakeSessionRepository([session]),
            new DirectUnitOfWork(),
            auditLogs,
            new FakeClock());

        var result = await handler.HandleAsync(session.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RevokedCount);
        Assert.True(result.Value.CurrentSessionRevoked);
        Assert.Equal(SessionRevocationReason.UserLogout, session.RevocationReason);
        Assert.Equal(AuditActions.SessionRevoked, Assert.Single(auditLogs.Entries).Action);
    }

    [Fact]
    public async Task RevokeAllMySessions_revokes_every_active_session()
    {
        var user = CreateUser();
        var current = CreateSession(user, "Current device");
        var other = CreateSession(user, "Other device");
        var auditLogs = new FakeAuditLogWriter();
        var handler = new RevokeAllMySessionsHandler(
            new FakeCurrentUser(user.Id, current.Id),
            new FakeSessionRepository([current, other]),
            new DirectUnitOfWork(),
            auditLogs,
            new FakeClock());

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.RevokedCount);
        Assert.True(result.Value.CurrentSessionRevoked);
        Assert.All([current, other], value =>
            Assert.Equal(SessionRevocationReason.UserLogoutAll, value.RevocationReason));
        Assert.Equal(AuditActions.UserSessionsRevoked, Assert.Single(auditLogs.Entries).Action);
    }

    [Fact]
    public async Task AdminRevokeSession_uses_administrator_reason_and_writes_audit_log()
    {
        var admin = CreateUser();
        var target = CreateUser();
        var adminSession = CreateSession(admin, "Admin device");
        var targetSession = CreateSession(target, "Target device");
        var auditLogs = new FakeAuditLogWriter();
        var handler = new RevokeUserSessionHandler(
            new FakeUserRepository(target),
            new FakeSessionRepository([adminSession, targetSession]),
            new FakeCurrentUser(admin.Id, adminSession.Id),
            new DirectUnitOfWork(),
            auditLogs,
            new FakeClock());

        var result = await handler.HandleAsync(
            target.Id,
            targetSession.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CurrentSessionRevoked);
        Assert.Equal(SessionRevocationReason.AdministratorRevoked, targetSession.RevocationReason);
        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.SessionRevoked, audit.Action);
        Assert.Equal(SessionRevocationReason.AdministratorRevoked.ToString(), audit.ReasonCode);
    }

    [Fact]
    public async Task AdminGetSessions_returns_not_found_for_unknown_user()
    {
        var handler = new GetUserSessionsHandler(
            new FakeUserRepository(null),
            new FakeSessionRepository([]),
            new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()),
            new FakeClock());

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.UserNotFound", result.Error.Code);
    }

    private static User CreateUser()
        => User.ProvisionStudent(
            Guid.NewGuid(),
            $"{Guid.NewGuid():N}@ugtm.edu.vn",
            $"{Guid.NewGuid():N}@UGTM.EDU.VN",
            "Test User",
            $"SV{Guid.NewGuid():N}"[..20],
            null,
            Now.AddDays(-2));

    private static AuthenticationSession CreateSession(User user, string userAgent)
        => AuthenticationSession.Create(
            user.Id,
            user.SecurityStamp,
            Now.AddMinutes(-10),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            userAgent,
            "ip-hash");

    private sealed class FakeSessionRepository(IEnumerable<AuthenticationSession> initial)
        : IAuthenticationSessionRepository
    {
        private readonly List<AuthenticationSession> _sessions = [.. initial];

        public Task<IReadOnlyList<AuthenticationSession>> GetActiveSessionsAsync(
            Guid userId,
            DateTime now,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AuthenticationSession>>(_sessions
                .Where(value => value.UserId == userId
                                && value.RevokedAt is null
                                && value.IdleExpiresAt > now
                                && value.AbsoluteExpiresAt > now)
                .OrderByDescending(value => value.LastSeenAt)
                .ToArray());

        public Task<bool> RevokeActiveSessionAsync(
            Guid sessionId,
            Guid userId,
            SessionRevocationReason reason,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var session = _sessions.FirstOrDefault(value =>
                value.Id == sessionId
                && value.UserId == userId
                && value.RevokedAt is null
                && value.IdleExpiresAt > now
                && value.AbsoluteExpiresAt > now);
            session?.Revoke(reason, now);
            return Task.FromResult(session is not null);
        }

        public Task<int> RevokeActiveSessionsAsync(
            Guid userId,
            SessionRevocationReason reason,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var active = _sessions.Where(value =>
                    value.UserId == userId
                    && value.RevokedAt is null
                    && value.IdleExpiresAt > now
                    && value.AbsoluteExpiresAt > now)
                .ToArray();
            foreach (var session in active)
            {
                session.Revoke(reason, now);
            }

            return Task.FromResult(active.Length);
        }

        public void AddSession(AuthenticationSession session) => _sessions.Add(session);
        public void AddRefreshToken(RefreshToken refreshToken) => throw new NotSupportedException();
        public Task<AuthenticationSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.FirstOrDefault(value => value.Id == sessionId));
        public Task<RefreshToken?> GetRefreshTokenForUpdateAsync(string selector, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public Task RevokeAllAsync(Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(user?.Id == id ? user : null);
        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddToRoleAsync(User value, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateRolesAsync(User value, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AcquireAccountProvisioningLockAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(User value) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser(Guid? userId, Guid? sessionId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
        public Guid? SessionId => sessionId;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class DirectUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);
    }

    private sealed class FakeAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Add(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => Now;
    }
}
