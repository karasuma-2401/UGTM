using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Infrastructure.Auditing;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Domain.Entities;

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

    [Fact]
    public async Task Reader_searches_before_paging_and_includes_legacy_outcomes_in_statistics_and_export()
    {
        await ResetDatabaseAsync();
        await using var db = CreateDbContext();
        db.AuditLogs.AddRange(
            AuditLog.Create(null, null, "User", "first", "Identity.User.Created", "Success", null, "request-1", "{\"note\":\"unrelated\"}", Now),
            AuditLog.Create(null, null, "User", "second", "Identity.User.Created", "Succeeded", null, "request-2", "{\"note\":\"needle\"}", Now.AddMinutes(-1)),
            AuditLog.Create(null, null, "User", "third", "Identity.User.Created", "Denied", null, "request-3", "{\"note\":\"needle\"}", Now.AddMinutes(-2)));
        await db.SaveChangesAsync();

        var reader = new AuditLogQueryReader(db);
        var query = new GetAuditLogsQuery(PageSize: 1, SearchTerm: "needle");
        var first = await reader.GetAuditLogsAsync(query, CancellationToken.None);
        var second = await reader.GetAuditLogsAsync(query with { PageNumber = 2 }, CancellationToken.None);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal("second", Assert.Single(first.Items).TargetId);
        Assert.Equal("Success", Assert.Single(first.Items).Outcome);
        Assert.Equal("third", Assert.Single(second.Items).TargetId);
        var stats = await reader.GetStatisticsAsync(query, CancellationToken.None);
        Assert.Equal((2, 1, 1, 0), (stats.Total, stats.Success, stats.Denied, stats.Failed));
        var success = await reader.GetAuditLogsAsync(query with { Outcome = "Success" }, CancellationToken.None);
        Assert.Equal("second", Assert.Single(success.Items).TargetId);
        var exported = new List<string>();
        await foreach (var row in reader.ExportAsync(query, CancellationToken.None)) exported.Add(row.TargetId);
        Assert.Equal(new[] { "second", "third" }, exported);
        var exact = await reader.GetAuditLogsAsync(new GetAuditLogsQuery(RequestId: "request-2"), CancellationToken.None);
        Assert.Equal("second", Assert.Single(exact.Items).TargetId);
        var literalWildcard = await reader.GetAuditLogsAsync(new GetAuditLogsQuery(SearchTerm: "%"), CancellationToken.None);
        Assert.Empty(literalWildcard.Items);
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
