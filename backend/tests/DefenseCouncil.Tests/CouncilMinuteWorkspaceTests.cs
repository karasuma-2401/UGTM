using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;
using Xunit;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilMinuteWorkspaceTests : IAsyncLifetime
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
    private readonly FakeReauthenticationVerifier _reauthVerifier = new();
    private readonly FakeMinuteConfigurationReader _configurations = new();

    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        builder.Services.AddSingleton<ThesisGradeCalculator>();
        builder.Services.AddScoped<IGradeCalculationService, GradeCalculationService>();

        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IDefenseCouncilUnitOfWork>(_uow);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentInfoReader);
        builder.Services.AddSingleton<IDefenseGroupContract>(_defenseGroupContract);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicDefenseContract);
        builder.Services.AddSingleton<IDefenseCouncilAuditOutbox>(_auditOutbox);
        builder.Services.AddSingleton<IReauthenticationVerifier>(_reauthVerifier);

        builder.Services.AddScoped<CouncilMinuteWorkspaceLoader>();
        builder.Services.AddSingleton<UGTM.Modules.DefenseCouncil.Application.ScoreSummaries.ICouncilMinuteWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddScoped<UGTM.Modules.Identity.Contracts.FinalSubmissions.IFinalSubmissionAuthorityReader, TestScoreAuthority>();
        builder.Services.AddSingleton<UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries.IScoreSummaryConfigurationReader>(_configurations);
        builder.Services.AddScoped<GetCouncilMinuteWorkspaceHandler>();
        builder.Services.AddScoped<SaveCouncilMinuteHandler>();
        builder.Services.AddScoped<LockCouncilMinuteHandler>();
        builder.Services.AddScoped<UnlockCouncilMinuteHandler>();

        builder.Services.AddAuthentication(TestMinuteAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestMinuteAuthHandler>(TestMinuteAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapCouncilEndpoints();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private void Authenticate(Guid userId, string role = "Lecturer")
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            $"test.{userId:N}.{role}");
    }

    private (Guid CouncilId, Guid AssignmentId, Guid GroupId, Guid ChairmanId, Guid SecretaryId, Guid MemberId, Guid ReviewerUserId, Guid AdvisorUserId, Guid SecretaryUserId, Guid ChairmanUserId)
        SetupTestCouncil(bool isReviewerInCouncil = true)
    {
        var batchId = Guid.NewGuid();
        var council = Council.Create(batchId, "HĐ01", DateOnly.FromDateTime(DateTime.Today.AddDays(7)), "08:00 - 11:30", "A101");
        council.Approve("QD-01", DateOnly.FromDateTime(DateTime.Today), null, Guid.NewGuid(), DateTime.UtcNow);
        _councilRepo.Councils.Add(council);

        var chairmanUserId = Guid.NewGuid();
        var secretaryUserId = Guid.NewGuid();
        var memberOrReviewerUserId = Guid.NewGuid();

        _userInfoReader.Users[chairmanUserId] = new CouncilUserInfoContractDto(chairmanUserId, "GS. Nguyễn Văn A", "nva@school.edu.vn", "GV001", true);
        _userInfoReader.Users[secretaryUserId] = new CouncilUserInfoContractDto(secretaryUserId, "TS. Trần Thị B", "ttb@school.edu.vn", "GV002", true);
        _userInfoReader.Users[memberOrReviewerUserId] = new CouncilUserInfoContractDto(memberOrReviewerUserId, "ThS. Lê Văn C", "lvc@school.edu.vn", "GV003", true);

        var chairman = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chairmanUserId, RoleInCouncil = CouncilMemberRole.Chairman };
        var secretary = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secretaryUserId, RoleInCouncil = CouncilMemberRole.Secretary };
        var member = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = memberOrReviewerUserId, RoleInCouncil = CouncilMemberRole.Member };

        _councilRepo.Members.AddRange([chairman, secretary, member]);

        var groupId = Guid.NewGuid();
        var advisorUserId = Guid.NewGuid();
        _userInfoReader.Users[advisorUserId] = new CouncilUserInfoContractDto(advisorUserId, "PGS. Phạm Văn D", "pvd@school.edu.vn", "GV004", true);

        var assignment = new CouncilAssignment
        {
            Id = Guid.NewGuid(),
            CouncilId = council.Id,
            GroupId = groupId,
            OrderNumber = 1
        };
        _councilRepo.Assignments.Add(assignment);

        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId,
            topicId,
            batchId,
            "Accepted",
            DateTime.UtcNow,
            [new DefenseGroupMemberContractDto(Guid.NewGuid(), batchId, "Confirmed")]);

        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId,
            batchId,
            advisorUserId,
            "Hệ thống Quản lý KLTN",
            "Thesis Management System");

        Guid reviewerUserId;
        if (isReviewerInCouncil)
        {
            reviewerUserId = memberOrReviewerUserId; // TH1: GVPB kiêm UV
        }
        else
        {
            reviewerUserId = Guid.NewGuid(); // TH2: GVPB độc lập
            _userInfoReader.Users[reviewerUserId] = new CouncilUserInfoContractDto(reviewerUserId, "TS. Hoàng Thị E", "hte@school.edu.vn", "GV005", true);
        }

        var revAssignment = ReviewerAssignment.Create(
            Guid.NewGuid(),
            groupId,
            reviewerUserId,
            advisorUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Guid.NewGuid(),
            DateTime.UtcNow);

        var revEvaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssignment.Id, 8.0m, "Phản biện tốt", DefenseApprovalStatus.Approved, reviewerUserId, DateTime.UtcNow);
        revAssignment.GetType().GetProperty("Evaluation")?.SetValue(revAssignment, revEvaluation);
        _councilRepo.ActiveReviewerAssignments[groupId] = revAssignment;

        var advEvaluation = AdvisorEvaluation.Create(
            Guid.NewGuid(),
            groupId,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            8.0m,
            "Hướng dẫn tốt",
            advisorUserId,
            DateTime.UtcNow);
        _councilRepo.AdvisorEvaluations[groupId] = advEvaluation;

        return (council.Id, assignment.Id, groupId, chairman.Id, secretary.Id, member.Id, reviewerUserId, advisorUserId, secretaryUserId, chairmanUserId);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public void Minute_preview_rounds_midpoints_away_from_zero()
    {
        var items = new[]
        {
            new CouncilMinuteWorkspaceCalculator.ItemEvaluationData("ADVISOR", "GVHD", "Advisor", 8.125m, 1m, "", false, false, SourceId: Guid.NewGuid())
        };
        var configuration = FakeMinuteConfigurationReader.ForBatch(Guid.NewGuid()) with
        { Components = [FakeMinuteConfigurationReader.Component("ADVISOR", "Advisor", 1m, 1)] };
        var (_, _, preview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(items, null, null, null, null, configuration);

        Assert.Equal(8.13m, preview.FinalRoundedScore);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("incomplete")]
    public void Minute_preview_does_not_return_a_final_score_without_valid_complete_inputs(string kind)
    {
        var config = FakeMinuteConfigurationReader.ForBatch(Guid.NewGuid()) with
        { Components = [FakeMinuteConfigurationReader.Component("A", "Advisor", 1, 1)] };
        var item = new CouncilMinuteWorkspaceCalculator.ItemEvaluationData("ADVISOR", "GVHD", "Advisor",
            kind == "incomplete" ? null : 8m, 2, "", false, false, SourceId: Guid.NewGuid());
        var (_, _, preview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData([item], null, null, null, null,
            kind == "missing" ? null : kind == "invalid" ? config with { IsValid = false } : config);
        Assert.Null(preview.FinalRoundedScore);
        Assert.False(preview.IsReadyToLock);
        Assert.NotNull(preview.CalculationErrorCode);
    }

    [Fact]
    public async Task Workspace_reads_active_nondefault_weights_and_configuration_version()
    {
        var ids = SetupTestCouncil();
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = ids.AssignmentId, CouncilMemberId = ids.ChairmanId, Score = 7 },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = ids.AssignmentId, CouncilMemberId = ids.SecretaryId, Score = 6 }
        ]);
        _configurations.Factory = batch => FakeMinuteConfigurationReader.ForBatch(batch) with
        {
            VersionNo = 19,
            Components = FakeMinuteConfigurationReader.ForBatch(batch).Components.Select(c => c.Source == "Advisor" ? c with { Weight = 7 } : c).ToArray()
        };
        Authenticate(ids.SecretaryUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        var preview = data.GetProperty("calculationPreview");
        Assert.Equal(19, preview.GetProperty("configurationVersion").GetInt32());
        Assert.Equal(11m, preview.GetProperty("adjustedDivisor").GetDecimal());
        Assert.Equal(7.73m, preview.GetProperty("finalRoundedScore").GetDecimal());
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Lecturer")]
    public async Task Workspace_and_preview_reject_users_outside_the_assigned_council(string role)
    {
        var assigned = SetupTestCouncil();
        var other = SetupTestCouncil();
        Authenticate(role == "Student" ? Guid.NewGuid() : other.ChairmanUserId, role);

        var workspace = await _client.GetAsync($"/api/councils/assignments/{assigned.AssignmentId}/minute-workspace");
        var preview = await _client.PutAsJsonAsync(
            $"/api/councils/assignments/{assigned.AssignmentId}/minute?preview=true",
            new SaveCouncilMinuteInputDto(SecretaryMarking: "AcceptedAsPerMinute", StudentFeedback: null));

        Assert.Equal(HttpStatusCode.Forbidden, workspace.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("AcademicAffairs")]
    public async Task Workspace_and_preview_allow_authorized_administrators(string role)
    {
        var ids = SetupTestCouncil();
        Authenticate(Guid.NewGuid(), role);

        var workspace = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");
        var preview = await _client.PutAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute?preview=true",
            new SaveCouncilMinuteInputDto(SecretaryMarking: "AcceptedAsPerMinute", StudentFeedback: null));

        Assert.Equal(HttpStatusCode.OK, workspace.StatusCode);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
    }

    [Fact]
    public async Task GetMinuteWorkspace_DualRoleReviewer_ReturnsExpectedFourBlocksAndFormulas()
    {
        // Arrange (TH1: GVPB kiêm Ủy viên)
        var ids = SetupTestCouncil(isReviewerInCouncil: true);

        // Thêm điểm của Chủ tịch (7.5) và Thư ký (7.5)
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.ChairmanId,
            Score = 7.5m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.SecretaryId,
            Score = 7.5m,
            EvaluatedAt = DateTime.UtcNow
        });

        // Act
        Authenticate(ids.SecretaryUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        // 1. Assignment Info
        var assignmentInfo = data.GetProperty("assignmentInfo");
        Assert.Equal("HĐ01", assignmentInfo.GetProperty("councilCode").GetString());
        Assert.Equal("N01", assignmentInfo.GetProperty("groupCode").GetString());
        Assert.Equal("GS. Nguyễn Văn A", assignmentInfo.GetProperty("chairmanName").GetString());
        Assert.Equal("TS. Trần Thị B", assignmentInfo.GetProperty("secretaryName").GetString());

        // 2. Score Items
        var scoreItems = data.GetProperty("scoreItems");
        Assert.Equal(5, scoreItems.GetArrayLength());

        var uvFound = false;
        var gvpbFound = false;
        foreach (var item in scoreItems.EnumerateArray())
        {
            var code = item.GetProperty("componentCode").GetString();
            if (code == "COUNCIL_MEMBER")
            {
                uvFound = true;
                Assert.Equal("Chỉ tham dự", item.GetProperty("statusText").GetString());
                Assert.Equal("Không thiếu điểm", item.GetProperty("category").GetString());
                Assert.True(item.GetProperty("isDualRoleReviewer").GetBoolean());
                Assert.Equal(JsonValueKind.Null, item.GetProperty("score").ValueKind);
                Assert.Equal(0m, item.GetProperty("weight").GetDecimal());
            }
            else if (code == "REVIEWER")
            {
                gvpbFound = true;
                Assert.Equal(8.0m, item.GetProperty("score").GetDecimal());
                Assert.Equal(2m, item.GetProperty("weight").GetDecimal());
                Assert.True(item.GetProperty("isDualRoleReviewer").GetBoolean());
            }
        }
        Assert.True(uvFound);
        Assert.True(gvpbFound);

        // 3. Calculation Preview: raw_total = 8*2 + 8*2 + 7.5*1 + 7.5*1 = 47, divisor = 6, avg = 47/6 ≈ 7.833333
        var calcPreview = data.GetProperty("calculationPreview");
        Assert.Equal(47m, calcPreview.GetProperty("rawTotal").GetDecimal());
        Assert.Equal(6m, calcPreview.GetProperty("rawDivisor").GetDecimal());
        Assert.Equal(7.833333m, calcPreview.GetProperty("rawAverage").GetDecimal());
        Assert.True(calcPreview.GetProperty("isReadyToLock").GetBoolean());
    }

    [Fact]
    public async Task SaveMinute_RejectedAsPerMinute_WithoutTargetComponent_ReturnsBadRequest()
    {
        // Arrange
        var ids = SetupTestCouncil(isReviewerInCouncil: true);
        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "RejectedAsPerMinute",
            TargetComponentCode: null, // Thiếu target code
            StudentFeedback: "Cần chỉnh sửa format bài báo cáo");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_NonSecretary_RealSave_ReturnsForbidden()
    {
        // Arrange
        var ids = SetupTestCouncil(isReviewerInCouncil: true);

        // Đăng nhập là Chủ tịch (không phải Thư ký)
        Authenticate(ids.ChairmanUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "AcceptedAsPerMinute",
            StudentFeedback: "Góp ý từ chủ tịch");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_PreviewMode_DoesNotPersistToDatabase()
    {
        // Arrange
        var ids = SetupTestCouncil(isReviewerInCouncil: true);
        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "RejectedAsPerMinute",
            TargetComponentCode: "REVIEWER",
            StudentFeedback: "Đang preview thử nghiệm");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute?preview=true", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonStr = await response.Content.ReadAsStringAsync();
        Assert.Contains("isPreview", jsonStr, StringComparison.OrdinalIgnoreCase);

        // Kiểm tra database: ReviewerEvaluation trong DB không bị đổi IsIgnoredInCalculation
        var reviewer = _councilRepo.ActiveReviewerAssignments[ids.GroupId];
        Assert.False(reviewer?.Evaluation?.IsIgnoredInCalculation);

        // CouncilMinute trong DB chưa được tạo hoặc chưa có StudentFeedback
        var minute = _councilRepo.Minutes.FirstOrDefault(m => m.CouncilAssignmentId == ids.AssignmentId);
        Assert.Null(minute);
        Assert.Empty(_auditOutbox.Entries);
    }

    [Fact]
    public async Task SaveMinute_rejects_exclusion_without_a_separate_reason_even_when_student_feedback_is_present()
    {
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute",
            new SaveCouncilMinuteInputDto("RejectedAsPerMinute", "GVPB", "Góp ý đủ dài nhưng không phải lý do nội bộ."));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ExclusionReasonRequired", await response.Content.ReadAsStringAsync());
        Assert.False(_councilRepo.ActiveReviewerAssignments[ids.GroupId].Evaluation!.IsIgnoredInCalculation);
        Assert.Empty(_auditOutbox.Entries);
    }

    [Fact]
    public async Task SaveMinute_rejects_unknown_marking_instead_of_resetting_existing_exclusions()
    {
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute",
            new SaveCouncilMinuteInputDto(SecretaryMarking: "typo"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_auditOutbox.Entries);
    }

    [Theory]
    [InlineData("COUNCIL_MEMBER")]
    [InlineData("COUNCIL_CHAIRMAN")]
    public async Task SaveMinute_rejects_attendance_only_or_unscored_target_without_resetting_exclusions(string target)
    {
        var ids = SetupTestCouncil(isReviewerInCouncil: true);
        Authenticate(ids.SecretaryUserId);
        var reviewer = _councilRepo.ActiveReviewerAssignments[ids.GroupId].Evaluation!;
        reviewer.SetCalculationExclusion(true, "Lý do đã được lưu trước đó.");
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute",
            new SaveCouncilMinuteInputDto("RejectedAsPerMinute", target, Reason: "Lý do loại điểm theo biên bản."));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("InvalidTargetComponent", await response.Content.ReadAsStringAsync());
        Assert.True(reviewer.IsIgnoredInCalculation);
        Assert.Empty(_auditOutbox.Entries);
    }

    [Fact]
    public async Task SaveMinute_RealSave_RejectedReviewer_CalculatesAndPersistsCorrectly()
    {
        // Arrange: raw: GVHD 8.0(x2) + GVPB 8.0(x2) + CT 7.5(x1) + TK 7.5(x1) = 47 / 6 ≈ 7.833333
        // Khi loại GVPB: adjusted: GVHD 8.0(x2) + CT 7.5(x1) + TK 7.5(x1) = 31 / 4 = 7.75
        var ids = SetupTestCouncil(isReviewerInCouncil: true);

        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.ChairmanId,
            Score = 7.5m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.SecretaryId,
            Score = 7.5m,
            EvaluatedAt = DateTime.UtcNow
        });

        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "RejectedAsPerMinute",
            TargetComponentCode: "REVIEWER",
            StudentFeedback: "Cần bổ sung tài liệu tham khảo và sửa kết luận.",
            Reason: "Hội đồng xác định phiếu phản biện không hợp lệ.");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.False(data.GetProperty("isPreview").GetBoolean());

        // Raw
        var calc = data.GetProperty("calculationPreview");
        Assert.Equal(47m, calc.GetProperty("rawTotal").GetDecimal());
        Assert.Equal(6m, calc.GetProperty("rawDivisor").GetDecimal());
        Assert.Equal(7.833333m, calc.GetProperty("rawAverage").GetDecimal());

        // Adjusted: loại GVPB hệ số 2 điểm 8.0 -> tổng 31, hệ số 4, avg 7.75, làm tròn 7.75
        Assert.Equal(31m, calc.GetProperty("adjustedTotal").GetDecimal());
        Assert.Equal(4m, calc.GetProperty("adjustedDivisor").GetDecimal());
        Assert.Equal(7.75m, calc.GetProperty("adjustedAverage").GetDecimal());
        Assert.Equal(7.75m, calc.GetProperty("finalRoundedScore").GetDecimal());
        Assert.True(calc.GetProperty("isReadyToLock").GetBoolean());

        // Kiểm tra database
        var reviewer = _councilRepo.ActiveReviewerAssignments[ids.GroupId];
        Assert.True(reviewer?.Evaluation?.IsIgnoredInCalculation);
        Assert.Equal(payload.Reason, reviewer?.Evaluation?.ExclusionReason);
        var savedAudit = Assert.Single(_auditOutbox.Entries).Entry;
        using var auditJson = JsonDocument.Parse(JsonSerializer.Serialize(savedAudit.Metadata));
        Assert.Equal(payload.Reason, auditJson.RootElement.GetProperty("Reason").GetString());
        Assert.False(auditJson.RootElement.GetProperty("Before").GetProperty("Reviewer").GetProperty("IsIgnoredInCalculation").GetBoolean());
        Assert.True(auditJson.RootElement.GetProperty("After").GetProperty("Reviewer").GetProperty("IsIgnoredInCalculation").GetBoolean());

        var minute = _councilRepo.Minutes.FirstOrDefault(m => m.CouncilAssignmentId == ids.AssignmentId);
        Assert.NotNull(minute);
        Assert.Equal("Cần bổ sung tài liệu tham khảo và sửa kết luận.", minute.RequiredRevisions);

        // Gọi lại GET minute-workspace để xác nhận tính nhất quán
        var getRes = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var getData = await ReadDataAsync(getRes);

        var disc = getData.GetProperty("discrepancySummary");
        Assert.Equal("RejectedAsPerMinute", disc.GetProperty("secretaryMarking").GetString());
        Assert.True(disc.GetProperty("isIgnoredInCalculation").GetBoolean());
        Assert.Equal("REVIEWER", disc.GetProperty("ignoredComponentCode").GetString());

        var getCalc = getData.GetProperty("calculationPreview");
        Assert.Equal(31m, getCalc.GetProperty("adjustedTotal").GetDecimal());
        Assert.Equal(4m, getCalc.GetProperty("adjustedDivisor").GetDecimal());
    }

    [Fact]
    public async Task GetMinuteWorkspace_WithoutToken_ReturnsUnauthorized()
    {
        var ids = SetupTestCouncil();
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_WithoutToken_ReturnsUnauthorized()
    {
        var ids = SetupTestCouncil();
        _client.DefaultRequestHeaders.Authorization = null;

        var payload = new SaveCouncilMinuteInputDto(SecretaryMarking: "AcceptedAsPerMinute");
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMinuteWorkspace_AssignmentNotFound_ReturnsNotFound()
    {
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var nonExistentId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/councils/assignments/{nonExistentId}/minute-workspace");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_AssignmentNotFound_ReturnsNotFound()
    {
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var nonExistentId = Guid.NewGuid();
        var payload = new SaveCouncilMinuteInputDto(SecretaryMarking: "AcceptedAsPerMinute");
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{nonExistentId}/minute", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_AdminRole_AllowsSaveWithoutBeingSecretary()
    {
        var ids = SetupTestCouncil();
        var adminUserId = Guid.NewGuid();
        _userInfoReader.Users[adminUserId] = new CouncilUserInfoContractDto(adminUserId, "Quản trị viên", "admin@school.edu.vn", "ADMIN", true);
        Authenticate(adminUserId, role: "Admin");

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "AcceptedAsPerMinute",
            StudentFeedback: "Lưu bởi Admin");

        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.False(data.GetProperty("isPreview").GetBoolean());
    }

    [Fact]
    public async Task SaveMinute_WhenMinuteLocked_ReturnsConflict()
    {
        var ids = SetupTestCouncil();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "AcceptedAsPerMinute",
            StudentFeedback: "Cố gắng sửa khi đã lock");

        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_InvalidTargetComponentCode_ReturnsBadRequest()
    {
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "RejectedAsPerMinute",
            TargetComponentCode: "UNKNOWN_ROLE_CODE",
            StudentFeedback: "Sai mã đầu điểm");

        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMinuteWorkspace_IndependentReviewer_ReturnsDivisorSeven()
    {
        // TH2: GVPB độc lập (không kiêm Ủy viên)
        var ids = SetupTestCouncil(isReviewerInCouncil: false);

        // Thêm điểm đầy đủ cho các thành viên
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.ChairmanId,
            Score = 8.0m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.SecretaryId,
            Score = 8.0m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.MemberId,
            Score = 8.0m,
            EvaluatedAt = DateTime.UtcNow
        });

        Authenticate(ids.SecretaryUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        // GVHD 8*2 + GVPB 8*2 + CT 8*1 + TK 8*1 + UV 8*1 = 56 / 7 = 8.0
        var calc = data.GetProperty("calculationPreview");
        Assert.Equal(56m, calc.GetProperty("rawTotal").GetDecimal());
        Assert.Equal(7m, calc.GetProperty("rawDivisor").GetDecimal());
        Assert.Equal(8.000000m, calc.GetProperty("rawAverage").GetDecimal());
        Assert.True(calc.GetProperty("isReadyToLock").GetBoolean());
    }

    [Fact]
    public async Task GetMinuteWorkspace_WithDiscrepancy_NotReadyToLockUntilResolved()
    {
        // Hội đồng có độ lệch >= 2.0 (CT chấm 9.5, TK chấm 7.0 => lệch 2.5)
        var ids = SetupTestCouncil(isReviewerInCouncil: true);

        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.ChairmanId,
            Score = 9.5m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.SecretaryId,
            Score = 7.0m,
            EvaluatedAt = DateTime.UtcNow
        });

        Authenticate(ids.SecretaryUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        var disc = data.GetProperty("discrepancySummary");
        Assert.True(disc.GetProperty("hasDiscrepancy").GetBoolean());
        Assert.Equal(2.5m, disc.GetProperty("scoreDifference").GetDecimal());
        Assert.Equal("None", disc.GetProperty("secretaryMarking").GetString());

        var calc = data.GetProperty("calculationPreview");
        // Do có sai biệt nhưng chưa đánh dấu xử lý -> isReadyToLock phải là false
        Assert.False(calc.GetProperty("isReadyToLock").GetBoolean());
    }

    [Fact]
    public async Task SaveMinute_AcceptedAsPerMinute_ResolvesDiscrepancyWithoutExclusion()
    {
        var ids = SetupTestCouncil(isReviewerInCouncil: true);

        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.ChairmanId,
            Score = 9.5m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.SecretaryId,
            Score = 7.0m,
            EvaluatedAt = DateTime.UtcNow
        });

        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "AcceptedAsPerMinute",
            StudentFeedback: "Hội đồng thống nhất giữ nguyên điểm.");

        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        var disc = data.GetProperty("discrepancySummary");
        Assert.Equal("AcceptedAsPerMinute", disc.GetProperty("secretaryMarking").GetString());
        Assert.False(disc.GetProperty("isIgnoredInCalculation").GetBoolean());
        Assert.Contains("Giữ nguyên các đầu điểm", disc.GetProperty("chairmanConclusion").GetString());

        var calc = data.GetProperty("calculationPreview");
        Assert.True(calc.GetProperty("isReadyToLock").GetBoolean());
        Assert.Equal(calc.GetProperty("rawTotal").GetDecimal(), calc.GetProperty("adjustedTotal").GetDecimal());
    }

    [Fact]
    public async Task SaveMinute_RejectAdvisor_ExcludesAdvisorScoreAndUpdatesAdvisorEvaluation()
    {
        var ids = SetupTestCouncil(isReviewerInCouncil: true);

        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.ChairmanId,
            Score = 7.5m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            CouncilMemberId = ids.SecretaryId,
            Score = 7.5m,
            EvaluatedAt = DateTime.UtcNow
        });

        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "RejectedAsPerMinute",
            TargetComponentCode: "ADVISOR",
            StudentFeedback: "Góp ý dành cho nhóm sinh viên.",
            Reason: "Loại điểm GVHD theo biên bản.");

        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        var disc = data.GetProperty("discrepancySummary");
        Assert.Equal("ADVISOR", disc.GetProperty("ignoredComponentCode").GetString());
        Assert.True(disc.GetProperty("isIgnoredInCalculation").GetBoolean());

        var advEval = _councilRepo.AdvisorEvaluations[ids.GroupId];
        Assert.True(advEval.IsIgnoredInCalculation);
        Assert.Equal(payload.Reason, advEval.ExclusionReason);
    }

    // =========================================================================
    // Negative & Boundary Tests: Sai kiểu dữ liệu, rỗng, tham chiếu không tồn tại
    // =========================================================================

    [Fact]
    public async Task GetMinuteWorkspace_InvalidGuidFormat_ReturnsNotFound()
    {
        // Truyền sai kiểu dữ liệu GUID trên URL route
        Authenticate(Guid.NewGuid());
        var response = await _client.GetAsync("/api/councils/assignments/not-a-valid-guid/minute-workspace");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_InvalidGuidFormat_ReturnsNotFound()
    {
        // Truyền sai kiểu dữ liệu GUID trên URL route khi gọi PUT
        Authenticate(Guid.NewGuid());
        var payload = new SaveCouncilMinuteInputDto(SecretaryMarking: "AcceptedAsPerMinute");
        var response = await _client.PutAsJsonAsync("/api/councils/assignments/12345-not-guid/minute", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_InvalidBooleanQueryParam_ReturnsBadRequest()
    {
        // Truyền sai kiểu dữ liệu cho query param boolean: ?preview=not_bool
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(SecretaryMarking: "AcceptedAsPerMinute");
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute?preview=abcxyz", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_MalformedJsonBody_ReturnsBadRequest()
    {
        // Truyền JSON sai format / cú pháp (Malformed JSON)
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var content = new StringContent("{ \"secretary_marking\": \"AcceptedAsPerMinute\", invalid_json_here }", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PutAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_EmptyRequestBody_DoesNotCrashAndReturnsOk()
    {
        // Truyền rỗng: body hoàn toàn rỗng `{}`
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto();
        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.False(data.GetProperty("isPreview").GetBoolean());
    }

    [Fact]
    public async Task SaveMinute_TargetComponentWhitespaceOnly_WhenRejected_ReturnsBadRequest()
    {
        // Truyền chuỗi toàn khoảng trắng cho target_component_code khi RejectedAsPerMinute
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "RejectedAsPerMinute",
            TargetComponentCode: "    ",
            StudentFeedback: "Nội dung góp ý");

        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveMinute_StudentFeedbackWhitespaceOnly_TrimsToNullWithoutError()
    {
        // Truyền chuỗi toàn khoảng trắng cho student_feedback -> trim thành null
        var ids = SetupTestCouncil();
        Authenticate(ids.SecretaryUserId);

        var payload = new SaveCouncilMinuteInputDto(
            SecretaryMarking: "AcceptedAsPerMinute",
            StudentFeedback: "   \t \n  ");

        var response = await _client.PutAsJsonAsync($"/api/councils/assignments/{ids.AssignmentId}/minute", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var minute = _councilRepo.Minutes.FirstOrDefault(m => m.CouncilAssignmentId == ids.AssignmentId);
        Assert.NotNull(minute);
        Assert.Null(minute.RequiredRevisions);
    }

    [Fact]
    public async Task GetMinuteWorkspace_NoScoresEnteredYet_HandlesZeroDivisorSafelyWithoutCrashing()
    {
        // Nhóm chưa ai chấm điểm nào cả (GVHD = null, GVPB = null, TV Hội đồng = null)
        var ids = SetupTestCouncil(isReviewerInCouncil: true);

        // Xóa điểm GVHD và GVPB khỏi repo để test kịch bản hoàn toàn trống điểm
        _councilRepo.AdvisorEvaluations.Remove(ids.GroupId);
        var revAssignment = _councilRepo.ActiveReviewerAssignments[ids.GroupId];
        revAssignment.GetType().GetProperty("Evaluation")?.SetValue(revAssignment, null);

        Authenticate(ids.SecretaryUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        var calc = data.GetProperty("calculationPreview");
        Assert.Equal(0m, calc.GetProperty("rawTotal").GetDecimal());
        Assert.Equal(0m, calc.GetProperty("rawDivisor").GetDecimal());
        Assert.Equal(0m, calc.GetProperty("rawAverage").GetDecimal());
        Assert.Equal(0m, calc.GetProperty("adjustedTotal").GetDecimal());
        Assert.Equal(0m, calc.GetProperty("adjustedDivisor").GetDecimal());
        Assert.Equal(0m, calc.GetProperty("adjustedAverage").GetDecimal());
        Assert.False(calc.GetProperty("isReadyToLock").GetBoolean());
        Assert.Contains("Chưa có đủ điểm thành phần", calc.GetProperty("formulaExplanation").GetString());
    }

    [Fact]
    public async Task GetMinuteWorkspace_NonExistentTopicOrAdvisor_GracefullyFallbacksName()
    {
        // Nhóm tồn tại nhưng GroupContract không có TopicId hoặc Topic không tìm thấy trong TopicCatalog
        var ids = SetupTestCouncil();

        // Xóa Topic khỏi Contract mock để kiểm tra xử lý tham chiếu không tồn tại
        _topicDefenseContract.Topics.Clear();

        Authenticate(ids.SecretaryUserId);
        var response = await _client.GetAsync($"/api/councils/assignments/{ids.AssignmentId}/minute-workspace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        var scoreItems = data.GetProperty("scoreItems");
        var gvhdItem = scoreItems.EnumerateArray().FirstOrDefault(i => i.GetProperty("componentCode").GetString() == "ADVISOR");
        // Fallback tên mặc định "GVHD" không bị crash
        Assert.Equal("GVHD", gvhdItem.GetProperty("lecturerName").GetString());
    }

    private sealed class TestMinuteAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "MinuteTestAuthScheme";

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
