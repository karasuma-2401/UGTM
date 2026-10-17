using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.GetTopicTrackingDetail;

public sealed class GetTopicTrackingDetailHandler(
    ITopicRepository topicRepository,
    ITopicQueryReader queryReader,
    ICurrentUser currentUser)
{
    public async Task<Result<TopicTrackingDetailDto>> HandleAsync(
        GetTopicTrackingDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.TopicId == Guid.Empty)
        {
            return Result.Failure<TopicTrackingDetailDto>(TopicErrors.InvalidInput);
        }

        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<TopicTrackingDetailDto>(TopicErrors.AccessDenied);
        }

        var topic = await topicRepository.GetByIdAsync(query.TopicId, cancellationToken);
        if (topic is null)
        {
            return Result.Failure<TopicTrackingDetailDto>(TopicErrors.TopicNotFound);
        }

        var isPrivileged = currentUser.Roles.Contains("Admin") || currentUser.Roles.Contains("CouncilMember");
        if (!isPrivileged && topic.AdvisorId != currentUser.UserId.Value)
        {
            return Result.Failure<TopicTrackingDetailDto>(TopicErrors.AccessDenied);
        }

        var detail = await queryReader.GetTopicTrackingDetailAsync(query.TopicId, cancellationToken);
        if (detail is null)
        {
            return Result.Failure<TopicTrackingDetailDto>(TopicErrors.TopicNotFound);
        }

        return Result.Success(detail);
    }
}
