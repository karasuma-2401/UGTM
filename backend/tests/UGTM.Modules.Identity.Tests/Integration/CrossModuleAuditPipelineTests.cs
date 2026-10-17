using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Infrastructure.Auditing;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Infrastructure.Auditing;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class CrossModuleAuditPipelineTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now =
        new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Academic_outbox_is_consumed_into_identity_audit_log()
    {
        await ResetAndMigrateDatabaseAsync();
        try
        {
            Guid eventId;

            await using (var academicDb = CreateAcademicDbContext())
            {
                var outbox = new AcademicPeriodsAuditOutbox(
                    academicDb,
                    new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()),
                    new HttpContextAccessor
                    {
                        HttpContext = new DefaultHttpContext
                        {
                            TraceIdentifier = "cross-module-request"
                        }
                    });
                outbox.Add(
                    new AuditIntegrationEntry(
                        "KltnBatch",
                        "42",
                        IntegrationAuditActions.BatchConfigurationUpdated,
                        "Success",
                        null,
                        new { ChangedFields = new[] { "startDate", "endDate" } }),
                    Now);
                eventId = Assert.Single(academicDb.AuditOutboxMessages.Local).Id;
                await academicDb.SaveChangesAsync();
            }

            await using (var academicDb = CreateAcademicDbContext())
            await using (var identityDb = CreateIdentityDbContext())
            {
                var source = new AcademicPeriodsAuditEventSource(academicDb);
                var consumer = new AuditIntegrationEventConsumer(identityDb);
                await source.ProcessBatchAsync(
                    consumer.ConsumeAsync,
                    Now,
                    20,
                    CancellationToken.None);
            }

            await using (var academicDb = CreateAcademicDbContext())
            await using (var identityDb = CreateIdentityDbContext())
            {
                var message = await academicDb.AuditOutboxMessages
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == eventId);
                var auditLog = await identityDb.AuditLogs
                    .AsNoTracking()
                    .SingleAsync(x => x.IntegrationEventId == eventId);

                Assert.NotNull(message.ProcessedAt);
                Assert.Equal("{}", message.PayloadJson);
                Assert.Equal(IntegrationAuditActions.BatchConfigurationUpdated, auditLog.Action);
                Assert.Equal("cross-module-request", auditLog.RequestId);
            }
        }
        finally
        {
            await ResetDatabaseAsync();
        }
    }

    private async Task ResetAndMigrateDatabaseAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
        await db.Database.MigrateAsync();
    }

    private async Task ResetDatabaseAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
    }

    private static Task ResetSchemasAsync(UGTMDbContext db)
        => db.Database.ExecuteSqlRawAsync("""
            DROP SCHEMA IF EXISTS identity CASCADE;
            DROP SCHEMA IF EXISTS academic CASCADE;
            DROP SCHEMA IF EXISTS topics CASCADE;
            DROP SCHEMA IF EXISTS thesis CASCADE;
            DROP SCHEMA IF EXISTS councils CASCADE;
            DROP SCHEMA IF EXISTS notifications CASCADE;
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);

    private UGTMDbContext CreateMigrationDbContext()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(
            options,
            fixture.ConnectionString,
            typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }

    private AcademicPeriodsDbContext CreateAcademicDbContext()
    {
        var options = new DbContextOptionsBuilder<AcademicPeriodsDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new AcademicPeriodsDbContext(options.Options);
    }

    private IdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new IdentityDbContext(options.Options);
    }

    private sealed class FakeCurrentUser(Guid? userId, Guid? sessionId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
        public Guid? SessionId => sessionId;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }
}
