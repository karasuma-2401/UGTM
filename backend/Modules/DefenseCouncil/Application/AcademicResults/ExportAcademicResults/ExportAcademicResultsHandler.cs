using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;

public sealed class ExportAcademicResultsHandler(
    AcademicResultsWorkspaceLoader loader,
    IAcademicResultsExportFileGenerator fileGenerator,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ValidPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "Midterm",
        "Final"
    };

    private static readonly HashSet<string> ValidFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "xlsx",
        "pdf"
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

    public async Task<Result<AcademicResultExportFileDto>> HandleAsync(
        ExportAcademicResultsQuery query,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<AcademicResultExportFileDto>(AcademicResultsErrors.AuthenticationRequired);
        }

        var isAuthorized = currentUser.Roles.Any(r => r is "Faculty" or "AcademicAffairs" or "Admin");
        if (!isAuthorized)
        {
            return Result.Failure<AcademicResultExportFileDto>(AcademicResultsErrors.Forbidden);
        }

        if (query.BatchId == Guid.Empty)
        {
            return Result.Failure<AcademicResultExportFileDto>(AcademicResultsErrors.InvalidBatchId);
        }

        var rawFormat = query.Format?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(rawFormat) || !ValidFormats.Contains(rawFormat))
        {
            return Result.Failure<AcademicResultExportFileDto>(AcademicResultsErrors.InvalidFormat);
        }

        var phase = string.IsNullOrWhiteSpace(query.Phase) ? "Final" : query.Phase.Trim();
        if (!ValidPhases.Contains(phase))
        {
            return Result.Failure<AcademicResultExportFileDto>(AcademicResultsErrors.InvalidPhase);
        }

        var normalizedPhase = AcademicResultsWorkspaceLoader.NormalizePhase(phase);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var trimmedStatus = query.Status.Trim();
            var isValidStatus = normalizedPhase == "Midterm"
                ? ValidMidtermStatuses.Contains(trimmedStatus)
                : ValidFinalStatuses.Contains(trimmedStatus);

            if (!isValidStatus)
            {
                return Result.Failure<AcademicResultExportFileDto>(AcademicResultsErrors.InvalidStatus);
            }
        }

        var rows = await loader.LoadAllAsync(
            query.BatchId,
            normalizedPhase,
            query.Keyword,
            query.Status,
            ct);

        var flatRows = rows.SelectMany(r => r.ExportRows).ToList();

        var fileDto = await fileGenerator.GenerateAsync(
            flatRows,
            rawFormat,
            normalizedPhase,
            ct);

        return Result.Success(fileDto);
    }
}
