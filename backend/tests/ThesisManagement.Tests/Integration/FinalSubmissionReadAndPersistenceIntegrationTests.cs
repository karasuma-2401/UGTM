using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Fact]
    public async Task Archival_stale_xmin_and_changed_key_body_cannot_overwrite_the_package()
    {
        await PrepareArchivalAsync();
        var key = Guid.NewGuid();
        using var first = await ArchiveAsync(key);
        var original = await ArchivalDataAsync(first);
        var version = original.GetProperty("submissionVersion").GetUInt32();
        using var missing = await ArchiveAsync();
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        using var collision = await ArchiveAsync(key, version);
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        Assert.Contains("FinalSubmission.IdempotencyConflict", await collision.Content.ReadAsStringAsync());
        using var revision = await ArchiveAsync(version: version);
        await ArchivalDataAsync(revision);
        using var stale = await ArchiveAsync(version: version);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Equal(2, await db.FinalSubmissionPackages.CountAsync());
        Assert.Equal(2, await db.FinalSubmissionIdempotencyRecords.CountAsync());
        Assert.Equal(4, (await StoredKeysAsync()).Count);
    }
    [Theory]
    [InlineData("start", false)]
    [InlineData("deadline", false)]
    [InlineData("before", true)]
    [InlineData("after", true)]
    public async Task Archival_effective_timezone_window_is_inclusive(string boundary, bool blocked)
    {
        await PrepareArchivalAsync();
        await using var db = Db<AcademicPeriodsDbContext>();
        var stage = await db.Stages.SingleAsync(s => s.StageCode == "ARCHIVAL_AND_PUBLICATION");
        var start = stage.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        _clock.UtcNow = boundary switch
        {
            "start" => start,
            "deadline" => stage.DeadlineAtUtc,
            "before" => start.AddTicks(-1),
            _ => stage.DeadlineAtUtc.AddTicks(1)
        };
        using var response = await ArchiveAsync();
        if (blocked) { Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Empty(await StoredKeysAsync()); }
        else { await ArchivalDataAsync(response); Assert.Equal(2, (await StoredKeysAsync()).Count); }
    }
    [Theory]
    [InlineData("Open")]
    [InlineData("AllScoresEntered")]
    [InlineData("UnlockedByOverride")]
    public async Task Archival_unlocked_or_incomplete_minutes_cannot_be_used_as_defense_proof(string status)
    {
        await PrepareArchivalAsync();
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.council_minute SET lock_status={status}");
        using var response = await ArchiveAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("FinalSubmission.DefenseNotLocked", await response.Content.ReadAsStringAsync());
        Assert.Empty(await StoredKeysAsync());
    }
    [Fact]
    public async Task Archival_student_with_two_batches_selects_exactly_the_requested_group()
    {
        await PrepareArchivalAsync();
        using var submitted = await ArchiveAsync();
        var current = await ArchivalDataAsync(submitted);
        Guid secondBatch;
        await using (var db = Db<AcademicPeriodsDbContext>())
        {
            var template = await db.ScheduleTemplateVersions.Include(t => t.StageDefinitions).SingleAsync(t => t.TemplateCode == "FINAL_REPORT_WORKFLOW");
            var batch = KltnBatch.Create(Guid.NewGuid(), "Historical batch selector", "2026-2027", 2,
                BatchStart, BatchStart.AddDays(146), template, _advisorId, SeedTime);
            db.KltnBatches.Add(batch);
            await db.SaveChangesAsync();
            secondBatch = batch.Id;
        }
        var topicId = Guid.NewGuid();
        await using (var topics = Db<TopicCatalogDbContext>())
        {
            topics.Topics.Add(new Topic
            {
                Id = topicId,
                BatchId = secondBatch,
                AdvisorId = _advisorId,
                TitleVi = "Second batch",
                StudentNumber = 1,
                Status = TopicStatus.Published,
                CreatedAt = SeedTime
            });
            await topics.SaveChangesAsync();
        }
        var other = new StudentGroup(Guid.NewGuid(), topicId, SeedTime);
        other.AddConfirmedMember(_studentId, secondBatch, SeedTime);
        await using (var thesis = Db<ThesisManagementDbContext>()) { thesis.StudentGroups.Add(other); await thesis.SaveChangesAsync(); }
        using var own = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/my-group?groupId={_groupId}", _studentId, "Student");
        Assert.Equal(current.GetProperty("submissionId").GetGuid(), (await ArchivalDataAsync(own)).GetProperty("submissionId").GetGuid());
        using var empty = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/my-group?groupId={other.Id}", _studentId, "Student");
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        using var eligibility = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/submission-eligibility?groupId={other.Id}", _studentId, "Student");
        Assert.Equal(other.Id, (await ArchivalDataAsync(eligibility)).GetProperty("groupId").GetGuid());
    }
    [Theory]
    [InlineData("Faculty")]
    [InlineData("AcademicAffairs")]
    public async Task Archival_management_reads_require_live_role_and_read_permission_and_do_not_grant_student_upload(string roleName)
    {
        await PrepareArchivalAsync();
        await using (var identity = Db<IdentityDbContext>())
        {
            var role = await identity.Roles.SingleAsync(r => r.RoleName == roleName);
            identity.UserRoles.Add(new UserRole { UserId = _replacementAdvisorId, RoleId = role.Id, AssignedAt = SeedTime });
            await identity.SaveChangesAsync();
        }
        using var submitted = await ArchiveAsync();
        var id = (await ArchivalDataAsync(submitted)).GetProperty("submissionId").GetGuid();
        using var list = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}?batchId={_batchId}&pageSize=1&searchTerm={_groupId}", _replacementAdvisorId, roleName);
        var data = await ArchivalDataAsync(list);
        Assert.Equal(1, data.GetProperty("totalCount").GetInt32());
        using var detail = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{id}", _replacementAdvisorId, roleName);
        await ArchivalDataAsync(detail);
        using var denied = await ArchiveAsync(student: _replacementAdvisorId);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using (var identity = Db<IdentityDbContext>())
            await identity.RolePermissions.Where(p => p.Role.RoleName == roleName && p.Permission.PermissionCode == "thesis.final-submissions.read").ExecuteDeleteAsync();
        using var removed = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{id}", _replacementAdvisorId, roleName);
        Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
    }
    [Fact]
    public async Task Archival_student_list_and_unrelated_lecturer_download_are_denied_but_closed_batch_reads_survive()
    {
        await PrepareArchivalAsync();
        await using (var identity = Db<IdentityDbContext>())
        {
            var lecturer = await identity.Roles.SingleAsync(r => r.RoleName == "Lecturer");
            identity.UserRoles.Add(new UserRole { UserId = _replacementAdvisorId, RoleId = lecturer.Id, AssignedAt = SeedTime });
            await identity.SaveChangesAsync();
        }
        using var submitted = await ArchiveAsync();
        var id = (await ArchivalDataAsync(submitted)).GetProperty("submissionId").GetGuid();
        using var list = await SendAsync(HttpMethod.Get, ArchivalRoute, _studentId, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        using var unrelated = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{id}/packages/1/files/ThesisPdf/download", _replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, unrelated.StatusCode);
        foreach (var status in new[] { "Closed", "Archived" })
        {
            await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status={status}");
            using var own = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/my-group?groupId={_groupId}", _studentId, "Student");
            await ArchivalDataAsync(own);
        }
    }
    [Fact]
    public async Task Archival_schema_rejects_half_package_and_immutable_history_edits()
    {
        await PrepareArchivalAsync();
        using var submitted = await ArchiveAsync();
        await ArchivalDataAsync(submitted);
        await using (var db = Db<ThesisManagementDbContext>())
        {
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE thesis.final_submission_package SET abstract_vi='changed'"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE thesis.submission_file SET file_size=1 WHERE entity_type='FinalSubmission'"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM thesis.final_submission_package_file WHERE file_role='ThesisPdf'"));
        }
        await using (var db = Db<ThesisManagementDbContext>())
        {
            var group = new StudentGroup(Guid.NewGuid(), _topicId, SeedTime);
            var row = new FinalSubmission { Id = Guid.NewGuid(), GroupId = group.Id };
            var package = new FinalSubmissionPackage
            {
                Id = Guid.NewGuid(),
                SubmissionId = row.Id,
                VersionNo = 1,
                AbstractVi = "vi",
                AbstractEn = "en",
                KeywordsVi = "kv",
                KeywordsEn = "ke",
                SubmittedBy = _studentId,
                SubmittedAtUtc = _clock.UtcNow,
                ManifestSha256 = new string('a', 64)
            };
            row.Submit(package);
            db.StudentGroups.Add(group);
            db.FinalSubmissions.Add(row);
            db.FinalSubmissionPackages.Add(package);
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.SaveChangesAsync());
            Assert.Equal("23514", error.SqlState);
        }
        await using var check = Db<ThesisManagementDbContext>();
        Assert.Single(await check.FinalSubmissions.ToListAsync());
        Assert.Single(await check.FinalSubmissionPackages.ToListAsync());
    }
    [Fact]
    public async Task Archival_migration_roundtrip_preserves_legacy_metadata_and_system_xmin_and_refuses_lossy_down()
    {
        await PrepareArchivalAsync();
        var legacyId = Guid.NewGuid();
        await using (var db = MigrationDb())
        {
            Assert.False(db.Database.HasPendingModelChanges());
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261030113753_AddReviewerEvaluationWorkflowFoundation");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO thesis.final_submission(id,group_id,status,current_version_no,abstract_vi,abstract_en,keywords_vi,keywords_en)
                VALUES({legacyId},{_groupId},'Submitted',4,'legacy vi','legacy en','legacy kv','legacy ke')
                """);
            await migrator.MigrateAsync();
        }
        uint xmin;
        await using (var db = Db<ThesisManagementDbContext>())
        {
            var legacy = await db.FinalSubmissions.SingleAsync();
            Assert.True(legacy.IsLegacy);
            Assert.Null(legacy.CurrentPackageId);
            Assert.Equal("legacy vi", legacy.AbstractVi);
            Assert.Equal(4, legacy.CurrentVersionNo);
            xmin = legacy.Version;
            Assert.True(xmin > 0);
        }
        using var missingVersion = await ArchiveAsync();
        Assert.Equal(HttpStatusCode.Conflict, missingVersion.StatusCode);
        using var upgrade = await ArchiveAsync(version: xmin);
        await ArchivalDataAsync(upgrade);
        await using (var db = Db<ThesisManagementDbContext>())
        {
            var row = await db.FinalSubmissions.SingleAsync();
            Assert.Equal(legacyId, row.Id);
            Assert.False(row.IsLegacy);
            Assert.Contains("legacy vi", row.LegacySnapshot);
        }
        await using var migration = MigrationDb();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migration.GetService<IMigrator>().MigrateAsync("20261030113753_AddReviewerEvaluationWorkflowFoundation"));
    }
    [Fact]
    public async Task Archival_contract_lock_requires_transaction_and_coordinator_uses_one_backend_and_forbids_reader_mutation()
    {
        await PrepareArchivalAsync();
        using var scope = _app.Services.CreateScope();
        var services = scope.ServiceProvider;
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<ILockedDefenseProofReader>().LockAsync(_groupId));
        var coordinator = services.GetRequiredService<IFinalSubmissionWorkflowTransactionCoordinator>();
        var ids = await coordinator.ExecuteWithPostSaveAsync(async ct =>
        {
            DbContext[] contexts = [services.GetRequiredService<ThesisManagementDbContext>(), services.GetRequiredService<DefenseCouncilDbContext>(),
                services.GetRequiredService<AcademicPeriodsDbContext>(), services.GetRequiredService<IdentityDbContext>(), services.GetRequiredService<TopicCatalogDbContext>()];
            var backends = new List<int>();
            foreach (var db in contexts) backends.Add(await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(ct));
            return FinalSubmissionTransactionResult<int[]>.Abort(backends.ToArray());
        }, (value, _) => Task.FromResult(value));
        Assert.Single(ids.Distinct());
        // Reuse after Abort, proving original reader connections were restored without disposal.
        Assert.Equal(LockedDefenseOutcome.Succeeded, (await services.GetRequiredService<ILockedDefenseProofReader>().GetAsync(_groupId)).Outcome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteWithPostSaveAsync(async ct =>
        {
            var db = services.GetRequiredService<DefenseCouncilDbContext>();
            (await db.CouncilMinutes.SingleAsync(ct)).GeneralFeedback = "forbidden cross-module write";
            return FinalSubmissionTransactionResult<bool>.Commit(true);
        }, (value, _) => Task.FromResult(value)));
        await using var check = Db<DefenseCouncilDbContext>();
        Assert.Null((await check.CouncilMinutes.SingleAsync()).GeneralFeedback);
    }
    [Theory]
    [InlineData("InvalidLegacyState", 1)]
    [InlineData("Submitted", 0)]
    public async Task Archival_migration_rejects_malformed_legacy_without_partial_schema_changes(string status, int version)
    {
        await using var db = MigrationDb();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261030113753_AddReviewerEvaluationWorkflowFoundation");
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO thesis.final_submission(id,group_id,status,current_version_no) VALUES({id},{_groupId},{status},{version})");
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains(id.ToString(), error.MessageText);
        Assert.False(await db.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='thesis'
                AND table_name='final_submission' AND column_name='current_package_id') AS "Value"
            """).SingleAsync());
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.final_submission SET status='Submitted',current_version_no=1 WHERE id={id}");
        await migrator.MigrateAsync();
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.True((await thesis.FinalSubmissions.SingleAsync()).IsLegacy);
        Assert.Empty(await thesis.FinalSubmissionPackages.ToListAsync());
    }
    [Fact]
    public async Task Archival_openapi_describes_multipart_key_and_group_selectors_without_CP2_confirmation_endpoint()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        var post = paths.GetProperty(ArchivalRoute).GetProperty("post");
        Assert.True(post.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        Assert.Contains(post.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key" && p.GetProperty("required").GetBoolean());
        foreach (var suffix in new[] { "/my-group", "/submission-eligibility" })
            Assert.Contains(paths.GetProperty(ArchivalRoute + suffix).GetProperty("get").GetProperty("parameters").EnumerateArray(),
                p => p.GetProperty("name").GetString() == "groupId" && p.GetProperty("required").GetBoolean());
        Assert.DoesNotContain(paths.EnumerateObject(), p => p.Name.StartsWith(ArchivalRoute, StringComparison.Ordinal) && p.Name.EndsWith("confirm-archive", StringComparison.Ordinal));
    }
}
