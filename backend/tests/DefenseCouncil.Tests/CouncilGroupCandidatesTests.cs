using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilGroupCandidates;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilGroupCandidatesTests
{
    [Fact]
    public async Task Faculty_sees_unassigned_group_without_an_empty_council_id()
    {
        var batchId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var assignment = new ReviewerAssignmentDto(Guid.NewGuid(), groupId, Guid.NewGuid(), batchId,
            "Đề tài thử nghiệm", null, Guid.NewGuid(), "Giảng viên phản biện", null, null, null, null,
            null, null, 1, true, false, 1, Guid.NewGuid(), DateTime.UtcNow, null, null, null);
        var reviewerAssignments = new StubReviewerAssignments(new ReviewerAssignmentListItemDto(assignment, true, false));
        var user = new FakeCurrentUser { Roles = new HashSet<string> { "Faculty" } };
        var handler = new GetCouncilGroupCandidatesHandler(reviewerAssignments, new FakeCouncilRepository(),
            new StubEligibility(groupId), user);

        var result = await handler.HandleAsync(batchId, null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(batchId, reviewerAssignments.LastQuery?.BatchId);
        Assert.True(reviewerAssignments.LastQuery?.IsActive);
        Assert.Single(result.Value.Items);
        Assert.Null(result.Value.Items[0].AssignedCouncilId);
        Assert.Equal("Đề tài thử nghiệm", result.Value.Items[0].TopicTitleVi);
    }

    [Fact]
    public async Task Faculty_reads_specific_reason_without_requiring_reviewer_management_permission()
    {
        var groupId = Guid.NewGuid();
        var user = new FakeCurrentUser { Roles = new HashSet<string> { "Faculty" } };
        var handler = new GetCouncilGroupCandidatesHandler(new StubReviewerAssignments(),
            new FakeCouncilRepository(), new StubEligibility(groupId), user);

        var result = await handler.EligibilityAsync(groupId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CanProceed);
        Assert.Equal("EvaluationNotSubmitted", result.Value.ReasonCode);
        user.Roles = new HashSet<string> { "Student" };
        Assert.True((await handler.EligibilityAsync(groupId)).IsFailure);
    }

    private sealed class StubReviewerAssignments(params ReviewerAssignmentListItemDto[] items) : IReviewerAssignmentReadRepository
    {
        public ReviewerAssignmentListQuery? LastQuery { get; private set; }

        public Task<PagedResult<ReviewerAssignmentListItemDto>> ListAsync(
            ReviewerAssignmentListQuery query, bool canManage, CancellationToken token)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<ReviewerAssignmentListItemDto>(items, items.Length, query.PageNumber, query.PageSize));
        }

        public Task<PagedResult<ReviewerAssignmentEligibilityItemDto>> EligibilityAsync(
            ReviewerAssignmentEligibilityQuery query, bool canManage, CancellationToken token) => throw new NotSupportedException();

        public Task<PagedResult<ReviewerAssignmentTaskDto>> TasksAsync(
            Guid reviewerId, ReviewerAssignmentTaskQuery query, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class StubEligibility(Guid groupId) : IReviewerDefenseEligibilityContract
    {
        public Task<ReviewerDefenseEligibilityContractDto?> GetAsync(Guid id, CancellationToken token = default)
            => Task.FromResult<ReviewerDefenseEligibilityContractDto?>(id == groupId
                ? new(id, Guid.NewGuid(), "Open", Guid.NewGuid(), null, null, null, null, null,
                    false, ReviewerDefenseEligibilityReason.EvaluationNotSubmitted)
                : null);

        public Task<ReviewerDefenseEligibilityContractDto?> LockForSchedulingAsync(
            Guid id, CancellationToken token = default) => throw new NotSupportedException();
    }
}
