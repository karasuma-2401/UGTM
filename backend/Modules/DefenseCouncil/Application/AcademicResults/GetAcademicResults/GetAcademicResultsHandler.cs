using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResults;

public sealed class GetAcademicResultsHandler(
    AcademicResultsWorkspaceLoader loader,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ValidPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "Midterm",
        "Final"
    };

    private static readonly HashSet<string> ValidMidtermStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL",
        "CONTINUED",
        "STOPPED",
        "PENDING_MIDTERM"
    };

    private static readonly HashSet<string> ValidFinalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL",
        "PUBLISHED",
        "READY_TO_PUBLISH",
        "MISSING_SCORE",
        "MISSING_ARCHIVE"
    };

    public async Task<Result<PagedResult<AcademicResultListItemDto>>> HandleAsync(
        GetAcademicResultsQuery query,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<PagedResult<AcademicResultListItemDto>>(AcademicResultsErrors.AuthenticationRequired);
        }

        var isAuthorized = currentUser.Roles.Any(r => r is "Faculty" or "AcademicAffairs" or "Admin");
        if (!isAuthorized)
        {
            return Result.Failure<PagedResult<AcademicResultListItemDto>>(AcademicResultsErrors.Forbidden);
        }

        if (query.BatchId == Guid.Empty)
        {
            return Result.Failure<PagedResult<AcademicResultListItemDto>>(AcademicResultsErrors.InvalidBatchId);
        }

        if (string.IsNullOrWhiteSpace(query.Phase) || !ValidPhases.Contains(query.Phase.Trim()))
        {
            return Result.Failure<PagedResult<AcademicResultListItemDto>>(AcademicResultsErrors.InvalidPhase);
        }

        var normalizedPhase = AcademicResultsWorkspaceLoader.NormalizePhase(query.Phase.Trim());

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var trimmedStatus = query.Status.Trim();
            var isValidStatus = normalizedPhase == "Midterm"
                ? ValidMidtermStatuses.Contains(trimmedStatus)
                : ValidFinalStatuses.Contains(trimmedStatus);

            if (!isValidStatus)
            {
                return Result.Failure<PagedResult<AcademicResultListItemDto>>(AcademicResultsErrors.InvalidStatus);
            }
        }

        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var rows = await loader.LoadAllAsync(
            query.BatchId,
            normalizedPhase,
            query.Keyword,
            query.Status,
            ct);

        var totalCount = rows.Count;
        var pagedItems = rows
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.ListItem)
            .ToList();

        var pagedResult = new PagedResult<AcademicResultListItemDto>(pagedItems, totalCount, pageIndex, pageSize);
        return Result.Success(pagedResult);
    }
}
