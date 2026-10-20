using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;

namespace UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;

public sealed class GetAllTopicsHandler(ITopicQueryReader queryReader)
{
    public async Task<Result<PagedResult<TopicSummaryDto>>> HandleAsync(
        GetAllTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => query.PageSize
        };

        var sanitizedQuery = query with { PageNumber = pageNumber, PageSize = pageSize };
        var result = await queryReader.GetAllTopicsAsync(sanitizedQuery, cancellationToken);
        return Result.Success(result);
    }
}
