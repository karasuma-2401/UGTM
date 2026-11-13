using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResultDetail;

public sealed class GetAcademicResultDetailHandler(
    AcademicResultsWorkspaceLoader loader,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ValidPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "Midterm",
        "Final"
    };

    public async Task<Result<AcademicResultDetailDto>> HandleAsync(
        GetAcademicResultDetailQuery query,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<AcademicResultDetailDto>(AcademicResultsErrors.AuthenticationRequired);
        }

        var isAuthorized = currentUser.Roles.Any(r => r is "Faculty" or "AcademicAffairs" or "Admin");
        if (!isAuthorized)
        {
            return Result.Failure<AcademicResultDetailDto>(AcademicResultsErrors.Forbidden);
        }

        if (query.GroupId == Guid.Empty)
        {
            return Result.Failure<AcademicResultDetailDto>(AcademicResultsErrors.GroupNotFound);
        }

        if (string.IsNullOrWhiteSpace(query.Phase) || !ValidPhases.Contains(query.Phase.Trim()))
        {
            return Result.Failure<AcademicResultDetailDto>(AcademicResultsErrors.InvalidPhase);
        }

        var detail = await loader.LoadDetailAsync(query.GroupId, query.Phase.Trim(), ct);
        if (detail == null)
        {
            return Result.Failure<AcademicResultDetailDto>(AcademicResultsErrors.GroupNotFound);
        }

        return Result.Success(detail);
    }
}
