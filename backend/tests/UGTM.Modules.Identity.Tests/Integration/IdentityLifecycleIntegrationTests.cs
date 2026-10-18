using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Application.Activation;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Authentication.Login;
using UGTM.Modules.Identity.Application.Authentication.RefreshSession;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Recovery;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Sessions;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Notifications;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class IdentityLifecycleIntegrationTests(PostgreSqlFixture fixture)
{
    private const string InitialPassword = "StrongPassword!123";
    private const string ResetPassword = "ResetPassword!456";
    private static readonly DateTime Now =
        new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Full_identity_lifecycle_activates_logs_in_rotates_revokes_and_recovers()
    {
        var tokens = new OpaqueTokenService();
        var passwordService = new AspNetPasswordService();
        var policy = new IdentitySecurityPolicy();
        var clock = new FixedClock();
        var audit = new RecordingAuditLogWriter();
        var dataProtection = new EphemeralDataProtectionProvider();
        var (userId, activationToken) = await SeedPendingStudentAsync(tokens, policy);

        await using (var db = CreateDbContext())
        {
            var activation = new CompleteActivationHandler(
                new OneTimeChallengeRepository(db),
                new UserRepository(db),
                tokens,
                passwordService,
                new PasswordPolicy(),
                new IdentityUnitOfWork(db),
                audit,
                clock);

            var result = await activation.HandleAsync(
                new CompleteActivationCommand(activationToken, InitialPassword),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        AuthenticatedSessionResult loggedIn;
        await using (var db = CreateDbContext())
        {
            loggedIn = AssertSuccess(await CreateLoginHandler(
                    db, tokens, passwordService, audit, clock, policy)
                .HandleAsync(
                    new LoginCommand(
                        "student@ugtm.edu.vn",
                        InitialPassword,
                        "Chrome / Windows",
                        "ip-hash"),
                    CancellationToken.None));
        }

        Guid sessionId;
        await using (var db = CreateDbContext())
        {
            sessionId = await db.AuthenticationSessions
                .Where(session => session.UserId == userId)
                .Select(session => session.Id)
                .SingleAsync();
        }

        AuthenticatedSessionResult refreshed;
        await using (var db = CreateDbContext())
        {
            var refresh = new RefreshSessionHandler(
                new AuthenticationSessionRepository(db),
                new UserProfileReader(db),
                tokens,
                new StubAccessTokenService(),
                new IdentityUnitOfWork(db),
                audit,
                clock,
                policy);

            refreshed = AssertSuccess(await refresh.HandleAsync(
                new RefreshSessionCommand(loggedIn.Tokens.RefreshToken),
                CancellationToken.None));
        }

        await using (var db = CreateDbContext())
        {
            var revoke = new RevokeMySessionHandler(
                new StubCurrentUser(userId, sessionId),
                new AuthenticationSessionRepository(db),
                new IdentityUnitOfWork(db),
                audit,
                clock);

            var result = await revoke.HandleAsync(sessionId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value.CurrentSessionRevoked);
        }

        await using (var db = CreateDbContext())
        {
            var refresh = new RefreshSessionHandler(
                new AuthenticationSessionRepository(db),
                new UserProfileReader(db),
                tokens,
                new StubAccessTokenService(),
                new IdentityUnitOfWork(db),
                audit,
                clock,
                policy);

            var result = await refresh.HandleAsync(
                new RefreshSessionCommand(refreshed.Tokens.RefreshToken),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal("Identity.InvalidSession", result.Error.Code);
        }

        StartPasswordRecoveryResult recovery;
        await using (var db = CreateDbContext())
        {
            var startRecovery = new StartPasswordRecoveryHandler(
                new UserRepository(db),
                new OneTimeChallengeRepository(db),
                tokens,
                new IdentityEmailOutbox(db, dataProtection),
                new IdentityUnitOfWork(db),
                audit,
                clock,
                policy);

            recovery = AssertSuccess(await startRecovery.HandleAsync(
                new StartPasswordRecoveryCommand("student@ugtm.edu.vn"),
                CancellationToken.None));
        }

        string recoveryCode;
        await using (var db = CreateDbContext())
        {
            var message = await db.IdentityOutboxMessages
                .AsNoTracking()
                .SingleAsync(value => value.Type == "recovery-otp");
            var json = dataProtection
                .CreateProtector(message.ProtectionPurpose)
                .Unprotect(message.ProtectedPayload);
            recoveryCode = JsonSerializer.Deserialize<IdentityEmailPayload>(json)!.Secret;
        }

        VerifyRecoveryOtpResult verified;
        await using (var db = CreateDbContext())
        {
            var verifyRecovery = new VerifyRecoveryOtpHandler(
                new OneTimeChallengeRepository(db),
                tokens,
                new IdentityUnitOfWork(db),
                audit,
                clock,
                policy);

            verified = AssertSuccess(await verifyRecovery.HandleAsync(
                new VerifyRecoveryOtpCommand(recovery.ChallengeId, recoveryCode),
                CancellationToken.None));
        }

        await using (var db = CreateDbContext())
        {
            var reset = new ResetPasswordHandler(
                new UserRepository(db),
                new OneTimeChallengeRepository(db),
                new AuthenticationSessionRepository(db),
                tokens,
                passwordService,
                new PasswordPolicy(),
                new IdentityUnitOfWork(db),
                audit,
                clock);

            var result = await reset.HandleAsync(
                new ResetPasswordCommand(verified.ResetGrant, ResetPassword),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        await using (var db = CreateDbContext())
        {
            var oldPassword = await CreateLoginHandler(
                    db, tokens, passwordService, audit, clock, policy)
                .HandleAsync(
                    new LoginCommand("SV0001", InitialPassword, null, null),
                    CancellationToken.None);

            Assert.True(oldPassword.IsFailure);
            Assert.Equal("Identity.InvalidCredentials", oldPassword.Error.Code);
        }

        await using (var db = CreateDbContext())
        {
            var newPassword = await CreateLoginHandler(
                    db, tokens, passwordService, audit, clock, policy)
                .HandleAsync(
                    new LoginCommand("SV0001", ResetPassword, null, null),
                    CancellationToken.None);

            Assert.True(newPassword.IsSuccess);
            Assert.Contains(SystemRoleName.Student.ToString(), newPassword.Value.User.Roles);
        }

        await using (var db = CreateDbContext())
        {
            var revokedSession = await db.AuthenticationSessions
                .AsNoTracking()
                .SingleAsync(value => value.Id == sessionId);
            Assert.Equal(SessionRevocationReason.UserLogout, revokedSession.RevocationReason);
            Assert.NotNull(revokedSession.RevokedAt);
        }

        Assert.Contains(audit.Entries, entry => entry.Action == AuditActions.AccountActivated);
        Assert.Contains(audit.Entries, entry =>
            entry.Action == AuditActions.Login && entry.Outcome == AuditOutcomes.Success);
        Assert.Contains(audit.Entries, entry => entry.Action == AuditActions.SessionRefreshed);
        Assert.Contains(audit.Entries, entry => entry.Action == AuditActions.SessionRevoked);
        Assert.Contains(audit.Entries, entry => entry.Action == AuditActions.PasswordReset);
    }

    private async Task<(Guid UserId, string ActivationToken)> SeedPendingStudentAsync(
        OpaqueTokenService tokens,
        IdentitySecurityPolicy policy)
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
        await db.Database.EnsureCreatedAsync();

        var user = User.ProvisionStudent(
            Guid.NewGuid(),
            "student@ugtm.edu.vn",
            "STUDENT@UGTM.EDU.VN",
            "Student Lifecycle",
            "SV0001",
            null,
            Now);
        var activationToken = tokens.CreateToken();
        var challenge = OneTimeChallenge.Create(
            user.Id,
            ChallengePurpose.ActivateAccount,
            activationToken.Selector,
            activationToken.SecretHash,
            user.Email,
            policy.ChallengeMaxAttempts,
            Now,
            Now.Add(policy.ActivationLifetime));
        var studentRoleId = await db.Roles
            .Where(role => role.RoleName == SystemRoleName.Student)
            .Select(role => role.Id)
            .SingleAsync();

        db.Users.Add(user);
        db.OneTimeChallenges.Add(challenge);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = studentRoleId,
            AssignedAt = Now
        });
        await db.SaveChangesAsync();
        return (user.Id, activationToken.RawToken);
    }

    private static LoginHandler CreateLoginHandler(
        IdentityDbContext db,
        IOpaqueTokenService tokens,
        IPasswordService passwords,
        IAuditLogWriter audit,
        IClock clock,
        IdentitySecurityPolicy policy)
        => new(
            new UserRepository(db),
            new AuthenticationSessionRepository(db),
            new UserProfileReader(db),
            passwords,
            tokens,
            new StubAccessTokenService(),
            new IdentityUnitOfWork(db),
            audit,
            clock,
            policy);

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new IdentityDbContext(options.Options);
    }

    private static T AssertSuccess<T>(UGTM.BuildingBlocks.Domain.Result<T> result)
    {
        Assert.True(result.IsSuccess, result.Error.Code);
        return result.Value;
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class StubAccessTokenService : IAccessTokenService
    {
        public AccessTokenResult Create(
            UserProfileDto user,
            Guid sessionId,
            DateTime now)
            => new($"access-token-{sessionId:N}", now.AddMinutes(10));
    }

    private sealed class StubCurrentUser(Guid userId, Guid sessionId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => sessionId;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class RecordingAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Add(AuditEntry entry) => Entries.Add(entry);
    }
}
