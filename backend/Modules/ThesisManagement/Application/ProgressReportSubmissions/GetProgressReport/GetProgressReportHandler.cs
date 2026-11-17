using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReport;

public sealed class GetProgressReportHandler(
    IProgressMilestoneRepository milestoneRepository,
    IProgressReportRepository progressReportRepository,
    IStudentGroupRepository groupRepository,
    ISubmissionFileRepository submissionFileRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    ICurrentUser currentUser,
    ISubmissionProductLinkRepository? productLinkRepository = null,
    ISubmissionAttachmentRepository? attachmentRepository = null)
{
    public async Task<Result<ProgressReportHistoryDto>> HandleAsync(
        GetProgressReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<ProgressReportHistoryDto>(ProgressReportErrors.AccessDenied);
        }

        if (query.MilestoneId == Guid.Empty)
        {
            return Result.Failure<ProgressReportHistoryDto>(ProgressReportErrors.InvalidRequest);
        }

        var milestone = await milestoneRepository.GetByIdAsync(query.MilestoneId, cancellationToken);
        if (milestone is null)
        {
            return Result.Failure<ProgressReportHistoryDto>(ProgressReportErrors.MilestoneNotFound);
        }

        var group = await groupRepository.GetByIdAsync(milestone.GroupId, cancellationToken);
        if (group is null || !await CanReadAsync(group, cancellationToken))
        {
            return Result.Failure<ProgressReportHistoryDto>(ProgressReportErrors.AccessDenied);
        }

        var report = await progressReportRepository.GetByMilestoneIdAsync(milestone.Id, cancellationToken);
        if (report is null)
        {
            return Result.Failure<ProgressReportHistoryDto>(ProgressReportErrors.ReportNotFound);
        }

        var versions = await progressReportRepository.GetVersionsAsync(report.Id, cancellationToken);
        var files = await submissionFileRepository.GetByEntityAsync(
            Domain.Enums.SubmissionEntityType.ProgressReport,
            report.Id,
            cancellationToken);
        var links = productLinkRepository is not null
            ? await productLinkRepository.GetByEntityAsync(Domain.Enums.SubmissionEntityType.ProgressReport, report.Id, cancellationToken)
            : [];
        var attachments = attachmentRepository is not null
            ? await attachmentRepository.GetByEntityAsync(Domain.Enums.SubmissionEntityType.ProgressReport, report.Id, cancellationToken)
            : [];
        var fileByVersion = files.ToDictionary(file => file.VersionNo);
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
        var history = versions.Select(version => new ProgressReportVersionDto(
            version.VersionNo,
            version.GitUrl,
            version.StudentNote,
            version.SubmittedBy,
            version.SubmittedAt,
            version.IsLate,
            version.Status.ToString(),
            fileByVersion.TryGetValue(version.VersionNo, out var file)
                ? SyllabusSubmissionMapper.ToDto(file)
                : null,
            version.ReviewedBy.HasValue && version.ReviewedAt.HasValue
                ? new ProgressReportReviewDto(
                    version.VersionNo,
                    version.Status.ToString(),
                    version.AdvisorFeedback,
                    version.ReviewedBy.Value,
                    version.ReviewedAt.Value)
                : null,
            linksByVersion.TryGetValue(version.VersionNo, out var versionLinks)
                ? versionLinks
                : [],
            attachmentsByVersion.TryGetValue(version.VersionNo, out var versionAttachments)
                ? versionAttachments
                : [])).ToArray();

        return Result.Success(new ProgressReportHistoryDto(
            report.Id,
            report.MilestoneId,
            report.GroupId,
            report.CurrentVersionNo,
            report.Status.ToString(),
            history));
    }

    private async Task<bool> CanReadAsync(Domain.Entities.StudentGroup group, CancellationToken cancellationToken)
    {
        if (ThesisAuthorization.IsConfirmedMember(group, currentUser.UserId!.Value)
            || ThesisAuthorization.CanReviewGates(currentUser))
        {
            return true;
        }

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
        return assignment?.AdvisorId == currentUser.UserId!.Value;
    }
}
