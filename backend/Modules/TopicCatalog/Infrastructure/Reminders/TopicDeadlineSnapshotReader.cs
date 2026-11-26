using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.Reminders;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Reminders;

internal sealed class TopicDeadlineSnapshotReader(TopicCatalogDbContext db)
    : ITopicDeadlineSnapshotReader
{
    public async Task<TopicBatchReminderSnapshotDto> GetBatchSnapshotAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var topics = await db.Topics
            .AsNoTracking()
            .Where(t => t.BatchId == batchId)
            .Select(t => new
            {
                t.Id,
                t.AdvisorId,
                t.TitleVi,
                t.Status
            })
            .ToListAsync(cancellationToken);

        var drafts = topics
            .Where(t => t.Status == TopicStatus.Draft)
            .Select(t => new DraftTopicReminderDto(t.Id, t.AdvisorId, t.TitleVi))
            .ToArray();

        var submitted = topics
            .Where(t => t.Status == TopicStatus.Submitted)
            .Select(t => new SubmittedTopicReminderDto(t.Id, t.AdvisorId, t.TitleVi))
            .ToArray();

        var byId = topics.ToDictionary(
            t => t.Id,
            t => new TopicReminderSummaryDto(t.Id, t.AdvisorId, t.TitleVi));

        return new TopicBatchReminderSnapshotDto(drafts, submitted, byId);
    }
}
