using UGTM.Modules.Identity.Application.Activation;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Authentication.ChangePassword;
using UGTM.Modules.Identity.Application.Authentication.Login;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Recovery;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class AuthenticationAuditHandlerTests
{
    private static readonly DateTime Now =
        new(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Login_persists_client_metadata_and_audits_success()
    {
        var user = CreateUser(active: true);
        var sessions = new FakeSessionRepository();
        var auditLogs = new FakeAuditLogWriter();
        var handler = new LoginHandler(
            new FakeUserRepository(user),
            sessions,
            new FakeUserProfileReader(user),
            new FakePasswordService(),
            new OpaqueTokenService(),
            new FakeAccessTokenService(),
            new DirectUnitOfWork(),
            auditLogs,
            new FakeClock(),
            new IdentitySecurityPolicy());

        var result = await handler.HandleAsync(
            new LoginCommand(
                user.Email,
                "current-password",
                "Chrome / Windows",
                "hmac-ip-hash"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(sessions.AddedSession);
        Assert.Equal("Chrome / Windows", sessions.AddedSession.UserAgentSummary);
        Assert.Equal("hmac-ip-hash", sessions.AddedSession.IpAddressHash);
        Assert.NotNull(sessions.AddedRefreshToken);
        AssertAudit(Assert.Single(auditLogs.Entries), AuditActions.Login, AuditOutcomes.Success, user.Id);
    }

    [Fact]
    public async Task Recovery_flow_audits_request_verification_and_password_reset()
    {
        var user = CreateUser(active: true);
        var users = new FakeUserRepository(user);
        var challenges = new FakeChallengeRepository();
        var tokens = new OpaqueTokenService();
        var emailOutbox = new FakeEmailOutbox();
        var unitOfWork = new DirectUnitOfWork();
        var auditLogs = new FakeAuditLogWriter();
        var clock = new FakeClock();
        var policy = new IdentitySecurityPolicy();

        var start = new StartPasswordRecoveryHandler(
            users,
            challenges,
            tokens,
            emailOutbox,
            unitOfWork,
            auditLogs,
            clock,
            policy);
        var started = await start.HandleAsync(
            new StartPasswordRecoveryCommand(user.Email),
            CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.NotNull(emailOutbox.RecoveryCode);
        AssertAudit(auditLogs.Entries[0], AuditActions.PasswordRecoveryRequested, AuditOutcomes.Success, user.Id);

        var verify = new VerifyRecoveryOtpHandler(
            challenges,
            tokens,
            unitOfWork,
            auditLogs,
            clock,
            policy);
        var verified = await verify.HandleAsync(
            new VerifyRecoveryOtpCommand(started.Value.ChallengeId, emailOutbox.RecoveryCode!),
            CancellationToken.None);

        Assert.True(verified.IsSuccess);
        AssertAudit(auditLogs.Entries[1], AuditActions.RecoveryOtpVerified, AuditOutcomes.Success, user.Id);

        var sessions = new FakeSessionRepository();
        var reset = new ResetPasswordHandler(
            users,
            challenges,
            sessions,
            tokens,
            new FakePasswordService(),
            new FakePasswordPolicy(),
            unitOfWork,
            auditLogs,
            clock);
        var resetResult = await reset.HandleAsync(
            new ResetPasswordCommand(verified.Value.ResetGrant, "NewStrongPassword!123"),
            CancellationToken.None);

        Assert.True(resetResult.IsSuccess);
        Assert.Equal(SessionRevocationReason.PasswordReset, sessions.LastReason);
        AssertAudit(auditLogs.Entries[2], AuditActions.PasswordReset, AuditOutcomes.Success, user.Id);
    }

    [Fact]
    public async Task Activation_completion_audits_success_without_token_or_password()
    {
        var user = CreateUser(active: false);
        var users = new FakeUserRepository(user);
        var challenges = new FakeChallengeRepository();
        var tokens = new OpaqueTokenService();
        var rawToken = tokens.CreateToken();
        challenges.Add(OneTimeChallenge.Create(
            user.Id,
            ChallengePurpose.ActivateAccount,
            rawToken.Selector,
            rawToken.SecretHash,
            user.Email,
            5,
            Now,
            Now.AddDays(1)));
        var auditLogs = new FakeAuditLogWriter();
        var handler = new CompleteActivationHandler(
            challenges,
            users,
            tokens,
            new FakePasswordService(),
            new FakePasswordPolicy(),
            new DirectUnitOfWork(),
            auditLogs,
            new FakeClock());

        var result = await handler.HandleAsync(
            new CompleteActivationCommand(rawToken.RawToken, "StrongPassword!123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountStatus.Active, user.Status);
        var audit = Assert.Single(auditLogs.Entries);
        AssertAudit(audit, AuditActions.AccountActivated, AuditOutcomes.Success, user.Id);
        Assert.Null(audit.Metadata);
        Assert.DoesNotContain(rawToken.RawToken, audit.TargetId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_password_audits_success_and_revokes_all_sessions()
    {
        var user = CreateUser(active: true);
        var sessions = new FakeSessionRepository();
        var auditLogs = new FakeAuditLogWriter();
        var handler = new ChangePasswordHandler(
            new FakeCurrentUser(user.Id, Guid.NewGuid()),
            new FakeUserRepository(user),
            sessions,
            new FakePasswordService(),
            new FakePasswordPolicy(),
            new DirectUnitOfWork(),
            auditLogs,
            new FakeClock());

        var result = await handler.HandleAsync(
            new ChangePasswordCommand("current-password", "NewStrongPassword!123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.PasswordChanged, sessions.LastReason);
        AssertAudit(Assert.Single(auditLogs.Entries), AuditActions.PasswordChanged, AuditOutcomes.Success, user.Id);
    }

    private static void AssertAudit(
        AuditEntry entry,
        string action,
        string outcome,
        Guid userId)
    {
        Assert.Equal(action, entry.Action);
        Assert.Equal(outcome, entry.Outcome);
        Assert.Equal("User", entry.TargetType);
        Assert.Equal(userId.ToString(), entry.TargetId);
    }

    private static User CreateUser(bool active)
    {
        var emailLocalPart = Guid.NewGuid().ToString("N");
        var email = $"{emailLocalPart}@ugtm.edu.vn";
        var user = User.ProvisionStudent(
            Guid.NewGuid(),
            email,
            email.ToUpperInvariant(),
            "Audit Test",
            $"SV{Guid.NewGuid():N}"[..20],
            null,
            Now.AddDays(-2));
        if (active)
        {
            user.Activate("hashed-current-password", Now.AddDays(-1));
        }

        return user;
    }

    private sealed class FakeChallengeRepository : IOneTimeChallengeRepository
    {
        private readonly List<OneTimeChallenge> _challenges = [];

        public Task<OneTimeChallenge?> GetAsync(
            string selector,
            ChallengePurpose purpose,
            CancellationToken cancellationToken)
            => Task.FromResult(_challenges.SingleOrDefault(value =>
                value.Selector == selector && value.Purpose == purpose));

        public Task<OneTimeChallenge?> GetForUpdateAsync(
            string selector,
            ChallengePurpose purpose,
            CancellationToken cancellationToken)
            => GetAsync(selector, purpose, cancellationToken);

        public Task InvalidateOutstandingAsync(
            Guid userId,
            ChallengePurpose purpose,
            DateTime now,
            CancellationToken cancellationToken)
        {
            foreach (var challenge in _challenges.Where(value =>
                         value.UserId == userId && value.Purpose == purpose))
            {
                challenge.Invalidate(now);
            }

            return Task.CompletedTask;
        }

        public void Add(OneTimeChallenge challenge) => _challenges.Add(challenge);
    }

    private sealed class FakeSessionRepository : IAuthenticationSessionRepository
    {
        public SessionRevocationReason? LastReason { get; private set; }
        public AuthenticationSession? AddedSession { get; private set; }
        public RefreshToken? AddedRefreshToken { get; private set; }

        public Task RevokeAllAsync(
            Guid userId,
            SessionRevocationReason reason,
            DateTime now,
            CancellationToken cancellationToken)
        {
            LastReason = reason;
            return Task.CompletedTask;
        }

        public void AddSession(AuthenticationSession session) => AddedSession = session;
        public void AddRefreshToken(RefreshToken refreshToken) => AddedRefreshToken = refreshToken;
        public Task<AuthenticationSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RefreshToken?> GetRefreshTokenForUpdateAsync(string selector, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuthenticationSession>> GetActiveSessionsAsync(Guid userId, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RevokeActiveSessionAsync(Guid sessionId, Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> RevokeActiveSessionsAsync(Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(user.Id == id ? user : null);
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedEmail == normalizedEmail ? user : null);
        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedEmail == normalizedEmail || user.UserCode == normalizedUserCode ? user : null);
        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private sealed class FakeEmailOutbox : IIdentityEmailOutbox
    {
        public string? RecoveryCode { get; private set; }

        public void EnqueueActivationEmail(Guid userId, string to, string fullName, string rawToken, DateTime now)
            => throw new NotSupportedException();

        public void EnqueuePasswordRecoveryEmail(
            Guid userId,
            string email,
            string fullName,
            string code,
            DateTime createdAt)
            => RecoveryCode = code;
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public string Hash(User user, string password) => $"hashed-{password}";

        public bool Verify(User user, string passwordHash, string providedPassword)
            => passwordHash == $"hashed-{providedPassword}";
    }

    private sealed class FakePasswordPolicy : IPasswordPolicy
    {
        public PasswordPolicyResult Validate(string password) => new(true, []);
    }

    private sealed class FakeUserProfileReader(User user) : IUserProfileReader
    {
        public Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<UserProfileDto?>(new UserProfileDto(
                user.Id,
                user.Email,
                user.FullName,
                user.UserCode,
                ["Student"],
                []));
    }

    private sealed class FakeAccessTokenService : IAccessTokenService
    {
        public AccessTokenResult Create(UserProfileDto user, Guid sessionId, DateTime now)
            => new("access-token", now.AddMinutes(10));
    }

    private sealed class FakeCurrentUser(Guid userId, Guid sessionId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
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
