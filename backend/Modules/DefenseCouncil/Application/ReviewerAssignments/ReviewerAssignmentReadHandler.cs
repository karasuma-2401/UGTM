using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public sealed class ReviewerAssignmentReadHandler(IReviewerAssignmentReadRepository reads,
    IReviewerAssignmentThesisReader thesis, IReviewerAssignmentTopicReader topics, IReviewerCandidateReader candidates,
    ICurrentUser user)
{
    private Error? Authenticate(bool management)
    {
        if (!user.IsAuthenticated || user.UserId is not { } id || id == Guid.Empty) return ReviewerAssignmentErrors.AuthenticationRequired;
        return management && !ReviewerAssignmentAuthorization.CanRead(user.Roles, user.Permissions) ? ReviewerAssignmentErrors.AccessDenied : null;
    }
    private bool CanManage => ReviewerAssignmentAuthorization.CanManage(user.Roles, user.Permissions);
    private static bool ValidKeyword(string? value) => value?.Trim().Length is not > 200;
    private static bool ValidId(Guid? id) => id != Guid.Empty;

    public async Task<Result<PagedResult<ReviewerAssignmentListItemDto>>> ListAsync(ReviewerAssignmentListQuery query, CancellationToken token = default)
    {
        if (Authenticate(true) is { } error) return Result.Failure<PagedResult<ReviewerAssignmentListItemDto>>(error);
        if (!ValidKeyword(query.Keyword) || !ValidId(query.BatchId) || !ValidId(query.GroupId) || !ValidId(query.ReviewerId))
            return Result.Failure<PagedResult<ReviewerAssignmentListItemDto>>(ReviewerAssignmentErrors.InvalidRequest);
        return Result.Success(await reads.ListAsync(query with { Keyword = query.Keyword?.Trim(), PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) }, CanManage, token));
    }

    public async Task<Result<PagedResult<ReviewerAssignmentEligibilityItemDto>>> EligibilityAsync(ReviewerAssignmentEligibilityQuery query, CancellationToken token = default)
    {
        if (Authenticate(true) is { } error) return Result.Failure<PagedResult<ReviewerAssignmentEligibilityItemDto>>(error);
        if (!ValidKeyword(query.Keyword) || query.BatchId == Guid.Empty)
            return Result.Failure<PagedResult<ReviewerAssignmentEligibilityItemDto>>(ReviewerAssignmentErrors.InvalidRequest);
        return Result.Success(await reads.EligibilityAsync(query with { Keyword = query.Keyword?.Trim(), PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) }, CanManage, token));
    }

    public async Task<Result<PagedResult<ReviewerCandidateContractDto>>> CandidatesAsync(ReviewerAssignmentCandidateQuery query, CancellationToken token = default)
    {
        if (Authenticate(true) is { } error) return Result.Failure<PagedResult<ReviewerCandidateContractDto>>(error);
        if (!ValidKeyword(query.Keyword) || query.GroupId == Guid.Empty)
            return Result.Failure<PagedResult<ReviewerCandidateContractDto>>(ReviewerAssignmentErrors.InvalidRequest);
        var group = await thesis.GetAsync(query.GroupId, token);
        if (group is null) return Result.Failure<PagedResult<ReviewerCandidateContractDto>>(ReviewerAssignmentErrors.GroupNotFound);
        var topic = await topics.GetAsync(group.TopicId, token);
        if (topic is null || topic.AdvisorId == Guid.Empty)
            return Result.Failure<PagedResult<ReviewerCandidateContractDto>>(ReviewerAssignmentErrors.ContextInvalid);
        var page = await candidates.SearchAsync(topic.AdvisorId, query.Keyword?.Trim(), query.PageNumber, query.PageSize, token);
        return Result.Success(new PagedResult<ReviewerCandidateContractDto>(page.Items, page.TotalCount, page.PageNumber, page.PageSize));
    }

    public async Task<Result<PagedResult<ReviewerAssignmentTaskDto>>> TasksAsync(ReviewerAssignmentTaskQuery query, CancellationToken token = default)
    {
        if (Authenticate(false) is { } error) return Result.Failure<PagedResult<ReviewerAssignmentTaskDto>>(error);
        var account = await candidates.GetAsync(user.UserId!.Value, token);
        if (account is not { IsActive: true, IsLecturer: true })
            return Result.Failure<PagedResult<ReviewerAssignmentTaskDto>>(ReviewerAssignmentErrors.AccessDenied);
        if (!ValidKeyword(query.Keyword) || !ValidId(query.BatchId))
            return Result.Failure<PagedResult<ReviewerAssignmentTaskDto>>(ReviewerAssignmentErrors.InvalidRequest);
        return Result.Success(await reads.TasksAsync(user.UserId.Value,
            query with { Keyword = query.Keyword?.Trim(), PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) }, token));
    }
}
