namespace UGTM.Modules.Identity.Contracts.Users;

public sealed record CouncilUserInfoContractDto(
    Guid Id,
    string FullName,
    string Email,
    string? UserCode,
    bool IsActive,
    bool IsLecturer = true);

public interface ICouncilUserInfoReader
{
    Task<CouncilUserInfoContractDto?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CouncilUserInfoContractDto>> GetActiveLecturersAsync(
        string? keyword = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CouncilUserInfoContractDto>>([]);

    Task GrantCouncilMemberRoleAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    Task RevokeCouncilMemberRoleAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
