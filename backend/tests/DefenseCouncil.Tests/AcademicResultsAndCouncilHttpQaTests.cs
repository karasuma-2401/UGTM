using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResultDetail;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMemberCandidates;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.RemoveCouncilMember;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilToAcademicAffairs;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.DefenseCouncil.Infrastructure.Exporting;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class AcademicResultsAndCouncilHttpQaTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _topicId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _advisorId = Guid.NewGuid();

    private readonly FakeCouncilRepository _councilRepo = new();
    private readonly FakeBatchReader _batchReader = new();
    private readonly FakeCouncilUserInfoReader _userInfoReader = new();
    private readonly FakeStudentInfoReader _studentReader = new();
    private readonly FakeDefenseGroupContract _defenseGroupContract = new();
    private readonly FakeTopicDefenseContract _topicContract = new();
    private readonly QaAcademicResultsThesisReader _thesisReader = new();

    public async Task InitializeAsync()
    {
        _batchReader.ExistingBatches.Add(_batchId);
        _topicContract.Topics[_topicId] = new TopicDefenseDetailsContractDto(_topicId, _batchId, _advisorId, "Đề tài QA Results", "QA Results Topic");
        _userInfoReader.Users[_advisorId] = new CouncilUserInfoContractDto(_advisorId, "TS. Advisor QA", "adv@ugtm.edu.vn", "GV01", true, true);
        _userInfoReader.Users[_studentId] = new CouncilUserInfoContractDto(_studentId, "Nguyễn Văn QA", "qa@ugtm.edu.vn", "22520001", true, true);
        _thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(
            GroupId: _groupId,
            TopicId: _topicId,
            BatchId: _batchId,
            GroupStatus: "InProgress",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-60),
            Members: [new DefenseGroupMemberContractDto(_studentId, _batchId, "Confirmed")],
            MidtermReport: new AcademicResultMidtermSnapshotContractDto(Guid.NewGuid(), "Approved", 1, "Đạt yêu cầu", _advisorId, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-10)),
            FinalSubmissionStatus: "ArchiveConfirmed"));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        builder.Services.AddSingleton<ThesisGradeCalculator>();
        builder.Services.AddScoped<IGradeCalculationService, GradeCalculationService>();
        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IDefenseCouncilUnitOfWork>(new FakeDefenseCouncilUnitOfWork());
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentReader);
        builder.Services.AddSingleton<IDefenseGroupContract>(_defenseGroupContract);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicContract);
        builder.Services.AddSingleton<IDefenseCouncilAuditOutbox>(new FakeDefenseCouncilAuditOutbox());
        builder.Services.AddSingleton<IReauthenticationVerifier>(new FakeReauthenticationVerifier());
        builder.Services.AddSingleton<ICouncilMinuteWriteGuard>(new TestCouncilScoreWriteGuard());
        builder.Services.AddSingleton<ICouncilScoreWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IFinalSubmissionAuthorityReader, TestScoreAuthority>();
        builder.Services.AddSingleton<IScoreSummaryConfigurationReader>(new FakeMinuteConfigurationReader());
        builder.Services.AddSingleton<ICouncilSchedulingGuard>(new FakeCouncilSchedulingGuard());

        builder.Services.AddSingleton<IAcademicResultsThesisReader>(_thesisReader);
        builder.Services.AddSingleton<IStudentSearchReader>(new QaStudentSearchReader());
        builder.Services.AddSingleton<IScoreSummaryStore>(new QaScoreSummaryStore());
        builder.Services.AddSingleton<IFinalSubmissionArchiveProofContract>(new QaArchiveProofContract());

        builder.Services.AddScoped<CouncilMinuteWorkspaceLoader>();
        builder.Services.AddScoped<CreateCouncilHandler>();
        builder.Services.AddScoped<AddCouncilMembersHandler>();
        builder.Services.AddScoped<GetCouncilMemberCandidatesHandler>();
        builder.Services.AddScoped<UpdateCouncilMembersHandler>();
        builder.Services.AddScoped<RemoveCouncilMemberHandler>();
        builder.Services.AddScoped<AssignCouncilGroupsHandler>();
        builder.Services.AddScoped<ApproveCouncilHandler>();
        builder.Services.AddScoped<GetCouncilScheduleHandler>();
        builder.Services.AddScoped<GetCouncilMinuteWorkspaceHandler>();
        builder.Services.AddScoped<SaveCouncilMinuteHandler>();
        builder.Services.AddScoped<LockCouncilMinuteHandler>();
        builder.Services.AddScoped<UnlockCouncilMinuteHandler>();
        builder.Services.AddScoped<GetCouncilsOverviewHandler>();
        builder.Services.AddScoped<UpdateCouncilScheduleHandler>();
        builder.Services.AddScoped<SubmitCouncilToAcademicAffairsHandler>();
        builder.Services.AddScoped<SubmitCouncilScoreHandler>();
        builder.Services.AddScoped<CheckCouncilDiscrepancyHandler>();

        builder.Services.AddScoped<AcademicResultsWorkspaceLoader>();
        builder.Services.AddScoped<IAcademicResultsExportFileGenerator, AcademicResultsExportFileGenerator>();
        builder.Services.AddScoped<GetAcademicResultsHandler>();
        builder.Services.AddScoped<GetAcademicResultDetailHandler>();
        builder.Services.AddScoped<ExportAcademicResultsHandler>();

        builder.Services.AddAuthentication(QaCouncilAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, QaCouncilAuthHandler>(QaCouncilAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapAcademicAffairsResultEndpoints();
        _app.MapCouncilEndpoints();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private void Authenticate(Guid userId, string role)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"test.{userId:N}.{role}");

    private void ClearAuth()
        => _client.DefaultRequestHeaders.Authorization = null;

    private async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object
            ? err.GetProperty("code").GetString() ?? string.Empty
            : string.Empty;
    }

    #region A. AcademicAffairsResultEndpoints QA

    [Fact]
    public async Task AcademicResults_AuthNAndAuthZ_AreEnforced()
    {
        ClearAuth();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/academic-affairs/results/{_groupId}?phase=Final")).StatusCode);

        foreach (var forbiddenRole in new[] { "Student", "Lecturer", "CouncilMember" })
        {
            Authenticate(Guid.NewGuid(), forbiddenRole);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/academic-affairs/results/{_groupId}?phase=Final")).StatusCode);
        }
    }

    [Fact]
    public async Task AcademicResults_HappyPath_List_Detail_ExportXlsxAndPdf()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");

        var listRes = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Midterm&status=CONTINUED");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);

        var detailRes = await _client.GetAsync($"/api/academic-affairs/results/{_groupId}?phase=Midterm");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);

        var xlsxRes = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx&phase=Midterm");
        Assert.Equal(HttpStatusCode.OK, xlsxRes.StatusCode);
        var xlsxBytes = await xlsxRes.Content.ReadAsByteArrayAsync();
        Assert.True(xlsxBytes.Length > 100 && xlsxBytes[0] == (byte)'P' && xlsxBytes[1] == (byte)'K');

        var pdfRes = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=pdf&phase=Final");
        Assert.Equal(HttpStatusCode.OK, pdfRes.StatusCode);
        var pdfBytes = await pdfRes.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdfBytes.Take(8).ToArray()));
    }

    [Theory]
    [InlineData("/api/academic-affairs/results")] // missing both batchId & phase
    [InlineData("/api/academic-affairs/results?phase=Final")] // missing batchId
    [InlineData("/api/academic-affairs/results?batchId=11111111-1111-1111-1111-111111111111")] // missing phase
    [InlineData("/api/academic-affairs/results?batchId=not-a-guid&phase=Final")]
    [InlineData("/api/academic-affairs/results?batchId=00000000-0000-0000-0000-000000000000&phase=Final")]
    [InlineData("/api/academic-affairs/results?batchId=11111111-1111-1111-1111-111111111111&phase=Invalid")]
    [InlineData("/api/academic-affairs/results?batchId=11111111-1111-1111-1111-111111111111&phase=Midterm&status=PUBLISHED")]
    [InlineData("/api/academic-affairs/results?batchId=11111111-1111-1111-1111-111111111111&phase=Final&pageIndex=abc")]
    [InlineData("/api/academic-affairs/results?batchId=11111111-1111-1111-1111-111111111111&phase=Final&pageSize=xyz")]
    [InlineData("/api/academic-affairs/results/11111111-1111-1111-1111-111111111111")] // detail missing phase
    [InlineData("/api/academic-affairs/results/export")] // export missing batchId & format
    [InlineData("/api/academic-affairs/results/export?batchId=11111111-1111-1111-1111-111111111111")] // export missing format
    [InlineData("/api/academic-affairs/results/export?batchId=11111111-1111-1111-1111-111111111111&format=docx")]
    public async Task AcademicResults_WithMissingOrInvalidParams_Return400BadRequestWithApiErrorNot500(string url)
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcademicResults_MissingRequiredQueryParams_ReturnStandardApiErrorCodes()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");

        var noBatch = await _client.GetAsync("/api/academic-affairs/results?phase=Final");
        Assert.Equal(HttpStatusCode.BadRequest, noBatch.StatusCode);
        Assert.Equal("AcademicResults.InvalidBatchId", await ReadErrorCodeAsync(noBatch));

        var noPhase = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.BadRequest, noPhase.StatusCode);
        Assert.Equal("AcademicResults.InvalidPhase", await ReadErrorCodeAsync(noPhase));

        var detailNoPhase = await _client.GetAsync($"/api/academic-affairs/results/{_groupId}");
        Assert.Equal(HttpStatusCode.BadRequest, detailNoPhase.StatusCode);
        Assert.Equal("AcademicResults.InvalidPhase", await ReadErrorCodeAsync(detailNoPhase));

        var exportNoFormat = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.BadRequest, exportNoFormat.StatusCode);
        Assert.Equal("AcademicResults.InvalidFormat", await ReadErrorCodeAsync(exportNoFormat));
    }

    [Fact]
    public async Task AcademicResults_NonExistentOrNonGuidGroupId_Returns404()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/academic-affairs/results/{Guid.NewGuid()}?phase=Final")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/academic-affairs/results/{Guid.Empty}?phase=Final")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/academic-affairs/results/not-a-guid?phase=Final")).StatusCode);
    }

    #endregion

    #region B. CouncilEndpoints HTTP Malformed / Wrong Type Boundary QA

    [Theory]
    [InlineData("POST", "/api/councils", """{"batchId": "not-a-guid"}""")]
    [InlineData("POST", "/api/councils", """{"batchId": "11111111-1111-1111-1111-111111111111", "councilCode": "HD-1", "defenseDate": "not-a-date", "timeSlot": "08:00", "room": "A1"}""")]
    [InlineData("POST", "/api/councils", """{bad json""")]
    [InlineData("POST", "/api/councils", "null")]
    [InlineData("POST", "/api/councils", "")]
    [InlineData("POST", "/api/councils/11111111-1111-1111-1111-111111111111/assignments", """{"assignments": [{"groupId": "11111111-1111-1111-1111-111111111111", "orderNumber": "first"}]}""")]
    [InlineData("PUT", "/api/councils/11111111-1111-1111-1111-111111111111", """{"defenseDate": "2026-13-45"}""")]
    [InlineData("POST", "/api/councils/assignments/11111111-1111-1111-1111-111111111111/scores", """{"details": [{"criteriaId": "not-a-guid", "score": "ten"}]}""")]
    [InlineData("POST", "/api/councils/assignments/11111111-1111-1111-1111-111111111111/scores", "null")]
    [InlineData("POST", "/api/councils/assignments/11111111-1111-1111-1111-111111111111/scores", "")]
    [InlineData("POST", "/api/councils/11111111-1111-1111-1111-111111111111/members", """{"members": [{"userId": "not-a-guid", "role": "Chair"}]}""")]
    [InlineData("PUT", "/api/councils/11111111-1111-1111-1111-111111111111/members", """{"members": "invalid"}""")]
    [InlineData("PUT", "/api/councils/11111111-1111-1111-1111-111111111111/approve", """{"approved": "yes"}""")]
    [InlineData("PUT", "/api/councils/assignments/11111111-1111-1111-1111-111111111111/minute", """{bad json""")]
    [InlineData("POST", "/api/councils/assignments/11111111-1111-1111-1111-111111111111/minute/lock", """{"lockVersion": "one"}""")]
    [InlineData("POST", "/api/councils/assignments/11111111-1111-1111-1111-111111111111/minute/unlock", """{"reason": 123}""")]
    public async Task CouncilEndpoints_WithMalformedOrTypeMismatchedJson_Return400BadRequestNot500(string method, string path, string rawJson)
    {
        Authenticate(Guid.NewGuid(), "Admin");
        using var req = new HttpRequestMessage(new HttpMethod(method), path);
        req.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    private sealed class QaCouncilAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CouncilHttpQaScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer test.", StringComparison.Ordinal))
                return Task.FromResult(AuthenticateResult.NoResult());

            var parts = header["Bearer test.".Length..].Split('.');
            if (parts.Length < 2 || !Guid.TryParseExact(parts[0], "N", out var userId))
                return Task.FromResult(AuthenticateResult.Fail("Invalid token."));

            var role = parts[1];
            var claims = new List<Claim>
            {
                new("sub", userId.ToString()),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, role),
                new("role", role)
            };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
        }
    }

    private sealed class QaAcademicResultsThesisReader : IAcademicResultsThesisReader
    {
        public List<AcademicResultGroupSnapshotContractDto> Groups { get; } = [];
        public Task<IReadOnlyList<AcademicResultGroupSnapshotContractDto>> GetGroupsByBatchIdAsync(Guid batchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AcademicResultGroupSnapshotContractDto>>(Groups.Where(g => g.BatchId == batchId).ToList());
        public Task<AcademicResultGroupSnapshotContractDto?> GetGroupByIdAsync(Guid groupId, CancellationToken ct = default)
            => Task.FromResult(Groups.FirstOrDefault(g => g.GroupId == groupId));
    }

    private sealed class QaStudentSearchReader : IStudentSearchReader
    {
        public Task<IReadOnlyList<Guid>> FindIdsByNameOrCodeAsync(string keyword, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class QaScoreSummaryStore : IScoreSummaryStore
    {
        public Task<IReadOnlyDictionary<Guid, ScoreFacts>> FactsAsync(IReadOnlyCollection<Guid> groups, bool locked, CancellationToken token)
            => Task.FromResult<IReadOnlyDictionary<Guid, ScoreFacts>>(groups.ToDictionary(g => g, _ => new ScoreFacts([], [], [], [], [], [], [], [], [])));
        public Task<IReadOnlyDictionary<Guid, ScoreHistory>> HistoriesAsync(IReadOnlyCollection<Guid> groups, CancellationToken token)
            => Task.FromResult<IReadOnlyDictionary<Guid, ScoreHistory>>(new Dictionary<Guid, ScoreHistory>());
        public Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummaryIdempotency?> ReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.FromResult<ScoreSummaryIdempotency?>(null);
        public Task LockAdvisorsAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummary?> SummaryAsync(Guid group, bool locked, CancellationToken token) => Task.FromResult<ScoreSummary?>(null);
        public Task<Guid?> AssignmentGroupAsync(Guid assignment, CancellationToken token) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyList<Guid>> CouncilGroupsAsync(Guid council, CancellationToken token) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<bool> PublishedAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.FromResult(false);
        public void Add(ScoreSummary summary) { }
        public void Add(ScoreSummaryRevision revision, IReadOnlyList<ScoreSummaryComponent> components) { }
        public void Add(ScoreSummaryIdempotency receipt) { }
    }

    private sealed class QaArchiveProofContract : IFinalSubmissionArchiveProofContract
    {
        public Task<FinalSubmissionArchiveProofContractDto> GetArchiveProofAsync(Guid groupId, CancellationToken ct = default)
            => Task.FromResult(new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, groupId, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, []));
        public Task<IReadOnlyList<FinalSubmissionArchiveProofContractDto>> GetManyArchiveProofsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FinalSubmissionArchiveProofContractDto>>(groupIds.Select(id => new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, id, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, [])).ToList());
        public Task<FinalSubmissionArchiveProofContractDto> LockArchiveProofForCalculationAsync(Guid groupId, CancellationToken ct = default)
            => GetArchiveProofAsync(groupId, ct);
    }
}
