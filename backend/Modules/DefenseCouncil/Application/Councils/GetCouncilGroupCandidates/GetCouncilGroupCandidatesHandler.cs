using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilGroupCandidates;

public sealed record CouncilGroupCandidateDto(Guid GroupId, string TopicTitleVi, string? ReviewerFullName,
    bool HasEvaluation, Guid? AssignedCouncilId);
public sealed record CouncilGroupEligibilityDto(Guid GroupId, bool CanProceed, string? ReasonCode);

public sealed class GetCouncilGroupCandidatesHandler(
    IReviewerAssignmentReadRepository reviewerAssignments,
    ICouncilRepository councils,
    IReviewerDefenseEligibilityContract eligibility,
    ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<CouncilGroupCandidateDto>>> HandleAsync(
        Guid batchId, string? keyword, int pageNumber, int pageSize, CancellationToken token = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.Roles.Any(role => role is "Faculty" or "Admin"))
            return Result.Failure<PagedResult<CouncilGroupCandidateDto>>(CouncilErrors.Forbidden);
        if (batchId == Guid.Empty || keyword?.Trim().Length > 200)
            return Result.Failure<PagedResult<CouncilGroupCandidateDto>>(
                Error.Validation("Council.InvalidGroupSearch", "Chọn một đợt khóa luận và nhập tối đa 200 ký tự tìm kiếm."));

        var number = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);
        var page = await reviewerAssignments.ListAsync(
            new ReviewerAssignmentListQuery(BatchId: batchId, IsActive: true, Keyword: keyword?.Trim(), PageNumber: number, PageSize: size),
            true, token);
        var groupIds = page.Items.Select(item => item.GroupId).ToArray();
        var assigned = await councils.GetAssignedGroupCouncilsAsync(groupIds, null, token);
        var items = page.Items.Select(item => new CouncilGroupCandidateDto(item.GroupId, item.TopicTitleVi,
            item.ReviewerFullName, item.HasEvaluation,
            assigned.TryGetValue(item.GroupId, out var councilId) ? councilId : null))
            .ToArray();
        return Result.Success(new PagedResult<CouncilGroupCandidateDto>(items, page.TotalCount, number, size));
    }

    public async Task<Result<CouncilGroupEligibilityDto>> EligibilityAsync(Guid groupId, CancellationToken token = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.Roles.Any(role => role is "Faculty" or "Admin"))
            return Result.Failure<CouncilGroupEligibilityDto>(CouncilErrors.Forbidden);
        if (groupId == Guid.Empty)
            return Result.Failure<CouncilGroupEligibilityDto>(Error.Validation("Council.InvalidGroup", "Chọn một nhóm bảo vệ."));
        var value = await eligibility.GetAsync(groupId, token);
        if (value is null)
            return Result.Failure<CouncilGroupEligibilityDto>(Error.NotFound("Council.GroupNotFound", "Không tìm thấy nhóm bảo vệ."));
        return Result.Success(new CouncilGroupEligibilityDto(groupId, value.CanProceed, value.ReasonCode?.ToString()));
    }
}
