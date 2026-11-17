namespace UGTM.Modules.Identity.Contracts.Users;

public interface IAdvisorNameReader
{
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IEnumerable<Guid> advisorIds,
        CancellationToken cancellationToken = default);
}
