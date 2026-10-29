using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Infrastructure;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportReviewQueue;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Contracts.Topics;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Infrastructure;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using IdentityUser = UGTM.Modules.Identity.Domain.Entities.User;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class MidtermReportReviewQueueHttpIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string Route = "/api/thesis/midterm-report/submissions";
    private static readonly DateTime Now = new(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);
    private readonly Guid _advisorId = Guid.NewGuid();
    private readonly Guid _batchA = Guid.NewGuid();
    private readonly Guid _batchB = Guid.NewGuid();
    private readonly List<SeededSubmission> _rows = [];
    private readonly CommandCapture _commands = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await using (var db = CreateMigrationDbContext())
        {
            await ResetSchemasAsync(db);
            await db.Database.MigrateAsync();
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = fixture.ConnectionString
        });
        new AcademicPeriodsModule().RegisterModule(builder.Services, builder.Configuration);
        new TopicCatalogModule().RegisterModule(builder.Services, builder.Configuration);
        new ThesisManagementModule().RegisterModule(builder.Services, builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddDbContext<IdentityDbContext>(options => DatabaseOptions.Configure(options, fixture.ConnectionString));
        builder.Services.ConfigureDbContext<IdentityDbContext>(options => options.AddInterceptors(_commands));
        builder.Services.ConfigureDbContext<TopicCatalogDbContext>(options => options.AddInterceptors(_commands));
        builder.Services.ConfigureDbContext<AcademicPeriodsDbContext>(options => options.AddInterceptors(_commands));
        builder.Services.ConfigureDbContext<ThesisManagementDbContext>(options => options.AddInterceptors(_commands));
        builder.Services.AddScoped<IStudentInfoReader, StudentInfoReader>();
        builder.Services.AddScoped<IStudentSearchReader, StudentSearchReader>();
        builder.Services.AddScoped<IAdvisorNameReader, AdvisorNameReader>();
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddAuthentication(TestAuthentication.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        new ThesisManagementModule().MapEndpoints(_app);
        await SeedAsync();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
        Authenticate(Guid.NewGuid(), "Faculty", PermissionCodes.ThesisReviewGatesReview);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
    }

    [Fact]
    public async Task Midterm_queue_is_separate_from_syllabus_even_when_report_ids_and_groups_overlap()
    {
        var midterm = await GetDataAsync();
        Assert.Equal(6, midterm.GetProperty("totalCount").GetInt32());
        Assert.Contains(Items(midterm), item => item.GetProperty("currentStatus").GetString() == "Submitted");
        using var response = await _client.GetAsync("/api/thesis/syllabus/submissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var syllabus = json.RootElement.GetProperty("data");
        Assert.Equal(8, syllabus.GetProperty("totalCount").GetInt32());
        Assert.All(Items(syllabus), item =>
        {
            Assert.Equal("Approved", item.GetProperty("currentStatus").GetString());
            Assert.False(item.TryGetProperty("midtermReportId", out _));
            Assert.Contains("syllabus", item.GetProperty("latestFile").GetProperty("fileName").GetString()!);
        });
        Assert.All(Items(midterm), item => Assert.False(item.TryGetProperty("syllabusId", out _)));
        var filtered = await GetDataAsync("?status=Submitted");
        Assert.Equal(2, filtered.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Queue_reflects_an_existing_midterm_review_without_changing_the_current_file()
    {
        Authenticate(_advisorId, "Faculty", PermissionCodes.ThesisReviewGatesReview);
        var before = await GetDataAsync("?status=Submitted");
        Assert.Contains(Items(before), item => item.GetProperty("midtermReportId").GetGuid() == _rows[0].MidtermReportId);
        using var review = await _client.PostAsJsonAsync($"/api/thesis/groups/{_rows[0].GroupId}/midterm-report/review",
            new { decision = "NeedRevision", feedback = "Cần bổ sung kết quả giữa kỳ." });
        Assert.True(review.StatusCode == HttpStatusCode.OK, await review.Content.ReadAsStringAsync());
        var after = await GetDataAsync("?status=NeedRevision");
        Assert.Equal(2, after.GetProperty("totalCount").GetInt32());
        var updated = Items(after).Single(item => item.GetProperty("midtermReportId").GetGuid() == _rows[0].MidtermReportId);
        Assert.Equal("NeedRevision", updated.GetProperty("currentStatus").GetString());
        Assert.Equal(1, updated.GetProperty("currentVersionNo").GetInt32());
        Assert.Equal(_rows[0].FileId, updated.GetProperty("latestFile").GetProperty("id").GetGuid());
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        var report = await db.MidtermReports.SingleAsync(value => value.Id == _rows[0].MidtermReportId);
        Assert.Equal(MidtermReportStatus.NeedRevision, report.Status);
        Assert.Equal(1, await db.AuditOutboxMessages.CountAsync());
        Assert.Equal(SyllabusStatus.Approved, (await db.Syllabuses.SingleAsync(value => value.Id == report.Id)).Status);
    }

    [Fact]
    public async Task Queue_returns_backend_contract_current_versions_and_no_duplicate_or_unsubmitted_rows()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        var versions = await db.StudentGroups.AsNoTracking().ToDictionaryAsync(group => group.Id, group => group.Version);

        var data = await GetDataAsync();
        Assert.Equal(6, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, data.GetProperty("pageNumber").GetInt32());
        Assert.Equal(20, data.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, data.GetProperty("totalPages").GetInt32());
        Assert.False(data.GetProperty("hasNextPage").GetBoolean());
        Assert.False(data.GetProperty("hasPreviousPage").GetBoolean());
        var items = Items(data);
        Assert.Equal(6, items.Length);
        Assert.Equal(6, items.Select(item => item.GetProperty("midtermReportId").GetGuid()).Distinct().Count());
        Assert.DoesNotContain(items, item => item.GetProperty("groupId").GetGuid() == _rows[6].GroupId);
        Assert.DoesNotContain(items, item => item.GetProperty("groupId").GetGuid() == _rows[7].GroupId);
        var first = items.Single(item => item.GetProperty("midtermReportId").GetGuid() == _rows[0].MidtermReportId);
        Assert.Equal(_rows[0].TopicId, first.GetProperty("topicId").GetGuid());
        Assert.Equal("Quản lý khóa luận UGTM", first.GetProperty("topicTitleVi").GetString());
        Assert.Equal("Thesis Portal", first.GetProperty("topicTitleEn").GetString());
        Assert.Equal(_batchA, first.GetProperty("batchId").GetGuid());
        Assert.Equal("Đợt A", first.GetProperty("batchName").GetString());
        Assert.Equal(_advisorId, first.GetProperty("advisor").GetProperty("advisorId").GetGuid());
        Assert.Equal("GV hướng dẫn", first.GetProperty("advisor").GetProperty("fullName").GetString());
        Assert.Equal("Nguyễn Minh Đức", first.GetProperty("students")[0].GetProperty("fullName").GetString());
        Assert.Equal("24521603", first.GetProperty("students")[0].GetProperty("userCode").GetString());
        Assert.Equal(_rows[0].Students[0], first.GetProperty("students")[0].GetProperty("studentId").GetGuid());
        var revised = items.Single(item => item.GetProperty("midtermReportId").GetGuid() == _rows[2].MidtermReportId);
        Assert.Equal(2, revised.GetProperty("currentVersionNo").GetInt32());
        Assert.Equal("Approved", revised.GetProperty("currentStatus").GetString());
        var file = revised.GetProperty("latestFile");
        Assert.Equal(_rows[2].FileId, file.GetProperty("id").GetGuid());
        Assert.Equal("current-v2.pdf", file.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", file.GetProperty("contentType").GetString());
        Assert.Equal(100, file.GetProperty("fileSize").GetInt64());
        Assert.Equal(".pdf", file.GetProperty("fileExtension").GetString());
        Assert.Equal(new string('a', 64), file.GetProperty("checksumSha256").GetString());
        Assert.Equal(Now.AddMinutes(20), file.GetProperty("uploadedAtUtc").GetDateTime());
        Assert.Equal(JsonValueKind.Null, items[^1].GetProperty("latestFile").ValueKind);
        Assert.Equal(_rows[5].MidtermReportId, items[^1].GetProperty("midtermReportId").GetGuid());
        var body = data.GetRawText();
        foreach (var field in new[] { "groupCode", "topicCode", "academicTitle", "isLate", "objectKey", "downloadUrl", "confirmationToken", "email" })
            Assert.DoesNotContain($"\"{field}\"", body);
        Assert.Empty(await db.AuditOutboxMessages.ToListAsync());
        var after = await db.StudentGroups.AsNoTracking().ToDictionaryAsync(group => group.Id, group => group.Version);
        Assert.All(versions, pair => Assert.Equal(pair.Value, after[pair.Key]));
    }

    [Fact]
    public async Task Keyword_search_uses_real_titles_names_and_codes_before_counting_and_paging()
    {
        (string Term, int[] Indices)[] cases =
        [
            ("  uGtM ", [0]), (" tHESIS pOrTaL ", [0]), ("khóa luận", [0]),
            ("Nguyễn Minh Đức", [0]), ("24521603", [0]), ("MATCH001", [1]), ("Nguyễn Chung", [1]),
            ("Shared project", [3, 4]), ("%", [3]), ("_", [3]), ("khoa luan", []), ("no-match", [])
        ];
        foreach (var (term, indices) in cases)
        {
            var data = await GetDataAsync($"?keyword={Uri.EscapeDataString(term)}&pageSize=1");
            Assert.Equal(indices.Length, data.GetProperty("totalCount").GetInt32());
            Assert.All(Items(data), item => Assert.Contains(item.GetProperty("midtermReportId").GetGuid(),
                indices.Select(index => _rows[index].MidtermReportId)));
        }
        var blank = await GetDataAsync("?keyword=%20%20");
        Assert.Equal(6, blank.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Batch_and_status_filters_intersect_keyword_matches_and_include_closed_batches()
    {
        foreach (var status in new[] { "Submitted", "NeedRevision", "Approved", "Rejected" })
        {
            var data = await GetDataAsync($"?status={Uri.EscapeDataString(" " + status.ToLowerInvariant() + " ")}");
            Assert.Equal(_rows.Take(6).Count(row => row.Status == status), data.GetProperty("totalCount").GetInt32());
            Assert.All(Items(data), item => Assert.Equal(status, item.GetProperty("currentStatus").GetString()));
        }
        var a = await GetDataAsync($"?batchId={_batchA}");
        var b = await GetDataAsync($"?batchId={_batchB}");
        Assert.Equal(4, a.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, b.GetProperty("totalCount").GetInt32());
        Assert.All(Items(b), item => Assert.Equal(_batchB, item.GetProperty("batchId").GetGuid()));
        var combined = await GetDataAsync($"?batchId={_batchA}&keyword=Shared%20project&status=Rejected");
        Assert.Equal(1, combined.GetProperty("totalCount").GetInt32());
        Assert.Equal(_rows[3].MidtermReportId, Assert.Single(Items(combined)).GetProperty("midtermReportId").GetGuid());
        var studentMatch = await GetDataAsync($"?batchId={_batchA}&keyword=MATCH001&status=NeedRevision");
        Assert.Equal(1, studentMatch.GetProperty("totalCount").GetInt32());
        var mismatch = await GetDataAsync($"?batchId={_batchB}&keyword=MATCH001");
        Assert.Empty(Items(mismatch));
        var unknown = await GetDataAsync($"?batchId={Guid.NewGuid()}");
        Assert.Equal(0, unknown.GetProperty("totalCount").GetInt32());
        Assert.Empty(Items(unknown));
    }

    [Fact]
    public async Task Pagination_is_stable_normalized_and_handles_offsets_beyond_int_max_value()
    {
        var all = Items(await GetDataAsync());
        var expected = all.Select(item => item.GetProperty("midtermReportId").GetGuid()).ToArray();
        Assert.Equal(new[] { _rows[4].MidtermReportId, _rows[1].MidtermReportId, _rows[3].MidtermReportId,
            _rows[2].MidtermReportId, _rows[0].MidtermReportId, _rows[5].MidtermReportId }, expected);
        var paged = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var data = await GetDataAsync($"?pageNumber={page}&pageSize=2");
            Assert.Equal(6, data.GetProperty("totalCount").GetInt32());
            Assert.Equal(3, data.GetProperty("totalPages").GetInt32());
            Assert.Equal(page < 3, data.GetProperty("hasNextPage").GetBoolean());
            Assert.Equal(page > 1, data.GetProperty("hasPreviousPage").GetBoolean());
            paged.AddRange(Items(data).Select(item => item.GetProperty("midtermReportId").GetGuid()));
        }
        Assert.Equal(expected, paged);
        var normalized = await GetDataAsync("?pageNumber=-20&pageSize=0");
        Assert.Equal(1, normalized.GetProperty("pageNumber").GetInt32());
        Assert.Equal(1, normalized.GetProperty("pageSize").GetInt32());
        Assert.Single(Items(normalized));
        var clamped = await GetDataAsync($"?pageSize={int.MaxValue}");
        Assert.Equal(100, clamped.GetProperty("pageSize").GetInt32());
        foreach (var page in new[] { 4, int.MaxValue })
        {
            var data = await GetDataAsync($"?pageNumber={page}&pageSize=100");
            Assert.Empty(Items(data));
            Assert.Equal(6, data.GetProperty("totalCount").GetInt32());
            Assert.Equal(page, data.GetProperty("pageNumber").GetInt32());
        }
    }

    [Fact]
    public async Task Invalid_filters_and_query_parameter_types_are_rejected()
    {
        foreach (var status in new[] { "Draft", "Invalid", "1", "99", "Submitted,Approved" })
        {
            using var response = await _client.GetAsync(Route + "?status=" + Uri.EscapeDataString(status));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(MidtermReportReviewQueueErrors.InvalidRequest.Code, await response.Content.ReadAsStringAsync());
        }
        foreach (var query in new[] { $"?batchId={Guid.Empty}", "?batchId=bad-guid", "?pageNumber=abc", "?pageSize=2147483648" })
        {
            using var response = await _client.GetAsync(Route + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Queue_uses_existing_permission_policy_and_application_authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        (Guid UserId, string Role, string? Permission, HttpStatusCode Expected)[] actors =
        [
            (_rows[0].Students[0], "Student", null, HttpStatusCode.Forbidden),
            (_advisorId, "Lecturer", null, HttpStatusCode.Forbidden),
            (Guid.NewGuid(), "Faculty", null, HttpStatusCode.Forbidden),
            (Guid.NewGuid(), "Faculty", PermissionCodes.ThesisReviewGatesReview, HttpStatusCode.OK),
            (Guid.NewGuid(), "Student", PermissionCodes.ThesisReviewGatesReview, HttpStatusCode.OK),
            (Guid.NewGuid(), "Admin", null, HttpStatusCode.OK),
            (Guid.Empty, "Faculty", PermissionCodes.ThesisReviewGatesReview, HttpStatusCode.Unauthorized)
        ];
        foreach (var actor in actors)
        {
            Authenticate(actor.UserId, actor.Role, actor.Permission);
            using var response = await _client.GetAsync(Route);
            Assert.Equal(actor.Expected, response.StatusCode);
            if (actor.Expected != HttpStatusCode.OK)
                Assert.DoesNotContain("midtermReportId", await response.Content.ReadAsStringAsync());
        }
        // Call the application handler directly to verify it also rejects missing permissions.
        using var scope = _app.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        accessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Faculty")], TestAuthentication.SchemeName))
        };
        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<GetMidtermReportReviewQueueHandler>()
                .HandleAsync(new GetMidtermReportReviewQueueQuery());
            Assert.False(result.IsSuccess);
            Assert.Equal(MidtermReportReviewQueueErrors.AccessDenied, result.Error);
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task Missing_display_data_is_nullable_without_dropping_submissions_or_students()
    {
        await ExecuteCorruptionAsync(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity.\"user\" WHERE id = {_advisorId} OR id = {_rows[0].Students[1]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM academic.kltn_batch WHERE id = {_batchA}");
        });
        var data = await GetDataAsync();
        Assert.Equal(6, data.GetProperty("totalCount").GetInt32());
        var row = Items(data).Single(item => item.GetProperty("midtermReportId").GetGuid() == _rows[0].MidtermReportId);
        Assert.Equal(_batchA, row.GetProperty("batchId").GetGuid());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("batchName").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("advisor").GetProperty("fullName").ValueKind);
        var students = row.GetProperty("students").EnumerateArray().ToArray();
        Assert.Equal(2, students.Length);
        Assert.Equal(_rows[0].Students[1], students[1].GetProperty("studentId").GetGuid());
        Assert.Equal(JsonValueKind.Null, students[1].GetProperty("fullName").ValueKind);
        Assert.Equal(JsonValueKind.Null, students[1].GetProperty("userCode").ValueKind);
    }

    [Theory]
    [InlineData("group")]
    [InlineData("topic")]
    [InlineData("batch-link")]
    public async Task Missing_core_links_produce_a_data_error_instead_of_silently_losing_rows(string link)
    {
        await ExecuteCorruptionAsync(async db =>
        {
            if (link == "group")
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM thesis.student_group WHERE id = {_rows[0].GroupId}");
            else if (link == "topic")
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM topics.topic WHERE id = {_rows[0].TopicId}");
            else
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group_member SET batch_id = {_batchB} WHERE student_id = {_rows[0].Students[0]}");
        });
        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(MidtermReportReviewQueueErrors.InconsistentData.Code, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readers_do_not_track_entities_and_query_count_does_not_grow_with_page_size()
    {
        var unfilteredReadCounts = new List<int>();
        foreach (var size in new[] { 1, 20 })
        {
            _commands.Reads.Clear();
            _commands.Writes.Clear();
            await GetDataAsync($"?pageSize={size}");
            unfilteredReadCounts.Add(_commands.Reads.Count);
            Assert.Empty(_commands.Writes);
            Assert.All(_commands.Reads, sql => Assert.StartsWith("SELECT", sql.TrimStart()));
        }
        Assert.All(unfilteredReadCounts, count => Assert.True(count > 0));
        Assert.Single(unfilteredReadCounts.Distinct());
        var filteredReadCounts = new List<int>();
        foreach (var size in new[] { 1, 20 })
        {
            _commands.Reads.Clear();
            _commands.Writes.Clear();
            var data = await GetDataAsync($"?keyword=Shared%20project&pageSize={size}");
            Assert.Equal(2, data.GetProperty("totalCount").GetInt32());
            filteredReadCounts.Add(_commands.Reads.Count);
            Assert.Empty(_commands.Writes);
            Assert.All(_commands.Reads, sql => Assert.StartsWith("SELECT", sql.TrimStart()));
        }
        Assert.All(filteredReadCounts, count => Assert.True(count > 0));
        Assert.Single(filteredReadCounts.Distinct());
        using var scope = _app.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<IMidtermReportReviewQueryReader>();
        var page = await query.GetPageAsync(new MidtermReportReviewFilter(null, null, null, null), 1, 20);
        Assert.Equal(6, page.Items.Count);
        var topics = scope.ServiceProvider.GetRequiredService<ITopicSearchReader>();
        var students = scope.ServiceProvider.GetRequiredService<IStudentSearchReader>();
        Assert.Single(await topics.FindIdsAsync(_batchA, "ugtm"));
        Assert.Equal(2, (await students.FindIdsByNameOrCodeAsync("MATCH001")).Count);
        Assert.Empty(await students.FindIdsByNameOrCodeAsync(" "));
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>().ChangeTracker.Entries());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<TopicCatalogDbContext>().ChangeTracker.Entries());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<IdentityDbContext>().ChangeTracker.Entries());
    }

    private async Task<JsonElement> GetDataAsync(string query = "")
    {
        using var response = await _client.GetAsync(Route + query);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {query}: {response.StatusCode} {body}");
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        return json.RootElement.GetProperty("data").Clone();
    }

    private static JsonElement[] Items(JsonElement data) => data.GetProperty("items").EnumerateArray().ToArray();

    private async Task SeedAsync()
    {
        using var scope = _app.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        identity.Users.Add(User(_advisorId, "advisor", "GV hướng dẫn", "GV01"));
        for (var index = 0; index < 8; index++)
        {
            var studentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var names = index switch
            {
                0 => new[] { "Nguyễn Minh Đức", "Trần Mai Anh" },
                1 => new[] { "Nguyễn Chung A", "Nguyễn Chung B" },
                _ => new[] { $"Sinh viên {index} A", $"Sinh viên {index} B" }
            };
            for (var student = 0; student < 2; student++)
                identity.Users.Add(User(studentIds[student], $"student-{index}-{student}", names[student],
                    index == 0 && student == 0 ? "24521603" : index == 1 ? $"MATCH001{student}" : $"SV{index}{student}"));
            _rows.Add(new SeededSubmission(Guid.NewGuid(), Guid.NewGuid(),
                Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"), studentIds,
                index is 4 or 5 ? _batchB : _batchA,
                index switch { 1 => "NeedRevision", 2 or 5 => "Approved", 3 => "Rejected", 6 => "Draft", _ => "Submitted" },
                index == 2 ? Guid.Parse("22222222-2222-2222-2222-222222222222") : Guid.NewGuid()));
        }
        await identity.SaveChangesAsync();
        var academic = scope.ServiceProvider.GetRequiredService<AcademicPeriodsDbContext>();
        var template = await academic.ScheduleTemplateVersions.Include(value => value.StageDefinitions)
            .SingleAsync(value => value.Status == ConfigurationVersionStatus.Active);
        for (var index = 0; index < 2; index++)
            academic.KltnBatches.Add(KltnBatch.Create(index == 0 ? _batchA : _batchB, index == 0 ? "Đợt A" : "Đợt B",
                "2026–2027", index + 1, new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 1), template, _advisorId, Now));
        await academic.SaveChangesAsync();
        await academic.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status = 'Closed' WHERE id = {_batchB}");
        var topics = scope.ServiceProvider.GetRequiredService<TopicCatalogDbContext>();
        var thesis = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        foreach (var batchId in new[] { _batchA, _batchB })
            thesis.ReviewGateConfigurations.Add(new ReviewGateConfiguration(Guid.NewGuid(), batchId, ReviewGateType.Midterm,
                Now.AddDays(10), Now.AddDays(20), Now.AddDays(30), _advisorId, Now));
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            topics.Topics.Add(new Topic
            {
                Id = row.TopicId,
                BatchId = row.BatchId,
                AdvisorId = _advisorId,
                TitleVi = index switch { 0 => "Quản lý khóa luận UGTM", 3 => "Shared project tỷ lệ 100%_đạt", 4 => "Shared project B", _ => $"Đề tài {index}" },
                TitleEn = index == 0 ? "Thesis Portal" : null,
                CreatedAt = Now,
                StudentNumber = 2
            });
        }
        await topics.SaveChangesAsync();
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            var group = new StudentGroup(row.GroupId, row.TopicId, Now);
            group.AddConfirmedMember(row.Students[0], row.BatchId, Now);
            group.AddPendingMember(row.Students[1], row.BatchId, row.Students[0], "private-invite", Now.AddMinutes(1), Now.AddHours(24));
            Assert.True(group.TrySubmitSyllabus());
            Assert.True(group.TryApproveSyllabus());
            thesis.StudentGroups.Add(group);
            // Cổng 1 and Cổng 2 deliberately use the same IDs to catch cross-gate lookups.
            var syllabus = Syllabus.CreateSubmitted(row.MidtermReportId, row.GroupId);
            syllabus.RecordReview(ReviewDecision.Approved, null, _advisorId, Now);
            thesis.Syllabuses.Add(syllabus);
            thesis.SubmissionFiles.Add(File(Guid.NewGuid(), syllabus.Id, 1, Now.AddDays(100),
                $"syllabus-{index}.pdf", SubmissionEntityType.Syllabus));
            if (index == 7) continue; // Group has a syllabus but has never submitted a midterm report.
            Assert.True(group.TrySubmitMidtermReport());
            if (row.Status == "Approved") Assert.True(group.TryApproveMidtermReport());
            if (row.Status == "NeedRevision") Assert.True(group.TryOpenMidtermReportRevision());
            if (row.Status == "Rejected") Assert.True(group.TryStop());
            var report = MidtermReport.CreateSubmitted(row.MidtermReportId, row.GroupId);
            if (index is 2 or 5)
            {
                report.RecordReview(ReviewDecision.NeedRevision, "Revise", _advisorId, Now);
                report.SubmitRevision();
            }
            if (row.Status is "NeedRevision" or "Approved" or "Rejected")
                report.RecordReview(Enum.Parse<ReviewDecision>(row.Status), "Feedback", _advisorId, Now);
            thesis.MidtermReports.Add(report);
            var uploadedAt = Now.AddMinutes(index switch { 1 or 3 => 30, 4 => 40, 2 => 20, _ => index });
            thesis.SubmissionFiles.Add(File(row.FileId, row.MidtermReportId, index == 2 ? 2 : 1, uploadedAt,
                index == 2 ? "current-v2.pdf" : $"report-{index}.pdf"));
            if (index == 2)
            {
                thesis.SubmissionFiles.AddRange(
                    File(Guid.NewGuid(), row.MidtermReportId, 1, Now.AddMinutes(500), "old-v1.pdf"),
                    File(Guid.NewGuid(), row.MidtermReportId, 3, Now.AddMinutes(600), "future-v3.pdf"),
                    File(Guid.NewGuid(), row.MidtermReportId, 2, Now.AddMinutes(700), "syllabus.pdf", SubmissionEntityType.Syllabus),
                    File(Guid.Parse("11111111-1111-1111-1111-111111111111"), row.MidtermReportId, 2, uploadedAt, "duplicate-v2.pdf"));
            }
        }
        await thesis.SaveChangesAsync();
        await thesis.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.midterm_report SET status = 'Draft' WHERE id = {_rows[6].MidtermReportId}");
    }

    private static IdentityUser User(Guid id, string emailName, string name, string code)
        => IdentityUser.Create(id, $"{emailName}@example.test", $"{emailName.ToUpperInvariant()}@EXAMPLE.TEST",
            name, code, null, null, AccountStatus.Active, Now);

    private SubmissionFile File(Guid id, Guid midtermReportId, int version, DateTime uploadedAt, string name,
        SubmissionEntityType type = SubmissionEntityType.MidtermReport) => new()
        {
            Id = id,
            EntityId = midtermReportId,
            EntityType = type,
            VersionNo = version,
            FileName = name,
            ObjectKey = $"private/{id:N}",
            ContentType = "application/pdf",
            FileSize = 100,
            FileExtension = ".pdf",
            ChecksumSha256 = new string('a', 64),
            UploadedBy = _advisorId,
            UploadedAt = uploadedAt
        };

    private async Task ExecuteCorruptionAsync(Func<UGTMDbContext, Task> mutation)
    {
        // Intentionally broken links exist only inside the disposable PostgreSQL fixture.
        await using var db = CreateMigrationDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
        await mutation(db);
        await transaction.CommitAsync();
    }

    private void Authenticate(Guid id, string role, string? permission)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            $"{id:N}.{role}" + (permission is null ? "" : $".{permission}"));

    private UGTMDbContext CreateMigrationDbContext()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }

    private static Task ResetSchemasAsync(UGTMDbContext db) => db.Database.ExecuteSqlRawAsync("""
        DROP SCHEMA IF EXISTS identity CASCADE;
        DROP SCHEMA IF EXISTS academic CASCADE;
        DROP SCHEMA IF EXISTS topics CASCADE;
        DROP SCHEMA IF EXISTS thesis CASCADE;
        DROP SCHEMA IF EXISTS councils CASCADE;
        DROP SCHEMA IF EXISTS notifications CASCADE;
        DROP SCHEMA IF EXISTS public CASCADE;
        CREATE SCHEMA public;
        """);

    private sealed record SeededSubmission(Guid GroupId, Guid TopicId, Guid MidtermReportId, Guid[] Students,
        Guid BatchId, string Status, Guid FileId);

    private sealed class CommandCapture : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Reads { get; } = new();
        public ConcurrentQueue<string> Writes { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Writes.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now.AddHours(1));
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "MidtermReportReviewQueueTest";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            var parts = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..].Split('.', 3) : [];
            if (parts.Length < 2 || !Guid.TryParse(parts[0], out var id))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", id.ToString()), new(ClaimTypes.Role, parts[1]) };
            if (parts.Length == 3) claims.Add(new Claim("permission", parts[2]));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
        }
    }
}
