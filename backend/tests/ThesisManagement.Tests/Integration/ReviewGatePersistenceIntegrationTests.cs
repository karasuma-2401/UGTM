using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReviewGatePersistenceIntegrationTests(PostgreSqlFixture fixture)
{
    private const string PreviousMigration = "20261021092342_EnforceUniqueAcademicPeriod";

    [Fact]
    public async Task Latest_migration_creates_review_gate_schema_and_seeds_required_permissions()
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
            var progressReportVersionColumns = await db.Database.SqlQueryRaw<string>(
                """
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'thesis' AND table_name = 'progress_report_version'
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
            var reviewGatePermissionCodes = await db.Database.SqlQueryRaw<string>(
                """
                SELECT permission_code AS "Value"
                FROM identity.permission
                WHERE permission_code = {0}
                """,
                PermissionCodes.ThesisReviewGateConfigurationsManage).ToListAsync();
            var reviewGatePermissionRoles = await db.Database.SqlQueryRaw<string>(
                """
                SELECT r.role_name AS "Value"
                FROM identity.role_permission rp
                JOIN identity.role r ON r.id = rp.role_id
                JOIN identity.permission p ON p.id = rp.permission_id
                WHERE p.permission_code = {0}
                ORDER BY r.role_name
                """,
                PermissionCodes.ThesisReviewGateConfigurationsManage).ToListAsync();
            var reviewPermissionCodes = await db.Database.SqlQueryRaw<string>(
                """
                SELECT permission_code AS "Value"
                FROM identity.permission
                WHERE permission_code = {0}
                """,
                PermissionCodes.ThesisReviewGatesReview).ToListAsync();
            var reviewPermissionRoles = await db.Database.SqlQueryRaw<string>(
                """
                SELECT r.role_name AS "Value"
                FROM identity.role_permission rp
                JOIN identity.role r ON r.id = rp.role_id
                JOIN identity.permission p ON p.id = rp.permission_id
                WHERE p.permission_code = {0}
                ORDER BY r.role_name
                """,
                PermissionCodes.ThesisReviewGatesReview).ToListAsync();

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
            Assert.Equal(
                [
                    "advisor_feedback", "git_url", "id", "is_late", "progress_report_id", "reviewed_at",
                    "reviewed_by", "status", "student_note", "submitted_at", "submitted_by", "version_no"
                ],
                progressReportVersionColumns);
            Assert.Contains("ck_review_gate_configuration_deadline_order", constraintNames);
            Assert.Contains("ck_submission_review_version_no", constraintNames);
            Assert.Contains("ix_review_gate_configuration_batch_gate_type", indexNames);
            Assert.Contains("ix_submission_review_gate_submission_version", indexNames);
            Assert.Contains("ux_progress_report_version_report_id_version_no", indexNames);
            Assert.Equal([PermissionCodes.ThesisReviewGateConfigurationsManage], reviewGatePermissionCodes);
            Assert.Equal(["AcademicAffairs", "Admin"], reviewGatePermissionRoles);
            Assert.Equal([PermissionCodes.ThesisReviewGatesReview], reviewPermissionCodes);
            Assert.Equal(["Admin", "Faculty"], reviewPermissionRoles);
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
