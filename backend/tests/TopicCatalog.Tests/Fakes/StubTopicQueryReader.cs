using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.TopicCatalog.Application.Topics;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;

namespace UGTM.Modules.TopicCatalog.Tests.Fakes;

public sealed class StubTopicQueryReader : ITopicQueryReader
{
    public GetAllTopicsQuery? LastGetAllQuery { get; private set; }
    public GetMyTopicsQuery? LastGetMyQuery { get; private set; }
    public Guid? LastEffectiveAdvisorId { get; private set; }
    public bool? LastIsPrivileged { get; private set; }
    public Guid? LastDetailTopicId { get; private set; }

    public PagedResult<TopicSummaryDto> GetAllTopicsResponse { get; set; } =
        new([], 0, 1, 10);

    public PagedResult<TopicTrackingSummaryDto> GetMyTopicsResponse { get; set; } =
        new([], 0, 1, 10);

    public TopicTrackingDetailDto? GetTopicTrackingDetailResponse { get; set; }

    public Task<PagedResult<TopicSummaryDto>> GetAllTopicsAsync(
        GetAllTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        LastGetAllQuery = query;
        return Task.FromResult(GetAllTopicsResponse);
    }

    public Task<PagedResult<TopicTrackingSummaryDto>> GetMyTopicsAsync(
        GetMyTopicsQuery query,
        Guid? advisorId,
        bool isPrivileged,
        CancellationToken cancellationToken = default)
    {
        LastGetMyQuery = query;
        LastEffectiveAdvisorId = advisorId;
        LastIsPrivileged = isPrivileged;
        return Task.FromResult(GetMyTopicsResponse);
    }

    public Task<TopicTrackingDetailDto?> GetTopicTrackingDetailAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        LastDetailTopicId = topicId;
        return Task.FromResult(GetTopicTrackingDetailResponse);
    }
}
