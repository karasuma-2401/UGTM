using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;

public sealed class SubmitTopicHandler(
    ITopicRepository topicRepository,
    ITopicUnitOfWork unitOfWork,
    ICurrentUser currentUser)
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
