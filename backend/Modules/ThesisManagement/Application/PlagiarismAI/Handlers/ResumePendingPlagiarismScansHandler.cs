using Microsoft.Extensions.Logging;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed record ResumePendingPlagiarismScansCommand(int Limit = 50);

public sealed record ResumePendingPlagiarismScansResult(
    int TotalResumed,
    int TotalSucceeded,
    IReadOnlyList<Guid> ResumedScanIds);

public sealed class ResumePendingPlagiarismScansHandler(
    IFinalReportPlagiarismScanRepository scanRepository,
    ICurrentUser currentUser,
    ExecutePlagiarismScanHandler executeScanHandler,
    ILogger<ResumePendingPlagiarismScansHandler> logger)
{
    public async Task<Result<ResumePendingPlagiarismScansResult>> HandleAsync(
        ResumePendingPlagiarismScansCommand command,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ThesisAuthorization.IsAdministrator(currentUser);
        bool HasRole(string role) => currentUser.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        var isFaculty = HasRole("Faculty") && ThesisAuthorization.CanReviewGates(currentUser);

        if (!isAdmin && !isFaculty)
        {
            return Result.Failure<ResumePendingPlagiarismScansResult>(
                Error.Forbidden("PlagiarismScan.ResumeNotAllowed", "Chỉ Quản trị viên hoặc Trưởng khoa mới có quyền kích hoạt quét lại hàng loạt."));
        }

        var limit = Math.Clamp(command.Limit, 1, 100);
        var pendingScans = await scanRepository.GetResumableScansAsync(limit, cancellationToken);

        var resumedIds = new List<Guid>();
        var successCount = 0;
        var nowUtc = DateTime.UtcNow;

        foreach (var scan in pendingScans)
        {
            logger.LogInformation("Resuming scan {ScanId} for FinalReport {ReportId} v{Version}", scan.Id, scan.FinalReportId, scan.VersionNo);
            scan.ResetForRetry(nowUtc);
            var success = await executeScanHandler.ExecuteAsync(scan, cancellationToken);
            resumedIds.Add(scan.Id);
            if (success) successCount++;
        }

        return Result.Success(new ResumePendingPlagiarismScansResult(
            TotalResumed: resumedIds.Count,
            TotalSucceeded: successCount,
            ResumedScanIds: resumedIds));
    }
}

