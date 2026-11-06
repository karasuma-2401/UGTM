using UGTM.Modules.DefenseCouncil.Application.Councils;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilApplicationTests
{
    private readonly FakeCouncilRepository _councilRepo = new();
    private readonly FakeDefenseCouncilUnitOfWork _uow = new();
    private readonly FakeBatchReader _batchReader = new();
    private readonly FakeCouncilUserInfoReader _userInfoReader = new();
    private readonly FakeStudentInfoReader _studentInfoReader = new();
    private readonly FakeDefenseGroupContract _defenseGroupContract = new();
    private readonly FakeTopicDefenseContract _topicDefenseContract = new();
    private readonly FakeDefenseCouncilAuditOutbox _auditOutbox = new();
    private readonly FakeCurrentUser _currentUser = new();

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly DateOnly _defenseDate = new(2026, 12, 15);
    private const string TimeSlotMorning = "08:00 - 11:30";
    private const string RoomA101 = "A101";

    public CouncilApplicationTests()
    {
        _batchReader.ExistingBatches.Add(_batchId);
    }

    #region 1. CreateCouncilHandler Tests

    [Fact]
    public async Task CreateCouncil_Succeeds_WhenValid()
    {
        var handler = new CreateCouncilHandler(_councilRepo, _uow, _batchReader, _currentUser);
        var command = new CreateCouncilCommand(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("HD01", result.Value.CouncilCode);
        Assert.Equal("Draft", result.Value.Status);
        Assert.Equal(1, _uow.SaveCount);
        Assert.Single(_councilRepo.Councils);
    }

    [Fact]
    public async Task CreateCouncil_Fails_WhenUnauthenticatedOrNotStaff()
    {
        _currentUser.Roles = new HashSet<string> { "Student" };
        var handler = new CreateCouncilHandler(_councilRepo, _uow, _batchReader, _currentUser);
        var command = new CreateCouncilCommand(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateCouncil_Fails_WhenBatchNotFound()
    {
        var handler = new CreateCouncilHandler(_councilRepo, _uow, _batchReader, _currentUser);
        var nonExistentBatch = Guid.NewGuid();
        var command = new CreateCouncilCommand(nonExistentBatch, "HD01", _defenseDate, TimeSlotMorning, RoomA101);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.BatchNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateCouncil_Fails_WhenCouncilCodeAlreadyExistsInBatch()
    {
        var existing = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(existing);

        var handler = new CreateCouncilHandler(_councilRepo, _uow, _batchReader, _currentUser);
        var command = new CreateCouncilCommand(_batchId, "hd01", _defenseDate, "13:00 - 16:30", "B202");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.DuplicateCode.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateCouncil_Fails_WhenRoomScheduleConflictExists()
    {
        var existing = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(existing);

        var handler = new CreateCouncilHandler(_councilRepo, _uow, _batchReader, _currentUser);
        var command = new CreateCouncilCommand(_batchId, "HD02", _defenseDate, " 08:00 - 11:30 ", " a101 ");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.RoomScheduleConflict.Code, result.Error.Code);
    }

    #endregion

    #region 2. AddCouncilMembersHandler Tests

    [Fact]
    public async Task AddCouncilMembers_Succeeds_WithChairmanSecretaryAndMember()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        _userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "PGS. TS. Nguyen Van A", "nguyenvana@uit.edu.vn", "CB001", true);
        _userInfoReader.Users[secretaryId] = new CouncilUserInfoContractDto(secretaryId, "ThS. Tran Thi B", "tranthib@uit.edu.vn", "CB002", true);
        _userInfoReader.Users[memberId] = new CouncilUserInfoContractDto(memberId, "TS. Le Van C", "levanc@uit.edu.vn", "CB003", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [
            new(chairmanId, CouncilMemberRole.Chairman),
            new(secretaryId, CouncilMemberRole.Secretary),
            new(memberId, CouncilMemberRole.Member)
        ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(3, _councilRepo.Members.Count);
    }

    [Fact]
    public async Task AddCouncilMembers_Succeeds_WithOnlyChairmanAndSecretary_ZeroMembers()
    {
        // Real-world university case: council has only Chairman and Secretary, no independent Member
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();

        _userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "Chủ tịch A", "a@uit.edu.vn", "CB001", true);
        _userInfoReader.Users[secretaryId] = new CouncilUserInfoContractDto(secretaryId, "Thư ký B", "b@uit.edu.vn", "CB002", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [
            new(chairmanId, CouncilMemberRole.Chairman),
            new(secretaryId, CouncilMemberRole.Secretary)
        ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenAddingMultipleChairmen()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        _userInfoReader.Users[c1] = new CouncilUserInfoContractDto(c1, "A", "a@uit.edu.vn", "CB001", true);
        _userInfoReader.Users[c2] = new CouncilUserInfoContractDto(c2, "B", "b@uit.edu.vn", "CB002", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [
            new(c1, CouncilMemberRole.Chairman),
            new(c2, CouncilMemberRole.Chairman)
        ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.DuplicateChairman.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenLecturerInactiveOrNotFound()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var inactiveId = Guid.NewGuid();
        _userInfoReader.Users[inactiveId] = new CouncilUserInfoContractDto(inactiveId, "Inactive", "in@uit.edu.vn", "CB009", false);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(inactiveId, CouncilMemberRole.Chairman)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.MemberNotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenMemberIsAdvisorOfAssignedGroup_ConflictOfInterest()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();

        // Assign a group to the council
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId, _batchId, advisorId, "Hệ thống AI nhận diện bệnh", null);

        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "TS. Nguyen Van GVHD", "gvhd@uit.edu.vn", "CB010", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        // Try to add the advisor as Chairman
        var command = new AddCouncilMembersCommand(council.Id, [new(advisorId, CouncilMemberRole.Chairman)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.MemberIsGroupAdvisor", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Allows_ReviewerToBeCouncilMember_WhileBlockingAdvisor()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId, _batchId, advisorId, "Đề tài nghiên cứu", null);

        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "GVPB Kiem Uy Vien", "gvpb@uit.edu.vn", "CB020", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        // Adding GVPB as Member must succeed
        var command = new AddCouncilMembersCommand(council.Id, [new(reviewerId, CouncilMemberRole.Member)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenLecturerHasScheduleConflictInAnotherCouncil()
    {
        var otherCouncil = Council.Create(_batchId, "HD_OTHER", _defenseDate, TimeSlotMorning, "A202");
        await _councilRepo.AddAsync(otherCouncil);

        var busyLecturerId = Guid.NewGuid();
        var otherMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = otherCouncil.Id, UserId = busyLecturerId, RoleInCouncil = CouncilMemberRole.Chairman };
        _councilRepo.Members.Add(otherMember);

        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        _userInfoReader.Users[busyLecturerId] = new CouncilUserInfoContractDto(busyLecturerId, "Lecturer Busy", "busy@uit.edu.vn", "CB099", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(busyLecturerId, CouncilMemberRole.Member)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.MemberScheduleConflict", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenMemberIsAssignedGroupReviewerAndRoleIsChairman()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        _councilRepo.Assignments.Add(new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 });
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài AI", null);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. GVPB", "gvpb@uit.edu.vn", "CB010", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(reviewerId, CouncilMemberRole.Chairman)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.MemberIsGroupReviewerWithInvalidRole", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenMemberIsAssignedGroupReviewerAndRoleIsSecretary()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        _councilRepo.Assignments.Add(new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 });
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài AI", null);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. GVPB", "gvpb@uit.edu.vn", "CB010", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(reviewerId, CouncilMemberRole.Secretary)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.MemberIsGroupReviewerWithInvalidRole", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Succeeds_WhenMemberIsAssignedGroupReviewerAndRoleIsMember()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        _councilRepo.Assignments.Add(new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 });
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài AI", null);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. GVPB", "gvpb@uit.edu.vn", "CB010", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(reviewerId, CouncilMemberRole.Member)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Member", result.Value[0].Role);
    }

    #endregion

    #region 3. AssignCouncilGroupsHandler Tests

    [Fact]
    public async Task AssignCouncilGroups_Succeeds_WhenValid()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var g1 = Guid.NewGuid();
        var t1 = Guid.NewGuid();
        var adv1 = Guid.NewGuid();

        _defenseGroupContract.Groups[g1] = new DefenseGroupSnapshotContractDto(g1, t1, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[t1] = new TopicDefenseDetailsContractDto(t1, _batchId, adv1, "Tiêu đề 1", "Title 1");

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(g1, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(1, result.Value[0].OrderNumber);
        Assert.Equal("Tiêu đề 1", result.Value[0].TopicTitleVi);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenAdvisorIsCouncilMember_ConflictOfInterest()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var advisorId = Guid.NewGuid();
        var member = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = advisorId, RoleInCouncil = CouncilMemberRole.Chairman };
        council.Members.Add(member);
        _councilRepo.Members.Add(member);

        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "Thầy A (Chủ tịch kiêm GVHD)", "a@uit.edu.vn", "CB001", true);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài của Thầy A", null);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.AdvisorIsInCouncil", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenReviewerRejectedGroup()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài bị đình chỉ", null);

        _councilRepo.RejectedGroupIds.Add(groupId);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupDefenseRejectedByReviewer", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenGroupAlreadyAssignedToAnotherCouncil()
    {
        var otherCouncil = Council.Create(_batchId, "HD_OTHER", _defenseDate, TimeSlotMorning, "A202");
        await _councilRepo.AddAsync(otherCouncil);

        var groupId = Guid.NewGuid();
        _councilRepo.Assignments.Add(new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = otherCouncil.Id, GroupId = groupId, OrderNumber = 1 });

        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupAlreadyAssigned", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenDuplicateOrderNumber()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();

        _defenseGroupContract.Groups[g1] = new DefenseGroupSnapshotContractDto(g1, t1, _batchId, "InProgress", DateTime.UtcNow, []);
        _defenseGroupContract.Groups[g2] = new DefenseGroupSnapshotContractDto(g2, t2, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[t1] = new TopicDefenseDetailsContractDto(t1, _batchId, Guid.NewGuid(), "T1", null);
        _topicDefenseContract.Topics[t2] = new TopicDefenseDetailsContractDto(t2, _batchId, Guid.NewGuid(), "T2", null);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(g1, 1), new(g2, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.DuplicateOrderNumber.Code, result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenGroupReviewerIsCouncilChairman()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var reviewerId = Guid.NewGuid();
        var chairman = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = reviewerId, RoleInCouncil = CouncilMemberRole.Chairman };
        council.Members.Add(chairman);
        _councilRepo.Members.Add(chairman);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "PGS. TS. Reviewer Chairman", "chair@uit.edu.vn", "CB001", true);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài Cloud", null);
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.ReviewerIsChairmanOrSecretaryInCouncil", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenGroupReviewerIsCouncilSecretary()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var reviewerId = Guid.NewGuid();
        var secretary = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = reviewerId, RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(secretary);
        _councilRepo.Members.Add(secretary);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "ThS. Reviewer Secretary", "sec@uit.edu.vn", "CB002", true);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài Cloud", null);
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.ReviewerIsChairmanOrSecretaryInCouncil", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Succeeds_WhenGroupReviewerIsCouncilMember()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var reviewerId = Guid.NewGuid();
        var member = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = reviewerId, RoleInCouncil = CouncilMemberRole.Member };
        council.Members.Add(member);
        _councilRepo.Members.Add(member);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. Reviewer Member", "member@uit.edu.vn", "CB003", true);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài Cloud", null);
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenGroupIsNotEligibleForDefense()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, Guid.NewGuid(), "Đề tài chưa hoàn tất phản biện", null);

        // Đánh dấu nhóm chưa đủ điều kiện
        _councilRepo.IneligibleDefenseGroupIds.Add(groupId);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupNotEligibleForDefense", result.Error.Code);
    }

    #endregion

    #region 4. ApproveCouncilHandler Tests

    [Fact]
    public async Task ApproveCouncil_Succeeds_WithChairmanAndSecretary_ZeroMembers()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };

        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();
        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chairmanId, RoleInCouncil = CouncilMemberRole.Chairman };
        var sMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secretaryId, RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(cMember);
        council.Members.Add(sMember);
        _councilRepo.Members.Add(cMember);
        _councilRepo.Members.Add(sMember);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài tốt nghiệp", null);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-1234/QĐ-ĐHCNTT", new DateOnly(2026, 12, 1), "https://storage.uit.edu.vn/qd-1234.pdf");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
        Assert.Equal("QD-1234/QĐ-ĐHCNTT", result.Value.DecisionNumber);
        Assert.Equal(1, _uow.SaveCount);
        Assert.Single(_auditOutbox.Entries);
        Assert.Equal("DefenseCouncil.Council.Approved", _auditOutbox.Entries[0].Entry.Action);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenUserIsNotAcademicAffairsOrAdmin()
    {
        _currentUser.Roles = new HashSet<string> { "Faculty" }; // Faculty cannot approve
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-1234");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenMissingSecretary()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        // Only Chairman, missing Secretary
        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Chairman };
        council.Members.Add(cMember);
        _councilRepo.Members.Add(cMember);

        var groupId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-1234");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.MissingSecretary.Code, result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenNoAssignedGroups()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Chairman };
        var sMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(cMember);
        council.Members.Add(sMember);
        _councilRepo.Members.Add(cMember);
        _councilRepo.Members.Add(sMember);

        // 0 assignments
        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-1234");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NoAssignedGroups.Code, result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenGroupReviewerIsChairman()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var reviewerId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();
        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = reviewerId, RoleInCouncil = CouncilMemberRole.Chairman };
        var sMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secretaryId, RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(cMember);
        council.Members.Add(sMember);
        _councilRepo.Members.Add(cMember);
        _councilRepo.Members.Add(sMember);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. Reviewer Chair", "rev@uit.edu.vn", "CB001", true);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài AI", null);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-1234");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.ReviewerIsChairmanOrSecretaryInCouncil", result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenGroupIsNotEligibleForDefense()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();
        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chairmanId, RoleInCouncil = CouncilMemberRole.Chairman };
        var sMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secretaryId, RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(cMember);
        council.Members.Add(sMember);
        _councilRepo.Members.Add(cMember);
        _councilRepo.Members.Add(sMember);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, _batchId, advisorId, "Đề tài AI", null);

        _councilRepo.IneligibleDefenseGroupIds.Add(groupId);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-1234");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupNotEligibleForDefense", result.Error.Code);
    }

    #endregion

    #region 5. GetCouncilScheduleHandler Tests

    [Fact]
    public async Task GetCouncilSchedule_Succeeds_ByGroupId()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Approve("QD-01", null, null, Guid.NewGuid(), DateTime.UtcNow);
        await _councilRepo.AddAsync(council);

        var chairmanId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chairmanId, RoleInCouncil = CouncilMemberRole.Chairman };
        var rMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = reviewerId, RoleInCouncil = CouncilMemberRole.Member };
        council.Members.Add(cMember);
        council.Members.Add(rMember);
        _councilRepo.Members.Add(cMember);
        _councilRepo.Members.Add(rMember);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);
        _councilRepo.ActiveReviewers[groupId] = reviewerId;

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, [
                new(studentId, _batchId, "Confirmed")
            ]);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId, _batchId, advisorId, "Phát triển phần mềm KLTN", "UGTM Development");

        _userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "Chủ tịch A", "ct@uit.edu.vn", "CB001", true);
        _userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "GVPB B (Ủy viên)", "pb@uit.edu.vn", "CB002", true);
        _userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "GVHD C", "hd@uit.edu.vn", "CB003", true);
        _studentInfoReader.Students[studentId] = new StudentInfoContractDto(studentId, "student@gm.uit.edu.vn", "Sinh Vien D", "21520001", "0900000001");

        _currentUser.Roles = new HashSet<string> { "Student" };

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(groupId));

        Assert.True(result.IsSuccess);
        Assert.Equal("HD01", result.Value.CouncilCode);
        Assert.Equal(RoomA101, result.Value.Room);
        Assert.Equal(2, result.Value.Members.Count);
        Assert.Single(result.Value.Groups);

        var group = result.Value.Groups[0];
        Assert.Equal("Phát triển phần mềm KLTN", group.TopicTitleVi);
        Assert.Equal("GVHD C", group.AdvisorName);
        Assert.Equal("GVPB B (Ủy viên)", group.ReviewerName);
        Assert.True(group.IsReviewerInCouncil); // Reviewer is member of council
        Assert.Single(group.Students);
        Assert.Equal("Sinh Vien D", group.Students[0].FullName);
    }

    [Fact]
    public async Task GetCouncilSchedule_Succeeds_ByStudentId()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Approve("QD-01", null, null, Guid.NewGuid(), DateTime.UtcNow);
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, Guid.NewGuid(), _batchId, "InProgress", DateTime.UtcNow, [
                new(studentId, _batchId, "Confirmed")
            ]);

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(studentId));

        Assert.True(result.IsSuccess);
        Assert.Equal(council.Id, result.Value.CouncilId);
    }

    [Fact]
    public async Task GetCouncilSchedule_HidesFromStudent_WhenCouncilIsDraft()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        // Council is in Draft status (not yet approved)
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _currentUser.Roles = new HashSet<string> { "Student" };

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(groupId));

        Assert.True(result.IsFailure);
        Assert.Equal("Council.ScheduleNotPublished", result.Error.Code);
    }

    [Fact]
    public async Task GetCouncilSchedule_AllowsStaffToView_WhenCouncilIsDraft()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _currentUser.Roles = new HashSet<string> { "Faculty" }; // Staff can view draft

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(groupId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Draft", result.Value.Status);
    }

    [Fact]
    public async Task GetCouncilSchedule_Fails_WhenNotFound()
    {
        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetCouncilSchedule_Fails_WhenUnauthenticated()
    {
        _currentUser.IsAuthenticated = false;

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetCouncilSchedule_AllowsStudent_WhenStatusIsCompleted()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Status = CouncilStatus.Completed;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _currentUser.Roles = new HashSet<string> { "Student" };

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(groupId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Value.Status);
    }

    [Fact]
    public async Task GetCouncilSchedule_FlagsReviewerInCouncil_WhenReviewerIsMember()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Approve("QD-01", null, null, Guid.NewGuid(), DateTime.UtcNow);
        await _councilRepo.AddAsync(council);

        var reviewerAndMemberId = Guid.NewGuid();
        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = reviewerAndMemberId, RoleInCouncil = CouncilMemberRole.Member };
        council.Members.Add(cMember);
        _councilRepo.Members.Add(cMember);

        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);
        _councilRepo.ActiveReviewers[groupId] = reviewerAndMemberId;

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, _batchId, "InProgress", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId, _batchId, Guid.NewGuid(), "T1", null);

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(groupId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Groups[0].IsReviewerInCouncil);
    }

    [Fact]
    public async Task GetCouncilSchedule_HandlesGroupWithoutReviewer()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Approve("QD-01", null, null, Guid.NewGuid(), DateTime.UtcNow);
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, Guid.NewGuid(), _batchId, "InProgress", DateTime.UtcNow, []);

        var handler = new GetCouncilScheduleHandler(
            _councilRepo, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _studentInfoReader, _currentUser);

        var result = await handler.HandleAsync(new GetCouncilScheduleQuery(groupId));

        Assert.True(result.IsSuccess);
        var g = result.Value.Groups[0];
        Assert.Null(g.ReviewerId);
        Assert.Null(g.ReviewerName);
        Assert.False(g.IsReviewerInCouncil);
    }

    #endregion

    #region 6. Boundary & Abnormal Tests for Create, AddMembers, AssignGroups, Approve

    [Theory]
    [InlineData("", "08:00 - 11:30", "A101")]
    [InlineData("   ", "08:00 - 11:30", "A101")]
    [InlineData("HD01", "", "A101")]
    [InlineData("HD01", "   ", "A101")]
    [InlineData("HD01", "08:00 - 11:30", "")]
    [InlineData("HD01", "08:00 - 11:30", "   ")]
    public async Task CreateCouncil_Fails_WhenInputsAreEmptyOrWhitespace(string code, string slot, string room)
    {
        var handler = new CreateCouncilHandler(_councilRepo, _uow, _batchReader, _currentUser);
        var command = new CreateCouncilCommand(_batchId, code, _defenseDate, slot, room);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.InvalidInput", result.Error.Code);
    }

    [Fact]
    public async Task CreateCouncil_Fails_WhenBatchIdIsEmpty()
    {
        var handler = new CreateCouncilHandler(_councilRepo, _uow, _batchReader, _currentUser);
        var command = new CreateCouncilCommand(Guid.Empty, "HD01", _defenseDate, TimeSlotMorning, RoomA101);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.BatchNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenCouncilNotFound()
    {
        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(Guid.NewGuid(), [new(Guid.NewGuid(), CouncilMemberRole.Chairman)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenCouncilIsNotDraft()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Status = CouncilStatus.Active;
        await _councilRepo.AddAsync(council);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(Guid.NewGuid(), CouncilMemberRole.Chairman)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotInDraftStatus.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenEmptyMembers()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, []);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.EmptyMembers", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenDuplicateMemberWithinRequest()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var userId = Guid.NewGuid();
        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        // Same user twice with different roles
        var command = new AddCouncilMembersCommand(council.Id, [
            new(userId, CouncilMemberRole.Chairman),
            new(userId, CouncilMemberRole.Secretary)
        ]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.DuplicateMemberInRequest", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenMultipleSecretaries()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        _userInfoReader.Users[s1] = new CouncilUserInfoContractDto(s1, "S1", "s1@uit.edu.vn", "CB001", true);
        _userInfoReader.Users[s2] = new CouncilUserInfoContractDto(s2, "S2", "s2@uit.edu.vn", "CB002", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [
            new(s1, CouncilMemberRole.Secretary),
            new(s2, CouncilMemberRole.Secretary)
        ]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.DuplicateSecretary.Code, result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenMemberAlreadyInCouncil()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var userId = Guid.NewGuid();
        var existingMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = userId, RoleInCouncil = CouncilMemberRole.Chairman };
        council.Members.Add(existingMember);
        _councilRepo.Members.Add(existingMember);

        _userInfoReader.Users[userId] = new CouncilUserInfoContractDto(userId, "Member A", "a@uit.edu.vn", "CB001", true);

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(userId, CouncilMemberRole.Member)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.DuplicateMember", result.Error.Code);
    }

    [Fact]
    public async Task AddCouncilMembers_Fails_WhenUnauthenticatedOrStudent()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        _currentUser.Roles = new HashSet<string> { "Student" };

        var handler = new AddCouncilMembersHandler(
            _councilRepo, _uow, _userInfoReader, _defenseGroupContract, _topicDefenseContract, _currentUser);

        var command = new AddCouncilMembersCommand(council.Id, [new(Guid.NewGuid(), CouncilMemberRole.Chairman)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenCouncilNotFound()
    {
        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenCouncilIsNotDraft()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Status = CouncilStatus.Completed;
        await _councilRepo.AddAsync(council);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(Guid.NewGuid(), 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotInDraftStatus.Code, result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenEmptyAssignments()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, []);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.EmptyAssignments", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenDuplicateGroupInRequest()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var gId = Guid.NewGuid();
        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(gId, 1), new(gId, 2)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.DuplicateGroupInRequest", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task AssignCouncilGroups_Fails_WhenOrderNumberIsZeroOrNegative(int invalidOrder)
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(Guid.NewGuid(), invalidOrder)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.InvalidOrderNumber", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenGroupNotFound()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var nonExistentGroupId = Guid.NewGuid();
        var command = new AssignCouncilGroupsCommand(council.Id, [new(nonExistentGroupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupNotFound", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenGroupNotInSameBatch()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        var otherBatchId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, Guid.NewGuid(), otherBatchId, "InProgress", DateTime.UtcNow, []);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupNotInBatch", result.Error.Code);
    }

    [Fact]
    public async Task AssignCouncilGroups_Fails_WhenGroupIsStopped()
    {
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var groupId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, Guid.NewGuid(), _batchId, "Stopped", DateTime.UtcNow, []);

        var handler = new AssignCouncilGroupsHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser);

        var command = new AssignCouncilGroupsCommand(council.Id, [new(groupId, 1)]);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupStopped", result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenCouncilNotFound()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(Guid.NewGuid(), "QD-01");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotFound.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApproveCouncil_Fails_WhenDecisionNumberIsEmptyOrWhitespace(string decisionNumber)
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };

        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, decisionNumber);
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.DecisionNumberRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenAlreadyApproved()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };

        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.Status = CouncilStatus.Active;
        await _councilRepo.AddAsync(council);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-01");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.AlreadyApproved.Code, result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenMissingChairman()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };

        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        // Only Secretary, no Chairman
        var sMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(sMember);
        _councilRepo.Members.Add(sMember);

        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = Guid.NewGuid(), OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-01");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.MissingChairman.Code, result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenRoomConflictDetectedAtApprovalTime()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };

        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Chairman };
        var sMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(cMember);
        council.Members.Add(sMember);
        _councilRepo.Members.Add(cMember);
        _councilRepo.Members.Add(sMember);

        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = Guid.NewGuid(), OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        // Simulate another council holding the exact room + date + timeslot
        var otherCouncil = Council.Create(_batchId, "HD_OTHER", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(otherCouncil);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-01");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.RoomScheduleConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task ApproveCouncil_Fails_WhenMemberScheduleConflictDetectedAtApprovalTime()
    {
        _currentUser.Roles = new HashSet<string> { "AcademicAffairs" };

        var busyLecturer = Guid.NewGuid();
        var council = Council.Create(_batchId, "HD01", _defenseDate, TimeSlotMorning, RoomA101);
        council.IsSubmittedToAcademicAffairs = true;
        await _councilRepo.AddAsync(council);

        var cMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = busyLecturer, RoleInCouncil = CouncilMemberRole.Chairman };
        var sMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Secretary };
        council.Members.Add(cMember);
        council.Members.Add(sMember);
        _councilRepo.Members.Add(cMember);
        _councilRepo.Members.Add(sMember);

        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = Guid.NewGuid(), OrderNumber = 1 };
        council.Assignments.Add(assignment);
        _councilRepo.Assignments.Add(assignment);

        // Simulate another council where busyLecturer is also a member at the same date + timeslot
        var otherCouncil = Council.Create(_batchId, "HD_OTHER", _defenseDate, TimeSlotMorning, "B202");
        await _councilRepo.AddAsync(otherCouncil);
        var otherMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = otherCouncil.Id, UserId = busyLecturer, RoleInCouncil = CouncilMemberRole.Member };
        _councilRepo.Members.Add(otherMember);

        var handler = new ApproveCouncilHandler(
            _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _auditOutbox, _currentUser);

        var command = new ApproveCouncilCommand(council.Id, "QD-01");
        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Council.MemberScheduleConflict", result.Error.Code);
    }

    #endregion
}
