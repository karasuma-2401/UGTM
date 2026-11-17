using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.PublishTopics;

public sealed class PublishTopicsHandler(
    ITopicRepository topicRepository,
    ITopicUnitOfWork unitOfWork,
    IKltnBatchReader batchReader,
    ICurrentUser currentUser)
{
    public async Task<Result<PublishTopicsResultDto>> HandleAsync(
        PublishTopicsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<PublishTopicsResultDto>(TopicErrors.AccessDenied);
        }

        var isFacultyOrAdmin = HasRole(currentUser, "Faculty") || HasRole(currentUser, "Admin");
        if (!isFacultyOrAdmin)
        {
            return Result.Failure<PublishTopicsResultDto>(TopicErrors.AccessDenied);
        }

        if (command.BatchId == Guid.Empty)
        {
            return Result.Failure<PublishTopicsResultDto>(TopicErrors.InvalidBatchId);
        }

        if (command.TopicIds is not null && command.TopicIds.Any(id => id == Guid.Empty))
        {
            return Result.Failure<PublishTopicsResultDto>(TopicErrors.InvalidInput);
        }

        var batchExists = await batchReader.ExistsAsync(command.BatchId, cancellationToken);
        if (!batchExists)
        {
            return Result.Failure<PublishTopicsResultDto>(TopicErrors.BatchNotFound);
        }

        var requestedIds = command.TopicIds?
            .Distinct()
            .ToList();

        var topics = await topicRepository.GetByBatchAsync(command.BatchId, requestedIds, cancellationToken);
        var publishedIds = new List<Guid>();

        if (requestedIds is { Count: > 0 })
        {
            if (topics.Count != requestedIds.Count)
            {
                return Result.Failure<PublishTopicsResultDto>(TopicErrors.TopicNotFound);
            }

            if (topics.Any(t => t.Status != TopicStatus.Approved))
            {
                return Result.Failure<PublishTopicsResultDto>(TopicErrors.InvalidStatusTransition);
            }

            foreach (var topic in topics)
            {
                topic.TryPublish();
                publishedIds.Add(topic.Id);
            }
        }
        else
        {
            var approvedTopics = topics
                .Where(t => t.Status == TopicStatus.Approved)
                .ToList();

            if (approvedTopics.Count == 0)
            {
                return Result.Failure<PublishTopicsResultDto>(TopicErrors.NoApprovedTopicsToPublish);
            }

            foreach (var topic in approvedTopics)
            {
                topic.TryPublish();
                publishedIds.Add(topic.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new PublishTopicsResultDto(
            command.BatchId,
            publishedIds.Count,
            publishedIds));
    }

    private static bool HasRole(ICurrentUser user, string role)
        => user.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
