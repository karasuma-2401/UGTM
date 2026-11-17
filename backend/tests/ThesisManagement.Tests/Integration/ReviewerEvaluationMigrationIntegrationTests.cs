using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentWorkflowPersistenceIntegrationTests
{
    [Fact]
    public async Task Reviewer_evaluation_migration_preserves_legacy_content_visibility_exclusion_and_metadata_through_down_and_reapply()
    {
        await using var migration = Db<UGTMDbContext>();
        await migration.Database.GetService<IMigrator>().MigrateAsync(CurrentMigration);
        var assignmentId = Guid.NewGuid(); var evaluationId = Guid.NewGuid();
        await InsertLegacyAsync(assignmentId, _groupId, _reviewerId, 1, true);
        await InsertEvaluationAsync(evaluationId, assignmentId);
        await migration.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE councils.reviewer_evaluation SET revision_feedback = {"Góp ý lịch sử"}, is_visible_to_student = true,
                is_ignored_in_calculation = true, exclusion_reason = {"Không tính điểm"} WHERE id = {evaluationId}
            """);
        await migration.Database.MigrateAsync();
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var legacy = await defense.ReviewerEvaluations.SingleAsync();
            Assert.True(legacy.IsLegacy); Assert.False(legacy.IsLocked);
            Assert.Equal(evaluationId, legacy.Id); Assert.Equal(8m, legacy.Score);
            Assert.Equal("Phiếu đã chấm", legacy.Comment); Assert.Equal("Góp ý lịch sử", legacy.RevisionFeedback);
            Assert.True(legacy.IsVisibleToStudent); Assert.True(legacy.IsIgnoredInCalculation); Assert.Equal("Không tính điểm", legacy.ExclusionReason);
            Assert.Null(legacy.Status); Assert.Null(legacy.CreatedBy); Assert.Null(legacy.CreatedAtUtc);
            Assert.Null(legacy.EvaluatedBy); Assert.Null(legacy.SubmittedBy); Assert.Null(legacy.SubmittedAtUtc);
            Assert.False(legacy.TrySaveDraft(10m, "Không được sửa", null, _actorId, Now));
            Assert.True(legacy.Version > 0);
        }
        Assert.False(migration.Database.HasPendingModelChanges());
        Assert.Equal(0, await migration.Database.SqlQueryRaw<int>("SELECT COUNT(*)::integer AS \"Value\" FROM information_schema.columns WHERE table_schema='councils' AND table_name='reviewer_evaluation' AND column_name='xmin'").SingleAsync());
        await migration.Database.GetService<IMigrator>().MigrateAsync(CurrentMigration);
        Assert.Equal(8m, await migration.Database.SqlQuery<decimal>($"SELECT score AS \"Value\" FROM councils.reviewer_evaluation WHERE id={evaluationId}").SingleAsync());
        Assert.Equal("Góp ý lịch sử", await migration.Database.SqlQuery<string>($"SELECT revision_feedback AS \"Value\" FROM councils.reviewer_evaluation WHERE id={evaluationId}").SingleAsync());
        await migration.Database.MigrateAsync();
        await using var reapplied = Db<DefenseCouncilDbContext>();
        Assert.True((await reapplied.ReviewerEvaluations.SingleAsync()).IsLegacy);
        await using var identity = Db<IdentityDbContext>();
        var roles = await identity.RolePermissions.Where(value => value.Permission.PermissionCode == PermissionCodes.ThesisReviewerEvaluationsRead)
            .Select(value => value.Role.RoleName).OrderBy(value => value).ToArrayAsync();
        Assert.Equal(["AcademicAffairs", "Admin", "Faculty"], roles);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reviewer_evaluation_migration_refuses_downgrade_with_workflow_rows_or_receipts_without_removing_data(bool receiptOnly)
    {
        await UpgradeAsync();
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            if (receiptOnly)
            {
                var receipt = ReviewerEvaluationIdempotencyRecord.Create(Guid.NewGuid(), _actorId, "ReviewerEvaluation.Save", Guid.NewGuid(), Checksum, 201, Now);
                receipt.SetResponsePayload("{\"evaluationVersion\":12}"); defense.ReviewerEvaluationIdempotencyRecords.Add(receipt);
            }
            else
            {
                var assignment = NewAssignment(); defense.ReviewerAssignments.Add(assignment);
                defense.ReviewerEvaluations.Add(ReviewerEvaluation.CreateDraft(Guid.NewGuid(), assignment.Id, null, null, null, _actorId, Now));
            }
            await defense.SaveChangesAsync();
        }
        await using var migration = Db<UGTMDbContext>();
        var error = await Assert.ThrowsAsync<PostgresException>(() => migration.Database.GetService<IMigrator>().MigrateAsync(CurrentMigration));
        Assert.Contains("SCRUM-105 rollback preflight", error.MessageText);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(receiptOnly ? 0 : 1, await verify.ReviewerEvaluations.CountAsync());
        Assert.Equal(receiptOnly ? 1 : 0, await verify.ReviewerEvaluationIdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task Reviewer_evaluation_migration_preflight_rejects_invalid_legacy_decision_atomically()
    {
        await using var migration = Db<UGTMDbContext>();
        await migration.Database.GetService<IMigrator>().MigrateAsync(CurrentMigration);
        var assignmentId = Guid.NewGuid(); await InsertLegacyAsync(assignmentId, _groupId, _reviewerId, 1, true);
        var id = Guid.NewGuid(); await InsertEvaluationAsync(id, assignmentId);
        await migration.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.reviewer_evaluation SET defense_approval='Unknown' WHERE id={id}");
        var error = await Assert.ThrowsAsync<PostgresException>(() => migration.Database.MigrateAsync());
        Assert.Contains("SCRUM-105 preflight", error.MessageText);
        Assert.Equal("Unknown", await migration.Database.SqlQuery<string>($"SELECT defense_approval AS \"Value\" FROM councils.reviewer_evaluation WHERE id={id}").SingleAsync());
        Assert.Equal(0, await migration.Database.SqlQueryRaw<int>("SELECT COUNT(*)::integer AS \"Value\" FROM information_schema.columns WHERE table_schema='councils' AND table_name='reviewer_evaluation' AND column_name='status'").SingleAsync());
    }

    [Fact]
    public async Task Reviewer_evaluation_migration_reapply_preserves_custom_permission_metadata_grants_and_assigned_time()
    {
        await UpgradeAsync();
        await using var migration = Db<UGTMDbContext>();
        await migration.Database.ExecuteSqlRawAsync("""
            UPDATE identity.permission SET permission_name='CUSTOM REVIEWER READ' WHERE permission_code='thesis.reviewer-evaluations.read';
            UPDATE identity.role_permission SET assigned_at=TIMESTAMPTZ '2020-01-01 00:00:00Z'
            WHERE permission_id=(SELECT id FROM identity.permission WHERE permission_code='thesis.reviewer-evaluations.read');
            INSERT INTO identity.role_permission (role_id,permission_id,assigned_at)
            SELECT r.id,p.id,TIMESTAMPTZ '2020-01-01 00:00:00Z' FROM identity.role r CROSS JOIN identity.permission p
            WHERE r.role_name='Student' AND p.permission_code='thesis.reviewer-evaluations.read';
            """);
        await migration.Database.GetService<IMigrator>().MigrateAsync(CurrentMigration);
        await migration.Database.MigrateAsync();
        await using var identity = Db<IdentityDbContext>();
        var permission = await identity.Permissions.SingleAsync(value => value.PermissionCode == PermissionCodes.ThesisReviewerEvaluationsRead);
        Assert.Equal("CUSTOM REVIEWER READ", permission.PermissionName);
        var grants = await identity.RolePermissions.Where(value => value.PermissionId == permission.Id).ToArrayAsync();
        Assert.Equal(4, grants.Length); Assert.All(grants, value => Assert.Equal(2020, value.AssignedAt.Year));
    }
}
