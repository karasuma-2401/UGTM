using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;

namespace UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;

public sealed class GetAllTopicsHandler(ITopicQueryReader queryReader)
{
    private static readonly HashSet<string> ValidAvailability = new(StringComparer.OrdinalIgnoreCase)
    {
        "all",
        "available",
        "full"
    };

    public async Task<Result<PagedResult<TopicSummaryDto>>> HandleAsync(
        GetAllTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        string? availability = null;
        if (!string.IsNullOrWhiteSpace(query.Availability))
        {
            availability = query.Availability.Trim().ToLowerInvariant();
            if (!ValidAvailability.Contains(availability))
            {
                return Result.Failure<PagedResult<TopicSummaryDto>>(TopicErrors.InvalidAvailabilityFilter);
            }
            if (availability == "all")
            {
                availability = null;
            }
        }

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "createdat" : query.SortBy.Trim().ToLowerInvariant();
        var sortOrder = string.IsNullOrWhiteSpace(query.SortOrder) ? "desc" : query.SortOrder.Trim().ToLowerInvariant();
        if (sortBy is not ("title" or "studentnumber" or "availableslots" or "createdat") || sortOrder is not ("asc" or "desc"))
        {
            return Result.Failure<PagedResult<TopicSummaryDto>>(TopicErrors.InvalidCatalogSort);
        }

        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => query.PageSize
        };

        var sanitizedQuery = query with
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Availability = availability,
            SortBy = sortBy,
            SortOrder = sortOrder
        };
        var result = await queryReader.GetAllTopicsAsync(sanitizedQuery, cancellationToken);
        return Result.Success(result);
    }
}
