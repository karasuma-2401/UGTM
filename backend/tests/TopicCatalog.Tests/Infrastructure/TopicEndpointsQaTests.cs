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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics;
using UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetTopicTrackingDetail;
using UGTM.Modules.TopicCatalog.Application.Topics.LookupStudentByCode;
using UGTM.Modules.TopicCatalog.Application.Topics.PublishTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Infrastructure.Endpoints;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Infrastructure;

public sealed class TopicEndpointsQaTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private readonly FakeTopicRepository _topicRepo = new();
    private readonly FakeTopicUnitOfWork _uow = new();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly FakeKltnBatchReader _batchReader;
    private readonly QaTopicQueryReader _queryReader;
    private readonly QaStudentInfoReader _studentReader = new();

    public TopicEndpointsQaTests()
    {
        _batchReader = new FakeKltnBatchReader(_batchId);
        _queryReader = new QaTopicQueryReader(_topicRepo);
        _studentReader.AddStudent(new StudentInfoContractDto(Guid.NewGuid(), "sv1@ugtm.edu.vn", "Nguyễn Sinh Viên", "22520001", "0901234567"));
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, QaHttpCurrentUser>();

        builder.Services.AddSingleton<ITopicRepository>(_topicRepo);
        builder.Services.AddSingleton<ITopicUnitOfWork>(_uow);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ITopicQueryReader>(_queryReader);
        builder.Services.AddSingleton<ITopicStatisticsReader>(_queryReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentReader);

        builder.Services.AddScoped<GetAllTopicsHandler>();
        builder.Services.AddScoped<GetMyTopicsHandler>();
        builder.Services.AddScoped<GetTopicTrackingDetailHandler>();
        builder.Services.AddScoped<CreateTopicHandler>();
        builder.Services.AddScoped<UpdateTopicHandler>();
        builder.Services.AddScoped<SubmitTopicHandler>();
        builder.Services.AddScoped<ReviewTopicHandler>();
        builder.Services.AddScoped<PublishTopicsHandler>();
        builder.Services.AddScoped<GetMyTopicStatisticsHandler>();
        builder.Services.AddScoped<LookupStudentByCodeHandler>();

        builder.Services.AddAuthentication(QaTopicAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, QaTopicAuthHandler>(QaTopicAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapTopicEndpoints();

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
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(QaTopicAuthHandler.SchemeName, payload);
    }

    private void ClearAuth() => _client.DefaultRequestHeaders.Authorization = null;

    private async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean(), $"Expected success response, got: {json}");
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

    #region A. AuthN & AuthZ

    [Fact]
    public async Task AllTopics_AllowsAnonymous_ProtectedEndpointsRequireAuth()
    {
        ClearAuth();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/topics/all-topics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/topics/my-topics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/topics/{Guid.NewGuid()}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/topics", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PutAsJsonAsync($"/api/topics/{Guid.NewGuid()}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync($"/api/topics/{Guid.NewGuid()}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync($"/api/topics/{Guid.NewGuid()}/review", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/topics/publish", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/topics/my-statistics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/topics/student-lookup?userCode=22520001")).StatusCode);
    }

    [Fact]
    public async Task RoleMatrix_EnforcesForbiddenForUnauthorizedRoles()
    {
        var lecturer1 = Guid.NewGuid();
        var lecturer2 = Guid.NewGuid();
        Authenticate(lecturer1, "Lecturer");
        var created = await ReadDataAsync(await _client.PostAsJsonAsync("/api/topics", new
        {
            batchId = _batchId,
            titleVi = "Đề tài bảo mật quyền",
            studentNumber = 2,
            submitImmediately = true
        }));
        var topicId = created.GetProperty("id").GetGuid();

        // Student blocked from all lecturer/faculty endpoints
        Authenticate(Guid.NewGuid(), "Student");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/topics/my-topics")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/topics/{topicId}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = "SV", studentNumber = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/topics/my-statistics")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/topics/student-lookup?userCode=22520001")).StatusCode);

        // Lecturer 2 blocked from viewing/updating/submitting Lecturer 1's topic
        Authenticate(lecturer2, "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/topics/{topicId}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync($"/api/topics/{topicId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/api/topics/{topicId}/review", new { decision = "Approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/topics/publish", new { batchId = _batchId })).StatusCode);

        // CouncilMember can read tracking detail & my-topics
        Authenticate(Guid.NewGuid(), "CouncilMember");
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/topics/{topicId}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/topics/my-topics")).StatusCode);
    }

    #endregion

    #region B. Happy Path E2E Lifecycle

    [Fact]
    public async Task FullTopicLifecycle_Draft_Update_Submit_NeedRevision_Resubmit_Approve_Publish_Stats_Lookup()
    {
        var advisorId = Guid.NewGuid();
        var facultyId = Guid.NewGuid();
        Authenticate(advisorId, "Lecturer");

        // 1. Lookup student
        var lookupRes = await _client.GetAsync("/api/topics/student-lookup?userCode=22520001");
        Assert.Equal(HttpStatusCode.OK, lookupRes.StatusCode);
        Assert.Equal("Nguyễn Sinh Viên", (await ReadDataAsync(lookupRes)).GetProperty("fullName").GetString());

        // 2. Create Draft topic
        var createRes = await _client.PostAsJsonAsync("/api/topics", new
        {
            batchId = _batchId,
            titleVi = "  Hệ thống phát hiện bất thường  ",
            titleEn = "Anomaly Detection System",
            description = "Mô tả",
            requirements = "Yêu cầu",
            studentNumber = 2,
            submitImmediately = false
        });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var topicId = (await ReadDataAsync(createRes)).GetProperty("id").GetGuid();

        // 3. Submit Draft -> Submitted
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/topics/{topicId}/submit", null)).StatusCode);

        // 4. Faculty marks NeedRevision -> Advisor edits -> Resubmits
        Authenticate(facultyId, "Faculty");
        var revRes = await _client.PostAsJsonAsync($"/api/topics/{topicId}/review", new
        {
            decision = "NeedRevision",
            reviewNote = "Cần làm rõ phạm vi dữ liệu"
        });
        Assert.Equal(HttpStatusCode.OK, revRes.StatusCode);
        Assert.Equal("NeedRevision", (await ReadDataAsync(revRes)).GetProperty("status").GetString());

        Authenticate(advisorId, "Lecturer");
        var updRes = await _client.PutAsJsonAsync($"/api/topics/{topicId}", new
        {
            titleVi = "Hệ thống phát hiện bất thường IoT",
            titleEn = "IoT Anomaly Detection",
            studentNumber = 2,
            submitImmediately = true
        });
        Assert.Equal(HttpStatusCode.OK, updRes.StatusCode);
        Assert.Equal("Submitted", (await ReadDataAsync(updRes)).GetProperty("status").GetString());

        var statsRes = await ReadDataAsync(await _client.GetAsync($"/api/topics/my-statistics?batchId={_batchId}"));
        Assert.Equal(1, statsRes.GetProperty("total").GetInt32());
        Assert.Equal(1, statsRes.GetProperty("submitted").GetInt32());

        // 5. Faculty approves and publishes
        Authenticate(facultyId, "Faculty");
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/topics/{topicId}/review", new { decision = "Approved" })).StatusCode);

        var pubRes = await _client.PostAsJsonAsync("/api/topics/publish", new { batchId = _batchId, topicIds = new[] { topicId } });
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);
        Assert.Equal(1, (await ReadDataAsync(pubRes)).GetProperty("publishedCount").GetInt32());

        ClearAuth();
        var publicList = await ReadDataAsync(await _client.GetAsync($"/api/topics/all-topics?batchId={_batchId}"));
        Assert.Equal(1, publicList.GetProperty("totalCount").GetInt32());
    }

    #endregion

    #region C. Type Mismatch, Malformed JSON & Invalid Query/Route Params

    [Theory]
    [InlineData("/api/topics/all-topics?batchId=not-a-guid")]
    [InlineData("/api/topics/all-topics?pageNumber=abc")]
    [InlineData("/api/topics/all-topics?pageSize=xyz")]
    [InlineData("/api/topics/all-topics?availability=invalid")]
    [InlineData("/api/topics/all-topics?sortBy=unknown")]
    [InlineData("/api/topics/all-topics?sortOrder=random")]
    [InlineData("/api/topics/my-topics?advisorId=not-a-guid")]
    [InlineData("/api/topics/my-topics?status=1")]
    [InlineData("/api/topics/my-topics?status=Unknown")]
    [InlineData("/api/topics/my-topics?registrationStatus=Invalid")]
    [InlineData("/api/topics/my-statistics?batchId=not-a-guid")]
    public async Task TopicGetEndpoints_WithInvalidQueryTypesOrValues_Return400BadRequestNot500(string url)
    {
        Authenticate(Guid.NewGuid(), "Admin");
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/topics/not-a-guid/tracking")]
    [InlineData("PUT", "/api/topics/not-a-guid")]
    [InlineData("POST", "/api/topics/not-a-guid/submit")]
    [InlineData("POST", "/api/topics/not-a-guid/review")]
    public async Task TopicEndpoints_WithNonGuidRouteId_Return404NotFound(string method, string path)
    {
        Authenticate(Guid.NewGuid(), "Admin");
        using var req = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "PUT" or "POST")
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/topics", """{"batchId": "not-a-guid", "titleVi": "A", "studentNumber": 1}""")]
    [InlineData("POST", "/api/topics", """{"batchId": "11111111-1111-1111-1111-111111111111", "titleVi": 123, "studentNumber": 1}""")]
    [InlineData("POST", "/api/topics", """{"batchId": "11111111-1111-1111-1111-111111111111", "titleVi": "A", "studentNumber": "two"}""")]
    [InlineData("POST", "/api/topics", """{"batchId": "11111111-1111-1111-1111-111111111111", "titleVi": "A", "studentNumber": 1, "submitImmediately": "yes"}""")]
    [InlineData("POST", "/api/topics", """{bad json""")]
    [InlineData("POST", "/api/topics", "null")]
    [InlineData("POST", "/api/topics", "")]
    [InlineData("POST", "/api/topics/publish", """{"batchId": "not-a-guid"}""")]
    [InlineData("POST", "/api/topics/publish", """{"topicIds": ["not-a-guid"]}""")]
    [InlineData("POST", "/api/topics/publish", "null")]
    public async Task TopicBodyEndpoints_WithMalformedOrTypeMismatchedOrNullJson_Return400BadRequestNot500(string method, string path, string rawJson)
    {
        Authenticate(Guid.NewGuid(), "Admin");
        using var req = new HttpRequestMessage(new HttpMethod(method), path);
        req.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopic_WithUnsupportedMediaType_Returns415()
    {
        Authenticate(Guid.NewGuid(), "Lecturer");
        using var content = new StringContent("""{"titleVi":"Plain"}""", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync("/api/topics", content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    #endregion

    #region D. Boundaries, Empty Values & Non-existent References

    [Fact]
    public async Task CreateTopic_ValidationBoundaries_And_DuplicateTitle_AreEnforced()
    {
        Authenticate(Guid.NewGuid(), "Lecturer");

        // Empty titleVi, over-length titleVi (301), over-length titleEn (351)
        Assert.Equal("Topic.InvalidTitleVi", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = "   ", studentNumber = 1 })));
        Assert.Equal("Topic.InvalidTitleVi", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = new string('A', 301), studentNumber = 1 })));
        Assert.Equal("Topic.InvalidTitleEn", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = "Valid", titleEn = new string('E', 351), studentNumber = 1 })));

        // StudentNumber not 1 or 2
        Assert.Equal("Topic.InvalidStudentNumber", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = "Valid", studentNumber = 3 })));
        Assert.Equal("Topic.InvalidStudentNumber", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = "Valid", studentNumber = 0 })));

        // Missing batch -> 404
        var missingBatchRes = await _client.PostAsJsonAsync("/api/topics", new { batchId = Guid.NewGuid(), titleVi = "Missing Batch", studentNumber = 1 });
        Assert.Equal(HttpStatusCode.NotFound, missingBatchRes.StatusCode);
        Assert.Equal("Topic.BatchNotFound", await ReadErrorCodeAsync(missingBatchRes));

        // Exact 300 & 350 succeeds; duplicate fails with 409
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = new string('T', 300), titleEn = new string('E', 350), studentNumber = 1 })).StatusCode);
        var dupRes = await _client.PostAsJsonAsync("/api/topics", new { batchId = _batchId, titleVi = new string('T', 300), studentNumber = 1 });
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);
        Assert.Equal("Topic.DuplicateTitle", await ReadErrorCodeAsync(dupRes));
    }

    [Fact]
    public async Task ReviewPublishLookupTracking_WithInvalidBoundariesOrMissingReferences_ReturnExpectedErrors()
    {
        Authenticate(Guid.NewGuid(), "Admin");

        // Student lookup missing / too long / empty
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/topics/student-lookup?userCode=99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/topics/student-lookup?userCode=" + new string('9', 21))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/topics/student-lookup")).StatusCode);

        // Tracking / Submit / Update / Review non-existent topic or Guid.Empty
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/topics/{Guid.Empty}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/topics/{Guid.NewGuid()}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/topics/{Guid.NewGuid()}/submit", null)).StatusCode);

        // Review numeric decision or missing note
        Assert.Equal("Topic.InvalidReviewDecision", await ReadErrorCodeAsync(await _client.PostAsJsonAsync($"/api/topics/{Guid.NewGuid()}/review", new { decision = "3" })));
        Assert.Equal("Topic.ReviewNoteRequired", await ReadErrorCodeAsync(await _client.PostAsJsonAsync($"/api/topics/{Guid.NewGuid()}/review", new { decision = "Rejected" })));

        // Publish empty guid item & non-existent batch
        Assert.Equal("Topic.InvalidInput", await ReadErrorCodeAsync(await _client.PostAsJsonAsync("/api/topics/publish", new { batchId = _batchId, topicIds = new[] { Guid.Empty } })));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/topics/publish", new { batchId = Guid.NewGuid() })).StatusCode);
    }

    #endregion

    private sealed class QaTopicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TopicQaScheme";

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

    private sealed class QaTopicQueryReader(FakeTopicRepository repo) : ITopicQueryReader, ITopicStatisticsReader
    {
        public Task<PagedResult<TopicSummaryDto>> GetAllTopicsAsync(GetAllTopicsQuery query, CancellationToken ct = default)
        {
            var q = repo.All.Where(t => t.IsPublished && t.Status == TopicStatus.Published);
            if (query.BatchId.HasValue) q = q.Where(t => t.BatchId == query.BatchId.Value);
            var items = q.Select(t => new TopicSummaryDto(t.Id, t.BatchId, t.AdvisorId, t.TitleVi, t.TitleEn, t.Description, t.Requirements, t.Status.ToString(), t.StudentNumber, t.CreatedAt, t.IsPublished, t.ReviewNote)).ToList();
            return Task.FromResult(new PagedResult<TopicSummaryDto>(items, items.Count, query.PageNumber, query.PageSize));
        }

        public Task<PagedResult<TopicTrackingSummaryDto>> GetMyTopicsAsync(GetMyTopicsQuery query, Guid? advisorId, bool isPrivileged, CancellationToken ct = default)
        {
            var q = isPrivileged && !advisorId.HasValue ? repo.All : repo.All.Where(t => t.AdvisorId == advisorId);
            var items = q.Select(t => new TopicTrackingSummaryDto(t.Id, t.BatchId, t.AdvisorId, t.TitleVi, t.TitleEn, t.Description, t.Requirements, t.Status.ToString(), t.StudentNumber, t.CreatedAt, 0, t.StudentNumber ?? 0, "Empty", false, true, null, t.IsPublished, t.ReviewNote)).ToList();
            return Task.FromResult(new PagedResult<TopicTrackingSummaryDto>(items, items.Count, query.PageNumber, query.PageSize));
        }

        public Task<TopicTrackingDetailDto?> GetTopicTrackingDetailAsync(Guid topicId, CancellationToken ct = default)
        {
            var t = repo.All.FirstOrDefault(x => x.Id == topicId);
            return Task.FromResult(t is null ? null : new TopicTrackingDetailDto(t.Id, t.BatchId, t.AdvisorId, t.TitleVi, t.TitleEn, t.Description, t.Requirements, t.Status.ToString(), t.StudentNumber, t.CreatedAt, 0, t.StudentNumber ?? 0, "Empty", false, true, null, t.IsPublished, t.ReviewNote));
        }

        public Task<TopicStatisticsDto> GetAsync(Guid advisorId, Guid? batchId, CancellationToken ct)
        {
            var q = repo.All.Where(t => t.AdvisorId == advisorId && (!batchId.HasValue || t.BatchId == batchId.Value)).ToList();
            return Task.FromResult(new TopicStatisticsDto(q.Count, q.Count(t => t.Status == TopicStatus.Submitted), q.Count(t => t.Status == TopicStatus.NeedRevision)));
        }
    }

    private sealed class QaHttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

        public Guid? UserId
        {
            get
            {
                var raw = Principal?.FindFirst("sub")?.Value ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return Guid.TryParse(raw, out var id) ? id : null;
            }
        }

        public Guid? SessionId => null;
        public string? Email => null;
        public string? UserCode => null;

        public IReadOnlySet<string> Roles { get; } = new HashSet<string>(
            accessor.HttpContext?.User?.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value) ?? [],
            StringComparer.OrdinalIgnoreCase);

        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(
            accessor.HttpContext?.User?.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value) ?? [],
            StringComparer.OrdinalIgnoreCase);

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && UserId.HasValue;
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }

    private sealed class QaStudentInfoReader : IStudentInfoReader
    {
        private readonly Dictionary<string, StudentInfoContractDto> _byCode = new(StringComparer.OrdinalIgnoreCase);
        public void AddStudent(StudentInfoContractDto s) { if (s.UserCode != null) _byCode[s.UserCode] = s; }
        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(Guid? studentId, string? userCode, CancellationToken ct = default)
            => Task.FromResult(userCode != null && _byCode.TryGetValue(userCode.Trim(), out var s) ? s : null);
        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(new Dictionary<Guid, StudentInfoContractDto>());
    }
}
