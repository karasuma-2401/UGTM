using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Application.Activation;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Authentication.RefreshSession;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthenticationConcurrencyTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now =
        new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Concurrent_refresh_only_rotates_once_and_revokes_reused_session()
    {
        var opaqueTokens = new OpaqueTokenService();
        var rawToken = opaqueTokens.CreateToken();
        var sessionId = await SeedActiveSessionAsync(rawToken);

        await using var firstDb = CreateDbContext();
        await using var secondDb = CreateDbContext();
        var auditLogs = new ConcurrentAuditLogWriter();
        var firstHandler = CreateRefreshHandler(firstDb, opaqueTokens, auditLogs);
        var secondHandler = CreateRefreshHandler(secondDb, opaqueTokens, auditLogs);

        var results = await Task.WhenAll(
            firstHandler.HandleAsync(new(rawToken.RawToken), CancellationToken.None),
            secondHandler.HandleAsync(new(rawToken.RawToken), CancellationToken.None));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.IsFailure);

        await using var verificationDb = CreateDbContext();
        var session = await verificationDb.AuthenticationSessions
            .AsNoTracking()
            .SingleAsync(value => value.Id == sessionId);
        var storedTokens = await verificationDb.RefreshTokens
            .AsNoTracking()
            .Where(value => value.SessionId == sessionId)
            .ToListAsync();

        Assert.Equal(SessionRevocationReason.RefreshTokenReuse, session.RevocationReason);
        Assert.NotNull(session.RevokedAt);
        Assert.Equal(2, storedTokens.Count);
        Assert.Single(storedTokens, value => value.ConsumedAt is not null);
        Assert.Contains(auditLogs.Entries, entry =>
            entry.Action == AuditActions.SessionRefreshed
            && entry.Outcome == AuditOutcomes.Success);
        Assert.Contains(auditLogs.Entries, entry =>
            entry.Action == AuditActions.RefreshTokenReuseDetected
            && entry.Outcome == AuditOutcomes.Denied);
    }

    [Fact]
    public async Task Concurrent_activation_consumes_magic_link_once()
    {
        var opaqueTokens = new OpaqueTokenService();
        var rawToken = opaqueTokens.CreateToken();
        var userId = await SeedPendingActivationAsync(rawToken);

        await using var firstDb = CreateDbContext();
        await using var secondDb = CreateDbContext();
        var auditLogs = new ConcurrentAuditLogWriter();
        var firstHandler = CreateActivationHandler(firstDb, opaqueTokens, auditLogs);
        var secondHandler = CreateActivationHandler(secondDb, opaqueTokens, auditLogs);
        var command = new CompleteActivationCommand(rawToken.RawToken, "StrongPassword!123");

        var results = await Task.WhenAll(
            firstHandler.HandleAsync(command, CancellationToken.None),
            secondHandler.HandleAsync(command, CancellationToken.None));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.IsFailure);

        await using var verificationDb = CreateDbContext();
        var user = await verificationDb.Users.AsNoTracking()
            .SingleAsync(value => value.Id == userId);
        var challenge = await verificationDb.OneTimeChallenges.AsNoTracking()
            .SingleAsync(value => value.Selector == rawToken.Selector);

        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.NotNull(user.PasswordHash);
        Assert.NotNull(challenge.ConsumedAt);
        Assert.Contains(auditLogs.Entries, entry =>
            entry.Action == AuditActions.AccountActivated
            && entry.Outcome == AuditOutcomes.Success);
        Assert.Contains(auditLogs.Entries, entry =>
            entry.Action == AuditActions.AccountActivated
            && entry.Outcome == AuditOutcomes.Denied);
    }

    private async Task<Guid> SeedActiveSessionAsync(OpaqueToken rawToken)
    {
        await ResetDatabaseAsync();
        await using var db = CreateDbContext();
        var user = CreateUser();
        user.Activate("seed-password-hash", Now.AddDays(-1));
        var session = AuthenticationSession.Create(
            user.Id,
            user.SecurityStamp,
            Now,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            null,
            null);
        var refreshToken = RefreshToken.Create(
            session.Id,
            rawToken.Selector,
            rawToken.SecretHash,
            Now,
            session.IdleExpiresAt);

        db.AddRange(user, session, refreshToken);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<Guid> SeedPendingActivationAsync(OpaqueToken rawToken)
    {
        await ResetDatabaseAsync();
        await using var db = CreateDbContext();
        var user = CreateUser();
        var challenge = OneTimeChallenge.Create(
            user.Id,
            ChallengePurpose.ActivateAccount,
            rawToken.Selector,
            rawToken.SecretHash,
            user.Email,
            5,
            Now,
            Now.AddDays(1));

        db.AddRange(user, challenge);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
        await db.Database.EnsureCreatedAsync();
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new IdentityDbContext(options.Options);
    }

    private static RefreshSessionHandler CreateRefreshHandler(
        IdentityDbContext db,
        IOpaqueTokenService tokens,
        IAuditLogWriter auditLogs)
        => new(
            new AuthenticationSessionRepository(db),
            new UserProfileReader(db),
            tokens,
            new StubAccessTokenService(),
            new IdentityUnitOfWork(db),
            auditLogs,
            new FixedClock(),
            new IdentitySecurityPolicy());

    private static CompleteActivationHandler CreateActivationHandler(
        IdentityDbContext db,
        IOpaqueTokenService tokens,
        IAuditLogWriter auditLogs)
        => new(
            new OneTimeChallengeRepository(db),
            new UserRepository(db),
            tokens,
            new AspNetPasswordService(),
            new PasswordPolicy(),
            new IdentityUnitOfWork(db),
            auditLogs,
            new FixedClock());

    private static User CreateUser()
        => User.ProvisionStudent(
            Guid.NewGuid(),
            "student@ugtm.edu.vn",
            "STUDENT@UGTM.EDU.VN",
            "Student",
            $"SV{Guid.NewGuid():N}"[..20],
            null,
            Now.AddDays(-2));

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class StubAccessTokenService : IAccessTokenService
    {
        public AccessTokenResult Create(UserProfileDto user, Guid sessionId, DateTime now)
            => new("access-token", now.AddMinutes(10));
    }

    private sealed class ConcurrentAuditLogWriter : IAuditLogWriter
    {
        public ConcurrentQueue<AuditEntry> Entries { get; } = new();

        public void Add(AuditEntry entry) => Entries.Enqueue(entry);
    }
}
