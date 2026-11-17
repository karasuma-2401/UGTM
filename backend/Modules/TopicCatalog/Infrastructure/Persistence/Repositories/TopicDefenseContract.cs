using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

public sealed class TopicDefenseContract(TopicCatalogDbContext context) : ITopicDefenseContract
{
    public async Task<TopicDefenseDetailsContractDto?> GetTopicDetailsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        return await context.Topics
            .AsNoTracking()
            .Where(topic => topic.Id == topicId)
            .Select(topic => new TopicDefenseDetailsContractDto(
                topic.Id,
                topic.BatchId,
                topic.AdvisorId,
                topic.TitleVi,
                topic.TitleEn))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TopicDefenseDetailsContractDto>> GetTopicsDetailsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        var idList = topicIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, TopicDefenseDetailsContractDto>();
        }

        var topics = await context.Topics
            .AsNoTracking()
            .Where(topic => idList.Contains(topic.Id))
            .Select(topic => new TopicDefenseDetailsContractDto(
                topic.Id,
                topic.BatchId,
                topic.AdvisorId,
                topic.TitleVi,
                topic.TitleEn))
            .ToListAsync(cancellationToken);

        return topics.ToDictionary(t => t.TopicId);
    }
}
