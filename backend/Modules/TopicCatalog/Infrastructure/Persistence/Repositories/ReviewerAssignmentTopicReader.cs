using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentTopicReader(TopicCatalogDbContext context) : IReviewerAssignmentTopicReader
{
    public async Task<ReviewerAssignmentTopicContractDto?> GetAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        var topic = await context.Topics.AsNoTracking().SingleOrDefaultAsync(value => value.Id == topicId, cancellationToken);
        return topic is null ? null : new(topic.Id, topic.BatchId, topic.AdvisorId);
    }

    public async Task<ReviewerAssignmentTopicContractDto?> LockAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        var topic = await context.Topics.FromSqlInterpolated($"""
            SELECT t.* FROM topics.topic t WHERE t.id = {topicId} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return topic is null ? null : new(topic.Id, topic.BatchId, topic.AdvisorId);
    }
}
