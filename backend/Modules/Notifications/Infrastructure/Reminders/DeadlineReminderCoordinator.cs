using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.AcademicPeriods.Contracts.Reminders;
using UGTM.Modules.DefenseCouncil.Contracts.Reminders;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.Reminders;
using UGTM.Modules.TopicCatalog.Contracts.Reminders;

namespace UGTM.Modules.Notifications.Infrastructure.Reminders;

public sealed class DeadlineReminderCoordinator(
    IAcademicDeadlineSnapshotReader academicReader,
    ITopicDeadlineSnapshotReader topicReader,
    IThesisDeadlineSnapshotReader thesisReader,
    IDefenseCouncilDeadlineSnapshotReader councilReader,
    INotificationRecipientDirectory recipientDirectory,
    INotificationIngestionService ingestionService,
    IOptions<DeadlineReminderOptions> configuredOptions,
    TimeProvider timeProvider,
    ILogger<DeadlineReminderCoordinator> logger) : IDeadlineReminderCoordinator
{
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var options = configuredOptions.Value;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var studentThresholds = NormalizeThresholds(options.StudentLeadTimeHours, [48, 24]);
        var staffThresholds = NormalizeThresholds(options.StaffLeadTimeHours, [24]);

        if (studentThresholds.Length == 0 && staffThresholds.Length == 0)
        {
            return 0;
        }

        var openBatches = await academicReader.GetOpenBatchesAsync(cancellationToken);
        if (openBatches.Count == 0)
        {
            return 0;
        }

        var totalCreated = 0;
        var staffMaxHours = staffThresholds.Length > 0 ? staffThresholds[^1] : 24;

        foreach (var batch in openBatches)
        {
            var topics = await topicReader.GetBatchSnapshotAsync(batch.BatchId, cancellationToken);
            var thesis = await thesisReader.GetBatchSnapshotAsync(batch.BatchId, cancellationToken);
            var council = await councilReader.GetBatchSnapshotAsync(batch.BatchId, cancellationToken);
            var context = new BatchReminderScanContext(batch, topics, thesis, council, nowUtc);

            var studentDirectory = await ResolveStudentDirectoryAsync(thesis, cancellationToken);
            var studentRequests = StudentDeadlineReminderBuilder.Build(context, studentDirectory, studentThresholds);

            var staffRequests = staffThresholds.Length > 0
                ? await StaffDeadlineDigestBuilder.BuildAsync(context, recipientDirectory, staffMaxHours, cancellationToken)
                : [];

            var allRequests = studentRequests.Concat(staffRequests).ToArray();
            if (allRequests.Length > 0)
            {
                var results = await ingestionService.IngestBatchAsync(allRequests, cancellationToken);
                totalCreated += results.Count(r => !r.Deduplicated);
            }
        }

        if (totalCreated > 0)
        {
            logger.LogInformation("DeadlineReminderCoordinator đã tạo {CreatedCount} thông báo nhắc hạn.", totalCreated);
        }

        return totalCreated;
    }

    private async Task<IReadOnlyDictionary<Guid, NotificationRecipientContractDto>> ResolveStudentDirectoryAsync(
        ThesisBatchReminderSnapshotDto thesis,
        CancellationToken cancellationToken)
    {
        var allStudentIds = thesis.Groups
            .SelectMany(g => g.ConfirmedStudentIds)
            .Distinct()
            .ToArray();

        if (allStudentIds.Length == 0)
        {
            return new Dictionary<Guid, NotificationRecipientContractDto>();
        }

        var resolved = await recipientDirectory.ResolveActiveRecipientsAsync(
            userIds: allStudentIds,
            cancellationToken: cancellationToken);
        return resolved.ToDictionary(r => r.UserId);
    }

    private static int[] NormalizeThresholds(int[]? configured, int[] fallback)
        => (configured ?? fallback)
            .Where(h => h > 0)
            .Distinct()
            .OrderBy(h => h)
            .ToArray();
}
