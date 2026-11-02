using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;
using Xunit;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilScoringHttpIntegrationTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private readonly FakeCouncilRepository _councilRepo = new();
    private readonly FakeDefenseCouncilUnitOfWork _uow = new();
    private readonly FakeBatchReader _batchReader = new();
    private readonly FakeCouncilUserInfoReader _userInfoReader = new();
    private readonly FakeStudentInfoReader _studentInfoReader = new();
    private readonly FakeDefenseGroupContract _defenseGroupContract = new();
    private readonly FakeTopicDefenseContract _topicDefenseContract = new();
    private readonly FakeDefenseCouncilAuditOutbox _auditOutbox = new();

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _councilId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _assignmentId = Guid.NewGuid();

    private readonly Guid _chairmanUserId = Guid.NewGuid();
    private readonly Guid _secretaryUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly Guid _outsideUserId = Guid.NewGuid();

    private readonly Guid _crit1Id = Guid.NewGuid();
    private readonly Guid _crit2Id = Guid.NewGuid();

    private Council _council = null!;
    private CouncilMember _chairmanMember = null!;
    private CouncilMember _secretaryMember = null!;
    private CouncilMember _regularMember = null!;
    private CouncilAssignment _assignment = null!;

    public async Task InitializeAsync()
    {
        _batchReader.ExistingBatches.Add(_batchId);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Domain & Application Services
        builder.Services.AddSingleton<ThesisGradeCalculator>();
        builder.Services.AddScoped<IGradeCalculationService, GradeCalculationService>();

        // Fake Repositories
        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IDefenseCouncilUnitOfWork>(_uow);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentInfoReader);
        builder.Services.AddSingleton<IDefenseGroupContract>(_defenseGroupContract);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicDefenseContract);
        builder.Services.AddSingleton<IDefenseCouncilAuditOutbox>(_auditOutbox);

        // Handlers
        builder.Services.AddScoped<CreateCouncilHandler>();
        builder.Services.AddScoped<AddCouncilMembersHandler>();
        builder.Services.AddScoped<AssignCouncilGroupsHandler>();
        builder.Services.AddScoped<ApproveCouncilHandler>();
        builder.Services.AddScoped<GetCouncilScheduleHandler>();
        builder.Services.AddScoped<SubmitCouncilScoreHandler>();
        builder.Services.AddScoped<CheckCouncilDiscrepancyHandler>();
        builder.Services.AddSingleton<UGTM.Modules.DefenseCouncil.Application.ScoreSummaries.ICouncilScoreWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddScoped<UGTM.Modules.Identity.Contracts.FinalSubmissions.IFinalSubmissionAuthorityReader, TestScoreAuthority>();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddAuthentication(TestScoringAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestScoringAuthHandler>(TestScoringAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapCouncilEndpoints();

        await _app.StartAsync();
        _client = _app.GetTestClient();

        SetupTestData();
    }

    private void SetupTestData()
    {
        _council = new Council
        {
            Id = _councilId,
            BatchId = _batchId,
            CouncilCode = "HD-TEST-SCORE",
            DefenseDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            TimeSlot = "08:00 - 11:30",
            Room = "P.302",
            Status = CouncilStatus.Active
        };
        _councilRepo.Councils.Add(_council);

        _chairmanMember = new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = _councilId,
            UserId = _chairmanUserId,
            RoleInCouncil = CouncilMemberRole.Chairman
        };
        _secretaryMember = new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = _councilId,
            UserId = _secretaryUserId,
            RoleInCouncil = CouncilMemberRole.Secretary
        };
        _regularMember = new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = _councilId,
            UserId = _memberUserId,
            RoleInCouncil = CouncilMemberRole.Member
        };
        _councilRepo.Members.AddRange([_chairmanMember, _secretaryMember, _regularMember]);

        _assignment = new CouncilAssignment
        {
            Id = _assignmentId,
            CouncilId = _councilId,
            GroupId = _groupId,
            OrderNumber = 1
        };
        _councilRepo.Assignments.Add(_assignment);

        _councilRepo.GradingCriteriaList.AddRange([
            new GradingCriteria { Id = _crit1Id, BatchId = _batchId, CriteriaCode = "C1", CriteriaName = "Nội dung khóa luận", MaxScore = 6.0m, OrderIndex = 1 },
            new GradingCriteria { Id = _crit2Id, BatchId = _batchId, CriteriaCode = "C2", CriteriaName = "Kỹ năng báo cáo", MaxScore = 4.0m, OrderIndex = 2 }
        ]);

        _userInfoReader.Users[_chairmanUserId] = new CouncilUserInfoContractDto(_chairmanUserId, "GS. Chủ Tịch", "chutich@uit.edu.vn", "GV001", true);
        _userInfoReader.Users[_secretaryUserId] = new CouncilUserInfoContractDto(_secretaryUserId, "TS. Thư Ký", "thuky@uit.edu.vn", "GV002", true);
        _userInfoReader.Users[_memberUserId] = new CouncilUserInfoContractDto(_memberUserId, "ThS. Ủy Viên", "uyvien@uit.edu.vn", "GV003", true);
        _userInfoReader.Users[_outsideUserId] = new CouncilUserInfoContractDto(_outsideUserId, "GV Ngoài", "ngoai@uit.edu.vn", "GV099", true);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    private void Authenticate(Guid userId, string role = "Lecturer")
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"test.{userId:N}.{role}");
    }

    private void ClearAuthentication()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static async Task<(string Code, string Message)> ReadErrorAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }

    // =========================================================================
    // POST /api/councils/assignments/{assignmentId}/scores E2E Tests
    // =========================================================================

    [Fact]
    public async Task SubmitScore_WithoutToken_ReturnsUnauthorized()
    {
        ClearAuthentication();
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([new(_crit1Id, 5.0m), new(_crit2Id, 3.0m)]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitScore_OutsideLecturer_ReturnsForbidden()
    {
        Authenticate(_outsideUserId);
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([new(_crit1Id, 5.0m), new(_crit2Id, 3.0m)]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await ReadErrorAsync(response);
        Assert.Equal("Council.UserNotCouncilMember", error.Code);
    }

    [Fact]
    public async Task SubmitScore_CouncilDraft_ReturnsConflict()
    {
        _council.Status = CouncilStatus.Draft;
        Authenticate(_chairmanUserId);

        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([new(_crit1Id, 5.0m), new(_crit2Id, 3.0m)]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await ReadErrorAsync(response);
        Assert.Equal("Council.NotActiveForScoring", error.Code);
    }

    [Fact]
    public async Task SubmitScore_GVPBKiêmỦyViên_TH1_BR07_ReturnsConflict()
    {
        // GVPB is _memberUserId
        _councilRepo.ActiveReviewerAssignments[_groupId] = ReviewerAssignment.Create(
            Guid.NewGuid(), _groupId, _memberUserId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, Guid.NewGuid(), new string('a', 64), Guid.NewGuid(), DateTime.UtcNow);

        Authenticate(_memberUserId);
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([new(_crit1Id, 5.0m), new(_crit2Id, 3.0m)]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await ReadErrorAsync(response);
        Assert.Equal("Council.ReviewerCouncilMemberCannotScoreTwice", error.Code);
    }

    [Fact]
    public async Task SubmitScore_ExceedsMaxScore_ReturnsBadRequest()
    {
        Authenticate(_chairmanUserId);
        // Crit1 max score is 6.0m; pass 6.5m
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([new(_crit1Id, 6.5m), new(_crit2Id, 3.0m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadErrorAsync(response);
        Assert.Equal("Council.ScoreExceedsMaxScore", error.Code);
    }

    [Fact]
    public async Task SubmitScore_ValidInput_ReturnsOkAndPersistsScore()
    {
        Authenticate(_chairmanUserId);
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto(
                [new(_crit1Id, 5.5m), new(_crit2Id, 3.5m)],
                Feedback: "Báo cáo xuất sắc, trả lời thuyết phục."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.Equal(9.0m, data.GetProperty("score").GetDecimal());
        Assert.Equal("Báo cáo xuất sắc, trả lời thuyết phục.", data.GetProperty("feedback").GetString());
        Assert.Equal(_chairmanUserId, data.GetProperty("userId").GetGuid());

        var details = data.GetProperty("details").EnumerateArray().ToList();
        Assert.Equal(2, details.Count);
    }

    [Fact]
    public async Task SubmitScore_Rescore_UpdatesExistingScore()
    {
        Authenticate(_chairmanUserId);

        // First submit: 8.0
        await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([new(_crit1Id, 5.0m), new(_crit2Id, 3.0m)]));

        // Rescore: 9.2
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([new(_crit1Id, 5.7m), new(_crit2Id, 3.5m)], Feedback: "Cập nhật sau phản biện"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.Equal(9.2m, data.GetProperty("score").GetDecimal());
        Assert.Equal("Cập nhật sau phản biện", data.GetProperty("feedback").GetString());
    }

    // =========================================================================
    // GET /api/councils/assignments/{assignmentId}/discrepancy-check E2E Tests
    // =========================================================================

    [Fact]
    public async Task CheckDiscrepancy_WithoutToken_ReturnsUnauthorized()
    {
        ClearAuthentication();
        var response = await _client.GetAsync($"/api/councils/assignments/{_assignmentId}/discrepancy-check");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CheckDiscrepancy_SmallDifference_ReturnsNoDiscrepancy()
    {
        // Chairman 8.5, Secretary 8.0 -> difference = 0.5 < 2.0
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 8.5m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 8.0m }
        ]);

        Authenticate(_chairmanUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{_assignmentId}/discrepancy-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.False(data.GetProperty("isDiscrepancyDetected").GetBoolean());
        Assert.Equal(0.5m, data.GetProperty("scoreDifference").GetDecimal());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("warningMessage").ValueKind);
    }

    [Fact]
    public async Task CheckDiscrepancy_DifferenceGreaterThanTwo_TriggersWarningFlag()
    {
        // Chairman 9.5, Secretary 7.0 -> difference = 2.5 >= 2.0
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 9.5m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.0m }
        ]);

        Authenticate(_secretaryUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{_assignmentId}/discrepancy-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.True(data.GetProperty("isDiscrepancyDetected").GetBoolean());
        Assert.Equal(2.5m, data.GetProperty("scoreDifference").GetDecimal());
        Assert.Equal(9.5m, data.GetProperty("maxScore").GetDecimal());
        Assert.Equal(7.0m, data.GetProperty("minScore").GetDecimal());
        Assert.NotNull(data.GetProperty("warningMessage").GetString());
        Assert.Contains("2.50", data.GetProperty("warningMessage").GetString()!);
    }

    [Fact]
    public async Task CheckDiscrepancy_WhenReviewerInCouncil_IncludesReviewerScoreInCalculation()
    {
        // GVPB is _regularMember with score 6.5. Chairman is 8.8. Difference = 2.3 >= 2.0 -> Discrepancy warning!
        var revAssignment = ReviewerAssignment.Create(
            Guid.NewGuid(), _groupId, _memberUserId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, Guid.NewGuid(), new string('c', 64), Guid.NewGuid(), DateTime.UtcNow);
        var revEval = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssignment.Id, 6.5m, "Feedback GVPB", DefenseApprovalStatus.Approved, _memberUserId, DateTime.UtcNow);
        revEval.TrySubmit(6.5m, "Feedback GVPB", DefenseApprovalStatus.Approved, _memberUserId, DateTime.UtcNow);
        typeof(ReviewerAssignment).GetProperty("Evaluation")!.SetValue(revAssignment, revEval);
        _councilRepo.ActiveReviewerAssignments[_groupId] = revAssignment;

        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = _assignmentId,
            CouncilMemberId = _chairmanMember.Id,
            Score = 8.8m
        });

        Authenticate(_chairmanUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{_assignmentId}/discrepancy-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.True(data.GetProperty("isDiscrepancyDetected").GetBoolean());
        Assert.Equal(2.3m, data.GetProperty("scoreDifference").GetDecimal());

        var members = data.GetProperty("memberScores").EnumerateArray().ToList();
        var reviewerMember = members.FirstOrDefault(m => m.GetProperty("userId").GetGuid() == _memberUserId);
        Assert.True(reviewerMember.GetProperty("isReviewerInCouncil").GetBoolean());
        Assert.True(reviewerMember.GetProperty("isBlockedFromScoring").GetBoolean());
        Assert.Equal(6.5m, reviewerMember.GetProperty("score").GetDecimal());
    }

    [Fact]
    public async Task SubmitScore_WhenDuplicateCriteriaInRequest_ReturnsBadRequest()
    {
        Authenticate(_chairmanUserId);
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([
                new(_crit1Id, 3.0m),
                new(_crit1Id, 2.0m),
                new(_crit2Id, 3.0m)
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadErrorAsync(response);
        Assert.Equal("Council.DuplicateCriteriaInRequest", error.Code);
    }

    [Fact]
    public async Task SubmitScore_BoundaryZeroAndMaxScore_ReturnsOk()
    {
        Authenticate(_chairmanUserId);
        var response = await _client.PostAsJsonAsync($"/api/councils/assignments/{_assignmentId}/scores",
            new SubmitCouncilScoreInputDto([
                new(_crit1Id, 6.0m), // max of crit1
                new(_crit2Id, 0.0m)  // min of crit2
            ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.Equal(6.0m, data.GetProperty("score").GetDecimal());
    }

    [Fact]
    public async Task CheckDiscrepancy_BoundaryExactTwoPointZero_ReturnsDiscrepancyDetected()
    {
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 9.0m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.0m }
        ]);

        Authenticate(_chairmanUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{_assignmentId}/discrepancy-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.True(data.GetProperty("isDiscrepancyDetected").GetBoolean());
        Assert.Equal(2.0m, data.GetProperty("scoreDifference").GetDecimal());
    }

    [Fact]
    public async Task CheckDiscrepancy_BoundaryOnePointNineNine_ReturnsNoDiscrepancy()
    {
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 8.99m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.00m }
        ]);

        Authenticate(_chairmanUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{_assignmentId}/discrepancy-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.False(data.GetProperty("isDiscrepancyDetected").GetBoolean());
        Assert.Equal(1.99m, data.GetProperty("scoreDifference").GetDecimal());
    }

    private sealed class TestScoringAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScoringTestAuthScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer test.", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = header["Bearer test.".Length..].Split('.');
            if (parts.Length < 2 || !Guid.TryParseExact(parts[0], "N", out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test bearer token."));
            }

            var role = parts[1];
            var claims = new List<Claim>
            {
                new("sub", userId.ToString()),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, role),
                new("role", role)
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
