using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    private async Task GrantArchivalRoleAsync(Guid user, string roleName)
    {
        await using var db = Db<IdentityDbContext>();
        var role = await db.Roles.SingleAsync(r => r.RoleName == roleName);
        db.UserRoles.Add(new UserRole { UserId = user, RoleId = role.Id, AssignedAt = SeedTime });
        await db.SaveChangesAsync();
    }
    private async Task<HttpResponseMessage> ConfirmArchivalAsync(JsonElement current, string action = "AdvisorConfirm", Guid? actor = null,
        string role = "Lecturer", string? reason = null, Guid? key = null, string? raw = null, string? suffix = null)
    {
        var id = current.GetProperty("submissionId").GetGuid();
        var json = raw ?? JsonSerializer.Serialize(new
        {
            action,
            expectedSubmissionVersion = current.GetProperty("submissionVersion").GetUInt32(),
            expectedPackageVersion = current.GetProperty("currentVersionNo").GetInt32(),
            reason
        });
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{ArchivalRoute}/{id}/confirm-archive{suffix}") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actor ?? _advisorId:N}.{role}");
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await _client.SendAsync(request);
    }
    private async Task<JsonElement> StartArchivalReviewAsync()
    {
        await PrepareArchivalAsync();
        using var submitted = await ArchiveAsync();
        return await ArchivalDataAsync(submitted);
    }
    private async Task<JsonElement> SealArchivalAsync()
    {
        var current = await StartArchivalReviewAsync();
        using var advisor = await ConfirmArchivalAsync(current);
        var confirmed = await ArchivalDataAsync(advisor);
        using var faculty = await ConfirmArchivalAsync(confirmed, "FacultyConfirm", _adminId, "Admin");
        return await ArchivalDataAsync(faculty);
    }

    [Fact]
    public async Task Archival_confirmation_two_steps_revision_version_history_real_archive_proof_and_admin_audit()
    {
        var current = await StartArchivalReviewAsync();
        using var revision = await ConfirmArchivalAsync(current, "RequestRevision", reason: "  Sửa theo góp ý Hội đồng  ");
        var rejected = await ArchivalDataAsync(revision);
        Assert.Equal("NeedRevision", rejected.GetProperty("status").GetString());
        Assert.Equal("Sửa theo góp ý Hội đồng", rejected.GetProperty("facultyFeedback").GetString());
        using var resubmit = await ArchiveAsync(version: rejected.GetProperty("submissionVersion").GetUInt32());
        var v2 = await ArchivalDataAsync(resubmit);
        Assert.Equal(2, v2.GetProperty("currentVersionNo").GetInt32());
        using var first = await ConfirmArchivalAsync(v2, actor: _adminId, role: "Admin");
        var advisor = await ArchivalDataAsync(first);
        using var second = await ConfirmArchivalAsync(advisor, "FacultyConfirm", _adminId, "Admin");
        var final = await ArchivalDataAsync(second);
        Assert.Equal("ArchiveConfirmed", final.GetProperty("status").GetString());
        Assert.Equal(_adminId, final.GetProperty("advisorConfirmedBy").GetGuid());
        Assert.Equal(_adminId, final.GetProperty("archivedConfirmedBy").GetGuid());
        using var history = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{final.GetProperty("submissionId").GetGuid()}/packages", _studentId, "Student");
        var packages = (await ArchivalDataAsync(history)).EnumerateArray().ToArray();
        Assert.Equal(2, packages[0].GetProperty("reviews").GetArrayLength());
        Assert.Equal("RequestRevision", packages[1].GetProperty("reviews")[0].GetProperty("action").GetString());
        using var scope = _app.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IFinalSubmissionArchiveProofContract>();
        var proof = await reader.GetArchiveProofAsync(_groupId);
        Assert.Equal(ArchiveProofOutcome.Succeeded, proof.Outcome);
        Assert.Equal(final.GetProperty("submissionVersion").GetUInt32(), proof.SubmissionVersion);
        Assert.Equal(2, proof.PackageVersionNo);
        Assert.Equal(new[] { "ThesisDocx", "ThesisPdf" }, proof.Files.Select(f => f.FileRole));
        Assert.All(proof.Files, f => { Assert.Equal(proof.PackageId, f.PackageId); Assert.Equal(64, f.ChecksumSha256.Length); });
        Assert.NotNull(proof.AdvisorReviewId);
        Assert.NotNull(proof.FacultyReviewId);
        Assert.Equal(_adminId, proof.AdvisorConfirmedBy);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.LockArchiveProofForCalculationAsync(_groupId));
        var coordinator = scope.ServiceProvider.GetRequiredService<IFinalSubmissionWorkflowTransactionCoordinator>();
        var store = scope.ServiceProvider.GetRequiredService<IFinalSubmissionStore>();
        var locked = await coordinator.ExecuteWithPostSaveAsync(async ct =>
        {
            await store.GetGroupAsync(_groupId, true, ct); // consumer owns earlier root guard
            return FinalSubmissionTransactionResult<FinalSubmissionArchiveProofContractDto>.Commit(await reader.LockArchiveProofForCalculationAsync(_groupId, ct));
        }, (p, _) => Task.FromResult(p));
        Assert.Equal(proof with { Files = locked.Files }, locked);
        await using var db = Db<ThesisManagementDbContext>();
        await using var migration = MigrationDb();
        Assert.False(migration.Database.HasPendingModelChanges());
        Assert.Equal(3, await db.FinalSubmissionReviews.CountAsync());
        Assert.Equal(5, await db.FinalSubmissionIdempotencyRecords.CountAsync());
        var audits = (await db.AuditOutboxMessages.ToListAsync()).Select(a => JsonSerializer.Deserialize<AuditIntegrationEvent>(a.PayloadJson)!).ToArray();
        Assert.Equal(5, audits.Length);
        Assert.Equal(2, audits.Count(a => a.ActorUserId == _adminId));
        Assert.Equal(new[] { "ThesisManagement.FinalSubmission.AdvisorConfirmed", "ThesisManagement.FinalSubmission.ArchiveConfirmed", "ThesisManagement.FinalSubmission.RevisionRequested" },
            audits.Where(a => a.Action != "ThesisManagement.FinalSubmission.Submitted").Select(a => a.Action).Order());
        foreach (var decision in audits.Where(a => a.Action != "ThesisManagement.FinalSubmission.Submitted"))
        {
            using var metadata = JsonDocument.Parse(decision.MetadataJson!);
            Assert.Equal(64, metadata.RootElement.GetProperty("ManifestSha256").GetString()!.Length);
            Assert.Equal(decision.ActorUserId, metadata.RootElement.GetProperty("ActorId").GetGuid());
            Assert.False(metadata.RootElement.TryGetProperty("Reason", out _));
            Assert.False(metadata.RootElement.TryGetProperty("AbstractVi", out _));
        }
        Assert.DoesNotContain("Sửa theo góp ý", string.Join("", audits.Select(a => JsonSerializer.Serialize(a))));
        Assert.Equal(proof.SubmissionVersion, (await db.FinalSubmissions.SingleAsync()).Version);
    }

    [Theory]
    [InlineData("Faculty", "AdvisorConfirm", 403)]
    [InlineData("Faculty", "FacultyConfirm", 409)]
    [InlineData("Faculty", "RequestRevision", 409)]
    [InlineData("AcademicAffairs", "AdvisorConfirm", 403)]
    [InlineData("AcademicAffairs", "FacultyConfirm", 403)]
    [InlineData("Lecturer", "AdvisorConfirm", 403)]
    [InlineData("Student", "AdvisorConfirm", 403)]
    public async Task Archival_confirmation_live_authority_and_no_stage_bypass(string role, string action, int status)
    {
        var current = await StartArchivalReviewAsync();
        await GrantArchivalRoleAsync(_replacementAdvisorId, role);
        using var response = await ConfirmArchivalAsync(current, action, _replacementAdvisorId, role, action == "RequestRevision" ? "Cần sửa" : null);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Empty(await db.FinalSubmissionReviews.ToListAsync());
        Assert.Single(await db.FinalSubmissionIdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task Archival_faculty_manage_permission_is_independent_of_read_and_revocation_blocks_writes()
    {
        var current = await StartArchivalReviewAsync();
        await GrantArchivalRoleAsync(_replacementAdvisorId, "Faculty");
        using var first = await ConfirmArchivalAsync(current);
        var advisor = await ArchivalDataAsync(first);
        await using (var db = Db<IdentityDbContext>())
            await db.RolePermissions.Where(p => p.Role.RoleName == "Faculty" && p.Permission.PermissionCode == "thesis.final-submissions.read").ExecuteDeleteAsync();
        using var detail = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{current.GetProperty("submissionId").GetGuid()}", _replacementAdvisorId, "Faculty");
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
        var key = Guid.NewGuid();
        using var faculty = await ConfirmArchivalAsync(advisor, "FacultyConfirm", _replacementAdvisorId, "Faculty", key: key);
        await ArchivalDataAsync(faculty);
        await using (var db = Db<IdentityDbContext>())
            await db.RolePermissions.Where(p => p.Role.RoleName == "Faculty" && p.Permission.PermissionCode == "thesis.final-submissions.manage").ExecuteDeleteAsync();
        using var replay = await ConfirmArchivalAsync(advisor, "FacultyConfirm", _replacementAdvisorId, "Faculty", key: key);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
    }

    [Theory]
    [InlineData("Open", 200)]
    [InlineData("Closed", 200)]
    [InlineData("Draft", 409)]
    [InlineData("Archived", 409)]
    public async Task Archival_staff_confirmation_ignores_student_deadline_but_respects_batch_state(string state, int status)
    {
        var current = await StartArchivalReviewAsync();
        _clock.UtcNow = _clock.UtcNow.AddYears(1);
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status={state} WHERE id={_batchId}");
        using var response = await ConfirmArchivalAsync(current);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        if (status == 200) await ArchivalDataAsync(response);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("enumNumber")]
    [InlineData("enumStringNumber")]
    [InlineData("enumCase")]
    [InlineData("versionString")]
    [InlineData("packageFraction")]
    [InlineData("zero")]
    [InlineData("noReason")]
    [InlineData("blankReason")]
    [InlineData("longReason")]
    [InlineData("nulReason")]
    [InlineData("invalidUnicode")]
    [InlineData("confirmReason")]
    [InlineData("array")]
    [InlineData("query")]
    public async Task Archival_confirmation_strict_JSON_and_reason_validation_has_no_side_effects(string kind)
    {
        var current = await StartArchivalReviewAsync();
        var version = current.GetProperty("submissionVersion").GetUInt32();
        var prefix = $"\"expectedSubmissionVersion\":{version},\"expectedPackageVersion\":1";
        var json = kind switch
        {
            "unknown" => "{" + prefix + ",\"action\":\"AdvisorConfirm\",\"actorId\":null}",
            "duplicate" => "{" + prefix + ",\"action\":\"AdvisorConfirm\",\"ACTION\":\"AdvisorConfirm\"}",
            "enumNumber" => "{" + prefix + ",\"action\":0}",
            "enumStringNumber" => "{" + prefix + ",\"action\":\"0\"}",
            "enumCase" => "{" + prefix + ",\"action\":\"advisorConfirm\"}",
            "versionString" => $"{{\"expectedSubmissionVersion\":\"{version}\",\"expectedPackageVersion\":1,\"action\":\"AdvisorConfirm\"}}",
            "packageFraction" => $"{{\"expectedSubmissionVersion\":{version},\"expectedPackageVersion\":1.5,\"action\":\"AdvisorConfirm\"}}",
            "zero" => "{\"expectedSubmissionVersion\":0,\"expectedPackageVersion\":1,\"action\":\"AdvisorConfirm\"}",
            "noReason" => "{" + prefix + ",\"action\":\"RequestRevision\"}",
            "blankReason" => "{" + prefix + ",\"action\":\"RequestRevision\",\"reason\":\"  \"}",
            "longReason" => "{" + prefix + ",\"action\":\"RequestRevision\",\"reason\":\"" + new string('a', 1001) + "\"}",
            "nulReason" => "{" + prefix + ",\"action\":\"RequestRevision\",\"reason\":\"x\\u0000\"}",
            "invalidUnicode" => "{" + prefix + ",\"action\":\"RequestRevision\",\"reason\":\"\\uD800\"}",
            "confirmReason" => "{" + prefix + ",\"action\":\"AdvisorConfirm\",\"reason\":\"\"}",
            "array" => "[]",
            _ => "{" + prefix + ",\"action\":\"AdvisorConfirm\"}"
        };
        using var response = await ConfirmArchivalAsync(current, raw: json, suffix: kind == "query" ? "?action=FacultyConfirm" : null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Empty(await db.FinalSubmissionReviews.ToListAsync());
        Assert.Single(await db.FinalSubmissionIdempotencyRecords.ToListAsync());
        Assert.Single(await db.AuditOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Archival_confirmation_replays_original_version_after_seal_or_archive_but_not_after_authority_loss()
    {
        var current = await StartArchivalReviewAsync();
        var key = Guid.NewGuid();
        using var first = await ConfirmArchivalAsync(current, key: key);
        var confirmed = await ArchivalDataAsync(first);
        using var second = await ConfirmArchivalAsync(confirmed, "FacultyConfirm", _adminId, "Admin");
        await ArchivalDataAsync(second);
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("UPDATE academic.kltn_batch SET status='Archived'");
        using var replay = await ConfirmArchivalAsync(current, key: key);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var collision = await ConfirmArchivalAsync(current, "RequestRevision", reason: "different", key: key);
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        Assert.Contains("IdempotencyConflict", await collision.Content.ReadAsStringAsync());
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE topics.topic SET advisor_id={_replacementAdvisorId} WHERE id={_topicId}");
        using var lost = await ConfirmArchivalAsync(current, key: key);
        Assert.Equal(HttpStatusCode.Forbidden, lost.StatusCode);
        await using var verify = Db<ThesisManagementDbContext>();
        Assert.Equal(2, await verify.FinalSubmissionReviews.CountAsync());
        Assert.Equal(3, await verify.FinalSubmissionIdempotencyRecords.CountAsync());
    }

    [Theory]
    [InlineData("minute")]
    [InlineData("advisor")]
    [InlineData("unlocked")]
    public async Task Archival_faculty_rejects_changed_defense_or_advisor_and_revision_recovers(string kind)
    {
        var current = await StartArchivalReviewAsync();
        using var first = await ConfirmArchivalAsync(current);
        var confirmed = await ArchivalDataAsync(first);
        await using (var db = MigrationDb())
        {
            if (kind == "advisor") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE topics.topic SET advisor_id={_replacementAdvisorId} WHERE id={_topicId}");
            else if (kind == "unlocked") await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET lock_status='Open'");
            else await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET general_feedback='changed minute'");
        }
        using var faculty = await ConfirmArchivalAsync(confirmed, "FacultyConfirm", _adminId, "Admin");
        Assert.Equal(HttpStatusCode.Conflict, faculty.StatusCode);
        using var revision = await ConfirmArchivalAsync(confirmed, "RequestRevision", _adminId, "Admin", "Sửa bản báo cáo");
        var needsRevision = await ArchivalDataAsync(revision);
        Assert.Equal(JsonValueKind.Null, needsRevision.GetProperty("advisorConfirmedBy").ValueKind);
        Assert.Equal("NeedRevision", needsRevision.GetProperty("status").GetString());
        using var scope = _app.Services.CreateScope();
        Assert.Equal(ArchiveProofOutcome.NotConfirmed, (await scope.ServiceProvider.GetRequiredService<IFinalSubmissionArchiveProofContract>().GetArchiveProofAsync(_groupId)).Outcome);
    }

    [Fact]
    public async Task Archival_sealed_proof_survives_later_live_advisor_grants_and_minute_changes_and_rejects_new_writes()
    {
        var final = await SealArchivalAsync();
        using var scope = _app.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IFinalSubmissionArchiveProofContract>();
        var before = await reader.GetArchiveProofAsync(_groupId);
        await using (var db = MigrationDb())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE topics.topic SET advisor_id={_replacementAdvisorId} WHERE id={_topicId}");
            await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET lock_status='Open',general_feedback='new context'");
        }
        await using (var db = Db<IdentityDbContext>()) await db.UserRoles.Where(r => r.UserId == _advisorId).ExecuteDeleteAsync();
        var after = await reader.GetArchiveProofAsync(_groupId);
        Assert.Equal(ArchiveProofOutcome.Succeeded, after.Outcome);
        Assert.Equal(before with { Files = after.Files }, after);
        foreach (var action in new[] { "AdvisorConfirm", "FacultyConfirm", "RequestRevision" })
        {
            using var denied = await ConfirmArchivalAsync(final, action, _adminId, "Admin", action == "RequestRevision" ? "reason" : null);
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
            Assert.Contains("FinalSubmission.Locked", await denied.Content.ReadAsStringAsync());
        }
        using var upload = await ArchiveAsync(version: final.GetProperty("submissionVersion").GetUInt32());
        Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("audit")]
    public async Task Archival_confirmation_save_failure_rolls_back_state_review_receipt_and_audit(string kind)
    {
        var current = await StartArchivalReviewAsync();
        await using (var db = MigrationDb())
        {
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION thesis.fail_review_save() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected review save failure'; END $$;");
            if (kind == "receipt") await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_review_save BEFORE INSERT ON thesis.final_submission_idempotency FOR EACH ROW EXECUTE FUNCTION thesis.fail_review_save()");
            else await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_review_save BEFORE INSERT ON thesis.audit_outbox_message FOR EACH ROW EXECUTE FUNCTION thesis.fail_review_save()");
        }
        using var response = await ConfirmArchivalAsync(current);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verify = Db<ThesisManagementDbContext>();
        var row = await verify.FinalSubmissions.SingleAsync();
        Assert.Equal(FinalSubmissionStatus.Submitted, row.Status);
        Assert.Equal(current.GetProperty("submissionVersion").GetUInt32(), row.Version);
        Assert.Empty(await verify.FinalSubmissionReviews.ToListAsync());
        Assert.Single(await verify.FinalSubmissionIdempotencyRecords.ToListAsync());
        Assert.Single(await verify.AuditOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Archival_confirmation_concurrent_same_intent_replays_once_and_different_intents_conflict()
    {
        var current = await StartArchivalReviewAsync();
        var key = Guid.NewGuid();
        var responses = await Task.WhenAll(ConfirmArchivalAsync(current, key: key), ConfirmArchivalAsync(current, key: key));
        foreach (var r in responses) await ArchivalDataAsync(r);
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
        foreach (var r in responses) r.Dispose();
        using var stale = await ConfirmArchivalAsync(current, "RequestRevision", reason: "stale");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Single(await db.FinalSubmissionReviews.ToListAsync());
        Assert.Equal(2, await db.FinalSubmissionIdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task Archival_upload_losing_to_advisor_confirmation_does_not_replace_reviewed_package()
    {
        var current = await StartArchivalReviewAsync();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _afterArchivalUpload = async key => { if (key.EndsWith(".docx", StringComparison.Ordinal)) { reached.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(30)); } };
        var upload = ArchiveAsync(version: current.GetProperty("submissionVersion").GetUInt32());
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using var confirmed = await ConfirmArchivalAsync(current);
            await ArchivalDataAsync(confirmed);
        }
        finally { release.TrySetResult(); }
        using var loser = await upload;
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Single(await db.FinalSubmissionPackages.ToListAsync());
        Assert.Single(await db.FinalSubmissionReviews.ToListAsync());
        Assert.Equal(2, (await StoredKeysAsync()).Count);
    }

    [Fact]
    public async Task Archival_replacement_winning_before_confirmation_rejects_stale_aggregate_and_package_versions()
    {
        var current = await StartArchivalReviewAsync();
        using var next = await ArchiveAsync(version: current.GetProperty("submissionVersion").GetUInt32());
        var replaced = await ArchivalDataAsync(next);
        using var stale = await ConfirmArchivalAsync(current);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var json = JsonSerializer.Serialize(new { action = "AdvisorConfirm", expectedSubmissionVersion = replaced.GetProperty("submissionVersion").GetUInt32(), expectedPackageVersion = 1 });
        using var wrongPackage = await ConfirmArchivalAsync(replaced, raw: json);
        Assert.Equal(HttpStatusCode.Conflict, wrongPackage.StatusCode);
        using var valid = await ConfirmArchivalAsync(replaced);
        await ArchivalDataAsync(valid);
    }

    [Fact]
    public async Task Archival_faculty_confirmation_and_revision_serialize_to_one_decision()
    {
        var current = await StartArchivalReviewAsync();
        using var advisor = await ConfirmArchivalAsync(current);
        var reviewed = await ArchivalDataAsync(advisor);
        var responses = await Task.WhenAll(ConfirmArchivalAsync(reviewed, "FacultyConfirm", _adminId, "Admin"),
            ConfirmArchivalAsync(reviewed, "RequestRevision", _advisorId, "Lecturer", "Sửa lại"));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var r in responses) r.Dispose();
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Equal(2, await db.FinalSubmissionReviews.CountAsync());
        Assert.Equal(3, await db.FinalSubmissionIdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task Archival_archive_proof_reports_missing_legacy_and_unconfirmed_without_partial_success_fields()
    {
        await PrepareArchivalAsync();
        using var scope = _app.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IFinalSubmissionArchiveProofContract>();
        Assert.Equal(ArchiveProofOutcome.Missing, (await reader.GetArchiveProofAsync(_groupId)).Outcome);
        var id = Guid.NewGuid();
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO thesis.final_submission(id,group_id,status,current_version_no) VALUES({id},{_groupId},'ArchiveConfirmed',1)");
        var legacy = await reader.GetArchiveProofAsync(_groupId);
        Assert.Equal(ArchiveProofOutcome.Legacy, legacy.Outcome);
        Assert.Null(legacy.PackageId);
        Assert.Null(legacy.SubmissionVersion);
        Assert.Empty(legacy.Files);
        // Legacy rows are not promoted by a read or by a confirmation.
        using var detail = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{id}", _advisorId, "Lecturer");
        var current = await ArchivalDataAsync(detail);
        using var confirm = await ConfirmArchivalAsync(current);
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        await using var verify = Db<ThesisManagementDbContext>();
        Assert.Empty(await verify.FinalSubmissionReviews.ToListAsync());
        Assert.Empty(await verify.FinalSubmissionIdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task Archival_archive_proof_share_locks_cover_owner_evidence_until_consumer_commit_without_writing()
    {
        var final = await SealArchivalAsync();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scope = _app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var coordinator = services.GetRequiredService<IFinalSubmissionWorkflowTransactionCoordinator>();
        var reader = services.GetRequiredService<IFinalSubmissionArchiveProofContract>();
        var consuming = coordinator.ExecuteWithPostSaveAsync(async ct =>
        {
            await services.GetRequiredService<IFinalSubmissionStore>().GetGroupAsync(_groupId, true, ct);
            var proof = await reader.LockArchiveProofForCalculationAsync(_groupId, ct);
            Assert.Equal(ArchiveProofOutcome.Succeeded, proof.Outcome);
            held.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            return FinalSubmissionTransactionResult<bool>.Commit(true);
        }, (value, _) => Task.FromResult(value));
        try
        {
            await held.Task.WaitAsync(TimeSpan.FromSeconds(30));
            foreach (var sql in new[] { "SELECT * FROM thesis.final_submission FOR UPDATE", "SELECT * FROM thesis.final_submission_package FOR UPDATE",
                "SELECT * FROM thesis.final_submission_package_file FOR UPDATE", "SELECT * FROM thesis.submission_file FOR UPDATE", "SELECT * FROM thesis.final_submission_review FOR UPDATE" })
            {
                await using var independent = MigrationDb();
                await using var tx = await independent.Database.BeginTransactionAsync();
                await independent.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='150ms'");
                var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => independent.Database.ExecuteSqlRawAsync(sql));
                Assert.Equal(Npgsql.PostgresErrorCodes.LockNotAvailable, failure.SqlState);
                await tx.RollbackAsync();
            }
        }
        finally { release.TrySetResult(); Assert.True(await consuming); }
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Equal(final.GetProperty("submissionVersion").GetUInt32(), (await db.FinalSubmissions.SingleAsync()).Version);
        Assert.Equal(3, await db.AuditOutboxMessages.CountAsync());
        Assert.Equal(3, await db.FinalSubmissionIdempotencyRecords.CountAsync());
        Assert.False(services.GetRequiredService<ThesisManagementDbContext>().ChangeTracker.HasChanges());
        // Locks are released after the consumer commits.
        await using var after = MigrationDb();
        await using var afterTx = await after.Database.BeginTransactionAsync();
        await after.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='500ms'");
        await after.Database.ExecuteSqlRawAsync("SELECT * FROM thesis.final_submission_review FOR UPDATE");
    }

    [Theory]
    [InlineData("Faculty")]
    [InlineData("Lecturer")]
    [InlineData("Admin")]
    public async Task Archival_revision_after_advisor_confirmation_clears_projection_and_retains_both_decisions(string role)
    {
        var current = await StartArchivalReviewAsync();
        if (role == "Faculty") await GrantArchivalRoleAsync(_replacementAdvisorId, role);
        using var advisor = await ConfirmArchivalAsync(current);
        var reviewed = await ArchivalDataAsync(advisor);
        var actor = role == "Faculty" ? _replacementAdvisorId : role == "Admin" ? _adminId : _advisorId;
        var key = Guid.NewGuid();
        using var revision = await ConfirmArchivalAsync(reviewed, "RequestRevision", actor, role, "  sửa theo biên bản  ", key);
        var needsRevision = await ArchivalDataAsync(revision);
        using var replay = await ConfirmArchivalAsync(reviewed, "RequestRevision", actor, role, "sửa theo biên bản", key);
        Assert.Equal(await revision.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("NeedRevision", needsRevision.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, needsRevision.GetProperty("advisorConfirmedAt").ValueKind);
        using var resubmit = await ArchiveAsync(version: needsRevision.GetProperty("submissionVersion").GetUInt32());
        var replacement = await ArchivalDataAsync(resubmit);
        Assert.Equal(2, replacement.GetProperty("currentVersionNo").GetInt32());
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Equal(2, await db.FinalSubmissionReviews.CountAsync());
        var review = await db.FinalSubmissionReviews.SingleAsync(r => r.Action == "RequestRevision");
        Assert.Equal(actor, review.ActorId);
        Assert.Equal(role == "Lecturer" ? "Advisor" : "Faculty", review.Stage);
        Assert.Equal(_advisorId, review.AssignedAdvisorId);
    }

    [Fact]
    public async Task Archival_confirmation_HTTP_contract_rejects_missing_duplicate_keys_and_wrong_content_before_writes()
    {
        var current = await StartArchivalReviewAsync();
        var route = $"{ArchivalRoute}/{current.GetProperty("submissionId").GetGuid()}/confirm-archive";
        foreach (var kind in new[] { "anonymous", "missing", "duplicate", "empty", "content", "oversize" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, route) { Content = new StringContent(kind == "oversize" ? new string('x', 17000) : "{}", Encoding.UTF8, kind == "content" ? "text/plain" : "application/json") };
            if (kind != "anonymous") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{_advisorId:N}.Lecturer");
            if (kind != "missing") request.Headers.Add("Idempotency-Key", (kind == "empty" ? Guid.Empty : Guid.NewGuid()).ToString());
            if (kind == "duplicate") request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var response = await _client.SendAsync(request);
            Assert.Equal(kind switch { "anonymous" => HttpStatusCode.Unauthorized, "content" => HttpStatusCode.UnsupportedMediaType, "oversize" => HttpStatusCode.RequestEntityTooLarge, _ => HttpStatusCode.BadRequest }, response.StatusCode);
        }
        using var scope = _app.Services.CreateScope();
        var proof = await scope.ServiceProvider.GetRequiredService<IFinalSubmissionArchiveProofContract>().GetArchiveProofAsync(_groupId);
        Assert.Equal(ArchiveProofOutcome.NotConfirmed, proof.Outcome);
        Assert.Empty(proof.Files);
        Assert.Null(proof.AdvisorReviewId);
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Empty(await db.FinalSubmissionReviews.ToListAsync());
    }
}
