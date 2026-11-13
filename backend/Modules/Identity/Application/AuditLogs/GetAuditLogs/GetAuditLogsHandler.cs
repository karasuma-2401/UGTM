using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.Common;

namespace UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;

public sealed class GetAuditLogsHandler(IAuditLogQueryReader queryReader)
{
    private static readonly HashSet<string> SupportedOutcomes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            AuditOutcomes.Success,
            AuditOutcomes.Denied,
            AuditOutcomes.Failed
        };

    public async Task<Result<PagedResult<AuditLogDto>>> HandleAsync(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeQuery(query);
        if (normalized.IsFailure)
            return Result.Failure<PagedResult<AuditLogDto>>(normalized.Error);

        var result = await queryReader.GetAuditLogsAsync(normalized.Value, cancellationToken);
        return Result.Success(result);
    }

    public static Result<GetAuditLogsQuery> NormalizeQuery(GetAuditLogsQuery query)
    {
        if (query.FromUtc is { Kind: not DateTimeKind.Utc }
            || query.ToUtc is { Kind: not DateTimeKind.Utc }
            || query.FromUtc > query.ToUtc
            || query.SearchTerm?.Length > 200
            || query.RequestId?.Length > 100
            || query.TargetId?.Length > 100)
        {
            return Result.Failure<GetAuditLogsQuery>(
                IdentityErrors.InvalidAuditLogFilter);
        }

        string? outcome = null;
        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            var requestedOutcome = query.Outcome.Trim();
            outcome = SupportedOutcomes.FirstOrDefault(x =>
                string.Equals(x, requestedOutcome, StringComparison.OrdinalIgnoreCase));

            if (outcome is null)
            {
                return Result.Failure<GetAuditLogsQuery>(
                    IdentityErrors.InvalidAuditLogFilter);
            }
        }

        var sortBy = Normalize(query.SortBy)?.ToLowerInvariant() ?? "timestamp";
        if (sortBy is not ("timestamp" or "actor" or "action" or "outcome"))
        {
            return Result.Failure<GetAuditLogsQuery>(IdentityErrors.InvalidSort);
        }

        var sortOrder = Normalize(query.SortOrder)?.ToLowerInvariant() ?? "desc";
        if (sortOrder is not ("asc" or "desc"))
        {
            return Result.Failure<GetAuditLogsQuery>(IdentityErrors.InvalidSort);
        }

        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        var sanitizedQuery = query with
        {
            Action = Normalize(query.Action),
            TargetType = Normalize(query.TargetType),
            SearchTerm = Normalize(query.SearchTerm),
            RequestId = Normalize(query.RequestId),
            TargetId = Normalize(query.TargetId),
            Outcome = outcome,
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = sortBy,
            SortOrder = sortOrder
        };

        return Result.Success(sanitizedQuery);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
