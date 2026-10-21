using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;

namespace UGTM.Modules.AcademicPeriods.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class AcademicPeriodsPersistenceIntegrationTests(PostgreSqlFixture fixture)
{
    private const string PreviousMigration = "20261021070807_EnforceSingleScoreConfigurationDraft";
    private static readonly Guid ActorId = Guid.Parse("7f7f306b-1881-48f4-8892-f215b85877c4");

    [Fact]
    public async Task Latest_migration_upgrades_existing_schema_and_enforces_one_batch_per_period()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            var migrator = migrationDb.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            await using (var seedDb = CreateAcademicDbContext())
            {
                seedDb.KltnBatches.Add(await CreateBatchAsync(seedDb, "2026-2027", 1));
                await seedDb.SaveChangesAsync();
            }

            await migrator.MigrateAsync();

            var assignments = await migrationDb.Database.SqlQueryRaw<string>(
                """
                SELECT role.role_name || ':' || permission.permission_code AS "Value"
                FROM identity.role_permission role_permission
                JOIN identity.role role ON role.id = role_permission.role_id
                JOIN identity.permission permission ON permission.id = role_permission.permission_id
                WHERE permission.permission_code LIKE 'academic.%'
                ORDER BY role.role_name, permission.permission_code
                """).ToListAsync();

            AssertPermissionRoles(assignments, "academic.batches.manage", "AcademicAffairs", "Admin");
            AssertPermissionRoles(
                assignments,
                "academic.batches.read",
                "AcademicAffairs",
                "Admin",
                "CouncilMember",
                "Faculty",
                "Lecturer",
                "Student");
            AssertPermissionRoles(assignments, "academic.score-config.manage", "AcademicAffairs", "Admin");
            AssertPermissionRoles(assignments, "academic.score-config.read", "AcademicAffairs", "Admin");

            await using var verificationDb = CreateAcademicDbContext();
            Assert.Equal(1, await verificationDb.KltnBatches.CountAsync());
            verificationDb.KltnBatches.Add(await CreateBatchAsync(verificationDb, "2026-2027", 1));

            var exception = await Assert.ThrowsAsync<AcademicPeriodsUniqueConstraintException>(
                () => verificationDb.SaveChangesAsync());
            Assert.Equal("ix_kltn_batch_academic_year_semester", exception.ConstraintName);
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    [Fact]
    public async Task Xmin_concurrency_is_translated_and_transaction_rolls_back_domain_and_audit_together()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            await migrationDb.Database.MigrateAsync();
            Guid batchId;
            DateTime? originalUpdatedAt;

            await using (var seedDb = CreateAcademicDbContext())
            {
                var batch = await CreateBatchAsync(seedDb, "2027-2028", 1);
                seedDb.KltnBatches.Add(batch);
                await seedDb.SaveChangesAsync();
                batchId = batch.Id;
                originalUpdatedAt = batch.UpdatedAt;
            }

            await using (var firstDb = CreateAcademicDbContext())
            await using (var secondDb = CreateAcademicDbContext())
            {
                var first = await LoadBatchAsync(firstDb, batchId);
                var second = await LoadBatchAsync(secondDb, batchId);
                firstDb.Entry(first).Property(x => x.UpdatedAt).CurrentValue = Utc(13);
                secondDb.Entry(second).Property(x => x.UpdatedAt).CurrentValue = Utc(14);

                await firstDb.SaveChangesAsync();
                await Assert.ThrowsAsync<AcademicPeriodsConcurrencyException>(
                    () => secondDb.SaveChangesAsync());
            }

            await using (var transactionDb = CreateAcademicDbContext())
            {
                await Assert.ThrowsAsync<IntentionalRollbackException>(() =>
                    transactionDb.ExecuteInTransactionAsync<int>(async cancellationToken =>
                    {
                        var batch = await LoadBatchAsync(transactionDb, batchId);
                        transactionDb.Entry(batch).Property(x => x.UpdatedAt).CurrentValue = Utc(15);
                        transactionDb.AuditOutboxMessages.Add(AuditOutboxMessage.Create(
                            Guid.NewGuid(),
                            "AcademicPeriods.Test.Rollback",
                            "{}",
                            Utc(15)));
                        await transactionDb.SaveChangesAsync(cancellationToken);
                        throw new IntentionalRollbackException();
                    }));
            }

            await using var verificationDb = CreateAcademicDbContext();
            var persisted = await verificationDb.KltnBatches.AsNoTracking().SingleAsync(x => x.Id == batchId);
            Assert.Equal(Utc(13), persisted.UpdatedAt);
            Assert.NotEqual(originalUpdatedAt, persisted.UpdatedAt);
            Assert.False(await verificationDb.AuditOutboxMessages.AnyAsync(x =>
                x.Action == "AcademicPeriods.Test.Rollback"));
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    [Fact]
    public async Task Database_allows_only_one_draft_and_one_active_score_configuration_per_batch()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            await migrationDb.Database.MigrateAsync();
            await using var db = CreateAcademicDbContext();
            var batch = await CreateBatchAsync(db, "2028-2029", 1);
            db.KltnBatches.Add(batch);
            await db.SaveChangesAsync();

            db.ScoreWeightConfigs.Add(CreateDraft(batch.Id, 1));
            await db.SaveChangesAsync();
            db.ScoreWeightConfigs.Add(CreateDraft(batch.Id, 2, "second version"));

            var exception = await Assert.ThrowsAsync<AcademicPeriodsUniqueConstraintException>(
                () => db.SaveChangesAsync());
            Assert.Equal("ix_score_weight_config_one_draft", exception.ConstraintName);
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    private async Task<KltnBatch> CreateBatchAsync(
        AcademicPeriodsDbContext db,
        string academicYear,
        int semester)
    {
        var template = await db.ScheduleTemplateVersions
            .Include(x => x.StageDefinitions)
            .AsNoTracking()
            .SingleAsync(x => x.Status == ConfigurationVersionStatus.Active);
        var startDate = new DateOnly(2026 + semester, 9, 1);
        var schedule = template.Materialize(startDate);
        return KltnBatch.Create(
            Guid.NewGuid(),
            $"KLTN {academicYear} HK{semester}",
            academicYear,
            semester,
            startDate,
            schedule[^1].EndDate,
            template,
            ActorId,
            Utc(12));
    }

    private static ScoreWeightConfig CreateDraft(Guid batchId, int versionNo, string? reason = null)
        => ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            batchId,
            versionNo,
            ScoreCalculationMethod.IntegerCoefficient,
            [new ScoreComponentDefinition(
                "COUNCIL",
                "Điểm hội đồng",
                ScoreComponentSource.CouncilAverage,
                ScoreComponentApplicability.Always,
                1,
                true,
                null,
                false,
                1)],
            ActorId,
            Utc(12),
            reason);

    private static async Task<KltnBatch> LoadBatchAsync(AcademicPeriodsDbContext db, Guid batchId)
        => await db.KltnBatches.Include(x => x.Stages).SingleAsync(x => x.Id == batchId);

    private static DateTime Utc(int hour) => new(2026, 10, 21, hour, 0, 0, DateTimeKind.Utc);

    private static void AssertPermissionRoles(
        IEnumerable<string> assignments,
        string permission,
        params string[] expectedRoles)
        => Assert.Equal(
            expectedRoles.Order(),
            assignments
                .Where(value => value.EndsWith($":{permission}", StringComparison.Ordinal))
                .Select(value => value[..value.IndexOf(':')])
                .Order());

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

    private sealed class IntentionalRollbackException : Exception;
}
