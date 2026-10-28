using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportHistory;

public sealed class GetFinalReportHistoryHandler(
    IStudentGroupRepository groupRepository,
    IFinalReportRepository finalReportRepository,
    ISubmissionFileRepository submissionFileRepository,
    FinalReportReadAccessChecker accessChecker,
    ICurrentUser currentUser)
{
    public async Task<Result<FinalReportHistoryDto>> HandleAsync(
        GetFinalReportHistoryQuery query, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<FinalReportHistoryDto>(FinalReportSubmissionErrors.AuthenticationRequired);
        }
        if (query.GroupId == Guid.Empty)
        {
            return Result.Failure<FinalReportHistoryDto>(FinalReportSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<FinalReportHistoryDto>(FinalReportSubmissionErrors.GroupNotFound);
        }
        if (!await accessChecker.CanReadAsync(group, cancellationToken))
        {
            return Result.Failure<FinalReportHistoryDto>(FinalReportSubmissionErrors.ReadAccessDenied);
        }

        var report = await finalReportRepository.GetByGroupAndVersionTypeAsync(
            group.Id, FinalReportVersionType.InitialFinal, cancellationToken);
        if (report is null)
        {
            return Result.Failure<FinalReportHistoryDto>(FinalReportSubmissionErrors.ReportNotFound);
        }
        var files = await submissionFileRepository.GetByEntityAsync(SubmissionEntityType.FinalReport, report.Id, cancellationToken);
        // A revision may commit between the report and file queries. Keep the response consistent
        // with the report version read above, rather than including a file from a newer revision.
        var versions = files.Where(file => file.VersionNo <= report.CurrentVersionNo)
            .OrderBy(file => file.VersionNo)
            .Select(file => new FinalReportHistoryItemDto(file.VersionNo, SyllabusSubmissionMapper.ToDto(file)))
            .ToArray();
        return Result.Success(new FinalReportHistoryDto(report.Id, report.GroupId, report.VersionType.ToString(),
            report.CurrentVersionNo, report.Status.ToString(), report.SubmittedAt, versions));
    }
}
