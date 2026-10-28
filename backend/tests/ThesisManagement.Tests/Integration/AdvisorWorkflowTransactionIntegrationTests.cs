using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class AdvisorWorkflowTransactionIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
        await db.Database.GetService<IMigrator>().MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
    }

    [Fact]
    public async Task Coordinator_commits_locked_report_binding_group_transition_and_audit_messages_together()
    {
        var ids = await SeedLockedReportAsync();
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var thesisWorkflow = scope.ServiceProvider.GetRequiredService<IAdvisorEvaluationWorkflowContract>();
        var thesis = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        var defense = scope.ServiceProvider.GetRequiredService<DefenseCouncilDbContext>();

        await SharedPostgresTransactionCoordinator.ExecuteAsync(thesis, defense, async cancellationToken =>
        {
            var locked = await thesisWorkflow.GetLockedInitialFinalReportAsync(ids.ReportId, cancellationToken);
            Assert.NotNull(locked);
            Assert.Equal(ids.GroupId, locked.GroupId);
            Assert.Equal(ids.FileId, locked.SubmissionFileId);

            var evaluation = AdvisorEvaluation.Create(
                Guid.NewGuid(), locked.GroupId, locked.FinalReportId, locked.ReportVersionNo,
                locked.SubmissionFileId, locked.SubmissionChecksumSha256, 8.5m,
                "Đủ điều kiện chuyển phản biện.", ids.ActorId, Now);
            Assert.True(evaluation.TryDecide(true, null, ids.ActorId, Now.AddMinutes(1)));
            defense.AdvisorEvaluations.Add(evaluation);
            defense.AuditOutboxMessages.Add(UGTM.Modules.DefenseCouncil.Domain.Entities.AuditOutboxMessage.Create(
                Guid.NewGuid(), "AdvisorEvaluationApproved", "{}", Now.AddMinutes(1)));

            var readiness = await thesisWorkflow.TryMakeGroupReadyForReviewerAssignmentAsync(
                locked.GroupId, locked.FinalReportId, locked.ReportVersionNo, cancellationToken);
            Assert.Equal(AdvisorEvaluationGroupReadinessOutcome.Succeeded, readiness.Outcome);
            thesis.AuditOutboxMessages.Add(UGTM.Modules.ThesisManagement.Domain.Entities.AuditOutboxMessage.Create(
                Guid.NewGuid(), "ReviewerAssignmentReadinessGranted", "{}", Now.AddMinutes(1)));
            return evaluation.Id;
        });

        await using var verificationThesis = CreateThesisDbContext();
        await using var verificationDefense = CreateDefenseDbContext();
        var group = await verificationThesis.StudentGroups.SingleAsync(value => value.Id == ids.GroupId);
        var evaluation = await verificationDefense.AdvisorEvaluations.SingleAsync();

        Assert.Equal(StudentGroupStatus.ReadyForReviewerAssignment, group.Status);
        Assert.Equal(ids.ReportId, evaluation.FinalReportId);
        Assert.True(evaluation.AllowsReviewerAssignment);
        Assert.Equal(1, await verificationThesis.AuditOutboxMessages.CountAsync());
        Assert.Equal(1, await verificationDefense.AuditOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Coordinator_rolls_back_decision_group_transition_and_both_audits_when_thesis_save_fails()
    {
        var ids = await SeedLockedReportAsync();
        await using var thesis = CreateThesisDbContext(new ThrowOnGroupUpdateInterceptor());
        await using var defense = CreateDefenseDbContext();
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            SharedPostgresTransactionCoordinator.ExecuteAsync(thesis, defense, async cancellationToken =>
        {
            var report = await thesis.FinalReports.SingleAsync(value => value.Id == ids.ReportId, cancellationToken);
            var group = await thesis.StudentGroups.SingleAsync(value => value.Id == ids.GroupId, cancellationToken);
            Assert.True(group.TryMakeReadyForReviewerAssignment());
            thesis.Entry(report).Property(value => value.Status).IsModified = true;

            var evaluation = AdvisorEvaluation.Create(
                Guid.NewGuid(), ids.GroupId, ids.ReportId, report.CurrentVersionNo, ids.FileId,
                ids.ChecksumSha256, 8.5m, "Đủ điều kiện chuyển phản biện.", ids.ActorId, Now);
            Assert.True(evaluation.TryDecide(true, null, ids.ActorId, Now.AddMinutes(1)));
            defense.AdvisorEvaluations.Add(evaluation);
            defense.AuditOutboxMessages.Add(UGTM.Modules.DefenseCouncil.Domain.Entities.AuditOutboxMessage.Create(
                Guid.NewGuid(), "AdvisorEvaluationApproved", "{}", Now.AddMinutes(1)));
            thesis.AuditOutboxMessages.Add(UGTM.Modules.ThesisManagement.Domain.Entities.AuditOutboxMessage.Create(
                Guid.NewGuid(), "ReviewerAssignmentReadinessGranted", "{}", Now.AddMinutes(1)));
            return evaluation.Id;
        }));

        Assert.Equal("Forced thesis group write failure.", exception.InnerException?.Message);

        await using var verificationThesis = CreateThesisDbContext();
        await using var verificationDefense = CreateDefenseDbContext();
        var group = await verificationThesis.StudentGroups.SingleAsync(value => value.Id == ids.GroupId);

        Assert.Equal(StudentGroupStatus.FinalReportSubmitted, group.Status);
        Assert.Empty(await verificationDefense.AdvisorEvaluations.ToListAsync());
        Assert.Equal(0, await verificationThesis.AuditOutboxMessages.CountAsync());
        Assert.Equal(0, await verificationDefense.AuditOutboxMessages.CountAsync());
    }

    private async Task<SeededWorkflowIds> SeedLockedReportAsync()
    {
        var actorId = Guid.NewGuid();
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        Assert.True(group.TrySubmitMidtermReport());
        Assert.True(group.TryApproveMidtermReport());
        Assert.True(group.TrySubmitFinalReport());
        var report = FinalReport.CreateSubmitted(Guid.NewGuid(), group.Id, FinalReportVersionType.InitialFinal, Now);
        Assert.True(report.TryBeginEvaluation());
        var checksum = new string('a', 64);
        var file = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityId = report.Id,
            EntityType = SubmissionEntityType.FinalReport,
            VersionNo = 1,
            FileName = "report.pdf",
            ObjectKey = $"test/{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            FileExtension = ".pdf",
            FileSize = 100,
            ChecksumSha256 = checksum,
            UploadedBy = actorId,
            UploadedAt = Now
        };

        await using (var db = CreateMigrationDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO identity."user" (id, email, normalized_email, full_name, status, security_stamp,
                                              concurrency_stamp, created_at, access_failed_count)
                VALUES ({actorId}, {"advisor@example.test"}, {"ADVISOR@EXAMPLE.TEST"}, {"Advisor"}, {"Active"},
                        {Guid.NewGuid()}, {Guid.NewGuid()}, {Now}, 0);
                """);
        }

        await using (var db = CreateThesisDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            // The isolated fixture only needs the group/report/file graph; TopicCatalog is outside this test.
            await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            db.StudentGroups.Add(group);
            db.FinalReports.Add(report);
            db.SubmissionFiles.Add(file);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        return new SeededWorkflowIds(group.Id, report.Id, file.Id, actorId, checksum);
    }

    private ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = fixture.ConnectionString
        }).Build();
        var services = new ServiceCollection();
        new UGTM.Modules.ThesisManagement.Infrastructure.ThesisManagementModule()
            .RegisterModule(services, configuration);
        services.AddScoped<DefenseCouncilDbContext>(_ => CreateDefenseDbContext());
        return services.BuildServiceProvider();
    }

    private ThesisManagementDbContext CreateThesisDbContext(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ThesisManagementDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return new ThesisManagementDbContext(options.Options);
    }

    private DefenseCouncilDbContext CreateDefenseDbContext()
    {
        var options = new DbContextOptionsBuilder<DefenseCouncilDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new DefenseCouncilDbContext(options.Options);
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

    private sealed record SeededWorkflowIds(
        Guid GroupId,
        Guid ReportId,
        Guid FileId,
        Guid ActorId,
        string ChecksumSha256);

    private sealed class ThrowOnGroupUpdateInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfGroupUpdate(command);

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfGroupUpdate(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void ThrowIfGroupUpdate(DbCommand command)
        {
            if (command.CommandText.Contains("student_group", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Forced thesis group write failure.");
            }
        }
    }
}
