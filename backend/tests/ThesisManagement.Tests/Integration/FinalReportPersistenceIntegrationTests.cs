using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class FinalReportPersistenceIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string PreviousMigration = "20261025030846_AddRenameRequestWorkflowAndSyllabusResubmission";
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
    public async Task Migration_preserves_existing_reports_and_initializes_the_submission_sequence()
    {
        var reportId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await using (var db = CreateMigrationDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO thesis.student_group (id, topic_id, status, created_at)
                VALUES ({groupId}, {Guid.NewGuid()}, 'FinalReportSubmitted', {Timestamp});
                INSERT INTO thesis.final_report (id, group_id, version_type, status, submitted_at)
                VALUES ({reportId}, {groupId}, 'InitialFinal', 'Submitted', {Timestamp});
                """);
            await transaction.CommitAsync();
            await db.Database.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateThesisDbContext())
        {
            var report = await db.FinalReports.SingleAsync();
            Assert.Equal(reportId, report.Id);
            Assert.Equal(groupId, report.GroupId);
            Assert.Equal(1, report.CurrentVersionNo);
            Assert.Equal(FinalReportVersionType.InitialFinal, report.VersionType);
            Assert.Equal(FinalReportStatus.Submitted, report.Status);
            Assert.Equal(Timestamp, report.SubmittedAt);
            Assert.NotEqual(0u, report.Version);
        }

        // Exercise rollback and re-apply without touching application data.
        await using (var db = CreateMigrationDbContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::integer AS \"Value\" FROM thesis.final_report").SingleAsync());
            await db.Database.GetService<IMigrator>().MigrateAsync();
        }
    }

    [Fact]
    public async Task Registered_repository_tracks_reports_and_separates_report_types_within_a_group()
    {
        await UpgradeAsync();
        var ids = await SeedReportAsync();
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFinalReportRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IThesisUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        Assert.Same(context, unitOfWork);

        var report = await repository.GetByGroupAndVersionTypeAsync(ids.GroupId, FinalReportVersionType.InitialFinal);
        Assert.NotNull(report);
        Assert.Same(report, await repository.GetByIdAsync(ids.ReportId));
        Assert.True(report.TrySubmitRevision(Timestamp.AddHours(1)));
        var revisedReport = FinalReport.CreateSubmitted(
            Guid.NewGuid(), ids.GroupId, FinalReportVersionType.RevisedAfterReviewer, Timestamp.AddHours(2));
        repository.Add(revisedReport);
        await unitOfWork.SaveChangesAsync();

        await using var verification = CreateThesisDbContext();
        Assert.Equal(2, (await verification.FinalReports.SingleAsync(r => r.Id == ids.ReportId)).CurrentVersionNo);
        Assert.Equal(revisedReport.Id,
            (await repository.GetByGroupAndVersionTypeAsync(ids.GroupId, FinalReportVersionType.RevisedAfterReviewer))!.Id);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await repository.GetByGroupAndVersionTypeAsync(Guid.NewGuid(), FinalReportVersionType.InitialFinal));
    }

    [Fact]
    public async Task A_group_cannot_have_duplicate_reports_of_the_same_type()
    {
        await UpgradeAsync();
        var ids = await SeedReportAsync();
        await using var db = CreateThesisDbContext();
        db.FinalReports.Add(FinalReport.CreateSubmitted(
            Guid.NewGuid(), ids.GroupId, FinalReportVersionType.InitialFinal, Timestamp));

        var exception = await Assert.ThrowsAsync<ThesisManagementUniqueConstraintException>(
            () => db.SaveChangesAsync());

        Assert.Equal("ux_final_report_group_id_version_type", exception.ConstraintName);
    }

    [Fact]
    public async Task A_final_report_version_has_one_file_without_restricting_other_submission_types()
    {
        await UpgradeAsync();
        var ids = await SeedReportAsync();
        await using var db = CreateThesisDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Only bypass unrelated cross-module user foreign keys in this isolated fixture.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
        db.SubmissionFiles.AddRange(
            CreateFile(ids.ReportId, SubmissionEntityType.FinalReport, 1),
            CreateFile(ids.ReportId, SubmissionEntityType.FinalReport, 2),
            CreateFile(ids.ReportId, SubmissionEntityType.FinalSubmission, 1),
            CreateFile(ids.ReportId, SubmissionEntityType.FinalSubmission, 1));
        await db.SaveChangesAsync();
        db.SubmissionFiles.Add(CreateFile(ids.ReportId, SubmissionEntityType.FinalReport, 1));

        var exception = await Assert.ThrowsAsync<ThesisManagementUniqueConstraintException>(
            () => db.SaveChangesAsync());

        Assert.Equal("ux_submission_file_final_report_entity_id_version_no", exception.ConstraintName);
    }

    [Fact]
    public async Task The_database_rejects_nonpositive_report_versions()
    {
        await UpgradeAsync();
        var ids = await SeedReportAsync();
        await using var db = CreateThesisDbContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE thesis.final_report SET current_version_no = 0 WHERE id = {ids.ReportId}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_final_report_current_version_no", exception.ConstraintName);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Concurrent_changes_reject_the_stale_report(bool firstStartsEvaluation, bool staleStartsEvaluation)
    {
        await UpgradeAsync();
        var ids = await SeedReportAsync();
        await using var firstRequest = CreateThesisDbContext();
        await using var staleRequest = CreateThesisDbContext();
        var firstReport = await firstRequest.FinalReports.SingleAsync(r => r.Id == ids.ReportId);
        var staleReport = await staleRequest.FinalReports.SingleAsync(r => r.Id == ids.ReportId);

        Assert.True(firstStartsEvaluation
            ? firstReport.TryBeginEvaluation()
            : firstReport.TrySubmitRevision(Timestamp.AddHours(1)));
        await firstRequest.SaveChangesAsync();
        Assert.True(staleStartsEvaluation
            ? staleReport.TryBeginEvaluation()
            : staleReport.TrySubmitRevision(Timestamp.AddHours(2)));

        await Assert.ThrowsAsync<ThesisManagementConcurrencyException>(() => staleRequest.SaveChangesAsync());

        await using var verification = CreateThesisDbContext();
        var saved = await verification.FinalReports.SingleAsync(r => r.Id == ids.ReportId);
        Assert.Equal(firstStartsEvaluation ? FinalReportStatus.UnderEvaluation : FinalReportStatus.Submitted, saved.Status);
        Assert.Equal(firstStartsEvaluation ? 1 : 2, saved.CurrentVersionNo);
        Assert.Equal(firstStartsEvaluation ? Timestamp : Timestamp.AddHours(1), saved.SubmittedAt);
    }

    private async Task UpgradeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await db.Database.GetService<IMigrator>().MigrateAsync();
    }

    private async Task<(Guid GroupId, Guid ReportId)> SeedReportAsync()
    {
        await using var db = CreateThesisDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Timestamp);
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        Assert.True(group.TrySubmitMidtermReport());
        Assert.True(group.TryApproveMidtermReport());
        Assert.True(group.TrySubmitFinalReport());
        var report = FinalReport.CreateSubmitted(Guid.NewGuid(), group.Id, FinalReportVersionType.InitialFinal, Timestamp);
        db.StudentGroups.Add(group);
        db.FinalReports.Add(report);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (group.Id, report.Id);
    }

    private static SubmissionFile CreateFile(Guid reportId, SubmissionEntityType entityType, int versionNo)
        => new()
        {
            Id = Guid.NewGuid(),
            EntityId = reportId,
            EntityType = entityType,
            VersionNo = versionNo,
            FileName = "report.pdf",
            ObjectKey = $"test/{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            FileExtension = ".pdf",
            FileSize = 100,
            UploadedBy = Guid.NewGuid(),
            UploadedAt = Timestamp
        };

    private ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = fixture.ConnectionString
        }).Build();
        var services = new ServiceCollection();
        new ThesisManagementModule().RegisterModule(services, configuration);
        return services.BuildServiceProvider();
    }

    private ThesisManagementDbContext CreateThesisDbContext()
    {
        var options = new DbContextOptionsBuilder<ThesisManagementDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new ThesisManagementDbContext(options.Options);
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
