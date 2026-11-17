using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;

namespace UGTM.Modules.TopicCatalog.Application.Topics;

public interface ITopicQueryReader
{
    Task<PagedResult<TopicSummaryDto>> GetAllTopicsAsync(
        GetAllTopicsQuery query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TopicTrackingSummaryDto>> GetMyTopicsAsync(
        GetMyTopicsQuery query,
        Guid? advisorId,
        bool isPrivileged,
        CancellationToken cancellationToken = default);

    Task<TopicTrackingDetailDto?> GetTopicTrackingDetailAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);
}
