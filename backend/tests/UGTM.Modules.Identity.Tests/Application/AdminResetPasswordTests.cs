using System.Net;
using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Users.ResetPassword;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Notifications;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class AdminResetPasswordTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private const string AdminPassword = "AdminCurrent!Password123";
    private const string StrongCustomPassword = "CustomStrong!Password123";

    [Fact]
    public void CryptoTemporaryPasswordGenerator_generates_strong_unambiguous_distinct_passwords()
    {
        var generator = new CryptoTemporaryPasswordGenerator();
        var policy = new PasswordPolicy();
        var ambiguousChars = new[] { '0', 'O', 'l', '1', 'I' };
        var generated = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 30; i++)
        {
            var password = generator.Generate();
            Assert.Equal(16, password.Length);
            Assert.True(policy.Validate(password).IsValid);
            Assert.DoesNotContain(password, c => ambiguousChars.Contains(c));
            generated.Add(password);
        }

        Assert.Equal(30, generated.Count);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    [InlineData(5000, false)]
    public void PasswordPolicy_enforces_max_length_and_excludes_whitespace_or_control_from_special_chars(
        int length,
        bool expectedValid)
    {
        var policy = new PasswordPolicy();
        var password = "Aa1!" + new string('x', length - 4);
        Assert.Equal(expectedValid, policy.Validate(password).IsValid);

        Assert.True(policy.Validate("My Strong Passphrase 2026!").IsValid);
        Assert.False(policy.Validate("Abcdefghijk1 ").IsValid);
        Assert.False(policy.Validate("Abcdefghijk1\t").IsValid);
        Assert.False(policy.Validate("Abcdefghijk1\n").IsValid);
    }

    [Theory]
    [InlineData("must-change", "yêu cầu bạn đổi sang mật khẩu cá nhân mới ngay lập tức")]
    [InlineData("no-change", "liên hệ quản trị viên qua kênh hỗ trợ nội bộ")]
    public void IdentityEmailTemplateRenderer_renders_admin_reset_alert_without_secret(
        string secretFlag,
        string expectedSnippet)
    {
        var renderer = new IdentityEmailTemplateRenderer(Options.Create(new IdentityEmailOptions
        {
            PublicWebBaseUrl = new Uri("https://ugtm.example"),
            OutboxPartition = "unit-tests"
        }));

        var message = renderer.Render(new IdentityEmailPayload(
            "student@ugtm.edu.vn",
            "Nguyễn Văn A",
            "admin-password-reset",
            secretFlag));

        var decodedHtmlBody = WebUtility.HtmlDecode(message.HtmlBody);

        Assert.Equal("student@ugtm.edu.vn", message.To);
        Assert.Contains("Nguyễn Văn A", decodedHtmlBody);
        Assert.Contains(expectedSnippet, decodedHtmlBody);
        Assert.Contains("không chứa mật khẩu", decodedHtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không chứa mật khẩu", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(StrongCustomPassword, message.HtmlBody);
        Assert.DoesNotContain(StrongCustomPassword, message.TextBody!);
    }

    [Fact]
    public async Task HandleAsync_generated_mode_resets_password_revokes_sessions_invalidates_challenges_and_audits_without_secret()
    {
        var store = new ResetTestStore();
        var actorId = Guid.NewGuid();
        var target = CreateUser("lecturer@ugtm.edu.vn", AccountStatus.Active, SystemRoleName.Lecturer);
        target.RecordFailedLogin(Now.AddMinutes(-5), 1, TimeSpan.FromMinutes(15));
        Assert.True(target.IsLockedOut(Now));
        store.Users.Add(target);

        var previousStamp = target.SecurityStamp;
        var handler = CreateHandler(store, actorId, ["Admin"]);

        var result = await handler.HandleAsync(
            new AdminResetPasswordCommand(
                target.Id,
                "Generated",
                null,
                RequirePasswordChange: true,
                AdminPassword,
                "  Quên mật khẩu và mất email  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(target.Id, result.Value.UserId);
        Assert.Equal("Generated", result.Value.Mode);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.TemporaryPassword));
        Assert.Equal(16, result.Value.TemporaryPassword!.Length);
        Assert.True(result.Value.MustChangePassword);
        Assert.True(result.Value.NotificationEmailQueued);
        Assert.Equal($"hash:{result.Value.TemporaryPassword}", target.PasswordHash);
        Assert.True(target.MustChangePassword);
        Assert.Equal(0, target.AccessFailedCount);
        Assert.Null(target.LockoutEndAt);
        Assert.NotEqual(previousStamp, target.SecurityStamp);

        Assert.Equal((target.Id, SessionRevocationReason.AdminPasswordReset), Assert.Single(store.Revocations));
        Assert.Contains((target.Id, ChallengePurpose.PasswordResetOtp), store.InvalidatedChallenges);
        Assert.Contains((target.Id, ChallengePurpose.PasswordResetGrant), store.InvalidatedChallenges);

        var alert = Assert.Single(store.AlertEmails);
        Assert.Equal(target.Email, alert.Email);
        Assert.True(alert.MustChangePassword);

        var audit = Assert.Single(store.Audits);
        Assert.Equal(AuditActions.UserPasswordResetByAdmin, audit.Action);
        Assert.Equal(AuditOutcomes.Success, audit.Outcome);
        var serializedMetadata = System.Text.Json.JsonSerializer.Serialize(audit.Metadata);
        Assert.DoesNotContain(result.Value.TemporaryPassword, serializedMetadata);
        Assert.DoesNotContain(AdminPassword, serializedMetadata);
    }

    [Theory]
    [InlineData(AccountStatus.Active, false)]
    [InlineData(AccountStatus.Suspended, true)]
    [InlineData(AccountStatus.Disabled, true)]
    public async Task HandleAsync_custom_mode_preserves_account_status_and_omits_temporary_password(
        AccountStatus initialStatus,
        bool requireChange)
    {
        var store = new ResetTestStore();
        var actorId = Guid.NewGuid();
        var target = CreateUser("staff@ugtm.edu.vn", initialStatus, SystemRoleName.Faculty);
        store.Users.Add(target);
        var handler = CreateHandler(store, actorId, ["Admin"]);

        var result = await handler.HandleAsync(
            new AdminResetPasswordCommand(
                target.Id,
                "custom",
                StrongCustomPassword,
                requireChange,
                AdminPassword,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Custom", result.Value.Mode);
        Assert.Null(result.Value.TemporaryPassword);
        Assert.Equal(requireChange, result.Value.MustChangePassword);
        Assert.Equal(initialStatus, target.Status);
        Assert.Equal($"hash:{StrongCustomPassword}", target.PasswordHash);
    }

    [Fact]
    public async Task HandleAsync_blocks_self_reset_and_non_admin_targeting_admin_and_pending_account()
    {
        var store = new ResetTestStore();
        var managerId = Guid.NewGuid();
        var adminTarget = CreateUser("root.admin@ugtm.edu.vn", AccountStatus.Active, SystemRoleName.Admin);
        var pendingTarget = CreateUser("pending@ugtm.edu.vn", AccountStatus.PendingActivation, SystemRoleName.Student);
        store.Users.Add(adminTarget);
        store.Users.Add(pendingTarget);

        // Self-reset blocked
        var adminHandler = CreateHandler(store, managerId, ["Admin"]);
        var selfResult = await adminHandler.HandleAsync(
            new AdminResetPasswordCommand(managerId, "Generated", null, true, AdminPassword, null),
            CancellationToken.None);
        Assert.True(selfResult.IsFailure);
        Assert.Equal("Identity.CannotResetOwnPassword", selfResult.Error.Code);

        // Non-Admin with identity.users.manage cannot reset Admin
        var managerHandler = CreateHandler(store, managerId, ["AcademicAffairs"], PermissionCodes.IdentityUsersManage);
        var escalateResult = await managerHandler.HandleAsync(
            new AdminResetPasswordCommand(adminTarget.Id, "Generated", null, true, AdminPassword, null),
            CancellationToken.None);
        Assert.True(escalateResult.IsFailure);
        Assert.Equal("Identity.CannotManageAdminPassword", escalateResult.Error.Code);
        Assert.Equal(AuditOutcomes.Denied, store.Audits.Last().Outcome);

        // Admin CAN reset another Admin
        var peerAdminResult = await adminHandler.HandleAsync(
            new AdminResetPasswordCommand(adminTarget.Id, "Generated", null, true, AdminPassword, null),
            CancellationToken.None);
        Assert.True(peerAdminResult.IsSuccess);

        // PendingActivation blocked
        var pendingResult = await adminHandler.HandleAsync(
            new AdminResetPasswordCommand(pendingTarget.Id, "Generated", null, true, AdminPassword, null),
            CancellationToken.None);
        Assert.True(pendingResult.IsFailure);
        Assert.Equal("Identity.CannotResetPasswordForPendingAccount", pendingResult.Error.Code);
        Assert.Equal(AuditOutcomes.Denied, store.Audits.Last().Outcome);
    }

    [Fact]
    public async Task HandleAsync_handles_reauth_failures_lockout_and_missing_user()
    {
        var store = new ResetTestStore();
        var actorId = Guid.NewGuid();
        var target = CreateUser("user@ugtm.edu.vn", AccountStatus.Active, SystemRoleName.Student);
        store.Users.Add(target);
        var handler = CreateHandler(store, actorId, ["Admin"]);

        store.ReauthOutcome = ReauthenticationOutcome.InvalidCredentials;
        var wrongAuth = await handler.HandleAsync(
            new AdminResetPasswordCommand(target.Id, "Generated", null, true, "WrongAdmin!Pass1", null),
            CancellationToken.None);
        Assert.True(wrongAuth.IsFailure);
        Assert.Equal("Identity.ReauthenticationFailed", wrongAuth.Error.Code);

        store.ReauthOutcome = ReauthenticationOutcome.AccountLocked;
        var lockedAuth = await handler.HandleAsync(
            new AdminResetPasswordCommand(target.Id, "Generated", null, true, AdminPassword, null),
            CancellationToken.None);
        Assert.True(lockedAuth.IsFailure);
        Assert.Equal("Identity.AccountLocked", lockedAuth.Error.Code);

        store.ReauthOutcome = ReauthenticationOutcome.Verified;
        var missingResult = await handler.HandleAsync(
            new AdminResetPasswordCommand(Guid.NewGuid(), "Generated", null, true, AdminPassword, null),
            CancellationToken.None);
        Assert.True(missingResult.IsFailure);
        Assert.Equal("Identity.UserNotFound", missingResult.Error.Code);
    }

    [Theory]
    [InlineData("", null, "Identity.InvalidPasswordResetMode")]
    [InlineData("0", null, "Identity.InvalidPasswordResetMode")]
    [InlineData("1", null, "Identity.InvalidPasswordResetMode")]
    [InlineData("Default", null, "Identity.InvalidPasswordResetMode")]
    [InlineData("Custom", "weak", "Identity.WeakPassword")]
    [InlineData("Custom", null, "Identity.WeakPassword")]
    [InlineData("Generated", "ShouldNotBeProvided123!", "Identity.InvalidInput")]
    public async Task HandleAsync_rejects_invalid_mode_and_password_combinations(
        string mode,
        string? newPassword,
        string expectedError)
    {
        var store = new ResetTestStore();
        var handler = CreateHandler(store, Guid.NewGuid(), ["Admin"]);

        var result = await handler.HandleAsync(
            new AdminResetPasswordCommand(Guid.NewGuid(), mode, newPassword, true, AdminPassword, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error.Code);
        Assert.False(store.ReauthCalled);
    }

    [Fact]
    public async Task HandleAsync_rejects_overlong_reason_empty_admin_password_empty_id_and_unauthenticated()
    {
        var store = new ResetTestStore();
        var unauthenticated = CreateHandler(store, null, []);
        var unauthResult = await unauthenticated.HandleAsync(
            new AdminResetPasswordCommand(Guid.NewGuid(), "Generated", null, true, AdminPassword, null),
            CancellationToken.None);
        Assert.Equal("Identity.InvalidSession", unauthResult.Error.Code);

        var handler = CreateHandler(store, Guid.NewGuid(), ["Admin"]);
        Assert.Equal("Identity.InvalidInput", (await handler.HandleAsync(
            new AdminResetPasswordCommand(Guid.Empty, "Generated", null, true, AdminPassword, null),
            CancellationToken.None)).Error.Code);
        Assert.Equal("Identity.InvalidInput", (await handler.HandleAsync(
            new AdminResetPasswordCommand(Guid.NewGuid(), "Generated", null, true, AdminPassword, new string('R', 501)),
            CancellationToken.None)).Error.Code);
        Assert.Equal("Identity.ReauthenticationFailed", (await handler.HandleAsync(
            new AdminResetPasswordCommand(Guid.NewGuid(), "Generated", null, true, string.Empty, null),
            CancellationToken.None)).Error.Code);
    }

    private static AdminResetPasswordHandler CreateHandler(
        ResetTestStore store,
        Guid? actorId,
        string[] roles,
        params string[] permissions)
    {
        var currentUser = new FakeCurrentUser(actorId, roles, permissions);
        return new AdminResetPasswordHandler(
            currentUser,
            store,
            store,
            store,
            store,
            store,
            new PasswordPolicy(),
            new CryptoTemporaryPasswordGenerator(),
            store,
            store,
            store,
            store);
    }

    private static User CreateUser(string email, AccountStatus status, SystemRoleName roleName)
    {
        var user = User.Create(
            Guid.NewGuid(),
            email,
            email.ToUpperInvariant(),
            "Test User",
            null,
            null,
            status == AccountStatus.PendingActivation ? null : "hash:OldPassword!123",
            status == AccountStatus.PendingActivation ? AccountStatus.PendingActivation : AccountStatus.Active,
            Now.AddDays(-1));
        if (status == AccountStatus.Suspended) user.Suspend(Now.AddHours(-2));
        if (status == AccountStatus.Disabled) user.Disable(Now.AddHours(-2));
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = (int)roleName + 1,
            Role = new Role { Id = (int)roleName + 1, RoleName = roleName.ToString(), Description = roleName.ToString() },
            User = user,
            AssignedAt = Now.AddDays(-1)
        });
        return user;
    }

    private sealed class FakeCurrentUser(Guid? userId, string[] roles, string[] permissions) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ResetTestStore :
        IUserRepository,
        IAuthenticationSessionRepository,
        IOneTimeChallengeRepository,
        IReauthenticationVerifier,
        IPasswordService,
        IIdentityEmailOutbox,
        IIdentityUnitOfWork,
        IAuditLogWriter,
        IClock
    {
        public List<User> Users { get; } = [];
        public List<AuditEntry> Audits { get; } = [];
        public List<(Guid UserId, SessionRevocationReason Reason)> Revocations { get; } = [];
        public List<(Guid UserId, ChallengePurpose Purpose)> InvalidatedChallenges { get; } = [];
        public List<(Guid UserId, string Email, bool MustChangePassword)> AlertEmails { get; } = [];
        public ReauthenticationOutcome ReauthOutcome { get; set; } = ReauthenticationOutcome.Verified;
        public bool ReauthCalled { get; private set; }
        public DateTime UtcNow => Now;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public void Add(AuditEntry entry) => Audits.Add(entry);

        public Task<ReauthenticationOutcome> VerifyPasswordAsync(Guid userId, string password, CancellationToken ct = default)
        {
            ReauthCalled = true;
            return Task.FromResult(ReauthOutcome);
        }

        public string Hash(User user, string password) => $"hash:{password}";
        public bool Verify(User user, string passwordHash, string suppliedPassword) => passwordHash == $"hash:{suppliedPassword}";

        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => GetByIdWithRolesAsync(id, ct);
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken ct) => throw new NotSupportedException();
        public Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken ct) => throw new NotSupportedException();
        public Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Role>> GetRolesByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken ct) => throw new NotSupportedException();
        public Task AddToRolesByNameAsync(User user, IReadOnlyCollection<string> roleNames, DateTime assignedAt, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateRolesByNameAsync(User user, IReadOnlyCollection<string> roleNames, DateTime assignedAt, CancellationToken ct) => throw new NotSupportedException();
        public Task AcquireAccountProvisioningLockAsync(CancellationToken ct) => Task.CompletedTask;
        public Task AcquireAdminMutationLockAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken ct) => Task.FromResult(0);
        public Task<bool> AnyAdminExistsAsync(CancellationToken ct) => Task.FromResult(false);
        public void Add(User user) => Users.Add(user);

        public void AddSession(AuthenticationSession session) { }
        public void AddRefreshToken(RefreshToken refreshToken) { }
        public Task<AuthenticationSession?> GetSessionAsync(Guid sessionId, CancellationToken ct) => Task.FromResult<AuthenticationSession?>(null);
        public Task<RefreshToken?> GetRefreshTokenForUpdateAsync(string selector, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task<IReadOnlyList<AuthenticationSession>> GetActiveSessionsAsync(Guid userId, DateTime now, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuthenticationSession>>([]);
        public Task<bool> RevokeActiveSessionAsync(Guid sessionId, Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken ct) => Task.FromResult(false);
        public Task<int> RevokeActiveSessionsAsync(Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken ct) => Task.FromResult(0);
        public Task RevokeAllAsync(Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken ct)
        {
            Revocations.Add((userId, reason));
            return Task.CompletedTask;
        }

        public void Add(OneTimeChallenge challenge) { }
        public Task<OneTimeChallenge?> GetAsync(string selector, ChallengePurpose purpose, CancellationToken ct) => Task.FromResult<OneTimeChallenge?>(null);
        public Task<OneTimeChallenge?> GetForUpdateAsync(string selector, ChallengePurpose purpose, CancellationToken ct) => Task.FromResult<OneTimeChallenge?>(null);
        public Task InvalidateOutstandingAsync(Guid userId, ChallengePurpose purpose, DateTime now, CancellationToken ct)
        {
            InvalidatedChallenges.Add((userId, purpose));
            return Task.CompletedTask;
        }

        public void EnqueueActivationEmail(Guid userId, string email, string fullName, string rawToken, DateTime createdAt) { }
        public void EnqueuePasswordRecoveryEmail(Guid userId, string email, string fullName, string code, DateTime createdAt) { }
        public void EnqueueAdminPasswordResetAlertEmail(Guid userId, string email, string fullName, bool mustChangePassword, DateTime createdAt)
            => AlertEmails.Add((userId, email, mustChangePassword));
    }
}
