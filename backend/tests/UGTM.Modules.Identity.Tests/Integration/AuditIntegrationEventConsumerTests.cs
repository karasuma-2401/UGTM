using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Infrastructure.Auditing;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuditIntegrationEventConsumerTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now =
        new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Concurrent_delivery_of_same_event_creates_one_audit_log()
    {
        await ResetDatabaseAsync();
        var eventId = Guid.NewGuid();
        var integrationEvent = new AuditIntegrationEvent(
            eventId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "request-123",
            "CouncilMinute",
            Guid.NewGuid().ToString("D"),
            IntegrationAuditActions.CouncilMinuteUnlockGranted,
            "Success",
            null,
            "{\"reason\":\"approved\"}",
            Now);

        await using var firstDb = CreateDbContext();
        await using var secondDb = CreateDbContext();
        var firstConsumer = new AuditIntegrationEventConsumer(firstDb);
        var secondConsumer = new AuditIntegrationEventConsumer(secondDb);

        await Task.WhenAll(
            firstConsumer.ConsumeAsync(integrationEvent, CancellationToken.None),
            secondConsumer.ConsumeAsync(integrationEvent, CancellationToken.None));

        await using var verificationDb = CreateDbContext();
        var logs = await verificationDb.AuditLogs
            .AsNoTracking()
            .Where(x => x.IntegrationEventId == eventId)
            .ToListAsync();
        var auditLog = Assert.Single(logs);
        Assert.Equal(integrationEvent.Action, auditLog.Action);
        Assert.Equal(integrationEvent.ActorUserId, auditLog.ActorUserId);
        using var metadata = JsonDocument.Parse(auditLog.MetadataJson!);
        Assert.Equal("approved", metadata.RootElement.GetProperty("reason").GetString());
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
}
