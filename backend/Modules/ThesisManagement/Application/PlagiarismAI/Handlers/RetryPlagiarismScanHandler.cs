using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed record RetryPlagiarismScanCommand(Guid FinalReportId, int? VersionNo = null);

public sealed class RetryPlagiarismScanHandler(
    IFinalReportRepository finalReportRepository,
    IFinalReportPlagiarismScanRepository scanRepository,
    IStudentGroupRepository studentGroupRepository,
    ICurrentUser currentUser,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    ExecutePlagiarismScanHandler executeScanHandler,
    GetPlagiarismReportHandler getReportHandler)
{
    public async Task<Result<PlagiarismReportResultDto>> HandleAsync(
        RetryPlagiarismScanCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FinalReportId == Guid.Empty)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        var report = await finalReportRepository.GetByIdAsync(command.FinalReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        var group = await studentGroupRepository.GetByIdAsync(report.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        var isAdmin = ThesisAuthorization.IsAdministrator(currentUser);
        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
        var isAssignedAdvisor = currentUser.UserId.HasValue && assignment?.AdvisorId == currentUser.UserId.Value;

        if (!isAdmin && !isAssignedAdvisor)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.RescanNotAllowed);
        }

        var scan = command.VersionNo.HasValue
            ? await scanRepository.GetByReportAndVersionAsync(command.FinalReportId, command.VersionNo.Value, cancellationToken)
            : await scanRepository.GetLatestByReportIdAsync(command.FinalReportId, cancellationToken);

        if (scan is null)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        // Reset state for retry
        var nowUtc = DateTime.UtcNow;
        scan.ResetForRetry(nowUtc);

        // Execute scan dispatch immediately
        await executeScanHandler.ExecuteAsync(scan, cancellationToken);

        // Return latest report view
        return await getReportHandler.HandleAsync(
            new GetPlagiarismReportQuery(command.FinalReportId, command.VersionNo),
            cancellationToken);
    }
}

