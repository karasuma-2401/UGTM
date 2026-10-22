using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;

public sealed class ReviewTopicHandler(
    ITopicRepository topicRepository,
    ITopicUnitOfWork unitOfWork,
    ICurrentUser currentUser)
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
}
