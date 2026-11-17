namespace UGTM.Modules.Identity.Contracts.FinalSubmissions;

public sealed record FinalSubmissionAuthorityDto(Guid UserId, bool IsActive,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public interface IFinalSubmissionAuthorityReader
{
    Task<FinalSubmissionAuthorityDto?> GetAsync(Guid userId, CancellationToken token = default);
    Task<FinalSubmissionAuthorityDto?> LockAsync(Guid userId, CancellationToken token = default);
}
