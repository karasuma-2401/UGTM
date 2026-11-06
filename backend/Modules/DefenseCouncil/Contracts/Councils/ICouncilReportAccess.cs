namespace UGTM.Modules.DefenseCouncil.Contracts.Councils;

public interface ICouncilReportAccess
{
    Task<bool> IsAssignedAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
}
