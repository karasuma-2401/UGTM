using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Tests.Fakes;

public sealed class FakeTopicRepository : ITopicRepository
{
    private readonly Dictionary<Guid, Topic> _topics = [];

    public void Add(Topic topic)
    {
        _topics[topic.Id] = topic;
    }

    public Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _topics.TryGetValue(id, out var topic);
        return Task.FromResult(topic);
    }
}
