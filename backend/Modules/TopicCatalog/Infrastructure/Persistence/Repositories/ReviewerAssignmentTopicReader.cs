using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentTopicReader(TopicCatalogDbContext context) : IReviewerAssignmentTopicReader
{
    public async Task<IReadOnlyList<ReviewerAssignmentTopicContractDto>> GetManyAsync(IReadOnlyCollection<Guid> topicIds,
        CancellationToken cancellationToken = default)
        => await context.Topics.AsNoTracking().Where(topic => topicIds.Contains(topic.Id))
            .Select(topic => new ReviewerAssignmentTopicContractDto(topic.Id, topic.BatchId, topic.AdvisorId, topic.TitleVi, topic.TitleEn))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindIdsAsync(Guid? batchId, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = context.Topics.AsNoTracking().AsQueryable();
        if (batchId.HasValue) query = query.Where(topic => topic.BatchId == batchId.Value);
        keyword = keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            if (keyword.Length > 200) throw new ArgumentOutOfRangeException(nameof(keyword));
            var pattern = "%" + keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(topic => EF.Functions.ILike(topic.TitleVi, pattern, "\\")
                || (topic.TitleEn != null && EF.Functions.ILike(topic.TitleEn, pattern, "\\")));
        }
        return await query.Select(topic => topic.Id).ToListAsync(cancellationToken);
    }
    public async Task<ReviewerAssignmentTopicContractDto?> GetAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        var topic = await context.Topics.AsNoTracking().SingleOrDefaultAsync(value => value.Id == topicId, cancellationToken);
        return topic is null ? null : new(topic.Id, topic.BatchId, topic.AdvisorId, topic.TitleVi, topic.TitleEn);
    }

    public async Task<ReviewerAssignmentTopicContractDto?> LockAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        var topic = await context.Topics.FromSqlInterpolated($"""
            SELECT t.* FROM topics.topic t WHERE t.id = {topicId} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return topic is null ? null : new(topic.Id, topic.BatchId, topic.AdvisorId, topic.TitleVi, topic.TitleEn);
    }
}
