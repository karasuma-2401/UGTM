using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    private const string EligibilityRoute = "/api/thesis/reviewer-assignment-eligibility";
    private const string CandidatesRoute = "/api/thesis/reviewer-candidates";
    private const string TasksRoute = "/api/thesis/me/reviewer-assignments";
    private readonly ReviewerReadCommands _readCommands = new();
    private readonly ReviewerDownloadStorage _reviewerStorage = new();

    private void ConfigureReviewerReads(IServiceCollection services)
    {
        services.RemoveAll<IThesisSubmissionObjectStorage>();
        services.AddSingleton<IThesisSubmissionObjectStorage>(_reviewerStorage);
        services.ConfigureDbContext<DefenseCouncilDbContext>(options => options.AddInterceptors(_readCommands));
        services.ConfigureDbContext<DefenseCouncilDbContext>(options => options.AddInterceptors(_cp3Queries));
        services.ConfigureDbContext<ThesisManagementDbContext>(options => options.AddInterceptors(_readCommands));
        services.ConfigureDbContext<TopicCatalogDbContext>(options => options.AddInterceptors(_readCommands));
        services.ConfigureDbContext<IdentityDbContext>(options => options.AddInterceptors(_readCommands));
        services.ConfigureDbContext<AcademicPeriodsDbContext>(options => options.AddInterceptors(_readCommands));
    }

    private async Task<HttpResponseMessage> ReadAsync(string route, Guid? actor = null, string role = "Admin", string? permission = null)
    {
        var client = await HttpAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actor ?? _actor:N}|{role}|{permission}");
        return await client.SendAsync(request);
    }

    private async Task<JsonDocument> ReadPageAsync(string route, Guid? actor = null, string role = "Admin", string? permission = null)
    {
        using var response = await ReadAsync(route, actor, role, permission);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, content);
        return JsonDocument.Parse(content);
    }
    private static JsonElement Page(JsonDocument json) => json.RootElement.GetProperty("data");
    private static JsonElement[] Items(JsonDocument json) => Page(json).GetProperty("items").EnumerateArray().ToArray();

    [Theory]
    [InlineData(false, "Admin", false, "ReviewerAssignment.AuthenticationRequired")]
    [InlineData(true, "Lecturer", true, "ReviewerAssignment.AccessDenied")]
    [InlineData(true, "Faculty", false, "ReviewerAssignment.AccessDenied")]
    public async Task Reviewer_reads_handlers_authorize_management_before_validation_or_database_lookup(
        bool authenticated, string role, bool read, string code)
    {
        var accessor = _app.Services.GetRequiredService<IHttpContextAccessor>();
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, _actor.ToString()), new(ClaimTypes.Role, role)];
        if (read) claims.Add(new("permission", PermissionCodes.ThesisReviewerAssignmentsRead));
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "ReadHandlerTest" : null)) };
        try
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ReviewerAssignmentReadHandler>();
            _readCommands.Count = 0;
            Assert.Equal(code, (await handler.ListAsync(new(GroupId: Guid.Empty))).Error.Code);
            Assert.Equal(code, (await handler.EligibilityAsync(new(Guid.Empty))).Error.Code);
            Assert.Equal(code, (await handler.CandidatesAsync(new(Guid.Empty))).Error.Code);
            Assert.Equal(0, _readCommands.Count);
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task Reviewer_reads_legacy_stays_visible_but_never_grants_file_access_and_blocks_default_eligibility()
    {
        var id = Guid.NewGuid();
        await using (var db = Db<DefenseCouncilDbContext>())
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO councils.reviewer_assignment (id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active)
                VALUES ({id}, {_group}, {_reviewer}, {_actor}, {Now.AddDays(-1)}, 2, true)
                """);
        using (var list = await ReadPageAsync(CreationRoute))
        {
            var item = Assert.Single(Items(list));
            Assert.True(item.GetProperty("isLegacy").GetBoolean()); Assert.True(item.GetProperty("canCancel").GetBoolean());
            Assert.Equal(2, item.GetProperty("assignmentOrder").GetInt32());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("advisorEvaluationId").ValueKind);
        }
        using (var tasks = await ReadPageAsync(TasksRoute, _reviewer, "Lecturer"))
        {
            var item = Assert.Single(Items(tasks));
            Assert.Equal("LegacyRecordNeedsResolution", item.GetProperty("accessBlockCode").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("reportFile").ValueKind);
        }
        using (var eligibility = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}")) Assert.Empty(Items(eligibility));
        using (var included = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}&includeAssigned=true"))
        {
            var item = Assert.Single(Items(included));
            Assert.Equal(id, item.GetProperty("activeAssignmentId").GetGuid()); Assert.False(item.GetProperty("canAssign").GetBoolean());
        }
        using var download = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, download.StatusCode);
        Assert.Empty(_reviewerStorage.Downloads);
        await AssertCountsAsync(1, 0, 0);
    }

    [Theory]
    [InlineData("Admin", null, true)]
    [InlineData("Faculty", PermissionCodes.ThesisReviewerAssignmentsRead, false)]
    [InlineData("AcademicAffairs", PermissionCodes.ThesisReviewerAssignmentsRead, false)]
    [InlineData("Faculty", PermissionCodes.ThesisReviewerAssignmentsManage, true)]
    [InlineData("AcademicAffairs", PermissionCodes.ThesisReviewerAssignmentsManage, true)]
    public async Task Reviewer_reads_management_roles_see_flattened_dto_and_accurate_action_flags(string role, string? permission, bool canManage)
    {
        var original = await CreateForCancellationAsync();
        using var list = await ReadPageAsync(CreationRoute, role: role, permission: permission);
        var item = Assert.Single(Items(list));
        Assert.Equal(original.Id, item.GetProperty("id").GetGuid());
        Assert.Equal(_file, item.GetProperty("submissionFileId").GetGuid());
        Assert.Equal(canManage, item.GetProperty("canCancel").GetBoolean());
        Assert.False(item.GetProperty("hasEvaluation").GetBoolean());
        Assert.False(item.TryGetProperty("assignment", out _));
        using var assigned = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}&includeAssigned=true", role: role, permission: permission);
        var eligible = Assert.Single(Items(assigned));
        Assert.True(eligible.GetProperty("hasActiveAssignment").GetBoolean());
        Assert.Equal(original.Id, eligible.GetProperty("activeAssignmentId").GetGuid());
        Assert.False(eligible.GetProperty("canAssign").GetBoolean());
        using var cancelled = await DeleteAsync(original.Id, original.Version);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var unassigned = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}", role: role, permission: permission);
        Assert.Equal(canManage, Assert.Single(Items(unassigned)).GetProperty("canAssign").GetBoolean());
    }

    [Theory]
    [InlineData("Lecturer", PermissionCodes.ThesisReviewerAssignmentsManage)]
    [InlineData("Student", PermissionCodes.ThesisReviewerAssignmentsRead)]
    [InlineData("CouncilMember", PermissionCodes.ThesisReviewerAssignmentsRead)]
    [InlineData("Faculty", null)]
    [InlineData("AcademicAffairs", null)]
    public async Task Reviewer_reads_require_management_role_and_read_or_manage_permission_before_query_parsing(string role, string? permission)
    {
        foreach (var route in new[] { CreationRoute, EligibilityRoute, CandidatesRoute })
        {
            using var response = await ReadAsync(route + "?batchId=bad&pageSize=overflow", role: role, permission: permission);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await AssertCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task Reviewer_reads_require_authentication_and_validate_query_shapes_duplicates_limits_and_forbidden_me_filters()
    {
        var client = await HttpAsync();
        foreach (var route in new[] { CreationRoute, EligibilityRoute, CandidatesRoute, TasksRoute })
        {
            using var anonymous = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }
        foreach (var query in new[] { "batchId=bad", $"groupId={Guid.Empty}", "isActive=yes", "includeHistory=1",
            "pageNumber=1.5", "pageSize=2147483648", "pageNumber=1&pageNumber=2", "reviewerId=", "keyword=" + new string('a', 201) })
        {
            using var response = await ReadAsync(CreationRoute + "?" + query);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        foreach (var route in new[] { EligibilityRoute, CandidatesRoute })
        {
            using var missing = await ReadAsync(route);
            await AssertErrorAsync(missing, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        using (var invalidBool = await ReadAsync($"{EligibilityRoute}?batchId={_batch}&includeAssigned=no"))
            await AssertErrorAsync(invalidBool, HttpStatusCode.BadRequest, "InvalidRequest");
        foreach (var query in new[] { $"reviewerId={_otherReviewer}", "includeHistory=false", "isActive=true", "REVIEWERID=bad", "pageSize=1&pageSize=2" })
        {
            using var response = await ReadAsync(TasksRoute + "?" + query, _reviewer, "Lecturer");
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        using var normalized = await ReadPageAsync(CreationRoute + "?pageNumber=-2&pageSize=1000");
        Assert.Equal(1, Page(normalized).GetProperty("pageNumber").GetInt32());
        Assert.Equal(100, Page(normalized).GetProperty("pageSize").GetInt32());
        await AssertCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task Reviewer_reads_history_filters_paging_and_keyword_search_include_inactive_names_but_not_private_comments_or_reasons()
    {
        var original = await CreateForCancellationAsync();
        using var cancel = await DeleteAsync(original.Id, original.Version, "NỘI DUNG CHỈ LƯU TRÊN PHÂN CÔNG");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var replacement = await CreateForCancellationAsync();
        await using (var writer = Db<UGTMDbContext>())
        {
            await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"user\" SET full_name = '50%_ Lecturer', status = 'Suspended' WHERE id = {_reviewer}");
            await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status = 'Closed' WHERE id = {_batch}");
        }
        using (var current = await ReadPageAsync(CreationRoute)) Assert.Equal(replacement.Id, Assert.Single(Items(current)).GetProperty("id").GetGuid());
        using (var history = await ReadPageAsync($"{CreationRoute}?batchId={_batch}&groupId={_group}&reviewerId={_reviewer}&includeHistory=true&pageSize=1"))
        {
            Assert.Equal(2, Page(history).GetProperty("totalCount").GetInt32());
            Assert.True(Page(history).GetProperty("hasNextPage").GetBoolean());
            Assert.Equal(replacement.Id, Assert.Single(Items(history)).GetProperty("id").GetGuid());
        }
        using (var second = await ReadPageAsync(CreationRoute + "?includeHistory=true&pageSize=1&pageNumber=2"))
        {
            Assert.True(Page(second).GetProperty("hasPreviousPage").GetBoolean());
            Assert.Equal(original.Id, Assert.Single(Items(second)).GetProperty("id").GetGuid());
        }
        using (var precedence = await ReadPageAsync(CreationRoute + "?includeHistory=true&isActive=false"))
            Assert.Equal(original.Id, Assert.Single(Items(precedence)).GetProperty("id").GetGuid());
        using (var literal = await ReadPageAsync(CreationRoute + "?includeHistory=true&keyword=" + Uri.EscapeDataString("%_")))
        {
            Assert.Equal(2, Page(literal).GetProperty("totalCount").GetInt32());
            Assert.All(Items(literal), item => Assert.Equal("50%_ Lecturer", item.GetProperty("reviewerFullName").GetString()));
        }
        foreach (var keyword in new[] { "Nhận xét bí mật", "NỘI DUNG CHỈ LƯU TRÊN PHÂN CÔNG" })
        {
            using var secret = await ReadPageAsync(CreationRoute + "?includeHistory=true&keyword=" + Uri.EscapeDataString(keyword));
            Assert.Empty(Items(secret));
        }
        using (var far = await ReadPageAsync(CreationRoute + "?includeHistory=true&pageNumber=2147483647&pageSize=100"))
        { Assert.Empty(Items(far)); Assert.Equal(2, Page(far).GetProperty("totalCount").GetInt32()); }
        using (var unknown = await ReadPageAsync($"{CreationRoute}?batchId={Guid.NewGuid()}")) Assert.Empty(Items(unknown));
        await AssertCountsAsync(2, 3, 3);
    }

    [Fact]
    public async Task Reviewer_reads_has_evaluation_blocks_can_cancel_without_exposing_the_sheet()
    {
        var original = await CreateForCancellationAsync();
        await using var db = Db<DefenseCouncilDbContext>();
        var sheet = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), original.Id, 0m, "PHIẾU RIÊNG", DefenseApprovalStatus.Rejected, _reviewer, Now);
        db.ReviewerEvaluations.Add(sheet);
        db.Entry(sheet).Property(value => value.IsIgnoredInCalculation).CurrentValue = true;
        await db.SaveChangesAsync();
        using var list = await ReadPageAsync(CreationRoute);
        var item = Assert.Single(Items(list));
        Assert.True(item.GetProperty("hasEvaluation").GetBoolean());
        Assert.False(item.GetProperty("canCancel").GetBoolean());
        Assert.DoesNotContain("PHIẾU RIÊNG", list.RootElement.GetRawText());
        Assert.False(item.TryGetProperty("score", out _));
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Reviewer_reads_eligibility_filters_before_count_and_page_and_reports_assignments_without_advisor_scores()
    {
        await SeedReadGroupAsync("Invalid old approval", -60, ready: false);
        var valid = await SeedReadGroupAsync("Đề tài thứ hai", 10);
        await SeedReadGroupAsync("Not approved", -30, approved: false);
        using (var page = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}&pageSize=1"))
        {
            Assert.Equal(2, Page(page).GetProperty("totalCount").GetInt32());
            Assert.Equal(_group, Assert.Single(Items(page)).GetProperty("groupId").GetGuid());
            Assert.False(Items(page)[0].TryGetProperty("score", out _));
            Assert.False(Items(page)[0].TryGetProperty("comment", out _));
        }
        var assigned = await CreateForCancellationAsync();
        using (var unassigned = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}"))
            Assert.Equal(valid.GroupId, Assert.Single(Items(unassigned)).GetProperty("groupId").GetGuid());
        using (var all = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}&includeAssigned=true"))
        { Assert.Equal(2, Items(all).Length); Assert.Equal(assigned.Id, Items(all)[0].GetProperty("activeAssignmentId").GetGuid()); }
        using (var title = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}&keyword=" + Uri.EscapeDataString("THỨ HAI")))
            Assert.Equal(valid.GroupId, Assert.Single(Items(title)).GetProperty("groupId").GetGuid());
        using (var absent = await ReadPageAsync($"{EligibilityRoute}?batchId={Guid.NewGuid()}")) Assert.Empty(Items(absent));
        await using (var writer = Db<UGTMDbContext>()) await writer.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
        using (var closed = await ReadPageAsync($"{EligibilityRoute}?batchId={_batch}&includeAssigned=true")) Assert.Empty(Items(closed));
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Reviewer_reads_candidates_allow_preparation_before_approval_filter_current_advisor_and_escape_search()
    {
        await using (var writer = Db<UGTMDbContext>())
        {
            await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.advisor_evaluation SET allows_reviewer_assignment = NULL, decision_by = NULL, decision_at = NULL WHERE group_id = {_group}");
            await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"user\" SET full_name = '50%_ Lecturer', user_code = 'ONLY_CODE' WHERE id = {_otherReviewer}");
        }
        using (var list = await ReadPageAsync($"{CandidatesRoute}?groupId={_group}&pageSize=1"))
        {
            Assert.Equal(2, Page(list).GetProperty("totalCount").GetInt32());
            Assert.DoesNotContain(Items(list), value => value.GetProperty("userId").GetGuid() == _advisor);
            Assert.Equal(new[] { "fullName", "userCode", "userId" }, Items(list)[0].EnumerateObject().Select(value => value.Name).OrderBy(value => value));
        }
        foreach (var keyword in new[] { "%_", "ONLY_CODE" })
        {
            using var searched = await ReadPageAsync($"{CandidatesRoute}?groupId={_group}&keyword=" + Uri.EscapeDataString(keyword));
            Assert.Equal(_otherReviewer, Assert.Single(Items(searched)).GetProperty("userId").GetGuid());
        }
        await using (var writer = Db<UGTMDbContext>()) await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"user\" SET status = 'Suspended' WHERE id = {_otherReviewer}");
        using (var activeOnly = await ReadPageAsync($"{CandidatesRoute}?groupId={_group}"))
            Assert.Equal(_reviewer, Assert.Single(Items(activeOnly)).GetProperty("userId").GetGuid());
        using (var missing = await ReadAsync($"{CandidatesRoute}?groupId={Guid.NewGuid()}"))
            await AssertErrorAsync(missing, HttpStatusCode.NotFound, "GroupNotFound");
        await AssertCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task Reviewer_reads_batch_lookups_have_bounded_query_count_and_do_not_write_audit_or_versions()
    {
        await CreateForCancellationAsync();
        for (var index = 0; index < 12; index++)
        {
            var group = await SeedReadGroupAsync($"Extra {index}", index + 1);
            await using var db = Db<DefenseCouncilDbContext>();
            db.ReviewerAssignments.Add(ReviewerAssignment.Create(Guid.NewGuid(), group.GroupId, _reviewer, _advisor, group.EvaluationId,
                group.ReportId, 1, group.FileId, Checksum, _actor, Now));
            await db.SaveChangesAsync();
        }
        await using var snapshot = Db<DefenseCouncilDbContext>();
        var versions = await snapshot.ReviewerAssignments.AsNoTracking().ToDictionaryAsync(value => value.Id, value => value.Version);
        foreach (var route in new[] { CreationRoute, TasksRoute })
        {
            _readCommands.Count = 0;
            using var one = await ReadPageAsync(route + "?pageSize=1", route == TasksRoute ? _reviewer : _actor, route == TasksRoute ? "Lecturer" : "Admin");
            var oneCount = _readCommands.Count;
            _readCommands.Count = 0;
            using var many = await ReadPageAsync(route + "?pageSize=100", route == TasksRoute ? _reviewer : _actor, route == TasksRoute ? "Lecturer" : "Admin");
            Assert.Equal(13, Items(many).Length);
            Assert.Equal(oneCount, _readCommands.Count);
            Assert.InRange(_readCommands.Count, 1, 12);
        }
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(versions.OrderBy(value => value.Key), (await verify.ReviewerAssignments.AsNoTracking().ToDictionaryAsync(value => value.Id, value => value.Version)).OrderBy(value => value.Key));
        await AssertCountsAsync(13, 1, 1);
        Assert.Empty(_reviewerStorage.Downloads);
    }

    private async Task<ReadGroup> SeedReadGroupAsync(string title, int minutes, bool ready = true, bool approved = true)
    {
        var student = Guid.NewGuid(); var topicId = Guid.NewGuid(); var groupId = Guid.NewGuid();
        var reportId = Guid.NewGuid(); var fileId = Guid.NewGuid(); var at = Now.AddMinutes(minutes);
        await using (var identity = Db<IdentityDbContext>())
        {
            identity.Users.Add(User.Create(student, $"{student:N}@test.example", $"{student:N}@TEST.EXAMPLE", "Student", null, null, null, AccountStatus.Active, at));
            await identity.SaveChangesAsync();
        }
        await using (var topics = Db<TopicCatalogDbContext>())
        {
            topics.Topics.Add(new Topic
            {
                Id = topicId,
                BatchId = _batch,
                AdvisorId = _advisor,
                TitleVi = title,
                TitleEn = "English " + title,
                Status = TopicStatus.Published,
                StudentNumber = 1,
                CreatedAt = at
            });
            await topics.SaveChangesAsync();
        }
        var group = new StudentGroup(groupId, topicId, at); group.AddConfirmedMember(student, _batch, at);
        Assert.True(group.TrySubmitSyllabus()); Assert.True(group.TryApproveSyllabus());
        Assert.True(group.TrySubmitMidtermReport()); Assert.True(group.TryApproveMidtermReport()); Assert.True(group.TrySubmitFinalReport());
        if (ready) Assert.True(group.TryMakeReadyForReviewerAssignment());
        var report = FinalReport.CreateSubmitted(reportId, groupId, FinalReportVersionType.InitialFinal, at); Assert.True(report.TryBeginEvaluation());
        await using (var thesis = Db<ThesisManagementDbContext>())
        {
            thesis.StudentGroups.Add(group); thesis.FinalReports.Add(report);
            thesis.SubmissionFiles.Add(new SubmissionFile
            {
                Id = fileId,
                EntityId = reportId,
                EntityType = SubmissionEntityType.FinalReport,
                VersionNo = 1,
                FileName = "other.pdf",
                ObjectKey = $"cp5/{fileId:N}",
                ContentType = "application/pdf",
                FileSize = 123,
                FileExtension = ".pdf",
                ChecksumSha256 = Checksum,
                UploadedBy = student,
                UploadedAt = at
            });
            await thesis.SaveChangesAsync();
        }
        var evaluation = AdvisorEvaluation.Create(Guid.NewGuid(), groupId, reportId, 1, fileId, Checksum, 8m, "PRIVATE ADVISOR COMMENT", _advisor, at);
        if (approved) Assert.True(evaluation.TryDecide(true, null, _advisor, at));
        await using (var defense = Db<DefenseCouncilDbContext>()) { defense.AdvisorEvaluations.Add(evaluation); await defense.SaveChangesAsync(); }
        return new(groupId, topicId, reportId, fileId, evaluation.Id);
    }
    private sealed record ReadGroup(Guid GroupId, Guid TopicId, Guid ReportId, Guid FileId, Guid EvaluationId);

    private sealed class ReviewerReadCommands : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Count); return ValueTask.FromResult(result); }
    }
    private sealed class ReviewerDownloadStorage : IThesisSubmissionObjectStorage
    {
        public List<(string ObjectKey, TimeSpan Lifetime)> Downloads { get; } = [];
        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default)
        { Downloads.Add((objectKey, validFor)); return Task.FromResult(new Uri($"https://download.test/{Downloads.Count}")); }
        public Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
