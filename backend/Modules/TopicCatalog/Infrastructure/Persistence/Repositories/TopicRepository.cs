using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

public sealed class TopicRepository : ITopicRepository
{
    private readonly TopicCatalogDbContext? _db;
    private readonly List<Topic>? _inMemoryTopics;

    public TopicRepository(TopicCatalogDbContext db)
    {
        _db = db;
    }

    private TopicRepository(List<Topic> inMemoryTopics)
    {
        _inMemoryTopics = inMemoryTopics;
    }

    public static TopicRepository CreateInMemory(IEnumerable<Topic> topics)
        => new(topics as List<Topic> ?? topics.ToList());

    public Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _inMemoryTopics is not null
            ? Task.FromResult(_inMemoryTopics.SingleOrDefault(x => x.Id == id))
            : _db!.Topics.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(Topic topic)
    {
        if (_inMemoryTopics is not null)
        {
            _inMemoryTopics.Add(topic);
            return;
        }

        _db!.Topics.Add(topic);
    }

    public void Remove(Topic topic)
    {
        if (_inMemoryTopics is not null)
        {
            _inMemoryTopics.Remove(topic);
            return;
        }

        _db!.Topics.Remove(topic);
    }

    public Task<bool> ExistsByAdvisorAndTitleAsync(
        Guid batchId,
        Guid advisorId,
        string titleVi,
        Guid? excludeTopicId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = titleVi.Trim().ToLowerInvariant();
        var query = QueryTopics()
            .Where(t => t.BatchId == batchId && t.AdvisorId == advisorId);

        if (excludeTopicId.HasValue)
        {
            query = query.Where(t => t.Id != excludeTopicId.Value);
        }

        query = query.Where(t => t.TitleVi.ToLower() == normalizedTitle);

        return query is IAsyncEnumerable<Topic>
            ? query.AnyAsync(cancellationToken)
            : Task.FromResult(query.Any());
    }

    public async Task<IReadOnlyList<Topic>> GetByBatchAsync(
        Guid batchId,
        IReadOnlyCollection<Guid>? topicIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = _inMemoryTopics is not null
            ? _inMemoryTopics.AsQueryable()
            : _db!.Topics.AsQueryable();

        query = query.Where(t => t.BatchId == batchId);

        if (topicIds is { Count: > 0 })
        {
            query = query.Where(t => topicIds.Contains(t.Id));
        }

        query = query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id);

        return query is IAsyncEnumerable<Topic>
            ? await query.ToListAsync(cancellationToken)
            : query.ToList();
    }

    private IQueryable<Topic> QueryTopics()
        => _inMemoryTopics is not null
            ? _inMemoryTopics.AsQueryable()
            : _db!.Topics.AsNoTracking();
}
