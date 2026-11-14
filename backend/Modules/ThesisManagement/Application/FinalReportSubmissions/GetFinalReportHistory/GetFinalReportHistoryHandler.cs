using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportHistory;

public sealed class GetFinalReportHistoryHandler(
    IStudentGroupRepository groupRepository,
    IFinalReportRepository finalReportRepository,
    ISubmissionFileRepository submissionFileRepository,
    IFinalReportPlagiarismScanRepository plagiarismScanRepository,
    FinalReportReadAccessChecker accessChecker,
    ICurrentUser currentUser,
    ITopicAdvisorAssignmentContract advisorAssignments,
    ISubmissionProductLinkRepository? productLinkRepository = null,
    ISubmissionAttachmentRepository? attachmentRepository = null)
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
        var scans = await plagiarismScanRepository.GetByReportIdAsync(report.Id, cancellationToken);
        var links = productLinkRepository is not null
            ? await productLinkRepository.GetByEntityAsync(SubmissionEntityType.FinalReport, report.Id, cancellationToken)
            : [];
        var attachments = attachmentRepository is not null
            ? await attachmentRepository.GetByEntityAsync(SubmissionEntityType.FinalReport, report.Id, cancellationToken)
            : [];
        var scanMap = scans.ToDictionary(s => s.VersionNo);
        var linksByVersion = links
            .GroupBy(link => link.VersionNo)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SubmissionProductLinkDto>)g
                    .OrderBy(item => item.DisplayOrder)
                    .Select(SubmissionSupplementalMapper.ToDto)
                    .ToArray());
        var attachmentsByVersion = attachments
            .GroupBy(attachment => attachment.VersionNo)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SubmissionAttachmentDto>)g
                    .OrderBy(item => item.DisplayOrder)
                    .Select(SubmissionSupplementalMapper.ToDto)
                    .ToArray());

        bool HasRole(string role) => currentUser.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        var advisor = await advisorAssignments.GetByTopicIdAsync(group.TopicId, cancellationToken);
        var canViewSimilarityScore = ThesisAuthorization.IsAdministrator(currentUser)
            || (HasRole("Faculty") && ThesisAuthorization.CanReviewGates(currentUser))
            || (currentUser.UserId.HasValue && advisor?.AdvisorId == currentUser.UserId.Value);
        // A revision may commit between the report and file queries. Keep the response consistent
        // with the report version read above, rather than including a file from a newer revision.
        var versions = files.Where(file => file.VersionNo <= report.CurrentVersionNo)
            .OrderBy(file => file.VersionNo)
            .Select(file =>
            {
                scanMap.TryGetValue(file.VersionNo, out var scan);
                var statusStr = scan?.Status.ToString();
                var similarityCoverage = canViewSimilarityScore ? scan?.SimilarityCoverage : null;

                return new FinalReportHistoryItemDto(
                    file.VersionNo,
                    SyllabusSubmissionMapper.ToDto(file),
                    statusStr,
                    similarityCoverage,
                    linksByVersion.TryGetValue(file.VersionNo, out var versionLinks) ? versionLinks : [],
                    attachmentsByVersion.TryGetValue(file.VersionNo, out var versionAttachments) ? versionAttachments : []);
            })
            .ToArray();
        return Result.Success(new FinalReportHistoryDto(report.Id, report.GroupId, report.VersionType.ToString(),
            report.CurrentVersionNo, report.Status.ToString(), report.SubmittedAt, versions));
    }
}
