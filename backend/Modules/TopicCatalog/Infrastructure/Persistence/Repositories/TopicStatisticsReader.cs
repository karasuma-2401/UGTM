using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Domain.Enums;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

public sealed class TopicStatisticsReader(TopicCatalogDbContext db) : ITopicStatisticsReader
{
    public async Task<TopicStatisticsDto> GetAsync(Guid advisorId, Guid? batchId, CancellationToken cancellationToken)
    {
        var query = db.Topics.AsNoTracking().Where(topic => topic.AdvisorId == advisorId);
        if (batchId.HasValue) query = query.Where(topic => topic.BatchId == batchId.Value);
        return await query.GroupBy(_ => 1).Select(topics => new TopicStatisticsDto(topics.Count(),
            topics.Count(topic => topic.Status == TopicStatus.Submitted),
            topics.Count(topic => topic.Status == TopicStatus.NeedRevision)))
            .SingleOrDefaultAsync(cancellationToken) ?? new(0, 0, 0);
    }
}
