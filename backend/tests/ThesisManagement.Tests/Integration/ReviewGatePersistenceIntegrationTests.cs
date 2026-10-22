using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReviewGatePersistenceIntegrationTests(PostgreSqlFixture fixture)
{
    private const string PreviousMigration = "20261021092342_EnforceUniqueAcademicPeriod";

    [Fact]
    public async Task Latest_migration_creates_review_gate_schema_and_preserves_submission_file_identity()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);

        try
        {
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await migrator.MigrateAsync();

            var reviewGateColumns = await db.Database.SqlQueryRaw<string>(
                """
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'thesis' AND table_name = 'review_gate_configuration'
                ORDER BY column_name
                """).ToListAsync();
            var submissionFileColumns = await db.Database.SqlQueryRaw<string>(
                """
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'thesis' AND table_name = 'submission_file'
                ORDER BY column_name
                """).ToListAsync();
            var constraintNames = await db.Database.SqlQueryRaw<string>(
                """
                SELECT conname AS "Value"
                FROM pg_constraint
                WHERE connamespace = 'thesis'::regnamespace
                ORDER BY conname
                """).ToListAsync();
            var indexNames = await db.Database.SqlQueryRaw<string>(
                """
                SELECT indexname AS "Value"
                FROM pg_indexes
                WHERE schemaname = 'thesis'
                ORDER BY indexname
                """).ToListAsync();

            Assert.Equal(
                [
                    "batch_id", "created_at_utc", "created_by", "faculty_response_deadline_utc",
                    "final_closure_deadline_utc", "gate_type", "id", "initial_submission_deadline_utc",
                    "updated_at_utc", "updated_by"
                ],
                reviewGateColumns);
            Assert.Contains("object_key", submissionFileColumns);
            Assert.DoesNotContain("file_url", submissionFileColumns);
            Assert.Contains("content_type", submissionFileColumns);
            Assert.Contains("checksum_sha256", submissionFileColumns);
            Assert.Contains("ck_review_gate_configuration_deadline_order", constraintNames);
            Assert.Contains("ck_submission_review_version_no", constraintNames);
            Assert.Contains("ix_review_gate_configuration_batch_gate_type", indexNames);
            Assert.Contains("ix_submission_review_gate_submission_version", indexNames);
        }
        finally
        {
            await ResetSchemasAsync(db);
        }
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
