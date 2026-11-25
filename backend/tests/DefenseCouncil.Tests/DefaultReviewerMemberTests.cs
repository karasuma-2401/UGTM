using UGTM.Modules.DefenseCouncil.Application.Councils;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed partial class CouncilApplicationTests
{
    private Guid SeedDefaultReviewerGroup(Guid reviewer, Guid? advisor = null)
    {
        var group = Guid.NewGuid();
        var topic = Guid.NewGuid();
        _defenseGroupContract.Groups[group] = new(group, topic, _batchId, "ReadyForReviewerAssignment", DateTime.UtcNow, []);
        _topicDefenseContract.Topics[topic] = new(topic, _batchId, advisor ?? Guid.NewGuid(), "Đề tài", null);
        _councilRepo.ActiveReviewers[group] = reviewer;
        _userInfoReader.Users.TryAdd(reviewer, new(reviewer, "GVPB", "reviewer@example.test", "GV02", true));
        return group;
    }

    private AssignCouncilGroupsHandler DefaultReviewerHandler() => new(
        _councilRepo, _uow, _defenseGroupContract, _topicDefenseContract, _userInfoReader, _currentUser, new FakeCouncilSchedulingGuard());

    [Fact]
    public async Task AssignGroup_PersistsReviewerAsMemberBeforeApproval_AndDoesNotDuplicateOnNextGroup()
    {
        var council = Council.Create(_batchId, "DEFAULT", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(council);
        var reviewer = Guid.NewGuid();
        var first = SeedDefaultReviewerGroup(reviewer);
        var result = await DefaultReviewerHandler().HandleAsync(new(council.Id, [new(first, 1)]));
        Assert.True(result.IsSuccess, result.Error?.Code);
        var member = Assert.Single(_councilRepo.Members);
        Assert.Equal(reviewer, member.UserId);
        Assert.Equal(CouncilMemberRole.Member, member.RoleInCouncil);
        Assert.Equal(CouncilStatus.Draft, council.Status);
        var second = SeedDefaultReviewerGroup(reviewer);
        Assert.True((await DefaultReviewerHandler().HandleAsync(new(council.Id, [new(second, 2)]))).IsSuccess);
        Assert.Single(_councilRepo.Members);
        Assert.Equal(2, _councilRepo.Assignments.Count);
    }

    [Fact]
    public async Task AssignGroup_PreservesAnExplicitIndependentMember()
    {
        var council = Council.Create(_batchId, "EXPLICIT", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(council);
        var member = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = Guid.NewGuid(), RoleInCouncil = CouncilMemberRole.Member };
        await _councilRepo.AddMembersAsync([member]);
        var group = SeedDefaultReviewerGroup(Guid.NewGuid());
        Assert.True((await DefaultReviewerHandler().HandleAsync(new(council.Id, [new(group, 1)]))).IsSuccess);
        Assert.Equal(member.UserId, Assert.Single(_councilRepo.Members).UserId);
    }

    [Fact]
    public async Task AssignGroups_WithDifferentReviewersAndNoMember_RequiresExplicitChoiceWithoutWrites()
    {
        var council = Council.Create(_batchId, "MULTI", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(council);
        var first = SeedDefaultReviewerGroup(Guid.NewGuid());
        var second = SeedDefaultReviewerGroup(Guid.NewGuid());
        var result = await DefaultReviewerHandler().HandleAsync(new(council.Id, [new(first, 1), new(second, 2)]));
        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.DefaultReviewerMemberAmbiguous.Code, result.Error.Code);
        Assert.Empty(_councilRepo.Members);
        Assert.Empty(_councilRepo.Assignments);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task DefaultReviewer_CannotBeAdvisorOfAnotherGroupInTheSameCouncil()
    {
        var council = Council.Create(_batchId, "CONFLICT", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(council);
        var reviewer = Guid.NewGuid();
        var group = SeedDefaultReviewerGroup(reviewer, reviewer);
        var result = await DefaultReviewerHandler().HandleAsync(new(council.Id, [new(group, 1)]));
        Assert.True(result.IsFailure);
        Assert.Equal("Council.MemberIsGroupAdvisor", result.Error.Code);
        Assert.Empty(_councilRepo.Members);
        Assert.Empty(_councilRepo.Assignments);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DefaultReviewer_MustBeAnActiveLecturer(bool active, bool lecturer)
    {
        var council = Council.Create(_batchId, "ACCOUNT", _defenseDate, TimeSlotMorning, RoomA101);
        await _councilRepo.AddAsync(council);
        var reviewer = Guid.NewGuid();
        var group = SeedDefaultReviewerGroup(reviewer);
        _userInfoReader.Users[reviewer] = new(reviewer, "GVPB", "r@example.test", "R", active, lecturer);
        var result = await DefaultReviewerHandler().HandleAsync(new(council.Id, [new(group, 1)]));
        Assert.True(result.IsFailure);
        Assert.Empty(_councilRepo.Members);
        Assert.Empty(_councilRepo.Assignments);
    }
}

public sealed class DefaultReviewerMinuteCalculationTests
{
    private static CouncilMinuteWorkspaceCalculator.ItemEvaluationData Item(string code, decimal? score, bool dual = false)
        => new(code, code, "Lecturer", score, 0, "", dual, false, SourceId: Guid.NewGuid());

    [Fact]
    public void ReviewerAsMember_UsesLockedReviewerScoreOnce_WithoutAnotherCouncilScore()
    {
        var items = new[] { Item("ADVISOR", 8.25m), Item("REVIEWER", 6m, true), Item("COUNCIL_CHAIRMAN", 7.25m), Item("COUNCIL_SECRETARY", 7.25m), Item("COUNCIL_MEMBER", null, true) };
        var result = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(items, null, null, null, null, FakeMinuteConfigurationReader.ForBatch(Guid.NewGuid()));
        Assert.True(result.CalculationPreview.IsReadyToLock);
        Assert.Equal(6m, result.CalculationPreview.AdjustedDivisor);
        Assert.Equal(7.17m, result.CalculationPreview.FinalRoundedScore);
        Assert.Empty(result.CalculationPreview.MissingSources!);
        Assert.Equal(0m, result.ScoreItems.Single(item => item.ComponentCode == "COUNCIL_MEMBER").Weight);
    }

    [Fact]
    public void IndependentMember_RequiresItsOwnScore_AndIdentifiesTheMissingSource()
    {
        var items = new[] { Item("ADVISOR", 8.25m), Item("REVIEWER", 6m), Item("COUNCIL_CHAIRMAN", 7.25m), Item("COUNCIL_SECRETARY", 7.25m) };
        var config = FakeMinuteConfigurationReader.ForBatch(Guid.NewGuid());
        var missing = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(items, null, null, null, null, config);
        Assert.False(missing.CalculationPreview.IsReadyToLock);
        Assert.Equal("ScoreSummary.MissingRequiredSource", missing.CalculationPreview.CalculationErrorCode);
        Assert.Equal(["CouncilMember"], missing.CalculationPreview.MissingSources);
        var completed = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(items.Append(Item("COUNCIL_MEMBER", 9m)), null, null, null, null, config);
        Assert.True(completed.CalculationPreview.IsReadyToLock);
        Assert.Equal(7m, completed.CalculationPreview.AdjustedDivisor);
        Assert.Equal(7.43m, completed.CalculationPreview.FinalRoundedScore);
    }
}
