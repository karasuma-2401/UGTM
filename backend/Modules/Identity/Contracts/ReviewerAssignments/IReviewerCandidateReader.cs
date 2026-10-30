namespace UGTM.Modules.Identity.Contracts.ReviewerAssignments;

public sealed record ReviewerAccountContractDto(Guid UserId, bool IsActive, bool IsLecturer);
public sealed record ReviewerCandidateContractDto(Guid UserId, string FullName, string? UserCode);
public sealed record ReviewerCandidatePageContractDto(
    IReadOnlyList<ReviewerCandidateContractDto> Items, int TotalCount, int PageNumber, int PageSize);

public interface IReviewerCandidateReader
{
    /// <summary>Search names/codes across account states for management history; does not confer eligibility.</summary>
    Task<IReadOnlyList<Guid>> FindUserIdsAsync(string keyword, CancellationToken cancellationToken = default);
    Task<ReviewerAccountContractDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Requires the caller's shared transaction; locks account and existing Lecturer grant/role.</summary>
    Task<ReviewerAccountContractDto?> LockAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ReviewerCandidatePageContractDto> SearchAsync(Guid excludedAdvisorId, string? keyword,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
