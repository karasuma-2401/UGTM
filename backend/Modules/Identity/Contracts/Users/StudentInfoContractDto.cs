namespace UGTM.Modules.Identity.Contracts.Users;

public sealed record StudentInfoContractDto(
    Guid Id,
    string Email,
    string FullName,
    string? UserCode,
    string? PhoneNumber);

public interface IStudentInfoReader
{
    Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(
        IEnumerable<Guid> studentIds,
        CancellationToken cancellationToken = default);
}
