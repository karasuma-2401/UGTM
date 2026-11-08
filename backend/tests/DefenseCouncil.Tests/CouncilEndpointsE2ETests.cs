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
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;
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

        Authenticate(facultyId, "Faculty");

        // 1. Create council
        var createResponse = await _client.PostAsJsonAsync("/api/councils",
            new CreateCouncilInputDto(_batchId, "HD-COI-01", _defenseDate, TimeSlotMorning, RoomA101));
        var councilId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();

        // 2. Assign group
        await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments",
            new AssignCouncilGroupsInputDto(Assignments: [new(groupId, 1)]));

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
