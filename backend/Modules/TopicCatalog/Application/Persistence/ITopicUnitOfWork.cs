namespace UGTM.Modules.TopicCatalog.Application.Persistence;

public interface ITopicUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
