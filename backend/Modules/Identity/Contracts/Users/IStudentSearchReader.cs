namespace UGTM.Modules.Identity.Contracts.Users;

public interface IStudentSearchReader
{
    Task<IReadOnlyList<Guid>> FindIdsByNameOrCodeAsync(
        string keyword,
        CancellationToken cancellationToken = default);
}
