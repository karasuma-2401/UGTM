using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

internal sealed class TopicSummaryReader(TopicCatalogDbContext db) : ITopicSummaryReader
{
    public async Task<IReadOnlyDictionary<Guid, TopicSummaryContractDto>> GetByIdsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        var ids = topicIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, TopicSummaryContractDto>();

        return await db.Topics.AsNoTracking()
            .Where(topic => ids.Contains(topic.Id))
            .Select(topic => new TopicSummaryContractDto(
                topic.Id, topic.BatchId, topic.AdvisorId, topic.TitleVi, topic.TitleEn))
            .ToDictionaryAsync(topic => topic.TopicId, cancellationToken);
    }
}
