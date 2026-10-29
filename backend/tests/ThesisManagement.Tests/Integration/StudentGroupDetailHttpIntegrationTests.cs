using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
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
using UGTM.Modules.ThesisManagement.Application.StudentGroups.GetStudentGroupDetail;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Contracts.Topics;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Infrastructure;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using IdentityUser = UGTM.Modules.Identity.Domain.Entities.User;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class StudentGroupDetailHttpIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _topicId = Guid.NewGuid();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _partnerId = Guid.NewGuid();
    private readonly Guid _advisorId = Guid.NewGuid();
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string Route => $"/api/thesis/groups/{_groupId}/detail";

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
        builder.Services.AddScoped<IStudentInfoReader, StudentInfoReader>();
        builder.Services.AddScoped<IAdvisorNameReader, AdvisorNameReader>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddAuthentication(TestAuthentication.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        new ThesisManagementModule().MapEndpoints(_app);
        await SeedAsync();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
        Authenticate(_studentId);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
    }

    [Fact]
    public async Task Detail_returns_real_topic_batch_advisor_and_all_members_without_writing_data()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        var before = await db.StudentGroups.AsNoTracking().SingleAsync();

        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(_groupId, data.GetProperty("groupId").GetGuid());
        Assert.Equal(_topicId, data.GetProperty("topicId").GetGuid());
        Assert.Equal("Hệ thống quản lý khóa luận", data.GetProperty("topicTitleVi").GetString());
        Assert.Equal("Thesis management system", data.GetProperty("topicTitleEn").GetString());
        Assert.Equal(_batchId, data.GetProperty("batchId").GetGuid());
        Assert.Equal("Đợt 1 HK1 2026–2027", data.GetProperty("batchName").GetString());
        Assert.Equal("Registered", data.GetProperty("groupStatus").GetString());
        var advisor = data.GetProperty("advisor");
        Assert.Equal(_advisorId, advisor.GetProperty("advisorId").GetGuid());
        Assert.Equal("Trần Hoàng Long", advisor.GetProperty("fullName").GetString());
        Assert.False(advisor.TryGetProperty("academicTitle", out _));
        Assert.False(advisor.TryGetProperty("email", out _));
        var members = data.GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(2, members.Length);
        Assert.Equal(_studentId, members[0].GetProperty("studentId").GetGuid());
        Assert.Equal("24521603", members[0].GetProperty("userCode").GetString());
        Assert.Equal("Nguyễn Minh Đức", members[0].GetProperty("fullName").GetString());
        Assert.Equal("student@example.test", members[0].GetProperty("email").GetString());
        Assert.Equal("Confirmed", members[0].GetProperty("confirmationStatus").GetString());
        Assert.Equal(_partnerId, members[1].GetProperty("studentId").GetGuid());
        Assert.Equal("Pending", members[1].GetProperty("confirmationStatus").GetString());
        var body = json.RootElement.GetRawText();
        foreach (var excluded in new[] { "groupCode", "topicCode", "isLate", "confirmationToken", "private-invitation-hash", "passwordHash", "securityStamp" })
            Assert.DoesNotContain(excluded, body);

        var after = await db.StudentGroups.AsNoTracking().SingleAsync();
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Status, after.Status);
        Assert.Empty(await db.AuditOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData("Student", true)]
    [InlineData("ConfirmedPartner", true)]
    [InlineData("Advisor", true)]
    [InlineData("FacultyWithPermission", true)]
    [InlineData("Admin", true)]
    [InlineData("PendingPartner", false)]
    [InlineData("OtherStudent", false)]
    [InlineData("OtherLecturer", false)]
    [InlineData("FacultyWithoutPermission", false)]
    [InlineData("PermissionWithoutFaculty", false)]
    [InlineData("MemberWithoutStudentRole", false)]
    public async Task Detail_enforces_existing_confirmed_member_staff_and_advisor_access(string actor, bool allowed)
    {
        if (actor == "ConfirmedPartner")
        {
            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
            var group = await db.StudentGroups.Include(value => value.Members).SingleAsync();
            Assert.True(group.ConfirmMember(_partnerId, Now.AddMinutes(2)));
            await db.SaveChangesAsync();
        }
        var (userId, role, permission) = actor switch
        {
            "Student" => (_studentId, "sTuDeNt", (string?)null),
            "ConfirmedPartner" or "PendingPartner" => (_partnerId, "Student", null),
            "Advisor" => (_advisorId, "Lecturer", null),
            "FacultyWithPermission" => (Guid.NewGuid(), "fAcUlTy", PermissionCodes.ThesisReviewGatesReview),
            "Admin" => (Guid.NewGuid(), "aDmIn", null),
            "FacultyWithoutPermission" => (Guid.NewGuid(), "Faculty", null),
            "PermissionWithoutFaculty" => (Guid.NewGuid(), "Student", PermissionCodes.ThesisReviewGatesReview),
            "MemberWithoutStudentRole" => (_studentId, "Lecturer", null),
            "OtherLecturer" => (Guid.NewGuid(), "Lecturer", null),
            _ => (Guid.NewGuid(), "Student", null)
        };
        Authenticate(userId, role, permission);

        using var response = await _client.GetAsync(Route);
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (!allowed)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains(StudentGroupDetailErrors.AccessDenied.Code, body);
            Assert.DoesNotContain("student@example.test", body);
            Assert.DoesNotContain("Nguyễn Minh Đức", body);
        }
    }

    [Fact]
    public async Task Advisor_reassignment_revokes_previous_advisor_and_returns_the_current_assignment()
    {
        Authenticate(_advisorId, "Lecturer");
        using var initial = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TopicCatalogDbContext>();
            var topic = await db.Topics.SingleAsync();
            topic.AdvisorId = _partnerId;
            topic.TitleEn = null;
            await db.SaveChangesAsync();
        }

        using var previous = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Forbidden, previous.StatusCode);
        Authenticate(_partnerId, "Lecturer");
        using var current = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        using var json = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(_partnerId, data.GetProperty("advisor").GetProperty("advisorId").GetGuid());
        Assert.Equal("Trần Mai Anh", data.GetProperty("advisor").GetProperty("fullName").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("topicTitleEn").ValueKind);
    }

    [Fact]
    public async Task Authentication_identifiers_and_missing_group_return_expected_status_codes()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Authenticate(Guid.Empty);
        using var emptyUser = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, emptyUser.StatusCode);
        Authenticate(_studentId);
        using var emptyGroup = await _client.GetAsync($"/api/thesis/groups/{Guid.Empty}/detail");
        Assert.Equal(HttpStatusCode.BadRequest, emptyGroup.StatusCode);
        using var missing = await _client.GetAsync($"/api/thesis/groups/{Guid.NewGuid()}/detail");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains(StudentGroupDetailErrors.GroupNotFound.Code, await missing.Content.ReadAsStringAsync());
        using var invalid = await _client.GetAsync("/api/thesis/groups/not-a-guid/detail");
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode); // Existing :guid route convention.
    }

    [Fact]
    public async Task Detail_remains_readable_for_stopped_groups_and_a_non_open_batch()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        var group = await db.StudentGroups.SingleAsync();
        Assert.True(group.TryStop());
        await db.SaveChangesAsync();
        // The seeded batch remains Draft; reading does not require an Open batch or a submission.
        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Stopped", json.RootElement.GetProperty("data").GetProperty("groupStatus").GetString());
    }

    [Fact]
    public async Task Missing_display_records_preserve_identifiers_and_members_with_null_details()
    {
        // Simulate legacy corruption only inside the disposable PostgreSQL fixture.
        await using (var db = CreateMigrationDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity.\"user\" WHERE id = {_advisorId} OR id = {_partnerId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM academic.kltn_batch WHERE id = {_batchId}");
            await transaction.CommitAsync();
        }
        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(_batchId, data.GetProperty("batchId").GetGuid());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("batchName").ValueKind);
        Assert.Equal(_advisorId, data.GetProperty("advisor").GetProperty("advisorId").GetGuid());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("advisor").GetProperty("fullName").ValueKind);
        var members = data.GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(2, members.Length);
        var missing = members.Single(value => value.GetProperty("studentId").GetGuid() == _partnerId);
        foreach (var field in new[] { "fullName", "email", "userCode" })
            Assert.Equal(JsonValueKind.Null, missing.GetProperty(field).ValueKind);
        Assert.Equal("Pending", missing.GetProperty("confirmationStatus").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Broken_core_links_return_a_data_error_only_to_authorized_readers(bool missingTopic)
    {
        await using (var db = CreateMigrationDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            if (missingTopic)
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM topics.topic WHERE id = {_topicId}");
            else
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group_member SET batch_id = {Guid.NewGuid()} WHERE student_id = {_partnerId}");
            await transaction.CommitAsync();
        }
        Authenticate(Guid.NewGuid());
        using var outsider = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        Authenticate(_studentId);
        using var member = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.InternalServerError, member.StatusCode);
        Assert.Contains(StudentGroupDetailErrors.InconsistentData.Code, await member.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registered_readers_use_postgresql_without_tracking_entities()
    {
        using var scope = _app.Services.CreateScope();
        var groupReader = scope.ServiceProvider.GetRequiredService<IStudentGroupDetailReader>();
        var group = await groupReader.GetByIdAsync(_groupId);
        Assert.NotNull(group);
        Assert.Equal(2, group.Members.Count);
        var topics = scope.ServiceProvider.GetRequiredService<ITopicSummaryReader>();
        var batches = scope.ServiceProvider.GetRequiredService<IBatchSummaryReader>();
        Assert.Single(await topics.GetByIdsAsync([_topicId, _topicId, Guid.NewGuid()]));
        Assert.Single(await batches.GetByIdsAsync([_batchId, _batchId, Guid.NewGuid()]));
        Assert.Empty(await topics.GetByIdsAsync([]));
        Assert.Empty(await batches.GetByIdsAsync([]));
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>().ChangeTracker.Entries());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<TopicCatalogDbContext>().ChangeTracker.Entries());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AcademicPeriodsDbContext>().ChangeTracker.Entries());
    }

    private async Task SeedAsync()
    {
        using var scope = _app.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        identity.Users.AddRange(
            IdentityUser.Create(_studentId, "student@example.test", "STUDENT@EXAMPLE.TEST", "Nguyễn Minh Đức", "24521603", null, null, AccountStatus.Active, Now),
            IdentityUser.Create(_partnerId, "partner@example.test", "PARTNER@EXAMPLE.TEST", "Trần Mai Anh", "24521604", null, null, AccountStatus.Active, Now),
            IdentityUser.Create(_advisorId, "advisor@example.test", "ADVISOR@EXAMPLE.TEST", "Trần Hoàng Long", "GV01", null, null, AccountStatus.Active, Now));
        await identity.SaveChangesAsync();
        var academic = scope.ServiceProvider.GetRequiredService<AcademicPeriodsDbContext>();
        var template = await academic.ScheduleTemplateVersions.Include(value => value.StageDefinitions)
            .SingleAsync(value => value.Status == ConfigurationVersionStatus.Active);
        academic.KltnBatches.Add(KltnBatch.Create(_batchId, "Đợt 1 HK1 2026–2027", "2026–2027", 1,
            new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 1), template, _advisorId, Now));
        await academic.SaveChangesAsync();
        var topics = scope.ServiceProvider.GetRequiredService<TopicCatalogDbContext>();
        topics.Topics.Add(new Topic
        {
            Id = _topicId,
            BatchId = _batchId,
            AdvisorId = _advisorId,
            TitleVi = "Hệ thống quản lý khóa luận",
            TitleEn = "Thesis management system",
            StudentNumber = 2,
            CreatedAt = Now
        });
        await topics.SaveChangesAsync();
        var group = new StudentGroup(_groupId, _topicId, Now);
        group.AddConfirmedMember(_studentId, _batchId, Now);
        group.AddPendingMember(_partnerId, _batchId, _studentId, "private-invitation-hash", Now.AddMinutes(1), Now.AddHours(24));
        var thesis = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        thesis.StudentGroups.Add(group);
        await thesis.SaveChangesAsync();
    }

    private void Authenticate(Guid userId, string role = "Student", string? permission = null)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            $"{userId:N}.{role}" + (permission is null ? "" : $".{permission}"));

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

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "StudentGroupDetailTest";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            var parts = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..].Split('.', 3) : [];
            if (parts.Length < 2 || !Guid.TryParse(parts[0], out var id))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", id.ToString()), new(ClaimTypes.Role, parts[1]) };
            if (parts.Length == 3) claims.Add(new Claim("permission", parts[2]));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
