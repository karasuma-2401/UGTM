using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Domain;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.CreateRenameRequest;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetGroupRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorAssignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorUnassignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.ConfirmTopicRegistration;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.GetMyRegistrationStatus;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.RegisterTopic;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class TopicRegistrationAndRenameEndpointsQaTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private readonly FakeTopicRegistrationStore _regStore = new();
    private readonly QaThesisStore _store = new();
    private static readonly DateTime NowUtc = new(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _advisorId = Guid.NewGuid();
    private readonly Guid _studentAId = Guid.NewGuid();
    private readonly Guid _studentBId = Guid.NewGuid();
    private readonly Guid _studentCId = Guid.NewGuid();
    private readonly Guid _blockedStudentId = Guid.NewGuid();
    private readonly Guid _publishedTopicId = Guid.NewGuid();
    private readonly Guid _draftTopicId = Guid.NewGuid();
    private readonly Guid _renameTopicId = Guid.NewGuid();
    private readonly StudentGroup _inProgressGroup;

    public TopicRegistrationAndRenameEndpointsQaTests()
    {
        _regStore.SeedTopic(_publishedTopicId, _batchId, studentNumber: 2, status: "Published", advisorId: _advisorId);
        _regStore.SeedTopic(_draftTopicId, _batchId, studentNumber: 2, status: "Draft", advisorId: _advisorId);
        _regStore.SetPenalty(_blockedStudentId, violationCount: 3, isBlocked: true);

        _store.Students[_studentAId] = new StudentInfoContractDto(_studentAId, "sva@ugtm.edu.vn", "SV A", "22520001", null);
        _store.Students[_studentBId] = new StudentInfoContractDto(_studentBId, "svb@ugtm.edu.vn", "SV B", "22520002", null);
        _store.Students[_studentCId] = new StudentInfoContractDto(_studentCId, "svc@ugtm.edu.vn", "SV C", "22520003", null);
        _store.Students[_blockedStudentId] = new StudentInfoContractDto(_blockedStudentId, "svblocked@ugtm.edu.vn", "SV Blocked", "22520099", null);

        _inProgressGroup = new StudentGroup(Guid.NewGuid(), _renameTopicId, NowUtc.AddDays(-20));
        _inProgressGroup.AddConfirmedMember(_studentAId, _batchId, NowUtc.AddDays(-20));
        _inProgressGroup.TrySubmitSyllabus();
        _inProgressGroup.TryApproveSyllabus();
        _store.Groups[_inProgressGroup.Id] = _inProgressGroup;
        _store.BatchId = _batchId;
        _store.AdvisorId = _advisorId;
        _store.TopicId = _renameTopicId;
        _store.TopicTitleVi = "Nghiên cứu bảo mật IoT";
        _store.TopicTitleEn = "IoT Security Research";
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseKestrelOrLoopback();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        builder.Services.AddSingleton<ITopicRegistrationStore>(_regStore);
        builder.Services.AddSingleton<IStudentInfoReader>(_store);
        builder.Services.AddSingleton<IEmailSender>(_store);
        builder.Services.AddSingleton<IThesisEmailTemplateRenderer>(_store);
        builder.Services.AddSingleton<TimeProvider>(new QaTimeProvider(NowUtc));
        builder.Services.AddSingleton<IRenameRequestRepository>(_store);
        builder.Services.AddSingleton<IStudentGroupRepository>(_store);
        builder.Services.AddSingleton<IKltnBatchReader>(_store);
        builder.Services.AddSingleton<ITopicRenameContract>(_store);
        builder.Services.AddSingleton<ITopicSummaryReader>(_store);
        builder.Services.AddSingleton<IBatchSummaryReader>(_store);
        builder.Services.AddSingleton<IAdvisorNameReader>(_store);
        builder.Services.AddSingleton<IThesisUnitOfWork>(_store);
        builder.Services.AddSingleton<IThesisManagementAuditOutbox>(_store);
        builder.Services.AddSingleton<IRenameRequestTransactionCoordinator>(_store);

        builder.Services.AddScoped<RegisterTopicHandler>();
        builder.Services.AddScoped<ConfirmTopicRegistrationHandler>();
        builder.Services.AddScoped<GetMyRegistrationStatusHandler>();
        builder.Services.AddScoped<AdvisorAssignTopicStudentsHandler>();
        builder.Services.AddScoped<AdvisorUnassignTopicStudentsHandler>();
        builder.Services.AddScoped<CreateRenameRequestHandler>();
        builder.Services.AddScoped<GetGroupRenameRequestsHandler>();
        builder.Services.AddScoped<GetRenameRequestsHandler>();
        builder.Services.AddScoped<ReviewRenameRequestByAdvisorHandler>();
        builder.Services.AddScoped<ReviewRenameRequestByFacultyHandler>();
        builder.Services.AddScoped<ReviewRenameRequestByAcademicAffairsHandler>();

        builder.Services.AddAuthentication(QaThesisAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, QaThesisAuthHandler>(QaThesisAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapTopicRegistrationEndpoints();
        _app.MapRenameRequestEndpoints();

        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private void Authenticate(Guid userId, params string[] roles)
    {
        var payload = string.Join(";", new[] { $"sub={userId}" }.Concat(roles.Select(r => $"role={r}")));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(QaThesisAuthHandler.SchemeName, payload);
    }

    private void ClearAuth() => _client.DefaultRequestHeaders.Authorization = null;

    private async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean(), $"Expected success, got: {json}");
        return json.GetProperty("data");
    }

    private async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object
            ? err.GetProperty("code").GetString() ?? string.Empty
            : string.Empty;
    }

    #region A. AuthN & AuthZ (including BOLA on /confirm)

    [Fact]
    public async Task ProtectedEndpoints_WhenUnauthenticated_Return401()
    {
        ClearAuth();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/thesis/topic-registrations/my-status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PutAsJsonAsync("/api/thesis/topic-registrations/advisor-assign", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.DeleteAsync($"/api/thesis/topic-registrations/advisor-assign/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync($"/api/thesis/groups/{Guid.NewGuid()}/rename-requests", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/thesis/groups/{Guid.NewGuid()}/rename-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/thesis/rename-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{Guid.NewGuid()}/advisor-review", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{Guid.NewGuid()}/faculty-review", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{Guid.NewGuid()}/academic-affairs-review", new { })).StatusCode);
    }

    [Fact]
    public async Task TopicRegistration_TwoStudents_ConfirmBolaProtection_AndAnonymousTokenSuccess()
    {
        // 1. Register Student A + partner Student B
        Authenticate(_studentAId, "Student");
        var regRes = await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new
        {
            topicId = _publishedTopicId,
            partnerUserCode = "22520002"
        });
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);
        var rawToken = Assert.Single(_regStore.QueuedInvitations).RawToken;

        // 2. Wrong student authenticated tries to confirm -> 403 BOLA
        Authenticate(_studentCId, "Student");
        var wrongStudentConfirm = await _client.PostAsJsonAsync("/api/thesis/topic-registrations/confirm", new { token = rawToken });
        Assert.Equal(HttpStatusCode.Forbidden, wrongStudentConfirm.StatusCode);
        Assert.Equal("TopicRegistration.Forbidden", await ReadErrorCodeAsync(wrongStudentConfirm));

        // 3. Anonymous confirm via email link succeeds
        ClearAuth();
        var okConfirm = await _client.PostAsJsonAsync("/api/thesis/topic-registrations/confirm", new { token = rawToken });
        Assert.Equal(HttpStatusCode.OK, okConfirm.StatusCode);

        // 4. Replay confirm -> 409 AlreadyConfirmed
        var replay = await _client.PostAsJsonAsync("/api/thesis/topic-registrations/confirm", new { token = rawToken });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal("TopicRegistration.AlreadyConfirmed", await ReadErrorCodeAsync(replay));

        // 5. Check /my-status for Student B
        Authenticate(_studentBId, "Student");
        var status = await ReadDataAsync(await _client.GetAsync("/api/thesis/topic-registrations/my-status"));
        Assert.True(status.GetProperty("hasRegistered").GetBoolean());
        Assert.Equal("Confirmed", status.GetProperty("confirmationStatus").GetString());
    }

    #endregion

    #region B. Rename Request 3-Tier Workflow + Rejections + Authorization

    [Fact]
    public async Task RenameRequest_ThreeTierApproval_UpdatesTopicTitle_AndWritesAuditLog()
    {
        // 1. Non-member student cannot create rename request
        Authenticate(_studentCId, "Student");
        var nonMemberRes = await _client.PostAsJsonAsync($"/api/thesis/groups/{_inProgressGroup.Id}/rename-requests", new
        {
            newTitleVi = "Tên trái phép",
            reason = "Test"
        });
        Assert.Equal(HttpStatusCode.Forbidden, nonMemberRes.StatusCode);

        // 2. Group member creates request
        Authenticate(_studentAId, "Student");
        var createRes = await _client.PostAsJsonAsync($"/api/thesis/groups/{_inProgressGroup.Id}/rename-requests", new
        {
            newTitleVi = "Hệ thống phát hiện tấn công IoT nâng cao",
            newTitleEn = "Advanced IoT Intrusion Detection",
            reason = "Mở rộng phạm vi thực nghiệm"
        });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var reqId = (await ReadDataAsync(createRes)).GetProperty("id").GetGuid();

        // Duplicate active request fails with 409
        var dupRes = await _client.PostAsJsonAsync($"/api/thesis/groups/{_inProgressGroup.Id}/rename-requests", new
        {
            newTitleVi = "Đơn thứ hai",
            reason = "Test"
        });
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);
        Assert.Equal("RenameRequest.ActiveRequestAlreadyExists", await ReadErrorCodeAsync(dupRes));

        // 3. Other lecturer cannot do advisor review -> 403
        Authenticate(Guid.NewGuid(), "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{reqId}/advisor-review", new { decision = "Approve" })).StatusCode);

        // 4. Faculty cannot skip advisor step -> 409 InvalidReviewState
        Authenticate(Guid.NewGuid(), "Faculty");
        var skipRes = await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{reqId}/faculty-review", new { decision = "Approve" });
        Assert.Equal(HttpStatusCode.Conflict, skipRes.StatusCode);
        Assert.Equal("RenameRequest.InvalidReviewState", await ReadErrorCodeAsync(skipRes));

        // 5. Tier 1: Advisor approves
        Authenticate(_advisorId, "Lecturer");
        var t1 = await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{reqId}/advisor-review", new { decision = "Approve" });
        Assert.Equal(HttpStatusCode.OK, t1.StatusCode);
        Assert.Equal("AdvisorConfirmed", (await ReadDataAsync(t1)).GetProperty("status").GetString());

        // 6. Tier 2: Faculty approves
        Authenticate(Guid.NewGuid(), "Faculty");
        var t2 = await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{reqId}/faculty-review", new { decision = "Approve" });
        Assert.Equal(HttpStatusCode.OK, t2.StatusCode);
        Assert.Equal("FacultyApproved", (await ReadDataAsync(t2)).GetProperty("status").GetString());

        // 7. Tier 3: AcademicAffairs approves -> Completed & Topic updated
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var t3 = await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{reqId}/academic-affairs-review", new { decision = "Approve" });
        Assert.Equal(HttpStatusCode.OK, t3.StatusCode);
        Assert.Equal("Completed", (await ReadDataAsync(t3)).GetProperty("status").GetString());
        Assert.Equal("Hệ thống phát hiện tấn công IoT nâng cao", _store.TopicTitleVi);
        Assert.True(_inProgressGroup.AllowSyllabusResubmission);
        Assert.Contains(_store.Audits, a => a.Action == IntegrationAuditActions.RenameRequestApproved);

        // 8. List & Group History
        var listRes = await _client.GetAsync($"/api/thesis/rename-requests?status=Completed&groupId={_inProgressGroup.Id}");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        Assert.Single((await ReadDataAsync(listRes)).EnumerateArray());
    }

    #endregion

    #region C. Type Mismatch, Malformed JSON & Invalid Params

    [Theory]
    [InlineData("/api/thesis/rename-requests?groupId=not-a-guid")]
    [InlineData("/api/thesis/rename-requests?status=0")]
    [InlineData("/api/thesis/rename-requests?status=1")]
    [InlineData("/api/thesis/rename-requests?status=UnknownStatus")]
    public async Task RenameRequestList_WithInvalidQueryTypesOrEnums_Returns400BadRequestNot500(string url)
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("DELETE", "/api/thesis/topic-registrations/advisor-assign/not-a-guid")]
    [InlineData("POST", "/api/thesis/groups/not-a-guid/rename-requests")]
    [InlineData("GET", "/api/thesis/groups/not-a-guid/rename-requests")]
    [InlineData("POST", "/api/thesis/rename-requests/not-a-guid/advisor-review")]
    [InlineData("POST", "/api/thesis/rename-requests/not-a-guid/faculty-review")]
    [InlineData("POST", "/api/thesis/rename-requests/not-a-guid/academic-affairs-review")]
    public async Task ThesisEndpoints_WithNonGuidRouteId_Return404NotFound(string method, string path)
    {
        Authenticate(Guid.NewGuid(), "Admin");
        using var req = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/thesis/topic-registrations/", """{"topicId": "not-a-guid"}""")]
    [InlineData("POST", "/api/thesis/topic-registrations/", """{bad""")]
    [InlineData("POST", "/api/thesis/topic-registrations/", "null")]
    [InlineData("POST", "/api/thesis/topic-registrations/", "")]
    [InlineData("POST", "/api/thesis/topic-registrations/confirm", """{"token": 12345}""")]
    [InlineData("POST", "/api/thesis/topic-registrations/confirm", "null")]
    [InlineData("PUT", "/api/thesis/topic-registrations/advisor-assign", """{"topicId": "bad", "studentIds": ["bad"]}""")]
    [InlineData("POST", "/api/thesis/rename-requests/11111111-1111-1111-1111-111111111111/advisor-review", """{"decision": 1}""")]
    [InlineData("POST", "/api/thesis/rename-requests/11111111-1111-1111-1111-111111111111/faculty-review", "null")]
    [InlineData("POST", "/api/thesis/rename-requests/11111111-1111-1111-1111-111111111111/academic-affairs-review", "")]
    public async Task ThesisBodyEndpoints_WithMalformedOrTypeMismatchedOrNullJson_Return400BadRequestNot500(string method, string path, string rawJson)
    {
        Authenticate(Guid.NewGuid(), "Admin");
        using var req = new HttpRequestMessage(new HttpMethod(method), path);
        req.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/thesis/groups/22222222-2222-2222-2222-222222222222/rename-requests", "null")]
    [InlineData("POST", "/api/thesis/groups/22222222-2222-2222-2222-222222222222/rename-requests", "")]
    [InlineData("POST", "/api/thesis/groups/22222222-2222-2222-2222-222222222222/rename-requests", """{"newTitleVi": 123, "reason": "r"}""")]
    public async Task CreateRenameRequest_WithMalformedOrNullJson_Returns400BadRequestNot500(string method, string path, string rawJson)
    {
        Authenticate(_studentAId, "Student");
        using var req = new HttpRequestMessage(new HttpMethod(method), path);
        req.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region D. Boundaries, Empty Values & Non-existent References

    [Fact]
    public async Task RegistrationAndRename_BoundaryAndMissingReferenceRules_AreEnforced()
    {
        // Self invite & non-published topic & missing partner & blocked student
        Authenticate(_studentAId, "Student");
        Assert.Equal("TopicRegistration.InvalidTopicId", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new { topicId = Guid.Empty })));
        Assert.Equal("TopicRegistration.CannotInviteSelf", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new { topicId = _publishedTopicId, partnerStudentId = _studentAId })));
        Assert.Equal("TopicRegistration.PartnerNotFound", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new { topicId = _publishedTopicId, partnerUserCode = "MISSING999" })));
        Assert.Equal("TopicRegistration.TopicNotPublished", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new { topicId = _draftTopicId })));
        Assert.Equal("TopicRegistration.TopicNotFound", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new { topicId = Guid.NewGuid() })));

        Authenticate(_blockedStudentId, "Student");
        var blockedRes = await _client.PostAsJsonAsync("/api/thesis/topic-registrations/", new { topicId = _publishedTopicId });
        Assert.Equal(HttpStatusCode.Forbidden, blockedRes.StatusCode);
        Assert.Equal("TopicRegistration.StudentRegistrationBlocked", await ReadErrorCodeAsync(blockedRes));

        // Advisor assign missing topic / empty list & unassign non-existent
        Authenticate(_advisorId, "Lecturer");
        Assert.Equal("TopicRegistration.EmptyStudentList", await ReadErrorCodeAsync(await _client.PutAsJsonAsync("/api/thesis/topic-registrations/advisor-assign", new { topicId = _draftTopicId })));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/thesis/topic-registrations/advisor-assign/{Guid.NewGuid()}")).StatusCode);

        // Rename: unchanged title, >300 chars, missing group, missing request on review, reject without reason
        Authenticate(_studentAId, "Student");
        Assert.Equal("RenameRequest.TitleUnchanged", await ReadErrorCodeAsync(await _client.PostAsJsonAsync($"/api/thesis/groups/{_inProgressGroup.Id}/rename-requests", new { newTitleVi = "nghiên cứu bảo mật iot", newTitleEn = "IoT Security Research", reason = "No change" })));
        Assert.Equal("RenameRequest.InvalidRequest", await ReadErrorCodeAsync(await _client.PostAsJsonAsync($"/api/thesis/groups/{_inProgressGroup.Id}/rename-requests", new { newTitleVi = new string('X', 301), reason = "Too long" })));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"/api/thesis/groups/{Guid.NewGuid()}/rename-requests", new { newTitleVi = "Valid", reason = "Missing group" })).StatusCode);

        Authenticate(Guid.NewGuid(), "Faculty");
        Assert.Equal("RenameRequest.InvalidDecision", await ReadErrorCodeAsync(await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{Guid.NewGuid()}/faculty-review", new { decision = "0" })));
        Assert.Equal("RenameRequest.RejectionReasonRequired", await ReadErrorCodeAsync(await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{Guid.NewGuid()}/faculty-review", new { decision = "Reject", rejectionReason = "  " })));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"/api/thesis/rename-requests/{Guid.NewGuid()}/faculty-review", new { decision = "Approve" })).StatusCode);
    }

    #endregion

    private sealed class QaThesisAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ThesisQaScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
                return Task.FromResult(AuthenticateResult.NoResult());

            var header = headerValues.ToString();
            if (!header.StartsWith($"{SchemeName} ", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.NoResult());

            var payload = header[(SchemeName.Length + 1)..];
            var claims = new List<Claim>();
            foreach (var part in payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var idx = part.IndexOf('=');
                if (idx <= 0) continue;
                var key = part[..idx];
                var val = part[(idx + 1)..];
                if (key == "sub") claims.Add(new Claim("sub", val));
                else if (key == "role") claims.Add(new Claim(ClaimTypes.Role, val));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
        }
    }

    private sealed class QaTimeProvider(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }

    private sealed class QaThesisStore :
        IStudentInfoReader,
        IEmailSender,
        IThesisEmailTemplateRenderer,
        IRenameRequestRepository,
        IStudentGroupRepository,
        IKltnBatchReader,
        ITopicRenameContract,
        ITopicSummaryReader,
        IBatchSummaryReader,
        IAdvisorNameReader,
        IThesisUnitOfWork,
        IThesisManagementAuditOutbox,
        IRenameRequestTransactionCoordinator
    {
        public Dictionary<Guid, StudentInfoContractDto> Students { get; } = [];
        public Dictionary<Guid, StudentGroup> Groups { get; } = [];
        public List<RenameRequest> RenameRequests { get; } = [];
        public List<AuditIntegrationEntry> Audits { get; } = [];
        public Guid BatchId { get; set; }
        public Guid AdvisorId { get; set; }
        public Guid TopicId { get; set; }
        public string TopicTitleVi { get; set; } = string.Empty;
        public string? TopicTitleEn { get; set; }

        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(Guid? studentId, string? userCode, CancellationToken ct = default)
        {
            if (studentId.HasValue && Students.TryGetValue(studentId.Value, out var byId)) return Task.FromResult<StudentInfoContractDto?>(byId);
            if (!string.IsNullOrWhiteSpace(userCode)) return Task.FromResult(Students.Values.FirstOrDefault(s => string.Equals(s.UserCode, userCode.Trim(), StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult<StudentInfoContractDto?>(null);
        }

        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(studentIds.Where(Students.ContainsKey).Distinct().ToDictionary(id => id, id => Students[id]));

        public Task SendAsync(EmailMessage message, CancellationToken ct) => Task.CompletedTask;
        public EmailMessage RenderTopicRegistrationInvite(string to, string partnerFullName, string studentAName, string rawToken, DateTime expiresAtUtc) => new(to, "Invite", "Body", null);
        public EmailMessage RenderTopicRegistrationConfirmationForInviter(string to, string studentAFullName, string partnerFullName, string partnerUserCode, DateTime expiresAtUtc) => new(to, "Confirm", "Body", null);
        public EmailMessage RenderAdvisorAssignedNotification(string to, string studentFullName, string studentUserCode) => new(to, "Assigned", "Body", null);
        public EmailMessage RenderTopicRegistrationWarning(string to, string studentFullName, string studentUserCode, int violationCount, int maxViolations) => new(to, "Warn", "Body", null);
        public EmailMessage RenderTopicRegistrationBlocked(string to, string studentFullName, string studentUserCode, int violationCount) => new(to, "Blocked", "Body", null);

        public void Add(RenameRequest request) => RenameRequests.Add(request);
        public Task<RenameRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(RenameRequests.FirstOrDefault(r => r.Id == id));
        public Task<RenameRequest?> LockByIdAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<RenameRequest>> GetByGroupIdAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RenameRequest>>(RenameRequests.Where(r => r.GroupId == groupId).ToList());
        public Task<bool> HasActiveRequestForGroupAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(RenameRequests.Any(r => r.GroupId == groupId && r.Status is RenameRequestStatus.Created or RenameRequestStatus.AdvisorConfirmed or RenameRequestStatus.FacultyApproved));
        public Task<IReadOnlyList<RenameRequest>> ListAsync(Guid? groupId, RenameRequestStatus? status, IReadOnlyCollection<Guid>? topicIdsFilter, CancellationToken ct = default)
        {
            var q = RenameRequests.AsEnumerable();
            if (groupId.HasValue) q = q.Where(r => r.GroupId == groupId.Value);
            if (status.HasValue) q = q.Where(r => r.Status == status.Value);
            return Task.FromResult<IReadOnlyList<RenameRequest>>(q.ToList());
        }

        Task<StudentGroup?> IStudentGroupRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Groups.TryGetValue(id, out var g) ? g : null);
        public void Add(StudentGroup group) => Groups[group.Id] = group;

        public Task<bool> ExistsAsync(Guid batchId, CancellationToken ct = default) => Task.FromResult(batchId == BatchId);
        public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken ct = default) => Task.FromResult<BatchRegistrationWindowContractDto?>(new BatchRegistrationWindowContractDto(batchId, "Open", true, nowUtc.AddDays(7)));
        public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken ct = default) => Task.FromResult<BatchTopicPreparationWindowContractDto?>(new BatchTopicPreparationWindowContractDto(batchId, "Open", true, nowUtc.AddDays(7)));
        public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken ct = default) => Task.FromResult<BatchRenameRequestWindowContractDto?>(new BatchRenameRequestWindowContractDto(batchId, "Open", true, DateOnly.FromDateTime(nowUtc.AddDays(-10)), nowUtc.AddDays(10)));
        public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken ct = default) => Task.FromResult<BatchFinalReportSubmissionWindowContractDto?>(null);

        public Task<TopicTitleSnapshotContractDto?> GetTopicSnapshotAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == TopicId ? new TopicTitleSnapshotContractDto(TopicId, BatchId, AdvisorId, TopicTitleVi, TopicTitleEn) : null);

        public Task<TopicTitleUpdateContractResultDto> UpdateTopicTitlesAsync(Guid id, string newTitleVi, string? newTitleEn, CancellationToken ct = default)
        {
            var oldVi = TopicTitleVi;
            var oldEn = TopicTitleEn;
            TopicTitleVi = newTitleVi;
            TopicTitleEn = newTitleEn;
            return Task.FromResult(TopicTitleUpdateContractResultDto.Success(oldVi, newTitleVi, oldEn, newTitleEn));
        }

        public Task<IReadOnlyDictionary<Guid, TopicSummaryContractDto>> GetByIdsAsync(IEnumerable<Guid> topicIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, TopicSummaryContractDto>>(topicIds.Contains(TopicId) ? new Dictionary<Guid, TopicSummaryContractDto> { [TopicId] = new(TopicId, BatchId, AdvisorId, TopicTitleVi, TopicTitleEn) } : new Dictionary<Guid, TopicSummaryContractDto>());
        Task<IReadOnlyDictionary<Guid, BatchSummaryContractDto>> IBatchSummaryReader.GetByIdsAsync(IEnumerable<Guid> batchIds, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<Guid, BatchSummaryContractDto>>(batchIds.Contains(BatchId) ? new Dictionary<Guid, BatchSummaryContractDto> { [BatchId] = new(BatchId, "KLTN QA") } : new Dictionary<Guid, BatchSummaryContractDto>());
        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> advisorIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(advisorIds.Contains(AdvisorId) ? new Dictionary<Guid, string> { [AdvisorId] = "TS. QA Advisor" } : new Dictionary<Guid, string>());

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Audits.Add(entry);
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> workflow, CancellationToken ct = default) => workflow(ct);
    }
}

file static class WebHostQaExtensions
{
    public static void UseKestrelOrLoopback(this ConfigureWebHostBuilder webHost)
        => webHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
}
