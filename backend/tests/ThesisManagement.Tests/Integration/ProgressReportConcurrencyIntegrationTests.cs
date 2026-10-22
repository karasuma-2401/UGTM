using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class ProgressReportConcurrencyIntegrationTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now = new(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Concurrent_review_of_the_same_version_rejects_the_stale_request()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            await migrationDb.Database.GetService<IMigrator>().MigrateAsync();
            var ids = await SeedSubmittedReportAsync();

            await using var firstRequest = CreateThesisDbContext();
            await using var staleRequest = CreateThesisDbContext();
            var firstReport = await firstRequest.ProgressReports.SingleAsync(report => report.Id == ids.ReportId);
            var firstVersion = await firstRequest.ProgressReportVersions.SingleAsync(version => version.Id == ids.VersionId);
            var staleReport = await staleRequest.ProgressReports.SingleAsync(report => report.Id == ids.ReportId);
            var staleVersion = await staleRequest.ProgressReportVersions.SingleAsync(version => version.Id == ids.VersionId);

            Assert.True(firstReport.TryReview(ProgressReportStatus.Accepted, null, Now));
            Assert.True(firstVersion.TryReview(ProgressReportStatus.Accepted, null, ids.AdvisorId, Now));
            await firstRequest.SaveChangesAsync();

            Assert.True(staleReport.TryReview(ProgressReportStatus.NeedRevision, "Bổ sung minh chứng.", Now.AddMinutes(1)));
            Assert.True(staleVersion.TryReview(
                ProgressReportStatus.NeedRevision,
                "Bổ sung minh chứng.",
                ids.AdvisorId,
                Now.AddMinutes(1)));
            await Assert.ThrowsAsync<ThesisManagementConcurrencyException>(
                async () => await staleRequest.SaveChangesAsync());

            await using var verification = CreateThesisDbContext();
            var savedReport = await verification.ProgressReports.AsNoTracking().SingleAsync(report => report.Id == ids.ReportId);
            var savedVersion = await verification.ProgressReportVersions.AsNoTracking().SingleAsync(version => version.Id == ids.VersionId);
            Assert.Equal(ProgressReportStatus.Accepted, savedReport.Status);
            Assert.Equal(ProgressReportStatus.Accepted, savedVersion.Status);
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    private async Task<SeededReportIds> SeedSubmittedReportAsync()
    {
        await using var db = CreateThesisDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
        var advisorId = Guid.NewGuid();
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-5));
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        var milestone = ProgressMilestone.Create(
            Guid.NewGuid(),
            group.Id,
            "Hoàn thiện kiểm thử",
            null,
            Now.AddDays(2),
            advisorId,
            Now.AddDays(-1));
        var report = ProgressReport.CreateSubmitted(
            Guid.NewGuid(),
            milestone.Id,
            group.Id,
            "Đã hoàn thiện kiểm thử tích hợp.",
            Now.AddHours(-1));
        var version = ProgressReportVersion.CreateSubmitted(
            Guid.NewGuid(),
            report.Id,
            1,
            "https://github.com/ugtm/progress-report",
            "Đã hoàn thiện kiểm thử tích hợp.",
            Guid.NewGuid(),
            Now.AddHours(-1),
            false);

        db.StudentGroups.Add(group);
        db.ProgressMilestones.Add(milestone);
        db.ProgressReports.Add(report);
        db.ProgressReportVersions.Add(version);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new SeededReportIds(report.Id, version.Id, advisorId);
    }

    private ThesisManagementDbContext CreateThesisDbContext()
    {
        var options = new DbContextOptionsBuilder<ThesisManagementDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
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

    private sealed record SeededReportIds(Guid ReportId, Guid VersionId, Guid AdvisorId);
}
