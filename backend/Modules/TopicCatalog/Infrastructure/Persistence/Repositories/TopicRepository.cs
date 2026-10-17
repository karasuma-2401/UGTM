using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

public sealed class TopicRepository(TopicCatalogDbContext db) : ITopicRepository
{
    public Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Topics.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(Topic topic) => db.Topics.Add(topic);
}
