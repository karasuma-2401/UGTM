using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class AdvisorEvaluationWorkflowPersistenceIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string PreviousMigration = "20261027174826_AddFinalReportSubmissionFoundation";
    private static readonly DateTime Timestamp = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
        await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
    }

    [Fact]
    public async Task Migration_preserves_legacy_evaluations_without_inventing_workflow_bindings()
    {
        var evaluationId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using (var db = CreateMigrationDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO thesis.student_group (id, topic_id, status, created_at)
                VALUES ({groupId}, {Guid.NewGuid()}, 'FinalReportSubmitted', {Timestamp});
                INSERT INTO councils.advisor_evaluation
                    (id, group_id, score, comment, evaluated_at, allows_reviewer_assignment,
                     is_ignored_in_calculation, exclusion_reason)
                VALUES
                    ({evaluationId}, {groupId}, {8.5m}, {"Nhận xét cũ"}, {Timestamp}, {true},
                     {false}, {null});
                """);
            await transaction.CommitAsync();
            await db.Database.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateMigrationDbContext())
        {
            var preserved = await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*)::integer AS \"Value\" FROM councils.advisor_evaluation WHERE id = {evaluationId}")
                .SingleAsync();
            var isLegacy = await db.Database.SqlQuery<bool>(
                $"SELECT is_legacy AS \"Value\" FROM councils.advisor_evaluation WHERE id = {evaluationId}")
                .SingleAsync();
            var missingBindings = await db.Database.SqlQuery<int>(
                $"""
                SELECT CASE WHEN final_report_id IS NULL
                                      AND report_version_no IS NULL
                                      AND submission_file_id IS NULL
                                      AND submission_checksum_sha256 IS NULL
                                      AND evaluated_by IS NULL
                                      AND decision_by IS NULL
                                      AND decision_at IS NULL
                                THEN 1 ELSE 0 END AS "Value"
                FROM councils.advisor_evaluation
                WHERE id = {evaluationId}
                """)
                .SingleAsync();
            var physicalXminColumns = await db.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::integer AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'councils'
                  AND table_name = 'advisor_evaluation'
                  AND column_name = 'xmin'
                """)
                .SingleAsync();
            var constraints = await db.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::integer AS "Value"
                FROM pg_constraint
                WHERE conrelid = 'councils.advisor_evaluation'::regclass
                  AND conname IN (
                      'ck_advisor_evaluation_score',
                      'ck_advisor_evaluation_workflow_binding',
                      'ck_advisor_evaluation_reviewer_assignment_decision')
                """)
                .SingleAsync();

            Assert.Equal(1, preserved);
            Assert.True(isLegacy);
            Assert.Equal(1, missingBindings);
            Assert.Equal(0, physicalXminColumns);
            Assert.Equal(3, constraints);
        }

        await using (var db = CreateMigrationDbContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::integer AS \"Value\" FROM councils.advisor_evaluation").SingleAsync());
            await db.Database.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateMigrationDbContext())
        {
            var isLegacyAfterReapply = await db.Database.SqlQuery<bool>(
                $"SELECT is_legacy AS \"Value\" FROM councils.advisor_evaluation WHERE id = {evaluationId}")
                .SingleAsync();
            Assert.True(isLegacyAfterReapply);
        }
    }

    [Fact]
    public async Task Migration_creates_the_workflow_columns_constraints_and_cross_module_foreign_keys()
    {
        await UpgradeAsync();

        await using var db = CreateMigrationDbContext();
        var workflowColumns = await db.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'councils'
              AND table_name = 'advisor_evaluation'
              AND column_name IN (
                  'decision_at', 'decision_by', 'evaluated_by', 'final_report_id', 'is_legacy',
                  'rejection_reason', 'report_version_no', 'submission_checksum_sha256', 'submission_file_id')
            """)
            .SingleAsync();
        var workflowForeignKeys = await db.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM pg_constraint
            WHERE conrelid = 'councils.advisor_evaluation'::regclass
              AND conname IN (
                  'fk_advisor_evaluation_final_reports_final_report_id',
                  'fk_advisor_evaluation_submission_files_submission_file_id',
                  'fk_advisor_evaluation_users_evaluated_by',
                  'fk_advisor_evaluation_users_decision_by')
            """)
            .SingleAsync();

        Assert.Equal(9, workflowColumns);
        Assert.Equal(4, workflowForeignKeys);
    }

    private async Task UpgradeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await db.Database.GetService<IMigrator>().MigrateAsync();
    }

    private UGTMDbContext CreateMigrationDbContext()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
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
}
