namespace UGTM.Modules.Identity.Contracts.Users;

public sealed record CouncilUserInfoContractDto(
    Guid Id,
    string FullName,
    string Email,
    string? UserCode,
    bool IsActive);

public interface ICouncilUserInfoReader
{
    Task<CouncilUserInfoContractDto?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);
}
