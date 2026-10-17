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
        if (query.FromUtc is { Kind: not DateTimeKind.Utc }
            || query.ToUtc is { Kind: not DateTimeKind.Utc }
            || query.FromUtc > query.ToUtc)
        {
            return Result.Failure<PagedResult<AuditLogDto>>(
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
                return Result.Failure<PagedResult<AuditLogDto>>(
                    IdentityErrors.InvalidAuditLogFilter);
            }
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
            Outcome = outcome,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await queryReader.GetAuditLogsAsync(sanitizedQuery, cancellationToken);
        return Result.Success(result);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
