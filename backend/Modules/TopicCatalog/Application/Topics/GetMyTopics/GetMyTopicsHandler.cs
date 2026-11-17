using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Enums;

namespace UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;

public sealed class GetMyTopicsHandler(
    ITopicQueryReader queryReader,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ValidRegistrationStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "All",
        "Empty",
        "Registered",
        "Full"
    };

    public async Task<Result<PagedResult<TopicTrackingSummaryDto>>> HandleAsync(
        GetMyTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<PagedResult<TopicTrackingSummaryDto>>(TopicErrors.AccessDenied);
        }

        var isPrivileged = HasRole(currentUser, "Admin")
            || HasRole(currentUser, "Faculty")
            || HasRole(currentUser, "CouncilMember");
        Guid? effectiveAdvisorId;

        if (!isPrivileged)
        {
            if (!HasRole(currentUser, "Lecturer"))
            {
                return Result.Failure<PagedResult<TopicTrackingSummaryDto>>(TopicErrors.AccessDenied);
            }

            effectiveAdvisorId = currentUser.UserId.Value;
        }
        else
        {
            effectiveAdvisorId = query.AdvisorId;
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var trimmedStatus = query.Status.Trim();
            if (int.TryParse(trimmedStatus, out _)
                || !Enum.TryParse<TopicStatus>(trimmedStatus, ignoreCase: true, out var statusEnum)
                || !Enum.IsDefined(statusEnum))
            {
                return Result.Failure<PagedResult<TopicTrackingSummaryDto>>(TopicErrors.InvalidTopicStatusFilter);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.RegistrationStatus))
        {
            var trimmedRegStatus = query.RegistrationStatus.Trim();
            if (!ValidRegistrationStatuses.Contains(trimmedRegStatus))
            {
                return Result.Failure<PagedResult<TopicTrackingSummaryDto>>(TopicErrors.InvalidRegistrationStatusFilter);
            }
        }

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "createdat" : query.SortBy.Trim().ToLowerInvariant();
        var sortOrder = string.IsNullOrWhiteSpace(query.SortOrder) ? "desc" : query.SortOrder.Trim().ToLowerInvariant();
        if (sortBy is not ("title" or "studentnumber" or "createdat") || sortOrder is not ("asc" or "desc"))
        {
            return Result.Failure<PagedResult<TopicTrackingSummaryDto>>(TopicErrors.InvalidSort);
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
            SortBy = sortBy,
            SortOrder = sortOrder,
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            RegistrationStatus = string.IsNullOrWhiteSpace(query.RegistrationStatus) ? null : query.RegistrationStatus.Trim()
        };
        var result = await queryReader.GetMyTopicsAsync(
            sanitizedQuery,
            effectiveAdvisorId,
            isPrivileged,
            cancellationToken);

        return Result.Success(result);
    }

    private static bool HasRole(ICurrentUser user, string role)
        => user.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
