using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;

public sealed class UpdateTopicHandler(
    ITopicRepository topicRepository,
    ITopicUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    private const int MaxTitleViLength = 300;
    private const int MaxTitleEnLength = 350;

    public async Task<Result<TopicSummaryDto>> HandleAsync(
        UpdateTopicCommand command,
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

        if (string.IsNullOrWhiteSpace(command.TitleVi))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidTitleVi);
        }

        var trimmedTitleVi = command.TitleVi.Trim();
        if (trimmedTitleVi.Length > MaxTitleViLength)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidTitleVi);
        }

        string? trimmedTitleEn = null;
        if (!string.IsNullOrWhiteSpace(command.TitleEn))
        {
            trimmedTitleEn = command.TitleEn.Trim();
            if (trimmedTitleEn.Length > MaxTitleEnLength)
            {
                return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidTitleEn);
            }
        }

        if (command.StudentNumber is not (1 or 2))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidStudentNumber);
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

        if (!topic.CanBeModified)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidStatusTransition);
        }

        var isDuplicate = await topicRepository.ExistsByAdvisorAndTitleAsync(
            topic.BatchId,
            topic.AdvisorId,
            trimmedTitleVi,
            excludeTopicId: topic.Id,
            cancellationToken);

        if (isDuplicate)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.DuplicateTitle);
        }

        topic.UpdateDetails(
            trimmedTitleVi,
            trimmedTitleEn,
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            string.IsNullOrWhiteSpace(command.Requirements) ? null : command.Requirements.Trim(),
            command.StudentNumber.Value);

        if (command.SubmitImmediately)
        {
            topic.TrySubmit();
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
