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
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMemberCandidates;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.RemoveCouncilMember;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilToAcademicAffairs;
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

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilEndpointsE2ETests : IAsyncLifetime
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
    private const string TimeSlotMorning = "08:00 - 11:30";
    private const string TimeSlotAfternoon = "13:30 - 17:00";
    private const string RoomA101 = "A101";
    private const string RoomB202 = "B202";

    public async Task InitializeAsync()
    {
        _batchReader.ExistingBatches.Add(_batchId);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Register domain & application services
        builder.Services.AddSingleton<ThesisGradeCalculator>();
        builder.Services.AddScoped<IGradeCalculationService, GradeCalculationService>();

        // Register fake test dependencies
        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IDefenseCouncilUnitOfWork>(_uow);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentInfoReader);
        builder.Services.AddSingleton<IDefenseGroupContract>(_defenseGroupContract);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicDefenseContract);
        builder.Services.AddSingleton<IDefenseCouncilAuditOutbox>(_auditOutbox);
        builder.Services.AddSingleton<IReauthenticationVerifier>(_reauthVerifier);

        // Register council command and query handlers
        builder.Services.AddScoped<CouncilMinuteWorkspaceLoader>();
        builder.Services.AddSingleton<UGTM.Modules.DefenseCouncil.Application.ScoreSummaries.ICouncilMinuteWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddScoped<UGTM.Modules.Identity.Contracts.FinalSubmissions.IFinalSubmissionAuthorityReader, TestScoreAuthority>();
        builder.Services.AddSingleton<UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries.IScoreSummaryConfigurationReader, FakeMinuteConfigurationReader>();
        builder.Services.AddScoped<CreateCouncilHandler>();
        builder.Services.AddScoped<AddCouncilMembersHandler>();
        builder.Services.AddScoped<GetCouncilMemberCandidatesHandler>();
        builder.Services.AddScoped<UpdateCouncilMembersHandler>();
        builder.Services.AddScoped<RemoveCouncilMemberHandler>();
        builder.Services.AddScoped<AssignCouncilGroupsHandler>();
        builder.Services.AddSingleton<UGTM.Modules.DefenseCouncil.Application.Councils.Common.ICouncilSchedulingGuard, FakeCouncilSchedulingGuard>();
        builder.Services.AddScoped<ApproveCouncilHandler>();
        builder.Services.AddScoped<GetCouncilScheduleHandler>();
        builder.Services.AddScoped<GetCouncilMinuteWorkspaceHandler>();
        builder.Services.AddScoped<SaveCouncilMinuteHandler>();
        builder.Services.AddScoped<LockCouncilMinuteHandler>();
        builder.Services.AddScoped<UnlockCouncilMinuteHandler>();
        builder.Services.AddScoped<GetCouncilsOverviewHandler>();
        builder.Services.AddScoped<UpdateCouncilScheduleHandler>();
        builder.Services.AddScoped<SubmitCouncilToAcademicAffairsHandler>();

        // Setup test authentication
        builder.Services.AddAuthentication(TestE2EAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestE2EAuthHandler>(TestE2EAuthHandler.SchemeName, _ => { });
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

    private void Authenticate(Guid userId, string role)
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

    private static async Task<bool> ReadSuccessAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("success").GetBoolean();
    }

    #region 1. Full E2E Lifecycle (Happy Path)

    [Fact]
    public async Task E2E_FullCouncilLifecycle_FromDraftToApprovedAndScheduleLookup_Succeeds()
    {
        var facultyId = Guid.NewGuid();
        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var studentId1 = Guid.NewGuid();
        var studentId2 = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        // Seed users
        _userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "PGS. TS. Trần Chủ Tịch", "chair@uit.edu.vn", "CB001", true);
        _userInfoReader.Users[secretaryId] = new CouncilUserInfoContractDto(secretaryId, "ThS. Nguyễn Thư Ký", "sec@uit.edu.vn", "CB002", true);
        _userInfoReader.Users[memberId] = new CouncilUserInfoContractDto(memberId, "TS. Lê Ủy Viên", "member@uit.edu.vn", "CB003", true);
        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "TS. Phạm Hướng Dẫn", "hd@uit.edu.vn", "CB004", true);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. Đỗ Phản Biện", "pb@uit.edu.vn", "CB005", true);

        // Seed students
        _studentInfoReader.Students[studentId1] = new StudentInfoContractDto(studentId1, "student1@gm.uit.edu.vn", "Nguyễn Văn Sinh Viên 1", "21520001", "0901234567");
        _studentInfoReader.Students[studentId2] = new StudentInfoContractDto(studentId2, "student2@gm.uit.edu.vn", "Trần Thị Sinh Viên 2", "21520002", "0901234568");

        // Seed group and topic
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, _batchId, "InProgress", DateTime.UtcNow,
            [
                new(studentId1, _batchId, "Confirmed"),
                new(studentId2, _batchId, "Confirmed")
            ]);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId, _batchId, advisorId, "Hệ thống quản lý khóa luận", "UGTM System");
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        // Step 1: Faculty creates council draft (POST /api/councils)
        Authenticate(facultyId, "Faculty");
        var createPayload = new CreateCouncilInputDto(_batchId, "HD-KLTN-01", _defenseDate, TimeSlotMorning, RoomA101);
        var createResponse = await _client.PostAsJsonAsync("/api/councils", createPayload);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.True(await ReadSuccessAsync(createResponse));
        var createData = await ReadDataAsync(createResponse);
        var councilId = createData.GetProperty("id").GetGuid();
        Assert.Equal("HD-KLTN-01", createData.GetProperty("councilCode").GetString());
        Assert.Equal("Draft", createData.GetProperty("status").GetString());
        Assert.Equal(RoomA101, createData.GetProperty("room").GetString());

        // Step 2: Faculty adds members (POST /api/councils/{id}/members)
        var addMembersPayload = new AddCouncilMembersInputDto(
            Members: [
                new(chairmanId, CouncilMemberRole.Chairman),
                new(secretaryId, CouncilMemberRole.Secretary),
                new(memberId, CouncilMemberRole.Member)
            ]);
        var addMembersResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members", addMembersPayload);

        Assert.Equal(HttpStatusCode.OK, addMembersResponse.StatusCode);
        Assert.True(await ReadSuccessAsync(addMembersResponse));
        var membersData = await ReadDataAsync(addMembersResponse);
        Assert.Equal(3, membersData.GetArrayLength());

        // Step 3: Faculty assigns student group (POST /api/councils/{id}/assignments)
        var assignGroupPayload = new AssignCouncilGroupsInputDto(
            Assignments: [new(groupId, 1)]);
        var assignGroupResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments", assignGroupPayload);

        Assert.Equal(HttpStatusCode.OK, assignGroupResponse.StatusCode);
        Assert.True(await ReadSuccessAsync(assignGroupResponse));
        var assignData = await ReadDataAsync(assignGroupResponse);
        Assert.Equal(1, assignData.GetArrayLength());
        Assert.Equal("Hệ thống quản lý khóa luận", assignData[0].GetProperty("topicTitleVi").GetString());

        // Step 4: Student attempts to view schedule while council is still Draft -> Hidden (404)
        Authenticate(studentId1, "Student");
        var studentDraftLookup = await _client.GetAsync($"/api/councils/schedule/{groupId}");
        Assert.Equal(HttpStatusCode.NotFound, studentDraftLookup.StatusCode);
        var studentDraftError = await ReadErrorAsync(studentDraftLookup);
        Assert.Equal("Council.ScheduleNotPublished", studentDraftError.Code);

        // Step 5: Faculty/Staff can preview schedule while council is Draft -> 200 OK
        Authenticate(facultyId, "Faculty");
        var facultyDraftLookup = await _client.GetAsync($"/api/councils/schedule/{groupId}");
        Assert.Equal(HttpStatusCode.OK, facultyDraftLookup.StatusCode);
        var facultyDraftData = await ReadDataAsync(facultyDraftLookup);
        Assert.Equal("Draft", facultyDraftData.GetProperty("status").GetString());

        // Step 5b: Faculty submits council to Academic Affairs (POST /api/councils/{id}/submit)
        var submitResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit", new SubmitCouncilToAcademicAffairsInputDto("Gửi PĐT thẩm định"));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // Step 6: Academic Affairs approves council (PUT /api/councils/{id}/approve)
        var aaUserId = Guid.NewGuid();
        Authenticate(aaUserId, "AcademicAffairs");
        var approvePayload = new ApproveCouncilInputDto("123/QĐ-ĐHCNTT", new DateOnly(2026, 12, 1), "https://storage.uit.edu.vn/qd-123.pdf");
        var approveResponse = await _client.PutAsJsonAsync($"/api/councils/{councilId}/approve", approvePayload);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.True(await ReadSuccessAsync(approveResponse));
        var approveData = await ReadDataAsync(approveResponse);
        Assert.Equal("Active", approveData.GetProperty("status").GetString());
        Assert.Equal("123/QĐ-ĐHCNTT", approveData.GetProperty("decisionNumber").GetString());
        Assert.Equal(2, _auditOutbox.Entries.Count);
        Assert.Contains(_auditOutbox.Entries, e => e.Entry.Action == "DefenseCouncil.Council.SubmittedToAcademicAffairs");
        Assert.Contains(_auditOutbox.Entries, e => e.Entry.Action == "DefenseCouncil.Council.Approved");

        // Step 7: Student can now view published schedule by groupId -> 200 OK
        Authenticate(studentId1, "Student");
        var studentScheduleResponse = await _client.GetAsync($"/api/councils/schedule/{groupId}");
        Assert.Equal(HttpStatusCode.OK, studentScheduleResponse.StatusCode);
        Assert.True(await ReadSuccessAsync(studentScheduleResponse));
        var schedule = await ReadDataAsync(studentScheduleResponse);
        Assert.Equal("HD-KLTN-01", schedule.GetProperty("councilCode").GetString());
        Assert.Equal(RoomA101, schedule.GetProperty("room").GetString());
        Assert.Equal(TimeSlotMorning, schedule.GetProperty("timeSlot").GetString());
        Assert.Equal("Active", schedule.GetProperty("status").GetString());
        Assert.Equal(3, schedule.GetProperty("members").GetArrayLength());

        var groupsArray = schedule.GetProperty("groups");
        Assert.Equal(1, groupsArray.GetArrayLength());
        var assignedGroup = groupsArray[0];
        Assert.Equal(1, assignedGroup.GetProperty("orderNumber").GetInt32());
        Assert.Equal("Hệ thống quản lý khóa luận", assignedGroup.GetProperty("topicTitleVi").GetString());
        Assert.Equal("TS. Phạm Hướng Dẫn", assignedGroup.GetProperty("advisorName").GetString());
        Assert.Equal("TS. Đỗ Phản Biện", assignedGroup.GetProperty("reviewerName").GetString());
        Assert.False(assignedGroup.GetProperty("isReviewerInCouncil").GetBoolean());

        var studentsArray = assignedGroup.GetProperty("students");
        Assert.Equal(2, studentsArray.GetArrayLength());
        Assert.Equal("21520001", studentsArray[0].GetProperty("userCode").GetString());
        Assert.Equal("21520002", studentsArray[1].GetProperty("userCode").GetString());

        // Step 8: Student can also query schedule by their own studentId
        var studentIdScheduleResponse = await _client.GetAsync($"/api/councils/schedule/{studentId2}");
        Assert.Equal(HttpStatusCode.OK, studentIdScheduleResponse.StatusCode);
        var studentIdData = await ReadDataAsync(studentIdScheduleResponse);
        Assert.Equal(councilId, studentIdData.GetProperty("councilId").GetGuid());

        // Step 9: Post-approval immutability: attempting to add member after approval fails (409 Conflict)
        Authenticate(facultyId, "Faculty");
        var newMemberId = Guid.NewGuid();
        _userInfoReader.Users[newMemberId] = new CouncilUserInfoContractDto(newMemberId, "Extra Member", "extra@uit.edu.vn", "CB099", true);
        var postApprovalMemberResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(newMemberId, CouncilMemberRole.Member)]));
        Assert.Equal(HttpStatusCode.Conflict, postApprovalMemberResponse.StatusCode);
        var postApprovalMemberError = await ReadErrorAsync(postApprovalMemberResponse);
        Assert.Equal("Council.NotInDraftStatus", postApprovalMemberError.Code);

        // Step 10: Post-approval immutability: attempting to assign group after approval fails (409 Conflict)
        var postApprovalGroupResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(Guid.NewGuid(), 2)]));
        Assert.Equal(HttpStatusCode.Conflict, postApprovalGroupResponse.StatusCode);
        var postApprovalGroupError = await ReadErrorAsync(postApprovalGroupResponse);
        Assert.Equal("Council.NotInDraftStatus", postApprovalGroupError.Code);
    }

    #endregion

    #region 2. Conflict of Interest E2E (Hard Validation)

    [Fact]
    public async Task E2E_ConflictOfInterest_WhenAddingMemberWhoIsAdvisorOfAssignedGroup_FailsWith409()
    {
        // Direction 1: Group is assigned first -> Member who is Advisor cannot be added
        var facultyId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "TS. Trần Hướng Dẫn", "hd@uit.edu.vn", "CB010", true);
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài AI", null);
        var defaultReviewerId = Guid.NewGuid();
        _councilRepo.ActiveReviewers[groupId] = defaultReviewerId;
        _userInfoReader.Users[defaultReviewerId] = new(defaultReviewerId, "GVPB", "reviewer@example.test", "GV02", true);

        Authenticate(facultyId, "Faculty");

        // 1. Create council
        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-COI-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        // 2. Assign group
        var assignedResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        Assert.Equal(HttpStatusCode.OK, assignedResponse.StatusCode);

        // 3. Attempt to add the Advisor as Chairman
        var addMemberResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(advisorId, CouncilMemberRole.Chairman)]));

        Assert.Equal(HttpStatusCode.Conflict, addMemberResponse.StatusCode);
        var error = await ReadErrorAsync(addMemberResponse);
        Assert.Equal("Council.MemberIsGroupAdvisor", error.Code);
        Assert.Contains("xung đột lợi ích", error.Message);
    }

    [Fact]
    public async Task E2E_ConflictOfInterest_WhenAssigningGroupWhoseAdvisorIsCouncilMember_FailsWith409()
    {
        // Direction 2: Member added first -> Group whose Advisor is Member cannot be assigned
        var facultyId = Guid.NewGuid();
        var advisorAndMemberId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[advisorAndMemberId] = new CouncilUserInfoContractDto(advisorAndMemberId, "TS. Lê Thành Viên", "tv@uit.edu.vn", "CB011", true);
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorAndMemberId, "Đề tài Blockchain", null);

        Authenticate(facultyId, "Faculty");

        // 1. Create council
        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-COI-02", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        // 2. Add member
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(advisorAndMemberId, CouncilMemberRole.Secretary)]));

        // 3. Attempt to assign group advised by this member
        var assignResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));

        Assert.Equal(HttpStatusCode.Conflict, assignResponse.StatusCode);
        var error = await ReadErrorAsync(assignResponse);
        Assert.Equal("Council.AdvisorIsInCouncil", error.Code);
        Assert.Contains("xung đột lợi ích", error.Message);
    }

    #endregion

    #region 3. University Special Case E2E (Reviewer as Member / 0 independent Members)

    [Fact]
    public async Task E2E_UniversitySpecialCase_CouncilWithReviewerAsMember_Succeeds()
    {
        var facultyId = Guid.NewGuid();
        var aaUserId = Guid.NewGuid();
        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();
        var reviewerAndMemberId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "Chủ tịch Hội đồng", "ct@uit.edu.vn", "CB021", true);
        _userInfoReader.Users[secretaryId] = new CouncilUserInfoContractDto(secretaryId, "Thư ký Hội đồng", "sec@uit.edu.vn", "CB022", true);
        _userInfoReader.Users[reviewerAndMemberId] = new CouncilUserInfoContractDto(reviewerAndMemberId, "GVPB kiêm Ủy viên", "pb@uit.edu.vn", "CB023", true);
        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "GVHD Độc lập", "hd@uit.edu.vn", "CB024", true);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài BigData", null);
        _councilRepo.ActiveReviewers[groupId] = reviewerAndMemberId; // Reviewer is reviewerAndMemberId

        Authenticate(facultyId, "Faculty");

        // 1. Create council
        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-SPEC-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        // 2. Add members: Chairman, Secretary, and Reviewer as Member
        var addMemberResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [
                new(chairmanId, CouncilMemberRole.Chairman),
                new(secretaryId, CouncilMemberRole.Secretary),
                new(reviewerAndMemberId, CouncilMemberRole.Member) // Reviewer is Member
            ]));
        Assert.Equal(HttpStatusCode.OK, addMemberResponse.StatusCode);

        // 3. Assign group
        var assignResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        // 3b. Faculty submits council
        var facultyUserId = Guid.NewGuid();
        Authenticate(facultyUserId, "Faculty");
        var submitResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit",
            new SubmitCouncilToAcademicAffairsInputDto("Submit"));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // 4. Academic Affairs approves council -> 200 OK
        Authenticate(aaUserId, "AcademicAffairs");
        var approveResponse = await _client.PutAsJsonAsync($"/api/councils/{councilId}/approve",
            new ApproveCouncilInputDto("QD-SPEC-01"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // 5. Query schedule -> isReviewerInCouncil is true
        var scheduleResponse = await _client.GetAsync($"/api/councils/schedule/{groupId}");
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
        var schedule = await ReadDataAsync(scheduleResponse);
        Assert.Equal("Active", schedule.GetProperty("status").GetString());
        Assert.Equal(3, schedule.GetProperty("members").GetArrayLength()); // 3 members in council

        var group = schedule.GetProperty("groups")[0];
        Assert.True(group.GetProperty("isReviewerInCouncil").GetBoolean());
        Assert.Equal(reviewerAndMemberId, group.GetProperty("reviewerId").GetGuid());
    }

    [Fact]
    public async Task E2E_AssignGroup_Fails_WhenReviewerIsSecretaryInCouncil()
    {
        var facultyId = Guid.NewGuid();
        var chairmanId = Guid.NewGuid();
        var reviewerAndSecretaryId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "Chủ tịch Hội đồng", "ct@uit.edu.vn", "CB031", true);
        _userInfoReader.Users[reviewerAndSecretaryId] = new CouncilUserInfoContractDto(reviewerAndSecretaryId, "GVPB xếp làm Thư ký", "pbsec@uit.edu.vn", "CB032", true);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài IoT", null);
        _councilRepo.ActiveReviewers[groupId] = reviewerAndSecretaryId;

        Authenticate(facultyId, "Faculty");

        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-SPEC-02", _defenseDate, TimeSlotMorning, RoomB202));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        var addMemberResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [
                new(chairmanId, CouncilMemberRole.Chairman),
                new(reviewerAndSecretaryId, CouncilMemberRole.Secretary)
            ]));
        Assert.Equal(HttpStatusCode.OK, addMemberResponse.StatusCode);

        var assignResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        Assert.Equal(HttpStatusCode.Conflict, assignResponse.StatusCode);
        var error = await ReadErrorAsync(assignResponse);
        Assert.Equal("Council.ReviewerIsChairmanOrSecretaryInCouncil", error.Code);
    }

    [Fact]
    public async Task E2E_AssignGroup_Fails_WhenGroupIsNotEligibleForDefense()
    {
        var facultyId = Guid.NewGuid();
        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "Chủ tịch Hội đồng", "ct@uit.edu.vn", "CB041", true);
        _userInfoReader.Users[secretaryId] = new CouncilUserInfoContractDto(secretaryId, "Thư ký Hội đồng", "sec@uit.edu.vn", "CB042", true);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài chưa đủ điều kiện", null);

        // Nhóm chưa hoàn thành phản biện
        _councilRepo.IneligibleDefenseGroupIds.Add(groupId);

        Authenticate(facultyId, "Faculty");

        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-SPEC-03", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [
                new(chairmanId, CouncilMemberRole.Chairman),
                new(secretaryId, CouncilMemberRole.Secretary)
            ]));

        var assignResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        Assert.Equal(HttpStatusCode.Conflict, assignResponse.StatusCode);
        var error = await ReadErrorAsync(assignResponse);
        Assert.Equal("Council.GroupNotEligibleForDefense", error.Code);
    }

    #endregion

    #region 4. Schedule Conflicts E2E (Room & Member Anti-Double Booking)

    [Fact]
    public async Task E2E_ScheduleConflict_RoomDoubleBooking_FailsWith409()
    {
        var facultyId = Guid.NewGuid();
        Authenticate(facultyId, "Faculty");

        // 1. Create Council 1 at Date D, Slot S, Room R1
        var c1Response = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-ROOM-01", _defenseDate, TimeSlotMorning, RoomA101));
        Assert.Equal(HttpStatusCode.OK, c1Response.StatusCode);

        // 2. Attempt to create Council 2 at same Date D, same Slot S, same Room R1 (case-insensitive & whitespace trimmed)
        var c2Response = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-ROOM-02", _defenseDate, " 08:00 - 11:30 ", " a101 "));
        Assert.Equal(HttpStatusCode.Conflict, c2Response.StatusCode);
        var error = await ReadErrorAsync(c2Response);
        Assert.Equal("Council.RoomScheduleConflict", error.Code);
    }

    [Fact]
    public async Task E2E_ScheduleConflict_MemberDoubleBooking_FailsWith409()
    {
        var facultyId = Guid.NewGuid();
        var busyLecturerId = Guid.NewGuid();
        _userInfoReader.Users[busyLecturerId] = new CouncilUserInfoContractDto(busyLecturerId, "TS. Busy", "busy@uit.edu.vn", "CB030", true);

        Authenticate(facultyId, "Faculty");

        // 1. Create Council 1 at Room A101
        var c1Response = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-BUSY-01", _defenseDate, TimeSlotMorning, RoomA101));
        var c1Id = (await ReadDataAsync(c1Response)).GetProperty("id").GetGuid();

        // Add busyLecturer to Council 1
        await _client.PostAsJsonAsync($"/api/councils/{c1Id}/members",
            new AddCouncilMembersInputDto(Members: [new(busyLecturerId, CouncilMemberRole.Chairman)]));

        // 2. Create Council 2 at same Date and Slot, but different Room B202
        var c2Response = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-BUSY-02", _defenseDate, TimeSlotMorning, RoomB202));
        var c2Id = (await ReadDataAsync(c2Response)).GetProperty("id").GetGuid();

        // 3. Attempt to add busyLecturer to Council 2 at same time
        var addMemberResponse = await _client.PostAsJsonAsync($"/api/councils/{c2Id}/members",
            new AddCouncilMembersInputDto(Members: [new(busyLecturerId, CouncilMemberRole.Member)]));

        Assert.Equal(HttpStatusCode.Conflict, addMemberResponse.StatusCode);
        var error = await ReadErrorAsync(addMemberResponse);
        Assert.Equal("Council.MemberScheduleConflict", error.Code);
    }

    #endregion

    #region 5. Security & RBAC E2E

    [Fact]
    public async Task E2E_Security_UnauthenticatedRequests_FailWith401()
    {
        ClearAuthentication();

        var postCouncil = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101));
        Assert.Equal(HttpStatusCode.Unauthorized, postCouncil.StatusCode);

        var postMembers = await _client.PostAsJsonAsync($"/api/councils/{Guid.NewGuid()}/members",
            new AddCouncilMembersInputDto(Members: []));
        Assert.Equal(HttpStatusCode.Unauthorized, postMembers.StatusCode);

        var postAssignments = await _client.PostAsJsonAsync($"/api/councils/{Guid.NewGuid()}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: []));
        Assert.Equal(HttpStatusCode.Unauthorized, postAssignments.StatusCode);

        var putApprove = await _client.PutAsJsonAsync($"/api/councils/{Guid.NewGuid()}/approve",
            new ApproveCouncilInputDto("QD-01"));
        Assert.Equal(HttpStatusCode.Unauthorized, putApprove.StatusCode);

        var getSchedule = await _client.GetAsync($"/api/councils/schedule/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, getSchedule.StatusCode);
    }

    [Fact]
    public async Task E2E_Security_StudentAttemptingManagementAPIs_FailsWith403()
    {
        Authenticate(Guid.NewGuid(), "Student");

        var postCouncil = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101));
        Assert.Equal(HttpStatusCode.Forbidden, postCouncil.StatusCode);

        var postMembers = await _client.PostAsJsonAsync($"/api/councils/{Guid.NewGuid()}/members",
            new AddCouncilMembersInputDto(Members: [new(Guid.NewGuid(), CouncilMemberRole.Chairman)]));
        Assert.Equal(HttpStatusCode.Forbidden, postMembers.StatusCode);

        var postAssignments = await _client.PostAsJsonAsync($"/api/councils/{Guid.NewGuid()}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(Guid.NewGuid(), 1)]));
        Assert.Equal(HttpStatusCode.Forbidden, postAssignments.StatusCode);

        var putApprove = await _client.PutAsJsonAsync($"/api/councils/{Guid.NewGuid()}/approve",
            new ApproveCouncilInputDto("QD-01"));
        Assert.Equal(HttpStatusCode.Forbidden, putApprove.StatusCode);
    }

    [Fact]
    public async Task E2E_Security_FacultyAttemptingToApproveCouncil_FailsWith403()
    {
        Authenticate(Guid.NewGuid(), "Faculty");

        var putApprove = await _client.PutAsJsonAsync($"/api/councils/{Guid.NewGuid()}/approve",
            new ApproveCouncilInputDto("QD-01"));
        Assert.Equal(HttpStatusCode.Forbidden, putApprove.StatusCode);
    }

    #endregion

    #region 6. Boundary & Abnormal Cases E2E

    [Theory]
    [InlineData("", "08:00 - 11:30", "A101")]
    [InlineData("HD01", "", "A101")]
    [InlineData("HD01", "08:00 - 11:30", "")]
    public async Task E2E_Boundary_CreateCouncilWithEmptyFields_FailsWith400(string code, string slot, string room)
    {
        Authenticate(Guid.NewGuid(), "Faculty");

        var response = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, code, _defenseDate, slot, room));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadErrorAsync(response);
        Assert.Equal("Council.InvalidInput", error.Code);
    }

    [Fact]
    public async Task E2E_Boundary_AssignGroupRejectedByReviewer_FailsWith409()
    {
        var facultyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài bị đình chỉ", null);
        _councilRepo.RejectedGroupIds.Add(groupId); // Reviewer rejected defense

        Authenticate(facultyId, "Faculty");

        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-REJ-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        var assignResponse = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));

        Assert.Equal(HttpStatusCode.Conflict, assignResponse.StatusCode);
        var error = await ReadErrorAsync(assignResponse);
        Assert.Equal("Council.GroupDefenseRejectedByReviewer", error.Code);
    }

    [Fact]
    public async Task E2E_Boundary_ApproveCouncilWithoutAssignedGroups_FailsWith400()
    {
        var facultyId = Guid.NewGuid();
        var aaId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var sId = Guid.NewGuid();

        _userInfoReader.Users[cId] = new CouncilUserInfoContractDto(cId, "Chủ tịch", "ct@uit.edu.vn", "CB041", true);
        _userInfoReader.Users[sId] = new CouncilUserInfoContractDto(sId, "Thư ký", "tk@uit.edu.vn", "CB042", true);

        Authenticate(facultyId, "Faculty");

        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-NOGRP-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(cId, CouncilMemberRole.Chairman), new(sId, CouncilMemberRole.Secretary)]));

        Authenticate(aaId, "AcademicAffairs");
        var approveResponse = await _client.PutAsJsonAsync($"/api/councils/{councilId}/approve",
            new ApproveCouncilInputDto("QD-NOGROUP-01"));

        Assert.Equal(HttpStatusCode.BadRequest, approveResponse.StatusCode);
        var error = await ReadErrorAsync(approveResponse);
        Assert.Equal("Council.NoAssignedGroups", error.Code);
    }

    [Fact]
    public async Task E2E_GetMemberCandidates_ReturnsLecturersWithConflictAndReviewerFlags()
    {
        var facultyId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var busyLecturerId = Guid.NewGuid();
        var chairmanInThisCouncilId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var inactiveLecturerId = Guid.NewGuid();

        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "TS. Nguyễn GVHD", "gvhd@uit.edu.vn", "GV101", true, true);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. Trần GVPB", "gvpb@uit.edu.vn", "GV102", true, true);
        _userInfoReader.Users[busyLecturerId] = new CouncilUserInfoContractDto(busyLecturerId, "ThS. Lê Trùng Lịch", "busy@uit.edu.vn", "GV103", true, true);
        _userInfoReader.Users[chairmanInThisCouncilId] = new CouncilUserInfoContractDto(chairmanInThisCouncilId, "PGS. Phạm Chủ Tịch", "ct@uit.edu.vn", "GV104", true, true);
        _userInfoReader.Users[studentUserId] = new CouncilUserInfoContractDto(studentUserId, "Sinh Viên A", "sv@uit.edu.vn", "SV001", true, false);
        _userInfoReader.Users[inactiveLecturerId] = new CouncilUserInfoContractDto(inactiveLecturerId, "GV Đã Nghỉ", "off@uit.edu.vn", "GV999", false, true);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId, _batchId, advisorId, "Đề tài Candidate Test", "Candidate Test");
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        Authenticate(facultyId, "Faculty");

        // Create another council at same date/slot with busyLecturerId
        var otherRes = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-CAND-BUSY", _defenseDate, TimeSlotMorning, RoomB202));
        var otherCouncilId = (await ReadDataAsync(otherRes)).GetProperty("id").GetGuid();
        await _client.PostAsJsonAsync($"/api/councils/{otherCouncilId}/members",
            new AddCouncilMembersInputDto(Members: [new(busyLecturerId, CouncilMemberRole.Secretary)]));

        // Create target council at same date/slot with chairmanInThisCouncilId + assigned group
        var targetRes = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-CAND-MAIN", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(targetRes)).GetProperty("id").GetGuid();
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(chairmanInThisCouncilId, CouncilMemberRole.Chairman)]));

        var candidatesResponse = await _client.GetAsync($"/api/councils/{councilId}/member-candidates");
        Assert.Equal(HttpStatusCode.OK, candidatesResponse.StatusCode);

        var candidates = await ReadDataAsync(candidatesResponse);
        Assert.Equal(4, candidates.GetArrayLength());

        var byId = candidates.EnumerateArray().ToDictionary(x => x.GetProperty("userId").GetGuid());
        Assert.False(byId.ContainsKey(studentUserId));
        Assert.False(byId.ContainsKey(inactiveLecturerId));

        // Advisor conflict
        var adv = byId[advisorId];
        Assert.True(adv.GetProperty("isAdvisorConflict").GetBoolean());
        Assert.False(adv.GetProperty("isEligible").GetBoolean());
        Assert.Equal(0, adv.GetProperty("allowedRoles").GetArrayLength());

        // Reviewer restricted to Member
        var rev = byId[reviewerId];
        Assert.True(rev.GetProperty("isReviewerForCouncilGroups").GetBoolean());
        Assert.True(rev.GetProperty("isEligible").GetBoolean());
        Assert.Equal(new[] { "Member" }, rev.GetProperty("allowedRoles").EnumerateArray().Select(r => r.GetString()!).ToArray());

        // Busy schedule conflict
        var busy = byId[busyLecturerId];
        Assert.True(busy.GetProperty("isScheduleConflict").GetBoolean());
        Assert.False(busy.GetProperty("isEligible").GetBoolean());
        Assert.Equal(0, busy.GetProperty("allowedRoles").GetArrayLength());

        // Already in THIS council as Chairman (not schedule-conflicting with self)
        var chair = byId[chairmanInThisCouncilId];
        Assert.True(chair.GetProperty("isAlreadyInCouncil").GetBoolean());
        Assert.Equal("Chairman", chair.GetProperty("currentRoleInCouncil").GetString());
        Assert.False(chair.GetProperty("isScheduleConflict").GetBoolean());
        Assert.True(chair.GetProperty("isEligible").GetBoolean());
        Assert.Equal(3, chair.GetProperty("allowedRoles").GetArrayLength());

        // Keyword filtering
        var searchResponse = await _client.GetAsync($"/api/councils/{councilId}/member-candidates?keyword=GV102");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var searched = await ReadDataAsync(searchResponse);
        Assert.Equal(1, searched.GetArrayLength());
        Assert.Equal(reviewerId, searched[0].GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task E2E_AddCouncilMembers_WhenUserIsNotLecturer_Returns400BadRequest()
    {
        var facultyId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        _userInfoReader.Users[studentId] = new CouncilUserInfoContractDto(studentId, "Sinh viên X", "svx@uit.edu.vn", "22520099", true, false);

        Authenticate(facultyId, "Faculty");
        var createRes = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-NONLEC-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createRes)).GetProperty("id").GetGuid();

        var addRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(studentId, CouncilMemberRole.Member)]));

        Assert.Equal(HttpStatusCode.BadRequest, addRes.StatusCode);
        var error = await ReadErrorAsync(addRes);
        Assert.Equal("Council.MemberNotLecturer", error.Code);
        Assert.DoesNotContain(studentId, _userInfoReader.GrantedCouncilMemberUserIds);
    }

    [Fact]
    public async Task E2E_UpdateCouncilMembers_InDraftCouncil_ReplacesRosterAndSyncsCouncilMemberRole()
    {
        var facultyId = Guid.NewGuid();
        var lecA = Guid.NewGuid();
        var lecB = Guid.NewGuid();
        var lecC = Guid.NewGuid();
        var lecD = Guid.NewGuid();

        _userInfoReader.Users[lecA] = new CouncilUserInfoContractDto(lecA, "Giảng viên A", "a@uit.edu.vn", "GV01", true, true);
        _userInfoReader.Users[lecB] = new CouncilUserInfoContractDto(lecB, "Giảng viên B", "b@uit.edu.vn", "GV02", true, true);
        _userInfoReader.Users[lecC] = new CouncilUserInfoContractDto(lecC, "Giảng viên C", "c@uit.edu.vn", "GV03", true, true);
        _userInfoReader.Users[lecD] = new CouncilUserInfoContractDto(lecD, "Giảng viên D", "d@uit.edu.vn", "GV04", true, true);

        Authenticate(facultyId, "Faculty");

        // Council 2 also includes lecA (so removing lecA from Council 1 must NOT revoke lecA's CouncilMember role)
        var c2Res = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-SYNC-02", _defenseDate.AddDays(1), TimeSlotAfternoon, RoomB202));
        var c2Id = (await ReadDataAsync(c2Res)).GetProperty("id").GetGuid();
        await _client.PostAsJsonAsync($"/api/councils/{c2Id}/members",
            new AddCouncilMembersInputDto(Members: [new(lecA, CouncilMemberRole.Member)]));

        // Council 1 starts with lecA (Chairman), lecB (Secretary), lecD (Member)
        var c1Res = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-SYNC-01", _defenseDate, TimeSlotMorning, RoomA101));
        var c1Id = (await ReadDataAsync(c1Res)).GetProperty("id").GetGuid();
        var addRes = await _client.PostAsJsonAsync($"/api/councils/{c1Id}/members",
            new AddCouncilMembersInputDto(Members:
            [
                new(lecA, CouncilMemberRole.Chairman),
                new(lecB, CouncilMemberRole.Secretary),
                new(lecD, CouncilMemberRole.Member)
            ]));
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);
        Assert.Contains(lecA, _userInfoReader.GrantedCouncilMemberUserIds);
        Assert.Contains(lecB, _userInfoReader.GrantedCouncilMemberUserIds);
        Assert.Contains(lecD, _userInfoReader.GrantedCouncilMemberUserIds);

        // Replace roster in Council 1 with lecB (Chairman) and lecC (Secretary)
        var putRes = await _client.PutAsJsonAsync($"/api/councils/{c1Id}/members",
            new UpdateCouncilMembersInputDto(
            [
                new(lecB, CouncilMemberRole.Chairman),
                new(lecC, CouncilMemberRole.Secretary)
            ]));

        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        var updatedMembers = await ReadDataAsync(putRes);
        Assert.Equal(2, updatedMembers.GetArrayLength());

        Assert.Contains(lecB, _userInfoReader.GrantedCouncilMemberUserIds);
        Assert.Contains(lecC, _userInfoReader.GrantedCouncilMemberUserIds);
        Assert.Contains(lecA, _userInfoReader.GrantedCouncilMemberUserIds); // Still in c2
        Assert.DoesNotContain(lecD, _userInfoReader.GrantedCouncilMemberUserIds); // Revoked completely
    }

    [Fact]
    public async Task E2E_UpdateCouncilMembers_WhenNotDraft_Returns409Conflict()
    {
        var facultyId = Guid.NewGuid();
        var aaId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var sId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[cId] = new CouncilUserInfoContractDto(cId, "Chủ tịch", "ct@uit.edu.vn", "GV11", true, true);
        _userInfoReader.Users[sId] = new CouncilUserInfoContractDto(sId, "Thư ký", "tk@uit.edu.vn", "GV12", true, true);
        _userInfoReader.Users[newId] = new CouncilUserInfoContractDto(newId, "Giảng viên Mới", "new@uit.edu.vn", "GV13", true, true);
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài Active", "Active");
        SeedDefaultReviewerForGroup(groupId);

        Authenticate(facultyId, "Faculty");
        var createRes = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-PUTLOCK-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createRes)).GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(cId, CouncilMemberRole.Chairman), new(sId, CouncilMemberRole.Secretary)]));
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit", new SubmitCouncilToAcademicAffairsInputDto());

        Authenticate(aaId, "AcademicAffairs");
        var approveRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/approve", new ApproveCouncilInputDto("QD-PUTLOCK"));
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        var putRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(newId, CouncilMemberRole.Chairman), new(sId, CouncilMemberRole.Secretary)]));

        Assert.Equal(HttpStatusCode.Conflict, putRes.StatusCode);
        var error = await ReadErrorAsync(putRes);
        Assert.Equal("Council.NotInDraftStatus", error.Code);
    }

    [Fact]
    public async Task E2E_RemoveCouncilMember_InDraftCouncil_RemovesMemberAndRevokesRoleWhenNoOtherCouncils()
    {
        var facultyId = Guid.NewGuid();
        var lec1 = Guid.NewGuid();
        var lec2 = Guid.NewGuid();
        var lec3 = Guid.NewGuid();

        _userInfoReader.Users[lec1] = new CouncilUserInfoContractDto(lec1, "Chủ tịch", "ct@uit.edu.vn", "GV21", true, true);
        _userInfoReader.Users[lec2] = new CouncilUserInfoContractDto(lec2, "Thư ký", "tk@uit.edu.vn", "GV22", true, true);
        _userInfoReader.Users[lec3] = new CouncilUserInfoContractDto(lec3, "Ủy viên", "uv@uit.edu.vn", "GV23", true, true);

        Authenticate(facultyId, "Faculty");
        var createRes = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-DEL-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createRes)).GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members:
            [
                new(lec1, CouncilMemberRole.Chairman),
                new(lec2, CouncilMemberRole.Secretary),
                new(lec3, CouncilMemberRole.Member)
            ]));
        Assert.Contains(lec3, _userInfoReader.GrantedCouncilMemberUserIds);

        var deleteRes = await _client.DeleteAsync($"/api/councils/{councilId}/members/{lec3}");
        Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);
        var remaining = await ReadDataAsync(deleteRes);
        Assert.Equal(2, remaining.GetArrayLength());
        Assert.Contains(lec1, _userInfoReader.GrantedCouncilMemberUserIds);
        Assert.Contains(lec2, _userInfoReader.GrantedCouncilMemberUserIds);
        Assert.DoesNotContain(lec3, _userInfoReader.GrantedCouncilMemberUserIds);

        var notFoundRes = await _client.DeleteAsync($"/api/councils/{councilId}/members/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);
        var error = await ReadErrorAsync(notFoundRes);
        Assert.Equal("Council.MemberNotInCouncil", error.Code);
    }

    #endregion

    #region QA Boundary, Negative & Malformed Input Tests — Council Member Assignment

    private void SeedDefaultReviewerForGroup(Guid groupId)
    {
        var reviewerId = Guid.NewGuid();
        _councilRepo.ActiveReviewers[groupId] = reviewerId;
        _userInfoReader.Users[reviewerId] = new(reviewerId, "GVPB mặc định", "reviewer@example.test", "GV99", true);
    }

    private async Task<Guid> CreateDraftCouncilForQaAsync(string code, DateOnly? date = null, string? slot = null, string? room = null)
    {
        Authenticate(Guid.NewGuid(), "Faculty");
        var res = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, code, date ?? _defenseDate, slot ?? TimeSlotMorning, room ?? RoomA101));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await ReadDataAsync(res)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> SendRawJsonAsync(HttpMethod method, string url, string rawJson)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json")
        };
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task QA_MemberAssignment_Unauthenticated_Returns401AcrossAllEndpoints()
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-401");
        ClearAuthentication();

        var getRes = await _client.GetAsync($"/api/councils/{councilId}/member-candidates");
        var postRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members", new AddCouncilMembersInputDto());
        var putRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members", new UpdateCouncilMembersInputDto());
        var delRes = await _client.DeleteAsync($"/api/councils/{councilId}/members/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, getRes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postRes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, putRes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delRes.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Lecturer")]
    [InlineData("CouncilMember")]
    public async Task QA_MemberAssignment_UnauthorizedRoles_Return403AcrossAllEndpoints(string role)
    {
        var councilId = await CreateDraftCouncilForQaAsync($"HD-QA-403-{role}");
        Authenticate(Guid.NewGuid(), role);

        var getRes = await _client.GetAsync($"/api/councils/{councilId}/member-candidates");
        var postRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members", new AddCouncilMembersInputDto());
        var putRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members", new UpdateCouncilMembersInputDto());
        var delRes = await _client.DeleteAsync($"/api/councils/{councilId}/members/{Guid.NewGuid()}");

        foreach (var res in new[] { getRes, postRes, putRes, delRes })
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            var err = await ReadErrorAsync(res);
            Assert.Equal("Council.Forbidden", err.Code);
        }
    }

    [Theory]
    [InlineData("Faculty")]
    [InlineData("AcademicAffairs")]
    [InlineData("Admin")]
    public async Task QA_MemberAssignment_AuthorizedRoles_SucceedAcrossWorkflow(string role)
    {
        var councilId = await CreateDraftCouncilForQaAsync($"HD-QA-OK-{role}");
        var lec1 = Guid.NewGuid();
        var lec2 = Guid.NewGuid();
        _userInfoReader.Users[lec1] = new CouncilUserInfoContractDto(lec1, "GV QA 1", "qa1@uit.edu.vn", "QA01", true, true);
        _userInfoReader.Users[lec2] = new CouncilUserInfoContractDto(lec2, "GV QA 2", "qa2@uit.edu.vn", "QA02", true, true);

        Authenticate(Guid.NewGuid(), role);

        var getRes = await _client.GetAsync($"/api/councils/{councilId}/member-candidates");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var postRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(lec1, CouncilMemberRole.Chairman)]));
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

        var putRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(lec1, CouncilMemberRole.Chairman), new(lec2, CouncilMemberRole.Secretary)]));
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        var delRes = await _client.DeleteAsync($"/api/councils/{councilId}/members/{lec2}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
    }

    [Fact]
    public async Task QA_MemberAssignment_InvalidRouteGuids_Return404NotFound()
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-BADGUID");

        var getBadCouncil = await _client.GetAsync("/api/councils/not-a-guid/member-candidates");
        var postBadCouncil = await _client.PostAsJsonAsync("/api/councils/not-a-guid/members", new AddCouncilMembersInputDto());
        var putBadCouncil = await _client.PutAsJsonAsync("/api/councils/not-a-guid/members", new UpdateCouncilMembersInputDto());
        var delBadCouncil = await _client.DeleteAsync($"/api/councils/not-a-guid/members/{Guid.NewGuid()}");
        var delBadUser = await _client.DeleteAsync($"/api/councils/{councilId}/members/12345-not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, getBadCouncil.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, postBadCouncil.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, putBadCouncil.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delBadCouncil.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delBadUser.StatusCode);
    }

    [Theory]
    [InlineData("{ \"members\": [ { invalid_json }")]
    [InlineData("not json at all")]
    [InlineData("{ \"members\": \"should-be-array\" }")]
    [InlineData("{ \"members\": [ { \"userId\": \"not-a-guid\", \"role\": \"Chairman\" } ] }")]
    [InlineData("{ \"members\": [ { \"userId\": \"11111111-1111-1111-1111-111111111111\", \"role\": \"SuperAdmin\" } ] }")]
    public async Task QA_MemberAssignment_MalformedOrWrongTypeJson_Returns400BadRequest(string rawJson)
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-BADJSON");

        var postRes = await SendRawJsonAsync(HttpMethod.Post, $"/api/councils/{councilId}/members", rawJson);
        var putRes = await SendRawJsonAsync(HttpMethod.Put, $"/api/councils/{councilId}/members", rawJson);

        Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    public async Task QA_MemberAssignment_OutOfRangeIntegerEnumRole_Returns400InvalidMember(int badRole)
    {
        var councilId = await CreateDraftCouncilForQaAsync($"HD-QA-ENUM-{badRole}");
        var lecId = Guid.NewGuid();
        var rawJson = $"{{ \"members\": [ {{ \"userId\": \"{lecId}\", \"role\": {badRole} }} ] }}";

        var postRes = await SendRawJsonAsync(HttpMethod.Post, $"/api/councils/{councilId}/members", rawJson);
        var putRes = await SendRawJsonAsync(HttpMethod.Put, $"/api/councils/{councilId}/members", rawJson);

        Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
        Assert.Equal("Council.InvalidMember", (await ReadErrorAsync(postRes)).Code);

        Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
        Assert.Equal("Council.InvalidMember", (await ReadErrorAsync(putRes)).Code);
    }

    [Fact]
    public async Task QA_MemberAssignment_StringEnumAndNumericEnumInRawJson_BothBindAndSucceed()
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-STRENUM");
        var lec1 = Guid.NewGuid();
        var lec2 = Guid.NewGuid();
        var lec3 = Guid.NewGuid();
        _userInfoReader.Users[lec1] = new CouncilUserInfoContractDto(lec1, "GV String 1", "s1@uit.edu.vn", "GV51", true, true);
        _userInfoReader.Users[lec2] = new CouncilUserInfoContractDto(lec2, "GV String 2", "s2@uit.edu.vn", "GV52", true, true);
        _userInfoReader.Users[lec3] = new CouncilUserInfoContractDto(lec3, "GV Num 3", "s3@uit.edu.vn", "GV53", true, true);

        // String enum via POST
        var postJson = $"{{ \"members\": [ {{ \"userId\": \"{lec1}\", \"role\": \"Chairman\" }}, {{ \"userId\": \"{lec2}\", \"role\": \"Secretary\" }} ] }}";
        var postRes = await SendRawJsonAsync(HttpMethod.Post, $"/api/councils/{councilId}/members", postJson);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

        // Mix string enum and numeric enum via PUT
        var putJson = $"{{ \"members\": [ {{ \"userId\": \"{lec1}\", \"role\": \"Chairman\" }}, {{ \"userId\": \"{lec2}\", \"role\": 1 }}, {{ \"userId\": \"{lec3}\", \"role\": \"Member\" }} ] }}";
        var putRes = await SendRawJsonAsync(HttpMethod.Put, $"/api/councils/{councilId}/members", putJson);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        var putData = await ReadDataAsync(putRes);
        Assert.Equal(3, putData.GetArrayLength());

        // Single-member shorthand on PUT
        var singlePutJson = $"{{ \"userId\": \"{lec1}\", \"role\": \"Chairman\" }}";
        var singlePutRes = await SendRawJsonAsync(HttpMethod.Put, $"/api/councils/{councilId}/members", singlePutJson);
        Assert.Equal(HttpStatusCode.OK, singlePutRes.StatusCode);
        Assert.Equal(1, (await ReadDataAsync(singlePutRes)).GetArrayLength());
    }

    [Theory]
    [InlineData("{}", "Council.EmptyMembers")]
    [InlineData("null", "Council.EmptyMembers")]
    [InlineData("{ \"members\": [] }", "Council.EmptyMembers")]
    [InlineData("{ \"members\": null }", "Council.EmptyMembers")]
    [InlineData("{ \"members\": [null] }", "Council.InvalidMember")]
    [InlineData("{ \"members\": [ { \"userId\": \"00000000-0000-0000-0000-000000000000\", \"role\": \"Chairman\" } ] }", "Council.InvalidMember")]
    public async Task QA_MemberAssignment_EmptyNullAndZeroGuidPayloads_Return400Safely(string rawJson, string expectedCode)
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-EMPTY");

        var postRes = await SendRawJsonAsync(HttpMethod.Post, $"/api/councils/{councilId}/members", rawJson);
        var putRes = await SendRawJsonAsync(HttpMethod.Put, $"/api/councils/{councilId}/members", rawJson);

        Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
        Assert.Equal(expectedCode, (await ReadErrorAsync(postRes)).Code);

        Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
        Assert.Equal(expectedCode, (await ReadErrorAsync(putRes)).Code);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("%_\\'")]
    [InlineData("  GV501  ")]
    public async Task QA_GetMemberCandidates_WhitespaceAndSpecialKeywords_HandledGracefully(string keyword)
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-KW");
        var lecId = Guid.NewGuid();
        _userInfoReader.Users[lecId] = new CouncilUserInfoContractDto(lecId, "TS. Keyword Test", "kw@uit.edu.vn", "GV501", true, true);

        var res = await _client.GetAsync($"/api/councils/{councilId}/member-candidates?keyword={Uri.EscapeDataString(keyword)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await ReadDataAsync(res);

        if (string.IsNullOrWhiteSpace(keyword))
        {
            Assert.True(data.GetArrayLength() >= 1);
        }
        else if (keyword.Trim() == "GV501")
        {
            Assert.Equal(1, data.GetArrayLength());
        }
        else
        {
            Assert.Equal(0, data.GetArrayLength());
        }
    }

    [Fact]
    public async Task QA_MemberAssignment_NonExistentCouncil_Returns404CouncilNotFound()
    {
        var missingCouncilId = Guid.NewGuid();
        var someUser = Guid.NewGuid();
        Authenticate(Guid.NewGuid(), "Faculty");

        var getRes = await _client.GetAsync($"/api/councils/{missingCouncilId}/member-candidates");
        var postRes = await _client.PostAsJsonAsync($"/api/councils/{missingCouncilId}/members",
            new AddCouncilMembersInputDto(Members: [new(someUser, CouncilMemberRole.Chairman)]));
        var putRes = await _client.PutAsJsonAsync($"/api/councils/{missingCouncilId}/members",
            new UpdateCouncilMembersInputDto(Members: [new(someUser, CouncilMemberRole.Chairman)]));
        var delRes = await _client.DeleteAsync($"/api/councils/{missingCouncilId}/members/{someUser}");

        foreach (var res in new[] { getRes, postRes, putRes, delRes })
        {
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.Equal("Council.NotFound", (await ReadErrorAsync(res)).Code);
        }
    }

    [Fact]
    public async Task QA_MemberAssignment_NonExistentInactiveAndNonLecturerUsers_RejectedProperly()
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-REFS");
        var missingUserId = Guid.NewGuid();
        var inactiveUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _userInfoReader.Users[inactiveUserId] = new CouncilUserInfoContractDto(inactiveUserId, "GV Nghỉ", "inactive@uit.edu.vn", "GV90", false, true);
        _userInfoReader.Users[studentId] = new CouncilUserInfoContractDto(studentId, "Sinh viên B", "svb@uit.edu.vn", "SV90", true, false);

        // Missing user -> 404 Council.MemberNotFound
        var postMissing = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(missingUserId, CouncilMemberRole.Chairman)]));
        var putMissing = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto(Members: [new(missingUserId, CouncilMemberRole.Chairman)]));
        Assert.Equal(HttpStatusCode.NotFound, postMissing.StatusCode);
        Assert.Equal("Council.MemberNotFound", (await ReadErrorAsync(postMissing)).Code);
        Assert.Equal(HttpStatusCode.NotFound, putMissing.StatusCode);
        Assert.Equal("Council.MemberNotFound", (await ReadErrorAsync(putMissing)).Code);

        // Inactive lecturer -> 404 Council.MemberNotFound
        var postInactive = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(inactiveUserId, CouncilMemberRole.Chairman)]));
        var putInactive = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto(Members: [new(inactiveUserId, CouncilMemberRole.Chairman)]));
        Assert.Equal(HttpStatusCode.NotFound, postInactive.StatusCode);
        Assert.Equal("Council.MemberNotFound", (await ReadErrorAsync(postInactive)).Code);
        Assert.Equal(HttpStatusCode.NotFound, putInactive.StatusCode);
        Assert.Equal("Council.MemberNotFound", (await ReadErrorAsync(putInactive)).Code);

        // Active Student (IsLecturer = false) via PUT -> 400 Council.MemberNotLecturer
        var putStudent = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto(Members: [new(studentId, CouncilMemberRole.Member)]));
        Assert.Equal(HttpStatusCode.BadRequest, putStudent.StatusCode);
        Assert.Equal("Council.MemberNotLecturer", (await ReadErrorAsync(putStudent)).Code);

        // DELETE with Guid.Empty -> 404 Council.MemberNotInCouncil
        var delEmptyUser = await _client.DeleteAsync($"/api/councils/{councilId}/members/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, delEmptyUser.StatusCode);
        Assert.Equal("Council.MemberNotInCouncil", (await ReadErrorAsync(delEmptyUser)).Code);
    }

    [Fact]
    public async Task QA_GetMemberCandidates_WhenAssignedGroupOrTopicMissing_HandlesOrphanReferenceGracefully()
    {
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-ORPHAN");
        var orphanGroupId = Guid.NewGuid();
        var lecId = Guid.NewGuid();
        _userInfoReader.Users[lecId] = new CouncilUserInfoContractDto(lecId, "TS. Orphan Safe", "orphan@uit.edu.vn", "GV601", true, true);

        // Inject orphan assignment directly into repository without corresponding group/topic contract entries
        _councilRepo.Assignments.Add(new CouncilAssignment
        {
            Id = Guid.NewGuid(),
            CouncilId = councilId,
            GroupId = orphanGroupId,
            OrderNumber = 1
        });

        var getRes = await _client.GetAsync($"/api/councils/{councilId}/member-candidates");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var putRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto(Members: [new(lecId, CouncilMemberRole.Chairman)]));
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
    }

    [Fact]
    public async Task QA_UpdateCouncilMembers_BusinessRulesRoleSwapAndAtomicity_EnforcedStrictly()
    {
        var advisorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var busyId = Guid.NewGuid();
        var lecA = Guid.NewGuid();
        var lecB = Guid.NewGuid();
        var extraNewValidId = Guid.NewGuid();

        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "GVHD Conflict", "gvhd@uit.edu.vn", "GV701", true, true);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "GVPB Reviewer", "gvpb@uit.edu.vn", "GV702", true, true);
        _userInfoReader.Users[busyId] = new CouncilUserInfoContractDto(busyId, "GV Busy", "busy@uit.edu.vn", "GV703", true, true);
        _userInfoReader.Users[lecA] = new CouncilUserInfoContractDto(lecA, "GV A", "a@uit.edu.vn", "GV704", true, true);
        _userInfoReader.Users[lecB] = new CouncilUserInfoContractDto(lecB, "GV B", "b@uit.edu.vn", "GV705", true, true);
        _userInfoReader.Users[extraNewValidId] = new CouncilUserInfoContractDto(extraNewValidId, "GV Valid", "valid@uit.edu.vn", "GV706", true, true);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài QA Rules", "QA Rules");
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        // Busy lecturer on same slot
        var otherCouncilId = await CreateDraftCouncilForQaAsync("HD-QA-BUSY2", _defenseDate, TimeSlotMorning, RoomB202);
        await _client.PostAsJsonAsync($"/api/councils/{otherCouncilId}/members",
            new AddCouncilMembersInputDto(Members: [new(busyId, CouncilMemberRole.Chairman)]));

        // Main council with assigned group and initial roster [lecA=Chairman, lecB=Secretary]
        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-RULES", _defenseDate, TimeSlotMorning, RoomA101);
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(lecA, CouncilMemberRole.Chairman), new(lecB, CouncilMemberRole.Secretary)]));

        // 1. Duplicate UserId in same PUT request -> 409
        var dupUserRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(lecA, CouncilMemberRole.Chairman), new(lecA, CouncilMemberRole.Secretary)]));
        Assert.Equal(HttpStatusCode.Conflict, dupUserRes.StatusCode);
        Assert.Equal("Council.DuplicateMemberInRequest", (await ReadErrorAsync(dupUserRes)).Code);

        // 2. Duplicate Chairman in PUT -> 409
        var dupChairRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(lecA, CouncilMemberRole.Chairman), new(lecB, CouncilMemberRole.Chairman)]));
        Assert.Equal(HttpStatusCode.Conflict, dupChairRes.StatusCode);
        Assert.Equal("Council.DuplicateChairman", (await ReadErrorAsync(dupChairRes)).Code);

        // 3. Duplicate Secretary in PUT -> 409
        var dupSecRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(lecA, CouncilMemberRole.Secretary), new(lecB, CouncilMemberRole.Secretary)]));
        Assert.Equal(HttpStatusCode.Conflict, dupSecRes.StatusCode);
        Assert.Equal("Council.DuplicateSecretary", (await ReadErrorAsync(dupSecRes)).Code);

        // 4. Reviewer assigned as Secretary in PUT -> 409
        var revSecRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(lecA, CouncilMemberRole.Chairman), new(reviewerId, CouncilMemberRole.Secretary)]));
        Assert.Equal(HttpStatusCode.Conflict, revSecRes.StatusCode);
        Assert.Equal("Council.MemberIsGroupReviewerWithInvalidRole", (await ReadErrorAsync(revSecRes)).Code);

        // 5. Atomicity test: PUT with 1 valid new member + 1 advisor conflict -> 409 AND existing roster & roles unchanged
        var atomicFailRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(extraNewValidId, CouncilMemberRole.Chairman), new(advisorId, CouncilMemberRole.Secretary)]));
        Assert.Equal(HttpStatusCode.Conflict, atomicFailRes.StatusCode);
        Assert.Equal("Council.MemberIsGroupAdvisor", (await ReadErrorAsync(atomicFailRes)).Code);

        // Verify extraNewValidId was NOT added or granted role, and lecA/lecB remain untouched
        var currentMembers = _councilRepo.Members.Where(m => m.CouncilId == councilId).ToList();
        Assert.Equal(3, currentMembers.Count);
        Assert.Contains(currentMembers, m => m.UserId == lecA && m.RoleInCouncil == CouncilMemberRole.Chairman);
        Assert.Contains(currentMembers, m => m.UserId == lecB && m.RoleInCouncil == CouncilMemberRole.Secretary);
        Assert.DoesNotContain(extraNewValidId, _userInfoReader.GrantedCouncilMemberUserIds);

        // 6. Schedule conflict in PUT -> 409
        var busyRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto([new(lecA, CouncilMemberRole.Chairman), new(busyId, CouncilMemberRole.Secretary)]));
        Assert.Equal(HttpStatusCode.Conflict, busyRes.StatusCode);
        Assert.Equal("Council.MemberScheduleConflict", (await ReadErrorAsync(busyRes)).Code);

        // 7. Role Swap (lecA <-> lecB) + Reviewer allowed as Member -> 200 OK
        var swapRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto(
            [
                new(lecB, CouncilMemberRole.Chairman),
                new(lecA, CouncilMemberRole.Secretary),
                new(reviewerId, CouncilMemberRole.Member)
            ]));
        Assert.Equal(HttpStatusCode.OK, swapRes.StatusCode);
        var swapped = await ReadDataAsync(swapRes);
        Assert.Equal(3, swapped.GetArrayLength());
        Assert.Contains(reviewerId, _userInfoReader.GrantedCouncilMemberUserIds);
    }

    [Fact]
    public async Task QA_RemoveCouncilMember_WhenNotDraft_Returns409NotInDraftStatus()
    {
        var aaId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var sId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[cId] = new CouncilUserInfoContractDto(cId, "Chủ tịch Active", "ca@uit.edu.vn", "GV801", true, true);
        _userInfoReader.Users[sId] = new CouncilUserInfoContractDto(sId, "Thư ký Active", "sa@uit.edu.vn", "GV802", true, true);
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài Lock DEL", "Lock DEL");
        SeedDefaultReviewerForGroup(groupId);

        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-DELLOCK");
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(cId, CouncilMemberRole.Chairman), new(sId, CouncilMemberRole.Secretary)]));
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit", new SubmitCouncilToAcademicAffairsInputDto());

        Authenticate(aaId, "AcademicAffairs");
        var approveRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/approve", new ApproveCouncilInputDto("QD-QA-DEL"));
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        var delRes = await _client.DeleteAsync($"/api/councils/{councilId}/members/{sId}");
        Assert.Equal(HttpStatusCode.Conflict, delRes.StatusCode);
        Assert.Equal("Council.NotInDraftStatus", (await ReadErrorAsync(delRes)).Code);
        Assert.Contains(sId, _userInfoReader.GrantedCouncilMemberUserIds);
    }

    [Fact]
    public async Task MemberModifications_WhenCouncilWasSubmitted_ResetsSubmissionFlag()
    {
        var facultyId = Guid.NewGuid();
        Authenticate(facultyId, "Faculty");

        var cId = Guid.NewGuid();
        var sId = Guid.NewGuid();
        var mId1 = Guid.NewGuid();
        var mId2 = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _userInfoReader.Users[cId] = new CouncilUserInfoContractDto(cId, "Chủ tịch Reset", "cr@uit.edu.vn", "GV810", true, true);
        _userInfoReader.Users[sId] = new CouncilUserInfoContractDto(sId, "Thư ký Reset", "sr@uit.edu.vn", "GV811", true, true);
        _userInfoReader.Users[mId1] = new CouncilUserInfoContractDto(mId1, "Ủy viên 1 Reset", "m1r@uit.edu.vn", "GV812", true, true);
        _userInfoReader.Users[mId2] = new CouncilUserInfoContractDto(mId2, "Ủy viên 2 Reset", "m2r@uit.edu.vn", "GV813", true, true);
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài Reset Test", "Reset Test");
        SeedDefaultReviewerForGroup(groupId);

        var councilId = await CreateDraftCouncilForQaAsync("HD-QA-RESET-FLAG");
        var council = _councilRepo.Councils.First(c => c.Id == councilId);

        // 1. Initial setup: Add Chairman + Secretary, assign 1 group, then submit to Academic Affairs
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(cId, CouncilMemberRole.Chairman), new(sId, CouncilMemberRole.Secretary)]));
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));
        var submitRes1 = await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit", new SubmitCouncilToAcademicAffairsInputDto());
        Assert.Equal(HttpStatusCode.OK, submitRes1.StatusCode);
        Assert.True(council.IsSubmittedToAcademicAffairs);

        // 2. Action A: Add member (POST) -> should reset submission flag
        var addRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members",
            new AddCouncilMembersInputDto(Members: [new(mId1, CouncilMemberRole.Member)]));
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);
        Assert.False(council.IsSubmittedToAcademicAffairs);

        // Re-submit
        var submitRes2 = await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit", new SubmitCouncilToAcademicAffairsInputDto());
        Assert.Equal(HttpStatusCode.OK, submitRes2.StatusCode);
        Assert.True(council.IsSubmittedToAcademicAffairs);

        // 3. Action B: Replace members (PUT) -> should reset submission flag
        var putRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/members",
            new UpdateCouncilMembersInputDto(Members: [
                new(cId, CouncilMemberRole.Chairman),
                new(sId, CouncilMemberRole.Secretary),
                new(mId2, CouncilMemberRole.Member)
            ]));
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        Assert.False(council.IsSubmittedToAcademicAffairs);

        // Re-submit
        var submitRes3 = await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit", new SubmitCouncilToAcademicAffairsInputDto());
        Assert.Equal(HttpStatusCode.OK, submitRes3.StatusCode);
        Assert.True(council.IsSubmittedToAcademicAffairs);

        // 4. Action C: Remove member (DELETE) -> should reset submission flag
        var delRes = await _client.DeleteAsync($"/api/councils/{councilId}/members/{mId2}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
        Assert.False(council.IsSubmittedToAcademicAffairs);
    }

    #endregion

    private sealed class TestE2EAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CouncilE2ETestScheme";

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
