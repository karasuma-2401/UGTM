using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Notifications;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;

public sealed class ReviewTopicHandler(
    ITopicRepository topicRepository,
    ITopicUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ITopicCatalogNotificationOutbox? notificationOutbox = null,
    IBatchSummaryReader? batchSummaryReader = null,
    TimeProvider? timeProvider = null)
{
    private const int MaxReviewNoteLength = 2000;

    public async Task<Result<TopicSummaryDto>> HandleAsync(
        ReviewTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.AccessDenied);
        }

        var isFacultyOrAdmin = HasRole(currentUser, "Faculty") || HasRole(currentUser, "Admin");
        if (!isFacultyOrAdmin)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.AccessDenied);
        }

        if (command.TopicId == Guid.Empty)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidInput);
        }

        if (string.IsNullOrWhiteSpace(command.Decision))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidReviewDecision);
        }

        var trimmedDecision = command.Decision.Trim();
        if (int.TryParse(trimmedDecision, out _)
            || !Enum.TryParse<TopicStatus>(trimmedDecision, ignoreCase: true, out var decisionEnum)
            || decisionEnum is not (TopicStatus.Approved or TopicStatus.NeedRevision or TopicStatus.Rejected))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidReviewDecision);
        }

        string? trimmedNote = null;
        if (!string.IsNullOrWhiteSpace(command.ReviewNote))
        {
            trimmedNote = command.ReviewNote.Trim();
            if (trimmedNote.Length > MaxReviewNoteLength)
            {
                return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidReviewNote);
            }
        }

        if (decisionEnum is (TopicStatus.NeedRevision or TopicStatus.Rejected) && trimmedNote is null)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.ReviewNoteRequired);
        }

        var topic = await topicRepository.GetByIdAsync(command.TopicId, cancellationToken);
        if (topic is null)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.TopicNotFound);
        }

        if (!topic.TryReview(decisionEnum, trimmedNote))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidStatusTransition);
        }

        if (notificationOutbox is not null)
        {
            var occurredAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            var shortTitle = Shorten(topic.TitleVi, 70);

            if (decisionEnum == TopicStatus.NeedRevision)
            {
                string? batchName = null;
                if (batchSummaryReader is not null)
                {
                    var batches = await batchSummaryReader.GetByIdsAsync([topic.BatchId], cancellationToken);
                    if (batches.TryGetValue(topic.BatchId, out var summary))
                    {
                        batchName = summary.BatchName;
                    }
                }

                var metadata = new List<NotificationEmailMetaItem>
                {
                    new("Tên đề tài", topic.TitleVi.Trim())
                };
                if (!string.IsNullOrWhiteSpace(batchName))
                {
                    metadata.Add(new NotificationEmailMetaItem("Đợt KLTN", batchName));
                }
                metadata.Add(new NotificationEmailMetaItem("Kết quả thẩm định", "Yêu cầu chỉnh sửa"));
                metadata.Add(new NotificationEmailMetaItem("Góp ý từ Khoa", trimmedNote!));

                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "TOPIC_REVISION_REQUESTED",
                        Title: "Yêu cầu chỉnh sửa đề tài KLTN",
                        Content: $"Khoa yêu cầu chỉnh sửa đề tài \"{shortTitle}\". Vui lòng xem góp ý và cập nhật lại sớm.",
                        RecipientUserIds: [topic.AdvisorId],
                        ResourceType: "Topic",
                        ResourceId: topic.Id,
                        AcademicBatchId: topic.BatchId,
                        ActionPath: "/topics/propose",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Yêu cầu chỉnh sửa đề tài: {shortTitle}",
                        EmailCategoryLabel: "Thẩm định đề tài",
                        EmailCtaLabel: "Mở trang chỉnh sửa đề tài",
                        EmailNotice: "Vui lòng cập nhật nội dung và gửi duyệt lại theo kế hoạch đợt KLTN.",
                        EmailMetadata: metadata),
                    occurredAtUtc);
            }
            else if (decisionEnum == TopicStatus.Approved)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "TOPIC_APPROVED",
                        Title: "Đề tài KLTN đã được duyệt",
                        Content: $"Đề tài \"{shortTitle}\" đã được Khoa phê duyệt và chờ công bố đăng ký.",
                        RecipientUserIds: [topic.AdvisorId],
                        ResourceType: "Topic",
                        ResourceId: topic.Id,
                        AcademicBatchId: topic.BatchId,
                        ActionPath: "/topics/advising",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    occurredAtUtc);
            }
            else if (decisionEnum == TopicStatus.Rejected)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "TOPIC_REJECTED",
                        Title: "Đề tài KLTN không được duyệt",
                        Content: $"Đề tài \"{Shorten(topic.TitleVi, 60)}\" bị từ chối thẩm định. Lý do: {Shorten(trimmedNote!, 80)}",
                        RecipientUserIds: [topic.AdvisorId],
                        ResourceType: "Topic",
                        ResourceId: topic.Id,
                        AcademicBatchId: topic.BatchId,
                        ActionPath: "/topics/advising",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    occurredAtUtc);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TopicSummaryDto(
            topic.Id,
            topic.BatchId,
            topic.AdvisorId,
            topic.TitleVi,
            topic.TitleEn,
            topic.Description,
            topic.Requirements,
            topic.Status.ToString(),
            topic.StudentNumber,
            topic.CreatedAt,
            topic.IsPublished,
            topic.ReviewNote));
    }

    private static bool HasRole(ICurrentUser user, string role)
        => user.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    private static string Shorten(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 1)].TrimEnd()}…";
    }
}
