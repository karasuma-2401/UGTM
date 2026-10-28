using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.CreateRenameRequest;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetGroupRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class RenameRequestApplicationTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid TopicId = Guid.NewGuid();
    private static readonly Guid AdvisorId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid FacultyStaffId = Guid.NewGuid();
    private static readonly Guid AcademicAffairsStaffId = Guid.NewGuid();

    #region 1. Tạo đơn (CreateRenameRequest)

    [Fact]
    public async Task Create_Fails_When_Unauthenticated()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.AnonymousUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên mới", "New Name", "Lý do"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.AccessDenied.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("", "Lý do")]
    [InlineData("   ", "Lý do")]
    [InlineData("Tên mới", "")]
    [InlineData("Tên mới", "   ")]
    public async Task Create_Fails_When_InputInvalid(string newTitleVi, string reason)
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, newTitleVi, null, reason));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidRequest.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_Fails_When_TitleVi_Exceeds_300_Characters()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var longTitle = new string('A', 301);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, longTitle, null, "Lý do"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidRequest.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_Succeeds_When_TitleVi_Is_Exactly_300_Characters()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var exactTitle = new string('A', 300);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, exactTitle, null, "Lý do"));

        Assert.True(result.IsSuccess);
        Assert.Equal(exactTitle, result.Value.NewTitleVi);
    }

    [Fact]
    public async Task Create_Fails_When_TitleEn_Exceeds_350_Characters()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var longTitleEn = new string('B', 351);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên mới", longTitleEn, "Lý do"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidRequest.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_Succeeds_When_Only_TitleVi_Changed()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên tiếng Việt mới", "Old AI Research", "Lý do"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Tên tiếng Việt mới", result.Value.NewTitleVi);
        Assert.Equal("Old AI Research", result.Value.NewTitleEn);
    }

    [Fact]
    public async Task Create_Succeeds_When_Only_TitleEn_Changed()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Nghiên cứu AI cũ", "Brand New AI Research", "Lý do"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Nghiên cứu AI cũ", result.Value.NewTitleVi);
        Assert.Equal("Brand New AI Research", result.Value.NewTitleEn);
    }

    [Fact]
    public async Task Create_Fails_When_UserNotConfirmedMember()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var outsider = f.User(Guid.NewGuid());

        var result = await f.CreateHandler(outsider)
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên mới", null, "Lý do"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_Fails_When_GroupNotInProgress()
    {
        var group = new StudentGroup(Guid.NewGuid(), TopicId, Now.AddDays(-30));
        group.AddConfirmedMember(StudentId, BatchId, Now.AddDays(-30));
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên mới", null, "Lý do"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidGroupState.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_Succeeds_When_Group_In_MidtermSubmitted_Status()
    {
        var group = CreateInProgressGroup();
        group.TrySubmitMidtermReport();
        Assert.Equal(StudentGroupStatus.MidtermSubmitted, group.Status);
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên mới khi giữa kỳ", null, "Lý do"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_Fails_When_OutsideAllowedWindow()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group, isWindowOpen: false);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên mới", null, "Lý do"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.OutsideAllowedWindow.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(RenameRequestStatus.Created)]
    [InlineData(RenameRequestStatus.AdvisorConfirmed)]
    [InlineData(RenameRequestStatus.FacultyApproved)]
    public async Task Create_Fails_When_ActiveRequestAlreadyExists(RenameRequestStatus activeStatus)
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var existingRequest = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới 1", null, null, "Lý do 1", Now);
        if (activeStatus == RenameRequestStatus.AdvisorConfirmed) existingRequest.ApproveByAdvisor(Now);
        else if (activeStatus == RenameRequestStatus.FacultyApproved) { existingRequest.ApproveByAdvisor(Now); existingRequest.ApproveByFaculty(Now); }
        f.RenameRequests.Add(existingRequest);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên mới 2", null, "Lý do 2"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.ActiveRequestAlreadyExists.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(RenameRequestStatus.RejectedByAdvisor)]
    [InlineData(RenameRequestStatus.RejectedByFaculty)]
    [InlineData(RenameRequestStatus.RejectedByAA)]
    [InlineData(RenameRequestStatus.Completed)]
    public async Task Create_Succeeds_When_Previous_Request_Is_Not_Active(RenameRequestStatus oldStatus)
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var oldRequest = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên cũ 2", null, null, "Lý do", Now);
        if (oldStatus == RenameRequestStatus.RejectedByAdvisor) oldRequest.RejectByAdvisor("Lý do", AdvisorId, Now);
        else if (oldStatus == RenameRequestStatus.RejectedByFaculty) { oldRequest.ApproveByAdvisor(Now); oldRequest.RejectByFaculty("Lý do", FacultyStaffId, Now); }
        else if (oldStatus == RenameRequestStatus.RejectedByAA) { oldRequest.ApproveByAdvisor(Now); oldRequest.ApproveByFaculty(Now); oldRequest.RejectByAcademicAffairs("Lý do", AcademicAffairsStaffId, Now); }
        else if (oldStatus == RenameRequestStatus.Completed) { oldRequest.ApproveByAdvisor(Now); oldRequest.ApproveByFaculty(Now); oldRequest.ApproveByAcademicAffairs(Now); }
        f.RenameRequests.Add(oldRequest);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Tên hoàn toàn mới", null, "Lý do"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_Fails_When_TitleUnchanged()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Nghiên cứu AI cũ", "Old AI Research", "Lý do"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.TitleUnchanged.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_Succeeds_When_Valid()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.CreateHandler(f.StudentUser())
            .HandleAsync(new CreateRenameRequestCommand(group.Id, "Nghiên cứu AI mới", "New AI Research", "Mở rộng mô hình"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Nghiên cứu AI cũ", result.Value.OldTitleVi);
        Assert.Equal("Nghiên cứu AI mới", result.Value.NewTitleVi);
        Assert.Equal("Old AI Research", result.Value.OldTitleEn);
        Assert.Equal("New AI Research", result.Value.NewTitleEn);
        Assert.Equal(nameof(RenameRequestStatus.Created), result.Value.Status);
    }

    #endregion

    #region 2. Cấp 1: GVHD thẩm định (ReviewByAdvisor)

    [Fact]
    public async Task AdvisorReview_Fails_When_RejectWithoutReason()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        var result = await f.AdvisorReviewHandler(f.AdvisorUser())
            .HandleAsync(new ReviewRenameRequestByAdvisorCommand(request.Id, "Reject", "   "));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.RejectionReasonRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task AdvisorReview_Fails_When_InvalidDecision()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        var result = await f.AdvisorReviewHandler(f.AdvisorUser())
            .HandleAsync(new ReviewRenameRequestByAdvisorCommand(request.Id, "Hold", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidDecision.Code, result.Error.Code);
    }

    [Fact]
    public async Task AdvisorReview_Fails_When_RequestNotFound()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.AdvisorReviewHandler(f.AdvisorUser())
            .HandleAsync(new ReviewRenameRequestByAdvisorCommand(Guid.NewGuid(), "Approve", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.RequestNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task AdvisorReview_Fails_When_NotTopicAdvisor()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        var otherLecturer = f.User(Guid.NewGuid(), roles: ["Lecturer"]);
        var result = await f.AdvisorReviewHandler(otherLecturer)
            .HandleAsync(new ReviewRenameRequestByAdvisorCommand(request.Id, "Approve", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task AdvisorReview_Succeeds_When_Admin_Approves()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        var adminUser = f.User(Guid.NewGuid(), roles: ["Admin"]);
        var result = await f.AdvisorReviewHandler(adminUser)
            .HandleAsync(new ReviewRenameRequestByAdvisorCommand(request.Id, "Approve", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.AdvisorConfirmed), result.Value.Status);
    }

    [Fact]
    public async Task AdvisorReview_Reject_StopsWorkflow_And_SetsRejectedByAdvisor()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        var result = await f.AdvisorReviewHandler(f.AdvisorUser())
            .HandleAsync(new ReviewRenameRequestByAdvisorCommand(request.Id, "Reject", "Nội dung chưa phù hợp."));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.RejectedByAdvisor), result.Value.Status);
        Assert.Equal("Nội dung chưa phù hợp.", result.Value.RejectionReason);
        Assert.Equal(AdvisorId, result.Value.RejectedBy);
    }

    [Fact]
    public async Task AdvisorReview_Approve_MovesTo_AdvisorConfirmed()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        var result = await f.AdvisorReviewHandler(f.AdvisorUser())
            .HandleAsync(new ReviewRenameRequestByAdvisorCommand(request.Id, "Approve", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.AdvisorConfirmed), result.Value.Status);
        Assert.NotNull(result.Value.AdvisorConfirmedAt);
    }

    #endregion

    #region 3. Cấp 2: Văn phòng Khoa thẩm định (ReviewByFaculty)

    [Fact]
    public async Task FacultyReview_Fails_When_NotFacultyRole()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var result = await f.FacultyReviewHandler(f.StudentUser())
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "Approve", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task FacultyReview_Succeeds_When_Admin_Approves()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var adminUser = f.User(Guid.NewGuid(), roles: ["Admin"]);
        var result = await f.FacultyReviewHandler(adminUser)
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "Approve", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.FacultyApproved), result.Value.Status);
    }

    [Fact]
    public async Task FacultyReview_Succeeds_When_User_Has_ReviewGates_Permission()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var privilegedUser = f.User(Guid.NewGuid(), permissions: [PermissionCodes.ThesisReviewGatesReview]);
        var result = await f.FacultyReviewHandler(privilegedUser)
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "Approve", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.FacultyApproved), result.Value.Status);
    }

    [Fact]
    public async Task FacultyReview_Fails_When_InvalidState()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        var result = await f.FacultyReviewHandler(f.FacultyUser())
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "Approve", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidReviewState.Code, result.Error.Code);
    }

    [Fact]
    public async Task FacultyReview_Fails_When_InvalidDecision()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var result = await f.FacultyReviewHandler(f.FacultyUser())
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "ReviewLater", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidDecision.Code, result.Error.Code);
    }

    [Fact]
    public async Task FacultyReview_Fails_When_RejectWithoutReason()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var result = await f.FacultyReviewHandler(f.FacultyUser())
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "Reject", "   "));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.RejectionReasonRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task FacultyReview_Reject_SetsRejectedByFaculty()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var result = await f.FacultyReviewHandler(f.FacultyUser())
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "Reject", "Khoa không đồng ý"));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.RejectedByFaculty), result.Value.Status);
        Assert.Equal("Khoa không đồng ý", result.Value.RejectionReason);
    }

    [Fact]
    public async Task FacultyReview_Approve_MovesTo_FacultyApproved()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var result = await f.FacultyReviewHandler(f.FacultyUser())
            .HandleAsync(new ReviewRenameRequestByFacultyCommand(request.Id, "Approve", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.FacultyApproved), result.Value.Status);
        Assert.NotNull(result.Value.FacultyApprovedAt);
    }

    #endregion

    #region 4. Cấp 3: Phòng Đào tạo phê duyệt (ReviewByAcademicAffairs)

    [Fact]
    public async Task AcademicAffairsReview_Fails_When_NotAcademicAffairsRole()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        request.ApproveByFaculty(Now);
        f.RenameRequests.Add(request);

        var result = await f.AcademicAffairsReviewHandler(f.FacultyUser())
            .HandleAsync(new ReviewRenameRequestByAcademicAffairsCommand(request.Id, "Approve", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task AcademicAffairsReview_Succeeds_When_Admin_Approves()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        request.ApproveByFaculty(Now);
        f.RenameRequests.Add(request);

        var adminUser = f.User(Guid.NewGuid(), roles: ["Admin"]);
        var result = await f.AcademicAffairsReviewHandler(adminUser)
            .HandleAsync(new ReviewRenameRequestByAcademicAffairsCommand(request.Id, "Approve", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.Completed), result.Value.Status);
    }

    [Fact]
    public async Task AcademicAffairsReview_Fails_When_InvalidReviewState()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        f.RenameRequests.Add(request);

        var result = await f.AcademicAffairsReviewHandler(f.AcademicAffairsUser())
            .HandleAsync(new ReviewRenameRequestByAcademicAffairsCommand(request.Id, "Approve", null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidReviewState.Code, result.Error.Code);
    }

    [Fact]
    public async Task AcademicAffairsReview_Reject_SetsRejectedByAA()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        request.ApproveByFaculty(Now);
        f.RenameRequests.Add(request);

        var result = await f.AcademicAffairsReviewHandler(f.AcademicAffairsUser())
            .HandleAsync(new ReviewRenameRequestByAcademicAffairsCommand(request.Id, "Reject", "PĐT từ chối"));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.RejectedByAA), result.Value.Status);
        Assert.Equal("PĐT từ chối", result.Value.RejectionReason);
    }

    [Fact]
    public async Task AcademicAffairsReview_Fails_When_TopicUpdate_Fails()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group, shouldFailTopicUpdate: true);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        request.ApproveByFaculty(Now);
        f.RenameRequests.Add(request);

        var result = await f.AcademicAffairsReviewHandler(f.AcademicAffairsUser())
            .HandleAsync(new ReviewRenameRequestByAcademicAffairsCommand(request.Id, "Approve", null));

        Assert.True(result.IsFailure);
        Assert.Equal("Topic.DuplicateTitle", result.Error.Code);
        Assert.Equal(RenameRequestStatus.FacultyApproved, request.Status);
    }

    [Fact]
    public async Task AcademicAffairsReview_Approve_Completes_UpdatesTopic_EnablesSyllabusResubmission_And_LogsAudit()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Nghiên cứu AI cũ", "Nghiên cứu AI mới 2026", "Old AI", "New AI 2026", "Đổi tên phù hợp", Now);
        request.ApproveByAdvisor(Now);
        request.ApproveByFaculty(Now);
        f.RenameRequests.Add(request);

        var result = await f.AcademicAffairsReviewHandler(f.AcademicAffairsUser())
            .HandleAsync(new ReviewRenameRequestByAcademicAffairsCommand(request.Id, "Approve", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RenameRequestStatus.Completed), result.Value.Status);
        Assert.NotNull(result.Value.AaApprovedAt);

        // Verify Topic Titles updated
        Assert.Equal("Nghiên cứu AI mới 2026", f.TopicContract.UpdatedTitleVi);
        Assert.Equal("New AI 2026", f.TopicContract.UpdatedTitleEn);

        // Verify StudentGroup AllowSyllabusResubmission enabled
        Assert.True(group.AllowSyllabusResubmission);

        // Verify Audit Log recorded
        Assert.Single(f.Audit.Entries);
        var auditEntry = f.Audit.Entries[0];
        Assert.Equal(IntegrationAuditActions.RenameRequestApproved, auditEntry.Action);
        Assert.Equal("RenameRequest", auditEntry.TargetType);
        Assert.Equal(request.Id.ToString(), auditEntry.TargetId);
    }

    #endregion

    #region 5. Queries (Tra cứu đơn)

    [Fact]
    public async Task GetGroupRenameRequests_Success_ForMember_And_Fails_ForOutsider()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var request = RenameRequest.Create(
            Guid.NewGuid(), group.Id, StudentId, "Tên cũ", "Tên mới", null, null, "Lý do", Now);
        f.RenameRequests.Add(request);

        // Member
        var memberResult = await f.GetGroupHandler(f.StudentUser())
            .HandleAsync(new GetGroupRenameRequestsQuery(group.Id));
        Assert.True(memberResult.IsSuccess);
        Assert.Single(memberResult.Value);

        // Outsider
        var outsider = f.User(Guid.NewGuid());
        var outsiderResult = await f.GetGroupHandler(outsider)
            .HandleAsync(new GetGroupRenameRequestsQuery(group.Id));
        Assert.True(outsiderResult.IsFailure);
        Assert.Equal(RenameRequestErrors.AccessDenied.Code, outsiderResult.Error.Code);
    }

    [Fact]
    public async Task GetGroupRenameRequests_Allows_TopicAdvisor()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);
        var req = RenameRequest.Create(Guid.NewGuid(), group.Id, StudentId, "A", "B", null, null, "R", Now);
        f.RenameRequests.Add(req);

        var result = await f.GetGroupHandler(f.AdvisorUser())
            .HandleAsync(new GetGroupRenameRequestsQuery(group.Id));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task GetRenameRequests_Fails_When_CalledByStudent()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.GetListHandler(f.StudentUser())
            .HandleAsync(new GetRenameRequestsQuery(null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetRenameRequests_Fails_When_Invalid_Status_Filter()
    {
        var group = CreateInProgressGroup();
        var f = new Fixtures(group);

        var result = await f.GetListHandler(f.FacultyUser())
            .HandleAsync(new GetRenameRequestsQuery(null, "NonExistentStatus"));

        Assert.True(result.IsFailure);
        Assert.Equal(RenameRequestErrors.InvalidRequest.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetRenameRequests_Filters_By_Status_And_GroupId()
    {
        var group1 = CreateInProgressGroup();
        var group2 = CreateInProgressGroup();
        var f = new Fixtures(group1);

        var req1 = RenameRequest.Create(Guid.NewGuid(), group1.Id, StudentId, "A", "B", null, null, "R1", Now);
        var req2 = RenameRequest.Create(Guid.NewGuid(), group1.Id, StudentId, "A", "C", null, null, "R2", Now);
        req2.ApproveByAdvisor(Now);
        var req3 = RenameRequest.Create(Guid.NewGuid(), group2.Id, StudentId, "A", "D", null, null, "R3", Now);
        f.RenameRequests.Add(req1);
        f.RenameRequests.Add(req2);
        f.RenameRequests.Add(req3);

        // Filter by group1 and status AdvisorConfirmed
        var result = await f.GetListHandler(f.FacultyUser())
            .HandleAsync(new GetRenameRequestsQuery(group1.Id, nameof(RenameRequestStatus.AdvisorConfirmed)));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(req2.Id, result.Value[0].Id);
    }

    #endregion

    private static StudentGroup CreateInProgressGroup()
    {
        var group = new StudentGroup(Guid.NewGuid(), TopicId, Now.AddDays(-30));
        group.AddConfirmedMember(StudentId, BatchId, Now.AddDays(-30));
        // Transition to InProgress
        group.TrySubmitSyllabus();
        group.TryApproveSyllabus();
        return group;
    }

    private sealed class Fixtures
    {
        public FakeStudentGroupRepository Groups { get; }
        public FakeRenameRequestRepository RenameRequests { get; }
        public FakeBatchReader BatchReader { get; }
        public FakeTopicRenameContract TopicContract { get; }
        public FakeAuditOutbox Audit { get; }
        private readonly FakeUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public Fixtures(StudentGroup group, bool isWindowOpen = true, bool shouldFailTopicUpdate = false)
        {
            Groups = new FakeStudentGroupRepository(group);
            RenameRequests = new FakeRenameRequestRepository();
            BatchReader = new FakeBatchReader(isWindowOpen);
            TopicContract = new FakeTopicRenameContract(TopicId, BatchId, AdvisorId, "Nghiên cứu AI cũ", "Old AI Research", shouldFailTopicUpdate);
            Audit = new FakeAuditOutbox();
            _unitOfWork = new FakeUnitOfWork();
            _timeProvider = new FixedTimeProvider(Now);
        }

        public CreateRenameRequestHandler CreateHandler(ICurrentUser user)
            => new(Groups, RenameRequests, BatchReader, TopicContract, _unitOfWork, user, _timeProvider);

        public ReviewRenameRequestByAdvisorHandler AdvisorReviewHandler(ICurrentUser user)
            => new(RenameRequests, Groups, TopicContract, _unitOfWork, user, _timeProvider);

        public ReviewRenameRequestByFacultyHandler FacultyReviewHandler(ICurrentUser user)
            => new(RenameRequests, _unitOfWork, user, _timeProvider);

        public ReviewRenameRequestByAcademicAffairsHandler AcademicAffairsReviewHandler(ICurrentUser user)
            => new(RenameRequests, Groups, TopicContract, Audit, _unitOfWork, user, _timeProvider);

        public GetGroupRenameRequestsHandler GetGroupHandler(ICurrentUser user)
            => new(RenameRequests, Groups, TopicContract, user);

        public GetRenameRequestsHandler GetListHandler(ICurrentUser user)
            => new(RenameRequests, user);

        public ICurrentUser StudentUser() => User(StudentId, roles: ["Student"]);
        public ICurrentUser AdvisorUser() => User(AdvisorId, roles: ["Lecturer"]);
        public ICurrentUser FacultyUser() => User(FacultyStaffId, roles: ["Faculty"]);
        public ICurrentUser AcademicAffairsUser() => User(AcademicAffairsStaffId, roles: ["AcademicAffairs"]);
        public ICurrentUser AnonymousUser() => new AnonymousFakeUser();
        public ICurrentUser User(Guid id, string[]? roles = null, string[]? permissions = null)
            => new FakeCurrentUser(id, roles, permissions);
    }

    private sealed class FakeCurrentUser(
        Guid userId,
        IEnumerable<string>? roles = null,
        IEnumerable<string>? permissions = null) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = (roles ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> Permissions { get; } = (permissions ?? []).ToHashSet(StringComparer.Ordinal);
    }

    private sealed class AnonymousFakeUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FakeStudentGroupRepository(StudentGroup group) : IStudentGroupRepository
    {
        public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StudentGroup?>(id == group.Id ? group : null);

        public void Add(StudentGroup value) => throw new NotSupportedException();
    }

    private sealed class FakeRenameRequestRepository : IRenameRequestRepository
    {
        private readonly List<RenameRequest> _requests = [];

        public void Add(RenameRequest request) => _requests.Add(request);

        public Task<RenameRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.SingleOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<RenameRequest>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RenameRequest>>(_requests.Where(r => r.GroupId == groupId).OrderByDescending(r => r.CreatedAt).ToList());

        public Task<bool> HasActiveRequestForGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.Any(r => r.GroupId == groupId && r.Status is RenameRequestStatus.Created or RenameRequestStatus.AdvisorConfirmed or RenameRequestStatus.FacultyApproved));

        public Task<IReadOnlyList<RenameRequest>> ListAsync(
            Guid? groupId,
            RenameRequestStatus? status,
            IReadOnlyCollection<Guid>? topicIdsFilter,
            CancellationToken cancellationToken = default)
        {
            var q = _requests.AsEnumerable();
            if (groupId.HasValue) q = q.Where(r => r.GroupId == groupId.Value);
            if (status.HasValue) q = q.Where(r => r.Status == status.Value);
            return Task.FromResult<IReadOnlyList<RenameRequest>>(q.OrderByDescending(r => r.CreatedAt).ToList());
        }
    }

    private sealed class FakeBatchReader(bool isWindowOpen) : IKltnBatchReader
    {
        public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(
            Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchRegistrationWindowContractDto?>(null);

        public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchTopicPreparationWindowContractDto?>(null);

        public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchRenameRequestWindowContractDto?>(
                new BatchRenameRequestWindowContractDto(
                    batchId,
                    "Open",
                    isWindowOpen,
                    DateOnly.FromDateTime(nowUtc.AddDays(-14)),
                    nowUtc.AddDays(14)));
    }

    private sealed class FakeTopicRenameContract(
        Guid topicId,
        Guid batchId,
        Guid advisorId,
        string titleVi,
        string? titleEn,
        bool shouldFailUpdate = false) : ITopicRenameContract
    {
        public string? UpdatedTitleVi { get; private set; }
        public string? UpdatedTitleEn { get; private set; }

        public Task<TopicTitleSnapshotContractDto?> GetTopicSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id != topicId) return Task.FromResult<TopicTitleSnapshotContractDto?>(null);
            return Task.FromResult<TopicTitleSnapshotContractDto?>(
                new TopicTitleSnapshotContractDto(topicId, batchId, advisorId, titleVi, titleEn));
        }

        public Task<TopicTitleUpdateContractResultDto> UpdateTopicTitlesAsync(
            Guid id,
            string newTitleVi,
            string? newTitleEn,
            CancellationToken cancellationToken = default)
        {
            if (shouldFailUpdate)
            {
                return Task.FromResult(TopicTitleUpdateContractResultDto.Failure("Topic.DuplicateTitle", "Tên đề tài đã tồn tại."));
            }

            UpdatedTitleVi = newTitleVi;
            UpdatedTitleEn = newTitleEn;
            return Task.FromResult(TopicTitleUpdateContractResultDto.Success(titleVi, newTitleVi, titleEn, newTitleEn));
        }
    }

    private sealed class FakeAuditOutbox : IThesisManagementAuditOutbox
    {
        public List<AuditIntegrationEntry> Entries { get; } = [];

        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeUnitOfWork : IThesisUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
