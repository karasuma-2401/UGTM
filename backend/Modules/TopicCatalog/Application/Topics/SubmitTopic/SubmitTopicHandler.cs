using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Notifications;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;

public sealed class SubmitTopicHandler(
    ITopicRepository topicRepository,
    ITopicUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ITopicCatalogNotificationOutbox? notificationOutbox = null,
    TimeProvider? timeProvider = null)
{
    public async Task<Result<TopicSummaryDto>> HandleAsync(
        SubmitTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.AccessDenied);
        }

        var isAdmin = HasRole(currentUser, "Admin");
        var isLecturer = HasRole(currentUser, "Lecturer");

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.AccessDenied);
        }

        if (command.TopicId == Guid.Empty)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidInput);
        }

        var topic = await topicRepository.GetByIdAsync(command.TopicId, cancellationToken);
        if (topic is null)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.TopicNotFound);
        }

        if (!isAdmin && topic.AdvisorId != currentUser.UserId.Value)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.AccessDenied);
        }

        if (string.IsNullOrWhiteSpace(topic.TitleVi) || topic.TitleVi.Trim().Length > 300)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidTitleVi);
        }

        if (topic.StudentNumber is not (1 or 2))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidStudentNumber);
        }

        if (!topic.TrySubmit())
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidStatusTransition);
        }

        var occurredAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        notificationOutbox?.Enqueue(
            new NotificationOutboxEntry(
                NotificationType: "TOPIC_SUBMITTED",
                Title: "Đề tài KLTN mới chờ thẩm định",
                Content: $"Đề tài \"{Shorten(topic.TitleVi, 75)}\" vừa được gửi duyệt. Khoa vui lòng thẩm định theo kế hoạch.",
                RecipientRoles: ["Faculty"],
                ExcludeUserIds: currentUser.UserId.HasValue ? [currentUser.UserId.Value] : null,
                ResourceType: "Topic",
                ResourceId: topic.Id,
                AcademicBatchId: topic.BatchId,
                ActionPath: "/topics/review",
                Priority: "Normal",
                EnqueuePush: true,
                EnqueueEmail: false),
            occurredAtUtc);

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
