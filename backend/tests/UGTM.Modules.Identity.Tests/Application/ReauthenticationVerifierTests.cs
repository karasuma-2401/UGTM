using UGTM.BuildingBlocks.Application;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Authentication.Reauthentication;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using Xunit;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class ReauthenticationVerifierTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task VerifyPasswordAsync_CorrectPassword_ReturnsVerifiedAndEmitsSuccessAudit()
    {
        var user = CreateActiveUser();
        var auditLogs = new FakeAuditLogWriter();
        var passwords = new FakePasswordService("CorrectPass123!");
        var verifier = CreateVerifier(user, passwords, auditLogs);

        var outcome = await verifier.VerifyPasswordAsync(user.Id, "CorrectPass123!", CancellationToken.None);

        Assert.Equal(ReauthenticationOutcome.Verified, outcome);
        Assert.Equal(0, user.AccessFailedCount);

        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.Reauthentication, audit.Action);
        Assert.Equal(AuditOutcomes.Success, audit.Outcome);
        Assert.Equal(user.Id.ToString(), audit.TargetId);
    }

    [Fact]
    public async Task VerifyPasswordAsync_WrongPassword_ReturnsInvalidCredentialsAndIncrementsAttempts()
    {
        var user = CreateActiveUser();
        var auditLogs = new FakeAuditLogWriter();
        var passwords = new FakePasswordService("CorrectPass123!");
        var verifier = CreateVerifier(user, passwords, auditLogs);

        var outcome = await verifier.VerifyPasswordAsync(user.Id, "WrongPass", CancellationToken.None);

        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, outcome);
        Assert.Equal(1, user.AccessFailedCount);

        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.Reauthentication, audit.Action);
        Assert.Equal(AuditOutcomes.Denied, audit.Outcome);
        Assert.Equal(IdentityErrors.InvalidCredentials.Code, audit.ReasonCode);
    }

    [Fact]
    public async Task VerifyPasswordAsync_WrongPasswordExceedsMaxAttempts_LocksAccount()
    {
        var user = CreateActiveUser();
        var policy = new IdentitySecurityPolicy { MaxFailedLoginAttempts = 3, LockoutDuration = TimeSpan.FromMinutes(15) };
        var auditLogs = new FakeAuditLogWriter();
        var passwords = new FakePasswordService("CorrectPass123!");
        var verifier = CreateVerifier(user, passwords, auditLogs, policy);

        // Fail 2 times
        await verifier.VerifyPasswordAsync(user.Id, "WrongPass1", CancellationToken.None);
        await verifier.VerifyPasswordAsync(user.Id, "WrongPass2", CancellationToken.None);

        // 3rd failure locks the account
        var outcome = await verifier.VerifyPasswordAsync(user.Id, "WrongPass3", CancellationToken.None);

        Assert.Equal(ReauthenticationOutcome.AccountLocked, outcome);
        Assert.True(user.IsLockedOut(Now));
        Assert.NotNull(user.LockoutEndAt);

        var lastAudit = auditLogs.Entries.Last();
        Assert.Equal(AuditOutcomes.Denied, lastAudit.Outcome);
        Assert.Equal(IdentityErrors.AccountLocked.Code, lastAudit.ReasonCode);
    }

    [Fact]
    public async Task VerifyPasswordAsync_AlreadyLockedAccount_ReturnsAccountLocked()
    {
        var user = CreateActiveUser();
        user.RecordFailedLogin(Now, 1, TimeSpan.FromMinutes(15)); // Locked out
        var auditLogs = new FakeAuditLogWriter();
        var passwords = new FakePasswordService("CorrectPass123!");
        var verifier = CreateVerifier(user, passwords, auditLogs);

        var outcome = await verifier.VerifyPasswordAsync(user.Id, "CorrectPass123!", CancellationToken.None);

        Assert.Equal(ReauthenticationOutcome.AccountLocked, outcome);

        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(IdentityErrors.AccountLocked.Code, audit.ReasonCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task VerifyPasswordAsync_EmptyPassword_ReturnsInvalidCredentials(string? password)
    {
        var user = CreateActiveUser();
        var auditLogs = new FakeAuditLogWriter();
        var passwords = new FakePasswordService("CorrectPass123!");
        var verifier = CreateVerifier(user, passwords, auditLogs);

        var outcome = await verifier.VerifyPasswordAsync(user.Id, password!, CancellationToken.None);

        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, outcome);
    }

    [Fact]
    public async Task VerifyPasswordAsync_NonExistentUser_ReturnsInvalidCredentials()
    {
        var auditLogs = new FakeAuditLogWriter();
        var passwords = new FakePasswordService("CorrectPass123!");
        var verifier = CreateVerifier(null, passwords, auditLogs);

        var outcome = await verifier.VerifyPasswordAsync(Guid.NewGuid(), "AnyPass", CancellationToken.None);

        Assert.Equal(ReauthenticationOutcome.InvalidCredentials, outcome);
    }

    private static User CreateActiveUser()
    {
        var user = User.ProvisionStudent(
            Guid.NewGuid(),
            "lecturer@ugtm.edu.vn",
            "LECTURER@UGTM.EDU.VN",
            "Lecturer",
            "GV001",
            null,
            Now);
        user.Activate("hashed-password", Now);
        return user;
    }

    private static ReauthenticationVerifier CreateVerifier(
        User? user,
        IPasswordService passwords,
        IAuditLogWriter auditLogs,
        IdentitySecurityPolicy? policy = null)
    {
        return new ReauthenticationVerifier(
            new SingleUserRepository(user),
            passwords,
            new InlineUnitOfWork(),
            auditLogs,
            new FixedClock(Now),
            policy ?? new IdentitySecurityPolicy());
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class FakePasswordService(string validPassword) : IPasswordService
    {
        public string Hash(User user, string password) => "hashed-" + password;

        public bool Verify(User user, string hash, string providedPassword)
            => providedPassword == validPassword;

        public bool NeedsRehash(string hash) => false;
    }

    private sealed class InlineUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
            => operation(cancellationToken);
    }

    private sealed class FakeAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Add(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class SingleUserRepository(User? user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(user != null && user.Id == id ? user : null);

        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddToRoleAsync(User value, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task UpdateRolesAsync(User value, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AcquireAccountProvisioningLockAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(User value) => throw new NotSupportedException();
    }
}
