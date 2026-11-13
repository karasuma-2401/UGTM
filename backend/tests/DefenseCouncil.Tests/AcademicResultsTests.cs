using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResultDetail;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.DefenseCouncil.Infrastructure.Exporting;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class AcademicResultsTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private readonly FakeAcademicResultsThesisReader _thesisReader = new();
    private readonly FakeTopicDefenseContract _topicContract = new();
    private readonly FakeCouncilUserInfoReader _userInfoReader = new();
    private readonly FakeStudentSearchReader _studentSearchReader = new();
    private readonly FakeCouncilRepository _councilRepo = new();
    private readonly FakeScoreSummaryStore _scoreStore = new();
    private readonly FakeArchiveProofContract _archiveProofContract = new();

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _group1Id = Guid.NewGuid();
    private readonly Guid _group2Id = Guid.NewGuid();
    private readonly Guid _group3Id = Guid.NewGuid();
    private readonly Guid _group4Id = Guid.NewGuid();

    private readonly Guid _student1Id = Guid.NewGuid();
    private readonly Guid _student2Id = Guid.NewGuid();
    private readonly Guid _student3Id = Guid.NewGuid();
    private readonly Guid _student4Id = Guid.NewGuid();
    private readonly Guid _student5Id = Guid.NewGuid();

    private readonly Guid _topic1Id = Guid.NewGuid();
    private readonly Guid _topic2Id = Guid.NewGuid();
    private readonly Guid _topic3Id = Guid.NewGuid();
    private readonly Guid _topic4Id = Guid.NewGuid();

    private readonly Guid _advisor1Id = Guid.NewGuid();
    private readonly Guid _reviewer1Id = Guid.NewGuid();
    private readonly Guid _chairmanId = Guid.NewGuid();
    private readonly Guid _secretaryId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        SeedData();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Register fake services
        builder.Services.AddSingleton<IAcademicResultsThesisReader>(_thesisReader);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicContract);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentSearchReader>(_studentSearchReader);
        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IScoreSummaryStore>(_scoreStore);
        builder.Services.AddSingleton<IFinalSubmissionArchiveProofContract>(_archiveProofContract);

        // Application services
        builder.Services.AddScoped<AcademicResultsWorkspaceLoader>();
        builder.Services.AddScoped<IAcademicResultsExportFileGenerator, AcademicResultsExportFileGenerator>();
        builder.Services.AddScoped<GetAcademicResultsHandler>();
        builder.Services.AddScoped<GetAcademicResultDetailHandler>();
        builder.Services.AddScoped<ExportAcademicResultsHandler>();

        builder.Services.AddAuthentication(TestResultsAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestResultsAuthHandler>(TestResultsAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapAcademicAffairsResultEndpoints();

        await _app.StartAsync();
        _client = _app.GetTestClient();
        _client.BaseAddress = new Uri("http://localhost");
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    private void SeedData()
    {
        // 1. Users
        _userInfoReader.Users[_student1Id] = new CouncilUserInfoContractDto(_student1Id, "Nguyễn Văn An", "an@uit.edu.vn", "22520001", true);
        _userInfoReader.Users[_student2Id] = new CouncilUserInfoContractDto(_student2Id, "Trần Thị Bình", "binh@uit.edu.vn", "22520002", true);
        _userInfoReader.Users[_student3Id] = new CouncilUserInfoContractDto(_student3Id, "Lê Hoàng Cường", "cuong@uit.edu.vn", "22520003", true);
        _userInfoReader.Users[_student4Id] = new CouncilUserInfoContractDto(_student4Id, "Phạm Minh Dũng", "dung@uit.edu.vn", "22520004", true);
        _userInfoReader.Users[_student5Id] = new CouncilUserInfoContractDto(_student5Id, "Võ Thị Em", "em@uit.edu.vn", "22520005", true);

        _userInfoReader.Users[_advisor1Id] = new CouncilUserInfoContractDto(_advisor1Id, "TS. Đặng Hướng Dẫn", "hd@uit.edu.vn", "GV01", true);
        _userInfoReader.Users[_reviewer1Id] = new CouncilUserInfoContractDto(_reviewer1Id, "TS. Phan Phản Biện", "pb@uit.edu.vn", "GV02", true);
        _userInfoReader.Users[_chairmanId] = new CouncilUserInfoContractDto(_chairmanId, "PGS. TS. Trần Chủ Tịch", "ct@uit.edu.vn", "GV03", true);
        _userInfoReader.Users[_secretaryId] = new CouncilUserInfoContractDto(_secretaryId, "ThS. Hoàng Thư Ký", "tk@uit.edu.vn", "GV04", true);

        // 2. Topics
        _topicContract.Topics[_topic1Id] = new TopicDefenseDetailsContractDto(_topic1Id, _batchId, _advisor1Id, "Hệ thống AI nhận diện giọng nói", "AI Voice Recognition System");
        _topicContract.Topics[_topic2Id] = new TopicDefenseDetailsContractDto(_topic2Id, _batchId, _advisor1Id, "Nền tảng Blockchain quản lý chuỗi cung ứng", "Blockchain Supply Chain Platform");
        _topicContract.Topics[_topic3Id] = new TopicDefenseDetailsContractDto(_topic3Id, _batchId, _advisor1Id, "Ứng dụng IoT giám sát nông nghiệp thông minh", "IoT Smart Agriculture App");
        _topicContract.Topics[_topic4Id] = new TopicDefenseDetailsContractDto(_topic4Id, _batchId, _advisor1Id, "Xây dựng Microservices với .NET Core", "Building Microservices with .NET Core");

        // 3. Groups
        // Nhóm 1: 2 sinh viên, Giữa kỳ: Approved, Cuối kỳ: Published
        var g1Members = new List<DefenseGroupMemberContractDto>
        {
            new(_student1Id, _batchId, "Confirmed"),
            new(_student2Id, _batchId, "Confirmed")
        };
        var g1Midterm = new AcademicResultMidtermSnapshotContractDto(Guid.NewGuid(), "Approved", 1, "Báo cáo tốt", _advisor1Id, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-30));
        _thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(_group1Id, _topic1Id, _batchId, "InProgress", DateTime.UtcNow.AddDays(-60), g1Members, g1Midterm, "ArchiveConfirmed"));

        // Nhóm 2: 1 sinh viên, Giữa kỳ: Rejected, Cuối kỳ: Ready to publish (Đủ điểm + ArchiveConfirmed)
        var g2Members = new List<DefenseGroupMemberContractDto>
        {
            new(_student3Id, _batchId, "Confirmed")
        };
        var g2Midterm = new AcademicResultMidtermSnapshotContractDto(Guid.NewGuid(), "Rejected", 1, "Nội dung chưa đạt", _advisor1Id, DateTime.UtcNow.AddDays(-28), DateTime.UtcNow.AddDays(-28));
        _thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(_group2Id, _topic2Id, _batchId, "Stopped", DateTime.UtcNow.AddDays(-59), g2Members, g2Midterm, "ArchiveConfirmed"));

        // Nhóm 3: 1 sinh viên, Giữa kỳ: Submitted, Cuối kỳ: Missing Archive (Đủ điểm nhưng chưa lưu chiểu)
        var g3Members = new List<DefenseGroupMemberContractDto>
        {
            new(_student4Id, _batchId, "Confirmed")
        };
        var g3Midterm = new AcademicResultMidtermSnapshotContractDto(Guid.NewGuid(), "Submitted", 1, null, null, null, DateTime.UtcNow.AddDays(-20));
        _thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(_group3Id, _topic3Id, _batchId, "InProgress", DateTime.UtcNow.AddDays(-58), g3Members, g3Midterm, "Submitted"));

        // Nhóm 4: 1 sinh viên, Giữa kỳ: null (chưa nộp), Cuối kỳ: Missing Score (Chưa đủ điểm)
        var g4Members = new List<DefenseGroupMemberContractDto>
        {
            new(_student5Id, _batchId, "Confirmed")
        };
        _thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(_group4Id, _topic4Id, _batchId, "Registered", DateTime.UtcNow.AddDays(-57), g4Members, null, "NotSubmitted"));

        // 4. Council & Assignments
        var council = Council.Create(_batchId, "HD01", new DateOnly(2026, 12, 20), "08:00 - 11:30", "B1.401");
        var cm1 = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = _chairmanId, RoleInCouncil = CouncilMemberRole.Chairman };
        var sm1 = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = _secretaryId, RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(cm1);
        council.Members.Add(sm1);
        _councilRepo.Members.AddRange([cm1, sm1]);

        var assign1 = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = _group1Id, OrderNumber = 1, Council = council };
        var assign2 = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = _group2Id, OrderNumber = 2, Council = council };
        var assign3 = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = _group3Id, OrderNumber = 3, Council = council };

        council.Assignments.Add(assign1);
        council.Assignments.Add(assign2);
        council.Assignments.Add(assign3);
        _councilRepo.Assignments.AddRange([assign1, assign2, assign3]);
        _councilRepo.Councils.Add(council);

        // 5. Score Facts & Summaries
        var g1Minute = new CouncilMinute { Id = Guid.NewGuid(), CouncilAssignmentId = assign1.Id, LockStatus = CouncilMinuteLockStatus.Locked, LockedAt = DateTime.UtcNow, Assignment = assign1 };
        assign1.Minute = g1Minute;

        var g2Minute = new CouncilMinute { Id = Guid.NewGuid(), CouncilAssignmentId = assign2.Id, LockStatus = CouncilMinuteLockStatus.Locked, LockedAt = DateTime.UtcNow, Assignment = assign2 };
        assign2.Minute = g2Minute;

        var g3Minute = new CouncilMinute { Id = Guid.NewGuid(), CouncilAssignmentId = assign3.Id, LockStatus = CouncilMinuteLockStatus.Locked, LockedAt = DateTime.UtcNow, Assignment = assign3 };
        assign3.Minute = g3Minute;

        _councilRepo.Minutes.AddRange([g1Minute, g2Minute, g3Minute]);

        var g1Scores = council.Members.Select(m => new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign1.Id,
            CouncilMemberId = m.Id,
            Score = 8.5m,
            EvaluatedAt = DateTime.UtcNow
        }).ToList();

        var g2Scores = council.Members.Select(m => new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign2.Id,
            CouncilMemberId = m.Id,
            Score = 7.5m,
            EvaluatedAt = DateTime.UtcNow
        }).ToList();

        var g3Scores = council.Members.Select(m => new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign3.Id,
            CouncilMemberId = m.Id,
            Score = 8.0m,
            EvaluatedAt = DateTime.UtcNow
        }).ToList();

        // Advisor eval
        var g1Adv = AdvisorEvaluation.Create(Guid.NewGuid(), _group1Id, Guid.NewGuid(), 1, Guid.NewGuid(), new string('0', 64), 9.0m, "Tốt", _advisor1Id, DateTime.UtcNow);
        var g2Adv = AdvisorEvaluation.Create(Guid.NewGuid(), _group2Id, Guid.NewGuid(), 1, Guid.NewGuid(), new string('0', 64), 7.0m, "Khá", _advisor1Id, DateTime.UtcNow);
        var g3Adv = AdvisorEvaluation.Create(Guid.NewGuid(), _group3Id, Guid.NewGuid(), 1, Guid.NewGuid(), new string('0', 64), 8.0m, "Khá giỏi", _advisor1Id, DateTime.UtcNow);

        // Reviewer eval
        var revAssign1 = ReviewerAssignment.Create(Guid.NewGuid(), _group1Id, _reviewer1Id, _advisor1Id, Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), new string('0', 64), Guid.NewGuid(), DateTime.UtcNow);
        var revAssign2 = ReviewerAssignment.Create(Guid.NewGuid(), _group2Id, _reviewer1Id, _advisor1Id, Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), new string('0', 64), Guid.NewGuid(), DateTime.UtcNow);
        var revAssign3 = ReviewerAssignment.Create(Guid.NewGuid(), _group3Id, _reviewer1Id, _advisor1Id, Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), new string('0', 64), Guid.NewGuid(), DateTime.UtcNow);

        var g1Rev = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssign1.Id, 8.5m, "Góp ý tốt", DefenseApprovalStatus.Approved, _reviewer1Id, DateTime.UtcNow);
        g1Rev.TrySubmit(8.5m, "Góp ý tốt", DefenseApprovalStatus.Approved, _reviewer1Id, DateTime.UtcNow);

        var g2Rev = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssign2.Id, 7.5m, "Đạt", DefenseApprovalStatus.Approved, _reviewer1Id, DateTime.UtcNow);
        g2Rev.TrySubmit(7.5m, "Đạt", DefenseApprovalStatus.Approved, _reviewer1Id, DateTime.UtcNow);

        var g3Rev = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssign3.Id, 8.0m, "Đạt tốt", DefenseApprovalStatus.Approved, _reviewer1Id, DateTime.UtcNow);
        g3Rev.TrySubmit(8.0m, "Đạt tốt", DefenseApprovalStatus.Approved, _reviewer1Id, DateTime.UtcNow);

        _scoreStore.Facts[_group1Id] = new ScoreFacts([g1Adv], [revAssign1], [g1Rev], [assign1], [council], council.Members.ToList(), [g1Minute], g1Scores, []);
        _scoreStore.Facts[_group2Id] = new ScoreFacts([g2Adv], [revAssign2], [g2Rev], [assign2], [council], council.Members.ToList(), [g2Minute], g2Scores, []);
        _scoreStore.Facts[_group3Id] = new ScoreFacts([g3Adv], [revAssign3], [g3Rev], [assign3], [council], council.Members.ToList(), [g3Minute], g3Scores, []);
        _scoreStore.Facts[_group4Id] = new ScoreFacts([], [], [], [], [], [], [], [], []);

        // Group 1 summary: Published
        var sum1 = new ScoreSummary
        {
            Id = Guid.NewGuid(),
            GroupId = _group1Id,
            Status = ScoreSummaryStatus.PublishedToStudent,
            FinalScore = 8.65m,
            PublishedAt = DateTime.UtcNow
        };
        _scoreStore.Histories[_group1Id] = new ScoreHistory(sum1, [], []);

        // Group 2 summary: Calculated (Ready to publish)
        var sum2 = new ScoreSummary
        {
            Id = Guid.NewGuid(),
            GroupId = _group2Id,
            Status = ScoreSummaryStatus.Calculated,
            FinalScore = 7.35m
        };
        _scoreStore.Histories[_group2Id] = new ScoreHistory(sum2, [], []);

        // Group 3 summary: Calculated (Missing Archive)
        var sum3 = new ScoreSummary
        {
            Id = Guid.NewGuid(),
            GroupId = _group3Id,
            Status = ScoreSummaryStatus.Calculated,
            FinalScore = 8.00m
        };
        _scoreStore.Histories[_group3Id] = new ScoreHistory(sum3, [], []);

        // Archive Proofs
        _archiveProofContract.Proofs[_group1Id] = new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Succeeded, _group1Id, _batchId, Guid.NewGuid(), 1, Guid.NewGuid(), 1, "manifest", Guid.NewGuid(), Guid.NewGuid(), _advisor1Id, DateTime.UtcNow, Guid.NewGuid(), DateTime.UtcNow, []);
        _archiveProofContract.Proofs[_group2Id] = new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Succeeded, _group2Id, _batchId, Guid.NewGuid(), 1, Guid.NewGuid(), 1, "manifest", Guid.NewGuid(), Guid.NewGuid(), _advisor1Id, DateTime.UtcNow, Guid.NewGuid(), DateTime.UtcNow, []);
        _archiveProofContract.Proofs[_group3Id] = new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.NotConfirmed, _group3Id, _batchId, null, null, null, null, null, null, null, null, null, null, null, []);
        _archiveProofContract.Proofs[_group4Id] = new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, _group4Id, _batchId, null, null, null, null, null, null, null, null, null, null, null, []);
    }

    private void Authenticate(Guid userId, string role)
    {
        var token = $"test.{userId:N}.{role}";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    #region API 1: GET /api/academic-affairs/results Tests

    [Fact]
    public async Task GetResults_Returns401_WhenUnauthenticated()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Lecturer")]
    [InlineData("CouncilMember")]
    public async Task GetResults_Returns403_WhenRoleIsNotAuthorized(string role)
    {
        Authenticate(Guid.NewGuid(), role);
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetResults_Returns400_WhenBatchIdIsEmpty()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={Guid.Empty}&phase=Final");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("AcademicResults.InvalidBatchId", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("InvalidPhase")]
    [InlineData("Semester1")]
    public async Task GetResults_Returns400_WhenPhaseIsInvalid(string invalidPhase)
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase={invalidPhase}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("AcademicResults.InvalidPhase", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetResults_Returns400_WhenStatusIsInvalidForPhase()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        // Status 'PUBLISHED' is invalid for phase Midterm
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Midterm&status=PUBLISHED");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("AcademicResults.InvalidStatus", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetResults_Midterm_ReturnsCorrectStatusesAndLabels()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Midterm");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var items = data.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(4, items.Count);

        // Nhóm 1: Approved -> CONTINUED
        var item1 = items.First(i => i.GetProperty("groupId").GetGuid() == _group1Id);
        Assert.Equal("N01", item1.GetProperty("groupCode").GetString());
        Assert.Equal("CONTINUED", item1.GetProperty("statusCode").GetString());
        Assert.Equal("Tiếp tục", item1.GetProperty("statusLabel").GetString());
        Assert.Equal("Đã duyệt giữa kỳ", item1.GetProperty("detailLabel").GetString());
        Assert.True(item1.GetProperty("finalScore").ValueKind == JsonValueKind.Null);

        // Nhóm 2: Rejected -> STOPPED
        var item2 = items.First(i => i.GetProperty("groupId").GetGuid() == _group2Id);
        Assert.Equal("STOPPED", item2.GetProperty("statusCode").GetString());
        Assert.Equal("Dừng thực hiện", item2.GetProperty("statusLabel").GetString());
        Assert.Equal("Không đạt giữa kỳ", item2.GetProperty("detailLabel").GetString());

        // Nhóm 3: Submitted -> PENDING_MIDTERM ("Chờ Khoa duyệt")
        var item3 = items.First(i => i.GetProperty("groupId").GetGuid() == _group3Id);
        Assert.Equal("PENDING_MIDTERM", item3.GetProperty("statusCode").GetString());
        Assert.Equal("Chờ xét duyệt", item3.GetProperty("statusLabel").GetString());
        Assert.Equal("Chờ Khoa duyệt", item3.GetProperty("detailLabel").GetString());

        // Nhóm 4: Chưa nộp -> PENDING_MIDTERM ("Chưa nộp báo cáo")
        var item4 = items.First(i => i.GetProperty("groupId").GetGuid() == _group4Id);
        Assert.Equal("PENDING_MIDTERM", item4.GetProperty("statusCode").GetString());
        Assert.Equal("Chờ xét duyệt", item4.GetProperty("statusLabel").GetString());
        Assert.Equal("Chưa nộp báo cáo", item4.GetProperty("detailLabel").GetString());
    }

    [Fact]
    public async Task GetResults_Final_ReturnsCorrectStatusesAndScores()
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var items = data.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(4, items.Count);

        // Nhóm 1: Published -> PUBLISHED
        var item1 = items.First(i => i.GetProperty("groupId").GetGuid() == _group1Id);
        Assert.Equal("PUBLISHED", item1.GetProperty("statusCode").GetString());
        Assert.Equal("Khoa đã công bố", item1.GetProperty("statusLabel").GetString());
        Assert.Equal("Lưu chiểu đạt", item1.GetProperty("detailLabel").GetString());
        Assert.Equal(8.65m, item1.GetProperty("finalScore").GetDecimal());

        // Nhóm 2: Calculated + ArchiveConfirmed -> READY_TO_PUBLISH
        var item2 = items.First(i => i.GetProperty("groupId").GetGuid() == _group2Id);
        Assert.Equal("READY_TO_PUBLISH", item2.GetProperty("statusCode").GetString());
        Assert.Equal("Chờ công bố", item2.GetProperty("statusLabel").GetString());
        Assert.Equal("Lưu chiểu đạt", item2.GetProperty("detailLabel").GetString());

        // Nhóm 3: Calculated + Missing Archive -> MISSING_ARCHIVE
        var item3 = items.First(i => i.GetProperty("groupId").GetGuid() == _group3Id);
        Assert.Equal("MISSING_ARCHIVE", item3.GetProperty("statusCode").GetString());
        Assert.Equal("Chưa đủ lưu chiểu", item3.GetProperty("statusLabel").GetString());
        Assert.Equal("Chưa công bố", item3.GetProperty("detailLabel").GetString());

        // Nhóm 4: Chưa đủ điểm -> MISSING_SCORE
        var item4 = items.First(i => i.GetProperty("groupId").GetGuid() == _group4Id);
        Assert.Equal("MISSING_SCORE", item4.GetProperty("statusCode").GetString());
        Assert.Equal("Chưa đủ điểm", item4.GetProperty("statusLabel").GetString());
        Assert.Equal("Chưa công bố", item4.GetProperty("detailLabel").GetString());
    }

    [Fact]
    public async Task GetResults_FiltersByStatus_Successfully()
    {
        Authenticate(Guid.NewGuid(), "Admin");

        // Lọc Giữa kỳ: CONTINUED
        var resContinued = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Midterm&status=CONTINUED");
        var jsonContinued = await resContinued.Content.ReadAsStringAsync();
        using var docContinued = JsonDocument.Parse(jsonContinued);
        var itemsContinued = docContinued.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Single(itemsContinued);
        Assert.Equal(_group1Id, itemsContinued[0].GetProperty("groupId").GetGuid());

        // Lọc Cuối kỳ: PUBLISHED
        var resPublished = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&status=PUBLISHED");
        var jsonPublished = await resPublished.Content.ReadAsStringAsync();
        using var docPublished = JsonDocument.Parse(jsonPublished);
        var itemsPublished = docPublished.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Single(itemsPublished);
        Assert.Equal(_group1Id, itemsPublished[0].GetProperty("groupId").GetGuid());
    }

    [Fact]
    public async Task GetResults_SearchesByKeyword_Successfully()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");

        // 1. Tìm theo mã hồ sơ "N01"
        var res1 = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&keyword=N01");
        var json1 = await res1.Content.ReadAsStringAsync();
        using var doc1 = JsonDocument.Parse(json1);
        var items1 = doc1.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items1);
        Assert.Equal("N01", items1[0].GetProperty("groupCode").GetString());

        // 2. Tìm theo tên đề tài "Blockchain"
        var res2 = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&keyword=Blockchain");
        var json2 = await res2.Content.ReadAsStringAsync();
        using var doc2 = JsonDocument.Parse(json2);
        var items2 = doc2.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items2);
        Assert.Equal(_group2Id, items2[0].GetProperty("groupId").GetGuid());

        // 3. Tìm theo MSSV "22520003"
        var res3 = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&keyword=22520003");
        var json3 = await res3.Content.ReadAsStringAsync();
        using var doc3 = JsonDocument.Parse(json3);
        var items3 = doc3.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items3);
        Assert.Equal(_group2Id, items3[0].GetProperty("groupId").GetGuid());
    }

    [Fact]
    public async Task GetResults_PaginatesCorrectly()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&pageIndex=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(4, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, data.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, data.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, data.GetProperty("totalPages").GetInt32());
        Assert.True(data.GetProperty("hasNextPage").GetBoolean());
        Assert.False(data.GetProperty("hasPreviousPage").GetBoolean());

        var items = data.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
    }

    #endregion

    #region API 2: GET /api/academic-affairs/results/{groupId} Tests

    [Fact]
    public async Task GetDetail_Returns404_WhenGroupDoesNotExist()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{Guid.NewGuid()}?phase=Final");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_ReturnsCorrectMidtermData()
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group1Id}?phase=Midterm");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(_group1Id, data.GetProperty("groupId").GetGuid());
        Assert.Equal("N01", data.GetProperty("groupCode").GetString());
        Assert.Equal("Midterm", data.GetProperty("phase").GetString());
        Assert.Equal("Tiếp tục", data.GetProperty("statusLabel").GetString());

        // Điểm ở giữa kỳ phải là null
        Assert.True(data.GetProperty("advisorScore").ValueKind == JsonValueKind.Null);
        Assert.True(data.GetProperty("reviewerScore").ValueKind == JsonValueKind.Null);
        Assert.True(data.GetProperty("councilScore").ValueKind == JsonValueKind.Null);
        Assert.True(data.GetProperty("finalScore").ValueKind == JsonValueKind.Null);
        Assert.Empty(data.GetProperty("scoreSources").EnumerateArray());

        // Thông tin giữa kỳ
        var midterm = data.GetProperty("midtermDetail");
        Assert.Equal("Approved", midterm.GetProperty("status").GetString());
        Assert.Equal("Đã duyệt", midterm.GetProperty("statusLabel").GetString());
        Assert.Equal("Báo cáo tốt", midterm.GetProperty("facultyFeedback").GetString());
        Assert.Equal("TS. Đặng Hướng Dẫn", midterm.GetProperty("reviewerName").GetString());
    }

    [Fact]
    public async Task GetDetail_ReturnsCorrectFinalDataWithScoreSources()
    {
        Authenticate(Guid.NewGuid(), "Admin");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group1Id}?phase=Final");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(_group1Id, data.GetProperty("groupId").GetGuid());
        Assert.Equal("Final", data.GetProperty("phase").GetString());
        Assert.Equal("Khoa đã công bố", data.GetProperty("statusLabel").GetString());
        Assert.Equal(8.65m, data.GetProperty("finalScore").GetDecimal());
        Assert.Equal(9.00m, data.GetProperty("advisorScore").GetDecimal());
        Assert.Equal(8.50m, data.GetProperty("reviewerScore").GetDecimal());
        Assert.Equal(8.50m, data.GetProperty("councilScore").GetDecimal());
        Assert.Equal("Lưu chiểu đạt", data.GetProperty("archiveStatusLabel").GetString());
        Assert.Equal("Khoa đã công bố", data.GetProperty("publicationStatusLabel").GetString());

        // Kiểm tra danh sách nguồn điểm
        var sources = data.GetProperty("scoreSources").EnumerateArray().ToList();
        Assert.Contains(sources, s => s.GetProperty("sourceCode").GetString() == "ADVISOR" && s.GetProperty("score").GetDecimal() == 9.00m);
        Assert.Contains(sources, s => s.GetProperty("sourceCode").GetString() == "REVIEWER" && s.GetProperty("score").GetDecimal() == 8.50m);
        Assert.Contains(sources, s => s.GetProperty("sourceCode").GetString() == "COUNCIL_CHAIRMAN" && s.GetProperty("score").GetDecimal() == 8.50m);
        Assert.Contains(sources, s => s.GetProperty("sourceCode").GetString() == "COUNCIL_SECRETARY" && s.GetProperty("score").GetDecimal() == 8.50m);
    }

    #endregion

    #region API 3: GET /api/academic-affairs/results/export Tests

    [Fact]
    public async Task Export_Returns400_WhenFormatIsInvalid()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=csv");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("AcademicResults.InvalidFormat", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Export_Excel_GeneratesValidWorkbookWith11ColumnsAndStudentRows()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx&phase=Final");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        // Kiểm tra magic bytes của OOXML / ZIP (PK..)
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);

        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name == "KetQuaKLTN");
        Assert.NotNull(worksheet);

        // Header tại dòng 4
        Assert.Equal("MSSV", worksheet.Cell(4, 1).GetString());
        Assert.Equal("Họ tên SV", worksheet.Cell(4, 2).GetString());
        Assert.Equal("Mã nhóm", worksheet.Cell(4, 3).GetString());
        Assert.Equal("Tên đề tài", worksheet.Cell(4, 4).GetString());
        Assert.Equal("Khoa quản lý", worksheet.Cell(4, 5).GetString());
        Assert.Equal("Điểm GVHD", worksheet.Cell(4, 6).GetString());
        Assert.Equal("Điểm GVPB", worksheet.Cell(4, 7).GetString());
        Assert.Equal("Điểm Hội đồng", worksheet.Cell(4, 8).GetString());
        Assert.Equal("Điểm tổng kết", worksheet.Cell(4, 9).GetString());
        Assert.Equal("Trạng thái lưu chiểu", worksheet.Cell(4, 10).GetString());
        Assert.Equal("Trạng thái công bố", worksheet.Cell(4, 11).GetString());

        // Nhóm 1 có 2 sinh viên -> dòng 5 và dòng 6
        Assert.Equal("22520001", worksheet.Cell(5, 1).GetString());
        Assert.Equal("Nguyễn Văn An", worksheet.Cell(5, 2).GetString());
        Assert.Equal("N01", worksheet.Cell(5, 3).GetString());
        Assert.Equal("8.65", worksheet.Cell(5, 9).GetString());

        Assert.Equal("22520002", worksheet.Cell(6, 1).GetString());
        Assert.Equal("Trần Thị Bình", worksheet.Cell(6, 2).GetString());
        Assert.Equal("N01", worksheet.Cell(6, 3).GetString());
        Assert.Equal("8.65", worksheet.Cell(6, 9).GetString());
    }

    [Fact]
    public async Task Export_Pdf_GeneratesValidPdfDocument()
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=pdf&phase=Final");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        // Kiểm tra magic bytes của PDF (%PDF-1.4)
        var pdfHeader = Encoding.ASCII.GetString(bytes[..8]);
        Assert.StartsWith("%PDF-1.4", pdfHeader);
    }

    #endregion

    #region Additional Negative & Boundary Tests

    [Fact]
    public async Task GetResults_Returns400_WhenBatchIdIsNotValidGuidFormat()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync("/api/academic-affairs/results?batchId=not-a-valid-guid&phase=Final");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetResults_Returns400_WhenBatchIdIsMissing()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync("/api/academic-affairs/results?phase=Final");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("midterm")]
    [InlineData("MIDTERM")]
    [InlineData("mIdTeRm")]
    [InlineData("final")]
    [InlineData("FINAL")]
    [InlineData("FiNaL")]
    public async Task GetResults_PhaseIsCaseInsensitive(string phase)
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase={phase}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetResults_Returns200WithEmptyList_WhenBatchIdDoesNotExist()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var nonExistentBatchId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={nonExistentBatchId}&phase=Final");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(0, data.GetProperty("totalCount").GetInt32());
        Assert.Empty(data.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GetResults_ClampsPageSize_WhenExceeding100()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&pageSize=500");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(100, data.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetResults_ClampsPageIndexAndPageSize_WhenZeroOrNegative()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&pageIndex=0&pageSize=-10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("pageNumber").GetInt32());
        Assert.Equal(1, data.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetResults_ReturnsEmptyPage_WhenPageIndexExceedsTotalPages()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&pageIndex=999&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(4, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(999, data.GetProperty("pageNumber").GetInt32());
        Assert.Empty(data.GetProperty("items").EnumerateArray());
        Assert.True(data.GetProperty("hasPreviousPage").GetBoolean());
        Assert.False(data.GetProperty("hasNextPage").GetBoolean());
    }

    [Theory]
    [InlineData("  continued  ")]
    [InlineData("continued")]
    [InlineData("CONTINUED")]
    public async Task GetResults_StatusIsCaseInsensitive_AndHandlesWhitespace(string status)
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Midterm&status={Uri.EscapeDataString(status)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(_group1Id, items[0].GetProperty("groupId").GetGuid());
    }

    [Fact]
    public async Task GetResults_Returns400_WhenFinalStatusGivenToMidtermPhase()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Midterm&status=READY_TO_PUBLISH");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetResults_Returns400_WhenMidtermStatusGivenToFinalPhase()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&status=CONTINUED");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetResults_HandlesSpecialCharactersInKeyword_WithoutCrashing()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&keyword={Uri.EscapeDataString("%#@!$^&*")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetResults_HandlesWhitespaceOnlyKeyword_AsNoFilter()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&keyword={Uri.EscapeDataString("   ")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(4, items.Count);
    }

    [Fact]
    public async Task GetResults_HandlesLongKeyword_Gracefully()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var longKeyword = new string('a', 500);
        var response = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final&keyword={longKeyword}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetDetail_Returns404_WhenGroupIdIsNotGuidFormat()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync("/api/academic-affairs/results/not-a-valid-guid?phase=Final");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_Returns404_WhenGroupIdIsEmptyGuid()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{Guid.Empty}?phase=Final");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_Returns400_WhenPhaseIsMissing()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group1Id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_Returns400_WhenPhaseIsInvalid()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group1Id}?phase=Semester");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_PhaseIsCaseInsensitive()
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group1Id}?phase=mIdTeRm");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Midterm", doc.RootElement.GetProperty("data").GetProperty("phase").GetString());
    }

    [Fact]
    public async Task GetDetail_Returns403_WhenRoleIsNotAuthorized()
    {
        Authenticate(Guid.NewGuid(), "Student");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group1Id}?phase=Final");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_Returns401_WhenUnauthenticated()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group1Id}?phase=Final");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_HandlesGroupWithoutCouncil_InFinalPhase()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group4Id}?phase=Final");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(_group4Id, data.GetProperty("groupId").GetGuid());
        Assert.Equal("MISSING_SCORE", data.GetProperty("statusCode").GetString());
        Assert.Equal("Chưa đủ điểm", data.GetProperty("statusLabel").GetString());
        Assert.True(data.GetProperty("finalScore").ValueKind == JsonValueKind.Null);
        Assert.DoesNotContain(data.GetProperty("scoreSources").EnumerateArray(), s => s.GetProperty("sourceCode").GetString()?.StartsWith("COUNCIL") == true);
    }

    [Fact]
    public async Task GetDetail_HandlesGroupWithoutMidtermReport_InMidtermPhase()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/{_group4Id}?phase=Midterm");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(_group4Id, data.GetProperty("groupId").GetGuid());
        Assert.Equal("PENDING_MIDTERM", data.GetProperty("statusCode").GetString());
        Assert.Equal("Chờ xét duyệt", data.GetProperty("statusLabel").GetString());
        Assert.Equal("Chưa nộp báo cáo", data.GetProperty("detailLabel").GetString());
        Assert.True(data.GetProperty("midtermDetail").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Export_Returns400_WhenFormatIsMissing()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("doc")]
    [InlineData("txt")]
    [InlineData("csv")]
    public async Task Export_Returns400_WhenFormatIsUnsupported(string format)
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format={format}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_Returns400_WhenBatchIdIsEmpty()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={Guid.Empty}&format=xlsx");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("XLSX")]
    [InlineData("xlsx")]
    [InlineData("Xlsx")]
    [InlineData("PDF")]
    [InlineData("pdf")]
    [InlineData("PdF")]
    public async Task Export_FormatIsCaseInsensitive(string format)
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format={format}&phase=Final");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Export_DefaultsToFinalPhase_WhenPhaseIsOmitted()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheets.First();

        var title = worksheet.Cell(1, 1).GetString();
        Assert.Contains("CUỐI KỲ", title);
    }

    [Fact]
    public async Task Export_Returns400_WhenPhaseIsInvalid()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx&phase=Invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_Returns400_WhenStatusIsInvalid()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx&phase=Midterm&status=PUBLISHED");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsValidEmptyFiles_WhenNoResultsFound()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var nonExistentBatchId = Guid.NewGuid();

        // 1. Excel khi không có dữ liệu
        var resExcel = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={nonExistentBatchId}&format=xlsx&phase=Final");
        Assert.Equal(HttpStatusCode.OK, resExcel.StatusCode);
        var excelBytes = await resExcel.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name == "KetQuaKLTN");
        Assert.NotNull(worksheet);
        Assert.Equal("MSSV", worksheet.Cell(4, 1).GetString());
        // Không có dữ liệu ở dòng 5
        Assert.Empty(worksheet.Cell(5, 1).GetString());

        // 2. PDF khi không có dữ liệu
        var resPdf = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={nonExistentBatchId}&format=pdf&phase=Final");
        Assert.Equal(HttpStatusCode.OK, resPdf.StatusCode);
        var pdfBytes = await resPdf.Content.ReadAsByteArrayAsync();
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes[..8]);
        Assert.StartsWith("%PDF-1.4", pdfHeader);
    }

    [Fact]
    public async Task Export_Excel_MidtermPhase_LeavesScoreColumnsEmpty()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx&phase=Midterm");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheets.First();

        var title = worksheet.Cell(1, 1).GetString();
        Assert.Contains("GIỮA KỲ", title);

        // Kiểm tra dòng dữ liệu thứ 5: các cột điểm phải để trống
        Assert.Empty(worksheet.Cell(5, 6).GetString()); // GVHD
        Assert.Empty(worksheet.Cell(5, 7).GetString()); // GVPB
        Assert.Empty(worksheet.Cell(5, 8).GetString()); // HD
        Assert.Empty(worksheet.Cell(5, 9).GetString()); // Tong ket
    }

    [Fact]
    public async Task Export_FiltersByKeywordAndStatus_Correctly()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs");
        // Chỉ xuất nhóm đã công bố (Group 1 có 2 sinh viên)
        var response = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx&phase=Final&status=PUBLISHED");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheets.First();

        // Dòng 5 và 6 là 2 sinh viên của Nhóm 1
        Assert.Equal("22520001", worksheet.Cell(5, 1).GetString());
        Assert.Equal("22520002", worksheet.Cell(6, 1).GetString());
        // Dòng 7 phải rỗng vì chỉ có 2 dòng dữ liệu
        Assert.Empty(worksheet.Cell(7, 1).GetString());
    }

    #endregion

    #region Test Fakes & Auth Handler

    private sealed class FakeAcademicResultsThesisReader : IAcademicResultsThesisReader
    {
        public List<AcademicResultGroupSnapshotContractDto> Groups { get; } = [];

        public Task<IReadOnlyList<AcademicResultGroupSnapshotContractDto>> GetGroupsByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AcademicResultGroupSnapshotContractDto>>(
                Groups.Where(g => g.BatchId == batchId).ToList());
        }

        public Task<AcademicResultGroupSnapshotContractDto?> GetGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Groups.FirstOrDefault(g => g.GroupId == groupId));
        }
    }

    private sealed class FakeStudentSearchReader : IStudentSearchReader
    {
        public Task<IReadOnlyList<Guid>> FindIdsByNameOrCodeAsync(string keyword, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Guid>>([]);
        }
    }

    private sealed class FakeScoreSummaryStore : IScoreSummaryStore
    {
        public Dictionary<Guid, ScoreFacts> Facts { get; } = new();
        public Dictionary<Guid, ScoreHistory> Histories { get; } = new();

        public Task<IReadOnlyDictionary<Guid, ScoreFacts>> FactsAsync(IReadOnlyCollection<Guid> groups, bool locked, CancellationToken token)
        {
            var result = groups.ToDictionary(g => g, g => Facts.TryGetValue(g, out var f) ? f : new ScoreFacts([], [], [], [], [], [], [], [], []));
            return Task.FromResult<IReadOnlyDictionary<Guid, ScoreFacts>>(result);
        }

        public Task<IReadOnlyDictionary<Guid, ScoreHistory>> HistoriesAsync(IReadOnlyCollection<Guid> groups, CancellationToken token)
        {
            var result = groups.Where(Histories.ContainsKey).ToDictionary(g => g, g => Histories[g]);
            return Task.FromResult<IReadOnlyDictionary<Guid, ScoreHistory>>(result);
        }

        public Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummaryIdempotency?> ReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.FromResult<ScoreSummaryIdempotency?>(null);
        public Task LockAdvisorsAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummary?> SummaryAsync(Guid group, bool locked, CancellationToken token) => Task.FromResult(Histories.TryGetValue(group, out var h) ? h.Summary : null);
        public Task<Guid?> AssignmentGroupAsync(Guid assignment, CancellationToken token) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyList<Guid>> CouncilGroupsAsync(Guid council, CancellationToken token) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<bool> PublishedAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.FromResult(false);
        public void Add(ScoreSummary summary) { }
        public void Add(ScoreSummaryRevision revision, IReadOnlyList<ScoreSummaryComponent> components) { }
        public void Add(ScoreSummaryIdempotency receipt) { }
    }

    private sealed class FakeArchiveProofContract : IFinalSubmissionArchiveProofContract
    {
        public Dictionary<Guid, FinalSubmissionArchiveProofContractDto> Proofs { get; } = new();

        public Task<FinalSubmissionArchiveProofContractDto> GetArchiveProofAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Proofs.TryGetValue(groupId, out var p) ? p : new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, groupId, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, []));
        }

        public Task<IReadOnlyList<FinalSubmissionArchiveProofContractDto>> GetManyArchiveProofsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default)
        {
            var list = groupIds.Select(id => Proofs.TryGetValue(id, out var p) ? p : new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, id, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, [])).ToList();
            return Task.FromResult<IReadOnlyList<FinalSubmissionArchiveProofContractDto>>(list);
        }

        public Task<FinalSubmissionArchiveProofContractDto> LockArchiveProofForCalculationAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return GetArchiveProofAsync(groupId, cancellationToken);
        }
    }

    private sealed class TestResultsAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ResultsTestScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer test.", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = header["Bearer test.".Length..].Split('.', 3);
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

    #endregion
}
