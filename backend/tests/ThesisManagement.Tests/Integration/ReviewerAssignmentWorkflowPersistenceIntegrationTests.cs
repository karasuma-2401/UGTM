using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using IdentityUser = UGTM.Modules.Identity.Domain.Entities.User;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReviewerAssignmentWorkflowPersistenceIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string PreviousMigration = "20261028180816_AddAdvisorEvaluationIdempotency";
    private const string CurrentMigration = "20261030022105_AddReviewerAssignmentWorkflowFoundation";
    private const string Checksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTime Now = new(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _advisorId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _otherGroupId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await using var db = Db<UGTMDbContext>();
        await ResetAsync(db);
        await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await SeedReferencesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Db<UGTMDbContext>();
        await ResetAsync(db);
    }

    [Fact]
    public async Task Upgrade_adds_binding_constraints_system_concurrency_and_default_permissions()
    {
        await UpgradeAsync();
        await using var db = Db<UGTMDbContext>();
        Assert.Equal(10, await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::integer AS "Value" FROM information_schema.columns
            WHERE table_schema = 'councils' AND table_name = 'reviewer_assignment'
              AND column_name IN ('advisor_evaluation_id', 'advisor_id_at_assignment', 'final_report_id',
                  'report_version_no', 'submission_file_id', 'submission_checksum_sha256', 'is_legacy',
                  'cancelled_by', 'cancelled_at', 'cancellation_reason')
            """).SingleAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::integer AS "Value" FROM information_schema.columns
            WHERE table_schema = 'councils' AND table_name = 'reviewer_assignment' AND column_name = 'xmin'
            """).SingleAsync());
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::integer AS "Value" FROM pg_constraint
            WHERE conrelid = 'councils.reviewer_assignment'::regclass
              AND conname IN ('ck_reviewer_assignment_workflow_binding', 'ck_reviewer_assignment_cancellation')
            """).SingleAsync());
        Assert.False(db.Database.HasPendingModelChanges());

        await using var identity = Db<IdentityDbContext>();
        foreach (var code in new[] { PermissionCodes.ThesisReviewerAssignmentsRead, PermissionCodes.ThesisReviewerAssignmentsManage })
        {
            Assert.Single(await identity.Permissions.Where(value => value.PermissionCode == code).ToListAsync());
            var roles = await identity.RolePermissions.Where(value => value.Permission.PermissionCode == code)
                .Select(value => value.Role.RoleName).OrderBy(value => value).ToListAsync();
            Assert.Equal(["AcademicAffairs", "Admin", "Faculty"], roles);
        }
    }

    [Fact]
    public async Task Upgrade_and_rollback_preserve_legacy_actors_order_status_and_existing_evaluation()
    {
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        await InsertLegacyAsync(activeId, _groupId, _reviewerId, 1, true);
        await InsertLegacyAsync(inactiveId, _otherGroupId, _reviewerId, 2, false);
        await InsertEvaluationAsync(evaluationId, activeId);
        await UpgradeAsync();

        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var assignments = await defense.ReviewerAssignments.OrderBy(value => value.AssignmentOrder).ToListAsync();
            Assert.Equal(2, assignments.Count);
            Assert.All(assignments, value =>
            {
                Assert.True(value.IsLegacy);
                Assert.Equal(_actorId, value.AssignedBy);
                Assert.Equal(Now, value.AssignedAt);
                Assert.Null(value.AdvisorEvaluationId);
                Assert.Null(value.AdvisorIdAtAssignment);
                Assert.Null(value.FinalReportId);
                Assert.Null(value.ReportVersionNo);
                Assert.Null(value.SubmissionFileId);
                Assert.Null(value.SubmissionChecksumSha256);
                Assert.Null(value.CancelledBy);
                Assert.Null(value.CancelledAt);
                Assert.Null(value.CancellationReason);
            });
            Assert.True(assignments[0].IsActive);
            Assert.False(assignments[1].IsActive);
            Assert.Equal(2, assignments[1].AssignmentOrder);
            Assert.Equal(8m, (await defense.ReviewerEvaluations.SingleAsync()).Score);
        }

        await using (var db = Db<UGTMDbContext>())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(2, await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::integer AS \"Value\" FROM councils.reviewer_assignment").SingleAsync());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::integer AS \"Value\" FROM councils.reviewer_evaluation").SingleAsync());
            await db.Database.GetService<IMigrator>().MigrateAsync();
        }
        await using var verified = Db<DefenseCouncilDbContext>();
        Assert.All(await verified.ReviewerAssignments.ToListAsync(), value => Assert.True(value.IsLegacy));
        Assert.Equal(evaluationId, (await verified.ReviewerEvaluations.SingleAsync()).Id);
    }

    [Fact]
    public async Task Upgrade_preflight_rejects_multiple_active_assignments_without_changing_history_or_schema()
    {
        await InsertLegacyAsync(Guid.NewGuid(), _groupId, _reviewerId, 1, true);
        await InsertLegacyAsync(Guid.NewGuid(), _groupId, _actorId, 2, true);
        await using var db = Db<UGTMDbContext>();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.GetService<IMigrator>().MigrateAsync());
        Assert.Contains("SCRUM-104 preflight: multiple active", error.MessageText);
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*)::integer AS \"Value\" FROM councils.reviewer_assignment WHERE is_active").SingleAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::integer AS "Value" FROM information_schema.columns
            WHERE table_schema = 'councils' AND table_name = 'reviewer_assignment' AND column_name = 'is_legacy'
            """).SingleAsync());
        Assert.DoesNotContain(CurrentMigration, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Active_uniqueness_allows_cancellation_and_reassignment_to_the_same_reviewer_but_guards_downgrade()
    {
        await UpgradeAsync();
        var assignment = NewAssignment();
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            defense.ReviewerAssignments.Add(assignment);
            await defense.SaveChangesAsync();
            Assert.True(assignment.Version > 0);
        }
        await using (var competing = Db<DefenseCouncilDbContext>())
        {
            competing.ReviewerAssignments.Add(NewAssignment());
            var error = await Assert.ThrowsAsync<DefenseCouncilUniqueConstraintException>(() => competing.SaveChangesAsync());
            Assert.Equal("ux_reviewer_assignment_active_group", error.ConstraintName);
        }

        var oldVersion = assignment.Version;
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var active = await defense.ReviewerAssignments.SingleAsync();
            Assert.True(active.TryCancel(_actorId, Now.AddMinutes(1), "  Đổi phân công.  ", false));
            await defense.SaveChangesAsync();
            Assert.NotEqual(oldVersion, active.Version);
            defense.ReviewerAssignments.Add(NewAssignment());
            await defense.SaveChangesAsync();
        }

        await using var verification = Db<DefenseCouncilDbContext>();
        var history = await verification.ReviewerAssignments.ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Single(history, value => value.IsActive);
        Assert.All(history, value => Assert.Equal(1, value.AssignmentOrder));
        var cancelled = Assert.Single(history, value => !value.IsActive);
        Assert.Equal(assignment.Id, cancelled.Id);
        Assert.Equal("Đổi phân công.", cancelled.CancellationReason);
        Assert.Equal(_actorId, cancelled.CancelledBy);
        Assert.Equal(Now, cancelled.AssignedAt);
        Assert.Equal(Checksum, cancelled.SubmissionChecksumSha256);

        await using var db = Db<UGTMDbContext>();
        var downgrade = await Assert.ThrowsAsync<PostgresException>(() => db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Contains("rollback preflight", downgrade.MessageText);
        Assert.Contains(CurrentMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(2, await verification.ReviewerAssignments.CountAsync());
    }

    [Fact]
    public async Task Database_constraints_reject_incomplete_or_conflicting_binding_and_invalid_cancellation()
    {
        await UpgradeAsync();
        var assignment = NewAssignment();
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            defense.ReviewerAssignments.Add(assignment);
            await defense.SaveChangesAsync();
        }

        FormattableString[] invalidUpdates =
        [
            $"UPDATE councils.reviewer_assignment SET report_version_no = NULL WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET advisor_evaluation_id = NULL WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET advisor_id_at_assignment = reviewer_id WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET submission_checksum_sha256 = repeat('g', 64) WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET assignment_order = 2 WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET is_active = false WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET cancelled_by = assigned_by WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET is_active = false, cancelled_by = assigned_by, cancelled_at = assigned_at, cancellation_reason = '   ' WHERE id = {assignment.Id}",
            $"UPDATE councils.reviewer_assignment SET is_active = false, cancelled_by = assigned_by, cancelled_at = assigned_at - interval '1 second', cancellation_reason = 'Lý do' WHERE id = {assignment.Id}"
        ];
        await using var db = Db<UGTMDbContext>();
        foreach (var update in invalidUpdates)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(update));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }
        await using var verified = Db<DefenseCouncilDbContext>();
        Assert.True((await verified.ReviewerAssignments.SingleAsync()).IsActive);
        Assert.Equal(2, (await verified.ReviewerAssignments.SingleAsync()).ReportVersionNo);
    }

    [Fact]
    public async Task Existing_evaluation_blocks_hard_delete_and_group_cascade_and_loaded_navigation_blocks_cancel()
    {
        await UpgradeAsync();
        var assignment = NewAssignment();
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            defense.ReviewerAssignments.Add(assignment);
            await defense.SaveChangesAsync();
        }
        await InsertEvaluationAsync(Guid.NewGuid(), assignment.Id);
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var loaded = await defense.ReviewerAssignments.Include(value => value.Evaluation).SingleAsync();
            Assert.NotNull(loaded.Evaluation);
            Assert.False(loaded.TryCancel(_actorId, Now, "Không được hủy.", false));
        }
        await using var db = Db<UGTMDbContext>();
        var deleteAssignment = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM councils.reviewer_assignment WHERE id = {assignment.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deleteAssignment.SqlState);
        Assert.Equal("fk_reviewer_evaluation_reviewer_assignment_assignment_id", deleteAssignment.ConstraintName);
        var deleteGroup = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM thesis.student_group WHERE id = {_groupId}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deleteGroup.SqlState);
        await using var verification = Db<DefenseCouncilDbContext>();
        Assert.Equal(1, await verification.ReviewerAssignments.CountAsync());
        Assert.Equal(1, await verification.ReviewerEvaluations.CountAsync());
        Assert.Equal(1, await db.Database.SqlQuery<int>(
            $"SELECT COUNT(*)::integer AS \"Value\" FROM thesis.student_group WHERE id = {_groupId}").SingleAsync());
    }

    [Fact]
    public async Task Concurrent_cancellations_use_xmin_and_preserve_the_winning_actor_and_reason()
    {
        await UpgradeAsync();
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            defense.ReviewerAssignments.Add(NewAssignment());
            await defense.SaveChangesAsync();
        }
        await using var first = Db<DefenseCouncilDbContext>();
        await using var stale = Db<DefenseCouncilDbContext>();
        var winner = await first.ReviewerAssignments.SingleAsync();
        var loser = await stale.ReviewerAssignments.SingleAsync();
        Assert.True(winner.TryCancel(_actorId, Now.AddMinutes(1), "Quyết định đã thắng", false));
        Assert.True(loser.TryCancel(_advisorId, Now.AddMinutes(2), "Quyết định cũ", false));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DefenseCouncilConcurrencyException>(() => stale.SaveChangesAsync());
        await using var verification = Db<DefenseCouncilDbContext>();
        var saved = await verification.ReviewerAssignments.SingleAsync();
        Assert.Equal(_actorId, saved.CancelledBy);
        Assert.Equal("Quyết định đã thắng", saved.CancellationReason);
    }

    [Fact]
    public async Task Legacy_assignments_without_evaluations_can_be_cancelled_without_inventing_a_binding()
    {
        var id = Guid.NewGuid();
        await InsertLegacyAsync(id, _groupId, _reviewerId, 2, true);
        await UpgradeAsync();
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var legacy = await defense.ReviewerAssignments.SingleAsync();
            Assert.True(legacy.TryCancel(_actorId, Now.AddMinutes(1), "Làm rõ phân công cũ", false));
            await defense.SaveChangesAsync();
        }
        await using var verified = Db<DefenseCouncilDbContext>();
        var saved = await verified.ReviewerAssignments.SingleAsync();
        Assert.True(saved.IsLegacy);
        Assert.False(saved.IsActive);
        Assert.Equal(2, saved.AssignmentOrder);
        Assert.Null(saved.FinalReportId);
        Assert.Null(saved.AdvisorEvaluationId);
        Assert.Equal(_actorId, saved.CancelledBy);
        Assert.Equal(Now.AddMinutes(1), saved.CancelledAt);
    }

    [Fact]
    public async Task Idempotency_is_scoped_by_actor_operation_and_key_and_preserves_json_and_status()
    {
        await UpgradeAsync();
        var key = Guid.NewGuid();
        ReviewerAssignmentIdempotencyRecord Receipt(Guid actor, string operation, int status)
        {
            var value = ReviewerAssignmentIdempotencyRecord.Create(Guid.NewGuid(), actor, operation, key, Checksum, status, Now);
            value.SetResponsePayload("{\"assignmentVersion\":42,\"isActive\":true}");
            return value;
        }
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            defense.ReviewerAssignmentIdempotencyRecords.AddRange(
                Receipt(_actorId, "Create", 201), Receipt(_actorId, "Cancel", 200), Receipt(_advisorId, "Create", 201));
            await defense.SaveChangesAsync();
        }
        await using (var duplicate = Db<DefenseCouncilDbContext>())
        {
            duplicate.ReviewerAssignmentIdempotencyRecords.Add(Receipt(_actorId, "Create", 201));
            var error = await Assert.ThrowsAsync<DefenseCouncilUniqueConstraintException>(() => duplicate.SaveChangesAsync());
            Assert.Equal("ux_reviewer_assignment_idempotency_actor_operation_key", error.ConstraintName);
        }
        await using var verified = Db<DefenseCouncilDbContext>();
        var receipts = await verified.ReviewerAssignmentIdempotencyRecords.ToListAsync();
        Assert.Equal(3, receipts.Count);
        Assert.Equal(201, Assert.Single(receipts, value => value.ActorId == _actorId && value.Operation == "Create").ResponseStatusCode);
        Assert.Equal(200, Assert.Single(receipts, value => value.Operation == "Cancel").ResponseStatusCode);
        Assert.All(receipts, value => Assert.Contains("\"assignmentVersion\": 42", value.ResponsePayloadJson));
    }

    [Fact]
    public async Task Permission_seed_preserves_custom_metadata_grants_and_original_assignment_times_on_reapply()
    {
        await using var db = Db<UGTMDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO identity.permission (id, module, permission_code, permission_name)
            VALUES (9000, 'Custom', 'thesis.reviewer-assignments.read', 'Tên đã tùy chỉnh');
            INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
            SELECT id, 9000, TIMESTAMPTZ '2025-01-01 00:00:00Z' FROM identity.role
            WHERE role_name IN ('Faculty', 'CouncilMember');
            """);
        await db.Database.GetService<IMigrator>().MigrateAsync();
        await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await db.Database.GetService<IMigrator>().MigrateAsync();
        await using var identity = Db<IdentityDbContext>();
        var permission = await identity.Permissions.SingleAsync(value => value.Id == 9000);
        Assert.Equal("Tên đã tùy chỉnh", permission.PermissionName);
        Assert.Equal("Custom", permission.Module);
        var original = await identity.RolePermissions.Where(value => value.PermissionId == 9000
            && (value.Role.RoleName == "Faculty" || value.Role.RoleName == "CouncilMember")).ToListAsync();
        Assert.Equal(2, original.Count);
        Assert.All(original, value => Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), value.AssignedAt));
        Assert.Equal(2, await identity.Permissions.CountAsync(value => value.PermissionCode.StartsWith("thesis.reviewer-assignments.")));
    }

    private ReviewerAssignment NewAssignment()
        => ReviewerAssignment.Create(Guid.NewGuid(), _groupId, _reviewerId, _advisorId,
            Guid.NewGuid(), Guid.NewGuid(), 2, Guid.NewGuid(), Checksum, _actorId, Now);

    private async Task SeedReferencesAsync()
    {
        await using (var identity = Db<IdentityDbContext>())
        {
            identity.Users.AddRange(new[] { _actorId, _reviewerId, _advisorId }.Select((id, index) => IdentityUser.Create(
                id, $"reviewer{index}@test.example", $"REVIEWER{index}@TEST.EXAMPLE", $"Reviewer {index}",
                $"RA{index}", null, null, AccountStatus.Active, Now)));
            await identity.SaveChangesAsync();
        }
        StageTemplateInput Stage(int order, string code, int from, int to)
            => new(order, code, code, from, to, new TimeOnly(23, 59));
        var template = ScheduleTemplateVersion.CreateDraft(Guid.NewGuid(), "REVIEWER_PERSISTENCE", 1,
            "Reviewer persistence", KltnBatch.DefaultTimeZoneId,
            [Stage(1, "TOPIC_PREPARATION", 0, 6), Stage(2, "REGISTRATION_AND_SYLLABUS", 7, 13),
             Stage(3, "PROGRESS_EXECUTION", 14, 62), Stage(4, "MIDTERM_GATE", 63, 69),
             Stage(5, "FINAL_EVALUATION", 70, 125), Stage(6, "DEFENSE", 126, 139),
             Stage(7, "ARCHIVAL_AND_PUBLICATION", 140, 146)], _actorId, Now);
        template.Publish(_actorId, Now);
        var start = new DateOnly(2026, 8, 1);
        var batch = KltnBatch.Create(Guid.NewGuid(), "Reviewer persistence", "2026-2027", 1,
            start, start.AddDays(146), template, _actorId, Now);
        await using (var academic = Db<AcademicPeriodsDbContext>())
        {
            academic.ScheduleTemplateVersions.Add(template);
            academic.KltnBatches.Add(batch);
            await academic.SaveChangesAsync();
        }
        var topicId = Guid.NewGuid();
        await using (var topics = Db<TopicCatalogDbContext>())
        {
            topics.Topics.Add(new Topic
            {
                Id = topicId,
                BatchId = batch.Id,
                AdvisorId = _advisorId,
                TitleVi = "Reviewer persistence",
                StudentNumber = 2,
                Status = TopicStatus.Published,
                CreatedAt = Now
            });
            await topics.SaveChangesAsync();
        }
        await using var thesis = Db<ThesisManagementDbContext>();
        thesis.StudentGroups.AddRange(new StudentGroup(_groupId, topicId, Now), new StudentGroup(_otherGroupId, topicId, Now));
        await thesis.SaveChangesAsync();
    }

    private async Task InsertLegacyAsync(Guid id, Guid group, Guid reviewer, int order, bool active)
    {
        await using var db = Db<UGTMDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO councils.reviewer_assignment
                (id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active)
            VALUES ({id}, {group}, {reviewer}, {_actorId}, {Now}, {order}, {active})
            """);
    }

    private async Task InsertEvaluationAsync(Guid id, Guid assignmentId)
    {
        await using var db = Db<UGTMDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO councils.reviewer_evaluation
                (id, assignment_id, score, comment, revision_feedback, is_ignored_in_calculation,
                 exclusion_reason, defense_approval, is_visible_to_student, evaluated_at)
            VALUES ({id}, {assignmentId}, {8m}, {"Phiếu đã chấm"}, {null}, {false}, {null},
                {nameof(DefenseApprovalStatus.Approved)}, {false}, {Now})
            """);
    }

    private async Task UpgradeAsync()
    {
        await using var db = Db<UGTMDbContext>();
        await db.Database.GetService<IMigrator>().MigrateAsync();
    }

    private T Db<T>() where T : DbContext
    {
        var options = new DbContextOptionsBuilder<T>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return (T)Activator.CreateInstance(typeof(T), options.Options)!;
    }

    private static Task ResetAsync(UGTMDbContext db)
        => db.Database.ExecuteSqlRawAsync("""
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
