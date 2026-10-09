namespace UGTM.Modules.ThesisManagement.Application.Persistence;

public interface IThesisUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
