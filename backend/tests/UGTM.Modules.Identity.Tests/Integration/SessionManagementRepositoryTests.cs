using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class SessionManagementRepositoryTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now =
        new(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Active_session_queries_and_revocation_ignore_expired_or_stale_sessions()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
        await db.Database.EnsureCreatedAsync();

        var user = User.ProvisionStudent(
            Guid.NewGuid(),
            "session-test@ugtm.edu.vn",
            "SESSION-TEST@UGTM.EDU.VN",
            "Session Test",
            "SV-SESSION-TEST",
            null,
            Now.AddDays(-45));
        user.Activate("password-hash", Now.AddDays(-44));

        var stale = AuthenticationSession.Create(
            user.Id,
            user.SecurityStamp,
            Now.AddHours(-1),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            "Stale device",
            null);
        user.RotateSecurityStamp();
        var active = AuthenticationSession.Create(
            user.Id,
            user.SecurityStamp,
            Now.AddMinutes(-10),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            "Active device",
            null);
        var expired = AuthenticationSession.Create(
            user.Id,
            user.SecurityStamp,
            Now.AddDays(-40),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            "Expired device",
            null);

        db.AddRange(user, stale, active, expired);
        await db.SaveChangesAsync();
        var repository = new AuthenticationSessionRepository(db);

        var before = await repository.GetActiveSessionsAsync(
            user.Id,
            Now,
            CancellationToken.None);
        var staleRevoked = await repository.RevokeActiveSessionAsync(
            stale.Id,
            user.Id,
            SessionRevocationReason.AdministratorRevoked,
            Now,
            CancellationToken.None);
        var activeRevoked = await repository.RevokeActiveSessionAsync(
            active.Id,
            user.Id,
            SessionRevocationReason.AdministratorRevoked,
            Now,
            CancellationToken.None);
        var after = await repository.GetActiveSessionsAsync(
            user.Id,
            Now,
            CancellationToken.None);

        Assert.Equal(active.Id, Assert.Single(before).Id);
        Assert.False(staleRevoked);
        Assert.True(activeRevoked);
        Assert.Empty(after);
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new IdentityDbContext(options.Options);
    }
}
