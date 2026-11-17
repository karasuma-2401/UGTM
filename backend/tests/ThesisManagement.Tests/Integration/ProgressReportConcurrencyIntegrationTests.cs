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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirmation_and_expiration_cannot_both_commit(bool confirmationFirst)
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);
        try
        {
            await migrationDb.Database.GetService<IMigrator>().MigrateAsync();
            var studentId = Guid.NewGuid();
            Guid memberId;
            await using (var seed = CreateThesisDbContext())
            await using (var transaction = await seed.Database.BeginTransactionAsync())
            {
                await seed.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
                var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-1));
                var member = group.AddPendingMember(studentId, Guid.NewGuid(), Guid.NewGuid(), "token",
                    Now.AddDays(-1), Now.AddSeconds(-1));
                memberId = member.Id;
                seed.StudentGroups.Add(group);
                await seed.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            await using var confirm = CreateThesisDbContext();
            await using var expire = CreateThesisDbContext();
            await using var expireTransaction = await expire.Database.BeginTransactionAsync();
            await expire.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            var confirmingMember = await confirm.StudentGroupMembers.SingleAsync(m => m.Id == memberId);
            var expiringMember = await expire.StudentGroupMembers.Include(m => m.Group).SingleAsync(m => m.Id == memberId);
            confirmingMember.Confirm(Now.AddMinutes(-1));
            Assert.True(expiringMember.Group.RemoveMember(studentId));
            var penalty = StudentRegistrationPenalty.Create(studentId);
            penalty.RecordViolation(Now, 3);
            expire.StudentRegistrationPenalties.Add(penalty);

            if (confirmationFirst)
            {
                await confirm.SaveChangesAsync();
                await Assert.ThrowsAsync<ThesisManagementConcurrencyException>(() => expire.SaveChangesAsync());
            }
            else
            {
                await expire.SaveChangesAsync();
                await expireTransaction.CommitAsync();
                await Assert.ThrowsAsync<ThesisManagementConcurrencyException>(() => confirm.SaveChangesAsync());
            }

            await using var verification = CreateThesisDbContext();
            var saved = await verification.StudentGroupMembers.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memberId);
            Assert.Equal(!confirmationFirst, await verification.StudentRegistrationPenalties.AnyAsync(p => p.StudentId == studentId));
            if (confirmationFirst)
                Assert.Equal(MemberConfirmationStatus.Confirmed, saved!.ConfirmationStatus);
            else
                Assert.Null(saved);
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

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
