using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Auditing;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.ReviewGateDeadlines;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReviewGateDeadlineEnforcerIntegrationTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Expired_gates_stop_only_pending_groups_and_are_idempotent()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            await migrationDb.Database.GetService<IMigrator>().MigrateAsync();
            await using var thesisDb = CreateThesisDbContext();
            var ids = await SeedExpiredGateDataAsync(thesisDb);
            await using var staleRequestDb = CreateThesisDbContext();
            var staleGroup = await staleRequestDb.StudentGroups
                .SingleAsync(group => group.Id == ids.PendingSyllabusGroupId);
            var enforcer = new ReviewGateDeadlineEnforcer(
                thesisDb,
                new ThesisManagementAuditOutbox(
                    thesisDb,
                    new WorkerCurrentUser(),
                    new HttpContextAccessor()),
                Options.Create(new ReviewGateDeadlineEnforcementOptions
                {
                    BatchSize = 10,
                    PollingIntervalSeconds = 60
                }),
                new FixedTimeProvider(Now));

            var firstPass = await enforcer.EnforceAsync();
            var secondPass = await enforcer.EnforceAsync();

            Assert.True(staleGroup.TrySubmitSyllabus());
            await Assert.ThrowsAsync<ThesisManagementConcurrencyException>(
                async () => await staleRequestDb.SaveChangesAsync());

            var statuses = await thesisDb.StudentGroups
                .AsNoTracking()
                .ToDictionaryAsync(group => group.Id, group => group.Status);
            var closureAuditCount = await thesisDb.AuditOutboxMessages
                .CountAsync(message => message.Action == IntegrationAuditActions.ReviewGateDeadlineClosed);

            Assert.Equal(2, firstPass.ClosedGroupCount);
            Assert.Equal(0, secondPass.ClosedGroupCount);
            Assert.Equal(StudentGroupStatus.Stopped, statuses[ids.PendingSyllabusGroupId]);
            Assert.Equal(StudentGroupStatus.Stopped, statuses[ids.PendingMidtermGroupId]);
            Assert.Equal(StudentGroupStatus.InProgress, statuses[ids.ApprovedMidtermGroupId]);
            Assert.Equal(2, closureAuditCount);
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    private ThesisManagementDbContext CreateThesisDbContext()
    {
        var options = new DbContextOptionsBuilder<ThesisManagementDbContext>();
        DatabaseOptions.Configure(
            options,
            fixture.ConnectionString,
            typeof(UGTMDbContext).Assembly.GetName().Name);
        return new ThesisManagementDbContext(options.Options);
    }

    private UGTMDbContext CreateMigrationDbContext()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(
            options,
            fixture.ConnectionString,
            typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }

    private static async Task<SeededGroupIds> SeedExpiredGateDataAsync(ThesisManagementDbContext db)
    {
        var batchId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var pendingSyllabusGroupId = Guid.NewGuid();
        var pendingMidtermGroupId = Guid.NewGuid();
        var approvedMidtermGroupId = Guid.NewGuid();

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");

        await InsertGroupAsync(db, pendingSyllabusGroupId, batchId, StudentGroupStatus.Registered);
        await InsertGroupAsync(db, pendingMidtermGroupId, batchId, StudentGroupStatus.InProgress);
        await InsertGroupAsync(db, approvedMidtermGroupId, batchId, StudentGroupStatus.InProgress);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO thesis.midterm_report (
                id, group_id, current_version_no, status, faculty_feedback, reviewed_by, reviewed_at)
            VALUES ({Guid.NewGuid()}, {approvedMidtermGroupId}, 1, {MidtermReportStatus.Approved.ToString()}, NULL, {actorId}, {Now.AddDays(-2)})
            """);
        await InsertGateConfigurationAsync(db, batchId, actorId, ReviewGateType.Syllabus);
        await InsertGateConfigurationAsync(db, batchId, actorId, ReviewGateType.Midterm);

        await transaction.CommitAsync();
        return new SeededGroupIds(pendingSyllabusGroupId, pendingMidtermGroupId, approvedMidtermGroupId);
    }

    private static async Task InsertGroupAsync(
        ThesisManagementDbContext db,
        Guid groupId,
        Guid batchId,
        StudentGroupStatus status)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO thesis.student_group (id, topic_id, status, created_at)
            VALUES ({groupId}, {Guid.NewGuid()}, {status.ToString()}, {Now.AddDays(-20)})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO thesis.student_group_member (id, group_id, student_id, batch_id, joined_at)
            VALUES ({Guid.NewGuid()}, {groupId}, {Guid.NewGuid()}, {batchId}, {Now.AddDays(-20)})
            """);
    }

    private static async Task InsertGateConfigurationAsync(
        ThesisManagementDbContext db,
        Guid batchId,
        Guid actorId,
        ReviewGateType gateType)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO thesis.review_gate_configuration (
                id, batch_id, gate_type, initial_submission_deadline_utc, faculty_response_deadline_utc,
                final_closure_deadline_utc, created_by, created_at_utc, updated_by, updated_at_utc)
            VALUES (
                {Guid.NewGuid()}, {batchId}, {gateType.ToString()}, {Now.AddDays(-3)}, {Now.AddDays(-2)},
                {Now.AddDays(-1)}, {actorId}, {Now.AddDays(-4)}, {actorId}, {Now.AddDays(-4)})
            """);
    }

    private static Task ResetSchemasAsync(UGTMDbContext db)
        => db.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS identity CASCADE;
            DROP SCHEMA IF EXISTS academic CASCADE;
            DROP SCHEMA IF EXISTS topics CASCADE;
            DROP SCHEMA IF EXISTS thesis CASCADE;
            DROP SCHEMA IF EXISTS councils CASCADE;
            DROP SCHEMA IF EXISTS notifications CASCADE;
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);

    private sealed record SeededGroupIds(
        Guid PendingSyllabusGroupId,
        Guid PendingMidtermGroupId,
        Guid ApprovedMidtermGroupId);

    private sealed class WorkerCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
