namespace UGTM.Modules.DefenseCouncil.Application.Persistence;

public interface IDefenseCouncilUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
