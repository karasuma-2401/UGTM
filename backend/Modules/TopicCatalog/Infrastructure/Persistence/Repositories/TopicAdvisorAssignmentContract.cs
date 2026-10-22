using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

internal sealed class TopicAdvisorAssignmentContract(TopicCatalogDbContext context)
    : ITopicAdvisorAssignmentContract
{
    public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
        => context.Topics
            .AsNoTracking()
            .Where(topic => topic.Id == topicId)
            .Select(topic => new TopicAdvisorAssignmentContractDto(topic.Id, topic.AdvisorId))
            .SingleOrDefaultAsync(cancellationToken);
}
