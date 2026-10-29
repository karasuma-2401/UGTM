using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

internal sealed class TopicSearchReader(TopicCatalogDbContext db) : ITopicSearchReader
{
    public async Task<IReadOnlyList<Guid>> FindIdsAsync(
        Guid? batchId, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = db.Topics.AsNoTracking();
        if (batchId.HasValue) query = query.Where(topic => topic.BatchId == batchId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLowerInvariant();
            query = query.Where(topic => topic.TitleVi.ToLower().Contains(term)
                || (topic.TitleEn != null && topic.TitleEn.ToLower().Contains(term)));
        }
        return await query.Select(topic => topic.Id).ToListAsync(cancellationToken);
    }
}
