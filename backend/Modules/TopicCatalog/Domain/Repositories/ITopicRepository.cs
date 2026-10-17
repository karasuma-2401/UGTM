using UGTM.Modules.TopicCatalog.Domain.Entities;

namespace UGTM.Modules.TopicCatalog.Domain.Repositories;

public interface ITopicRepository
{
    Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Topic topic);
}
