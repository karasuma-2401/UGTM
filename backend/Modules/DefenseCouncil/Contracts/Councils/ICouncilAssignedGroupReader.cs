namespace UGTM.Modules.DefenseCouncil.Contracts.Councils;

public interface ICouncilAssignedGroupReader
{
    Task<IReadOnlyList<Guid>> GetAssignedGroupIdsAsync(Guid userId, CancellationToken cancellationToken = default);
}
