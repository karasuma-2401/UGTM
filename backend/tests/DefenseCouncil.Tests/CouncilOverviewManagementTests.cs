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
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilToAcademicAffairs;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;
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
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilOverviewManagementTests : IAsyncLifetime
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

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly DateOnly _defenseDate = new(2026, 12, 20);
    private const string TimeSlotMorning = "Ca sáng";
    private const string TimeSlotAfternoon = "Ca chiều";
    private const string RoomB1401 = "B1.401";
    private const string RoomB1402 = "B1.402";

    public async Task InitializeAsync()
    {
        _batchReader.ExistingBatches.Add(_batchId);

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
        builder.Services.AddScoped<IFinalSubmissionAuthorityReader, TestScoreAuthority>();

        builder.Services.AddScoped<CouncilMinuteWorkspaceLoader>();
        builder.Services.AddScoped<CreateCouncilHandler>();
        builder.Services.AddScoped<AddCouncilMembersHandler>();
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

        builder.Services.AddAuthentication(TestOverviewAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestOverviewAuthHandler>(TestOverviewAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapCouncilEndpoints();

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

    private void Authenticate(Guid userId, string role, params string[] permissions)
    {
        var token = $"test.{userId:N}.{role}";
        if (permissions.Length > 0)
        {
            token += $".{string.Join(",", permissions)}";
        }
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
        var errProp = doc.RootElement.GetProperty("error");
        return (
            errProp.GetProperty("code").GetString()!,
            errProp.GetProperty("message").GetString()!);
    }

    #region Helper Builders

    private (Council Council, Guid ChairmanId, Guid SecretaryId, Guid MemberId) CreateBaseCouncil(
        string code = "HĐ01",
        string room = RoomB1401,
        string slot = TimeSlotMorning)
    {
        var council = Council.Create(_batchId, code, _defenseDate, slot, room);
        _councilRepo.Councils.Add(council);

        var chId = Guid.NewGuid();
        var secId = Guid.NewGuid();
        var memId = Guid.NewGuid();

        _userInfoReader.Users[chId] = new CouncilUserInfoContractDto(chId, "TS. Lê Minh", "ch@uit.edu.vn", "CB01", true);
        _userInfoReader.Users[secId] = new CouncilUserInfoContractDto(secId, "TS. Nguyễn Bình", "sec@uit.edu.vn", "CB02", true);
        _userInfoReader.Users[memId] = new CouncilUserInfoContractDto(memId, "TS. Nguyễn An", "mem@uit.edu.vn", "CB03", true);

        var cm = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chId, RoleInCouncil = CouncilMemberRole.Chairman };
        var sm = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secId, RoleInCouncil = CouncilMemberRole.Secretary };
        var mm = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = memId, RoleInCouncil = CouncilMemberRole.Member };

        council.Members.Add(cm);
        council.Members.Add(sm);
        council.Members.Add(mm);
        _councilRepo.Members.AddRange([cm, sm, mm]);

        return (council, chId, secId, memId);
    }

    private (CouncilAssignment Assignment, Guid GroupId, Guid TopicId, Guid AdvisorId) AssignGroupToCouncil(
        Council council,
        int orderNumber = 1,
        string topicTitle = "Xây dựng ứng dụng KLTN")
    {
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();

        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "GVHD Đỗ Tuấn", "adv@uit.edu.vn", "CB04", true);

        var assignment = new CouncilAssignment
        {
            Id = Guid.NewGuid(),
            CouncilId = council.Id,
            GroupId = groupId,
            OrderNumber = orderNumber,
            Council = council
        };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, council.BatchId, "Eligible", DateTime.UtcNow, []);

        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId, council.BatchId, advisorId, topicTitle, null);

        return (assignment, groupId, topicId, advisorId);
    }

    #endregion

    #region 1. GET /api/councils Tests

    [Fact]
    public async Task GetCouncils_Unauthorized_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/councils");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCouncils_ForbiddenForStudent_Returns403()
    {
        Authenticate(Guid.NewGuid(), "Student");
        var response = await _client.GetAsync("/api/councils");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Faculty")]
    [InlineData("AcademicAffairs")]
    [InlineData("Admin")]
    public async Task GetCouncils_AllowedRoles_Returns200OK(string role)
    {
        Authenticate(Guid.NewGuid(), role);
        var response = await _client.GetAsync("/api/councils");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCouncils_WhenDraftCouncilHasNoAssignments_ReturnsRowWithNullAssignmentProperties()
    {
        var (council, chId, secId, memId) = CreateBaseCouncil("HĐ-EMPTY");

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/councils?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await ReadDataAsync(response);
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() >= 1);

        var item = data.EnumerateArray().First(x => x.GetProperty("id").GetString() == council.Id.ToString());
        Assert.Equal("HĐ-EMPTY", item.GetProperty("councilCode").GetString());
        Assert.Equal("Khoa CNTT", item.GetProperty("facultyName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assignmentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("groupCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("topicTitle").ValueKind);
        Assert.Equal("Pending", item.GetProperty("profileStatus").GetString());
        Assert.Equal("Chưa có phiếu", item.GetProperty("profileStatusLabel").GetString());
        Assert.Equal("Open", item.GetProperty("minuteLockStatus").GetString());
        Assert.Equal("Chưa khóa", item.GetProperty("minuteLockStatusLabel").GetString());
        Assert.Equal("IndependentMember", item.GetProperty("rosterModel").GetString());
        Assert.Equal("CT · TK · UV độc lập", item.GetProperty("rosterModelLabel").GetString());

        var chairman = item.GetProperty("chairman");
        Assert.Equal("TS. Lê Minh", chairman.GetProperty("fullName").GetString());
        Assert.False(chairman.GetProperty("isAdvisorConflict").GetBoolean());
    }

    [Fact]
    public async Task GetCouncils_WhenCouncilHasAssignedGroups_ReturnsFlattenedAssignmentsWithReviewerModel()
    {
        var (council, chId, secId, memId) = CreateBaseCouncil("HĐ01");
        var (assign1, grp1, top1, adv1) = AssignGroupToCouncil(council, orderNumber: 1, topicTitle: "Hệ thống RBAC");

        // Reviewer of Group 1 is the Council Member (ReviewerInCouncil)
        _councilRepo.ActiveReviewers[grp1] = memId;

        // Minute is Open
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign1.Id,
            LockStatus = CouncilMinuteLockStatus.Open
        });

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/councils?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await ReadDataAsync(response);
        var item = data.EnumerateArray().First(x => x.GetProperty("assignmentId").GetString() == assign1.Id.ToString());

        Assert.Equal("HĐ01", item.GetProperty("councilCode").GetString());
        Assert.Equal("N01", item.GetProperty("groupCode").GetString());
        Assert.Equal("Hệ thống RBAC", item.GetProperty("topicTitle").GetString());
        Assert.Equal("ReviewerInCouncil", item.GetProperty("rosterModel").GetString());
        Assert.Equal("CT · TK · GVPB kiêm UV", item.GetProperty("rosterModelLabel").GetString());

        var members = item.GetProperty("members").EnumerateArray().ToList();
        var reviewerMember = members.First(m => m.GetProperty("userId").GetString() == memId.ToString());
        Assert.True(reviewerMember.GetProperty("isReviewer").GetBoolean());
        Assert.Equal("Chỉ tham dự, không chấm UV", reviewerMember.GetProperty("note").GetString());
        Assert.False(reviewerMember.GetProperty("isAdvisorConflict").GetBoolean());
    }

    [Fact]
    public async Task GetCouncils_AdvisorConflict_FlaggedTrueWhenCouncilMemberIsAdvisor()
    {
        var (council, chId, secId, memId) = CreateBaseCouncil("HĐ-CONFLICT");
        var (assign, grp, top, _) = AssignGroupToCouncil(council, 1);

        // Advisor of this group is the Chairman!
        _topicDefenseContract.Topics[top] = new TopicDefenseDetailsContractDto(top, _batchId, chId, "Đề tài X", null);

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/councils?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await ReadDataAsync(response);
        var item = data.EnumerateArray().First(x => x.GetProperty("assignmentId").GetString() == assign.Id.ToString());

        var chairman = item.GetProperty("chairman");
        Assert.True(chairman.GetProperty("isAdvisorConflict").GetBoolean());
    }

    [Fact]
    public async Task GetCouncils_ProfileStatus_VerifiedWhenMinuteLocked()
    {
        var (council, chId, secId, memId) = CreateBaseCouncil("HĐ-LOCKED");
        council.Status = CouncilStatus.Active;
        var (assign, _, _, _) = AssignGroupToCouncil(council, 1);

        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign.Id,
            LockStatus = CouncilMinuteLockStatus.Locked,
            LockedAt = DateTime.UtcNow,
            LockedBy = secId
        });

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.GetAsync($"/api/councils?batchId={_batchId}&lockStatus=Locked");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await ReadDataAsync(response);
        var item = data.EnumerateArray().First(x => x.GetProperty("assignmentId").GetString() == assign.Id.ToString());

        Assert.Equal("Verified", item.GetProperty("profileStatus").GetString());
        Assert.Equal("Đã xác thực", item.GetProperty("profileStatusLabel").GetString());
        Assert.Equal("Locked", item.GetProperty("minuteLockStatus").GetString());
        Assert.Equal("Đã khóa", item.GetProperty("minuteLockStatusLabel").GetString());
    }

    [Fact]
    public async Task GetCouncils_FilterByKeyword_MatchesCodeOrTopicOrLecturer()
    {
        var (c1, _, _, _) = CreateBaseCouncil("HĐ-ALPHA", room: "B1.401");
        AssignGroupToCouncil(c1, 1, "Nghiên cứu PostgreSQL phân tán");

        var (c2, _, _, _) = CreateBaseCouncil("HĐ-BETA", room: "B1.402");
        AssignGroupToCouncil(c2, 1, "Trí tuệ nhân tạo Gemini");

        Authenticate(Guid.NewGuid(), "AcademicAffairs");

        var response = await _client.GetAsync("/api/councils?keyword=PostgreSQL");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.Single(data.EnumerateArray());
        Assert.Equal("HĐ-ALPHA", data.EnumerateArray().First().GetProperty("councilCode").GetString());
    }

    [Fact]
    public async Task GetCouncils_Pagination_CorrectlyPagesResults()
    {
        for (int i = 1; i <= 5; i++)
        {
            var (c, _, _, _) = CreateBaseCouncil($"HĐ-PAGE-{i:D2}");
            AssignGroupToCouncil(c, 1, $"Đề tài {i}");
        }

        Authenticate(Guid.NewGuid(), "Faculty");

        var p1Response = await _client.GetAsync("/api/councils?pageSize=2&pageNumber=1");
        Assert.Equal(HttpStatusCode.OK, p1Response.StatusCode);
        var p1Data = await ReadDataAsync(p1Response);
        Assert.Equal(2, p1Data.GetArrayLength());

        var p2Response = await _client.GetAsync("/api/councils?pageSize=2&pageNumber=2");
        Assert.Equal(HttpStatusCode.OK, p2Response.StatusCode);
        var p2Data = await ReadDataAsync(p2Response);
        Assert.Equal(2, p2Data.GetArrayLength());
    }

    #endregion

    #region 2. PUT /api/councils/{id} (UpdateCouncilSchedule) Tests

    [Fact]
    public async Task UpdateSchedule_Unauthorized_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PutAsJsonAsync($"/api/councils/{Guid.NewGuid()}",
            new UpdateCouncilScheduleInputDto(new DateOnly(2026, 12, 25), "Ca sáng", "B1.401"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSchedule_ForbiddenForAcademicAffairs_Returns403()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs"); // Only Faculty & Admin can update draft schedule
        var response = await _client.PutAsJsonAsync($"/api/councils/{Guid.NewGuid()}",
            new UpdateCouncilScheduleInputDto(new DateOnly(2026, 12, 25), "Ca sáng", "B1.401"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSchedule_NotFound_Returns404()
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PutAsJsonAsync($"/api/councils/{Guid.NewGuid()}",
            new UpdateCouncilScheduleInputDto(new DateOnly(2026, 12, 25), "Ca sáng", "B1.401"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.NotFound.Code, err.Code);
    }

    [Fact]
    public async Task UpdateSchedule_WhenNotDraft_Returns409Conflict()
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-ACTIVE");
        council.Status = CouncilStatus.Active;

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PutAsJsonAsync($"/api/councils/{council.Id}",
            new UpdateCouncilScheduleInputDto(new DateOnly(2026, 12, 25), "Ca chiều", "B1.402"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.NotInDraftStatus.Code, err.Code);
    }

    [Fact]
    public async Task UpdateSchedule_RoomDoubleBooking_Returns409Conflict()
    {
        var (c1, _, _, _) = CreateBaseCouncil("HĐ-FIRST", room: "B1.401", slot: "Ca sáng");
        var (c2, _, _, _) = CreateBaseCouncil("HĐ-SECOND", room: "B1.402", slot: "Ca chiều");

        Authenticate(Guid.NewGuid(), "Faculty");
        // Try to move c2 into B1.401 in "Ca sáng" (occupied by c1)
        var response = await _client.PutAsJsonAsync($"/api/councils/{c2.Id}",
            new UpdateCouncilScheduleInputDto(_defenseDate, "Ca sáng", "B1.401"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.RoomScheduleConflict.Code, err.Code);
    }

    [Fact]
    public async Task UpdateSchedule_MemberScheduleConflict_Returns409Conflict()
    {
        var (c1, ch1, _, _) = CreateBaseCouncil("HĐ-FIRST", room: "B1.401", slot: "Ca sáng");
        var (c2, _, _, _) = CreateBaseCouncil("HĐ-SECOND", room: "B1.402", slot: "Ca chiều");

        // ch1 is Chairman in c1 (Ca sáng). Add ch1 as Member in c2 (currently Ca chiều).
        var mConflict = new CouncilMember { Id = Guid.NewGuid(), CouncilId = c2.Id, UserId = ch1, RoleInCouncil = CouncilMemberRole.Member };
        c2.Members.Add(mConflict);
        _councilRepo.Members.Add(mConflict);

        Authenticate(Guid.NewGuid(), "Faculty");
        // Move c2 to "Ca sáng" -> ch1 is already in c1 in "Ca sáng"!
        var response = await _client.PutAsJsonAsync($"/api/councils/{c2.Id}",
            new UpdateCouncilScheduleInputDto(_defenseDate, "Ca sáng", "B1.402"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.MemberScheduleConflict("").Code, err.Code);
    }

    [Fact]
    public async Task UpdateSchedule_Succeeds_UpdatesDataAndResetsSubmissionFlag()
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-UPDATE", room: "B1.401", slot: "Ca sáng");
        council.IsSubmittedToAcademicAffairs = true;
        council.SubmittedAt = DateTime.UtcNow;

        Authenticate(Guid.NewGuid(), "Faculty");
        var newDate = new DateOnly(2026, 12, 28);
        var response = await _client.PutAsJsonAsync($"/api/councils/{council.Id}",
            new UpdateCouncilScheduleInputDto(newDate, "Ca chiều", "B1.505"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.Equal("2026-12-28", data.GetProperty("defenseDate").GetString());
        Assert.Equal("Ca chiều", data.GetProperty("timeSlot").GetString());
        Assert.Equal("B1.505", data.GetProperty("room").GetString());

        Assert.Equal(newDate, council.DefenseDate);
        Assert.Equal("Ca chiều", council.TimeSlot);
        Assert.Equal("B1.505", council.Room);
        Assert.False(council.IsSubmittedToAcademicAffairs); // Automatically reset
    }

    #endregion

    #region 3. POST /api/councils/assignments/{assignmentId}/minute/unlock Tests

    [Fact]
    public async Task UnlockMinute_WhenMinuteNotLocked_Returns400BadRequest()
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-UNL");
        var (assign, _, _, _) = AssignGroupToCouncil(council, 1);
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign.Id,
            LockStatus = CouncilMinuteLockStatus.Open
        });

        Authenticate(Guid.NewGuid(), "Admin");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{assign.Id}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do giải trình mở khóa hợp lệ"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.MinuteNotLocked.Code, err.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Quá ngắn")] // < 10 chars
    public async Task UnlockMinute_InvalidReasonLength_Returns400BadRequest(string reason)
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-UNL-LEN");
        var (assign, _, _, _) = AssignGroupToCouncil(council, 1);
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign.Id,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        Authenticate(Guid.NewGuid(), "Admin");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{assign.Id}/minute/unlock",
            new UnlockCouncilMinuteInputDto(reason));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.OverrideReasonRequired.Code, err.Code);
    }

    [Fact]
    public async Task UnlockMinute_ScoresAlreadyPublished_Returns409Conflict()
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-PUB");
        var (assign, grp, _, _) = AssignGroupToCouncil(council, 1);
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assign.Id,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        _councilRepo.ScoreSummaries[grp] = new ScoreSummary
        {
            Id = Guid.NewGuid(),
            GroupId = grp,
            Status = ScoreSummaryStatus.PublishedToStudent
        };

        Authenticate(Guid.NewGuid(), "Admin");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{assign.Id}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do mở khóa giải trình hợp lệ hơn mười ký tự."));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.ScoresAlreadyPublished.Code, err.Code);
    }

    [Fact]
    public async Task UnlockMinute_Succeeds_ReturnsMinuteIdAndSpecialLockStatus()
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-OK-UNL");
        var (assign, _, _, _) = AssignGroupToCouncil(council, 1);
        var minuteId = Guid.NewGuid();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = minuteId,
            CouncilAssignmentId = assign.Id,
            LockStatus = CouncilMinuteLockStatus.Locked,
            LockedAt = DateTime.UtcNow
        });

        var adminId = Guid.NewGuid();
        Authenticate(adminId, "Admin");
        const string reason = "Phát hiện nhầm lẫn trong barem điểm tiêu chí chuyên môn, cấp quyền mở khóa.";

        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{assign.Id}/minute/unlock",
            new UnlockCouncilMinuteInputDto(reason));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.Equal(minuteId.ToString(), data.GetProperty("minuteId").GetString());
        Assert.Equal(assign.Id.ToString(), data.GetProperty("assignmentId").GetString());
        Assert.Equal("UnlockedByOverride", data.GetProperty("lockStatus").GetString());
        Assert.Equal("Mở khóa đặc biệt", data.GetProperty("lockStatusLabel").GetString());
        Assert.Equal(reason, data.GetProperty("overrideReason").GetString());
        Assert.Equal(adminId.ToString(), data.GetProperty("unlockedBy").GetString());
        Assert.NotNull(data.GetProperty("unlockedAt").GetString());
    }

    #endregion

    #region 4. POST /api/councils/{id}/submit Tests

    [Fact]
    public async Task SubmitCouncil_Unauthorized_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync($"/api/councils/{Guid.NewGuid()}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitCouncil_ForbiddenForAcademicAffairs_Returns403()
    {
        Authenticate(Guid.NewGuid(), "AcademicAffairs"); // Only Faculty & Admin submit
        var response = await _client.PostAsJsonAsync($"/api/councils/{Guid.NewGuid()}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitCouncil_NotFound_Returns404()
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PostAsJsonAsync($"/api/councils/{Guid.NewGuid()}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmitCouncil_WhenNotDraft_Returns409Conflict()
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-SUB-ACT");
        council.Status = CouncilStatus.Active;

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PostAsJsonAsync($"/api/councils/{council.Id}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.NotInDraftStatus.Code, err.Code);
    }

    [Fact]
    public async Task SubmitCouncil_MissingChairman_Returns400BadRequest()
    {
        var council = Council.Create(_batchId, "HĐ-NOCHAIR", _defenseDate, TimeSlotMorning, RoomB1401);
        _councilRepo.Councils.Add(council);
        // Only secretary
        var secId = Guid.NewGuid();
        var sm = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secId, RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(sm);
        _councilRepo.Members.Add(sm);

        AssignGroupToCouncil(council, 1);

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PostAsJsonAsync($"/api/councils/{council.Id}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.MissingChairman.Code, err.Code);
    }

    [Fact]
    public async Task SubmitCouncil_MissingSecretary_Returns400BadRequest()
    {
        var council = Council.Create(_batchId, "HĐ-NOSEC", _defenseDate, TimeSlotMorning, RoomB1401);
        _councilRepo.Councils.Add(council);
        // Only chairman
        var chId = Guid.NewGuid();
        var cm = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chId, RoleInCouncil = CouncilMemberRole.Chairman };
        council.Members.Add(cm);
        _councilRepo.Members.Add(cm);

        AssignGroupToCouncil(council, 1);

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PostAsJsonAsync($"/api/councils/{council.Id}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.MissingSecretary.Code, err.Code);
    }

    [Fact]
    public async Task SubmitCouncil_NoAssignedGroups_Returns400BadRequest()
    {
        var (council, _, _, _) = CreateBaseCouncil("HĐ-NO-GRP");
        // No assignments

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PostAsJsonAsync($"/api/councils/{council.Id}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.NoAssignedGroups.Code, err.Code);
    }

    [Fact]
    public async Task SubmitCouncil_AdvisorIsInCouncil_Returns409Conflict()
    {
        var (council, chId, secId, memId) = CreateBaseCouncil("HĐ-ADV-CONFLICT");
        var (assign, grp, top, _) = AssignGroupToCouncil(council, 1);

        // Advisor of this group is the Chairman!
        _topicDefenseContract.Topics[top] = new TopicDefenseDetailsContractDto(top, _batchId, chId, "Đề tài X", null);

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PostAsJsonAsync($"/api/councils/{council.Id}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.AdvisorIsInCouncil("", "").Code, err.Code);
    }

    [Fact]
    public async Task SubmitCouncil_ReviewerIsChairmanOrSecretary_Returns409Conflict()
    {
        var (council, chId, secId, memId) = CreateBaseCouncil("HĐ-REV-CHAIR");
        var (assign, grp, top, _) = AssignGroupToCouncil(council, 1);

        // Active reviewer of group is Chairman!
        _councilRepo.ActiveReviewers[grp] = chId;

        Authenticate(Guid.NewGuid(), "Faculty");
        var response = await _client.PostAsJsonAsync($"/api/councils/{council.Id}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await ReadErrorAsync(response);
        Assert.Equal(CouncilErrors.ReviewerIsChairmanOrSecretaryInCouncil("", "").Code, err.Code);
    }

    [Fact]
    public async Task SubmitCouncil_Succeeds_UpdatesCouncilAndEmitsAuditLog()
    {
        var (council, chId, secId, memId) = CreateBaseCouncil("HĐ-SUB-SUCCESS");
        AssignGroupToCouncil(council, 1);

        var facultyId = Guid.NewGuid();
        Authenticate(facultyId, "Faculty");
        const string notes = "Khoa CNTT đã hoàn tất xếp lịch và kiểm tra phân công thành viên, kính gửi PĐT.";

        var response = await _client.PostAsJsonAsync($"/api/councils/{council.Id}/submit",
            new SubmitCouncilToAcademicAffairsInputDto(notes));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);

        Assert.Equal(council.Id.ToString(), data.GetProperty("id").GetString());
        Assert.Equal("HĐ-SUB-SUCCESS", data.GetProperty("councilCode").GetString());
        Assert.Equal("Draft", data.GetProperty("status").GetString());
        Assert.True(data.GetProperty("isSubmittedToAcademicAffairs").GetBoolean());
        Assert.NotNull(data.GetProperty("submittedAt").GetString());
        Assert.Equal(facultyId.ToString(), data.GetProperty("submittedBy").GetString());
        Assert.Equal(notes, data.GetProperty("notes").GetString());

        Assert.True(council.IsSubmittedToAcademicAffairs);
        Assert.Equal(notes, council.SubmissionNotes);
        Assert.Equal(facultyId, council.SubmittedBy);

        Assert.Contains(_auditOutbox.Entries, e =>
            e.Entry.Action == "DefenseCouncil.Council.SubmittedToAcademicAffairs"
            && e.Entry.TargetId == council.Id.ToString());
    }

    #endregion

    private sealed class TestOverviewAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CouncilOverviewTestScheme";

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

            if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
            {
                var permissions = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var perm in permissions)
                {
                    claims.Add(new Claim("permission", perm));
                }
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
