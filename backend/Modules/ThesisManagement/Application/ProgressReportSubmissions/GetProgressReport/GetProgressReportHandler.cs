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
    ICurrentUser currentUser)
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
        var fileByVersion = files.ToDictionary(file => file.VersionNo);
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
                : null)).ToArray();

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
        if (group.Members.Any(member => member.StudentId == currentUser.UserId!.Value)
            || ThesisAuthorization.CanReviewGates(currentUser))
        {
            return true;
        }

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
        return assignment?.AdvisorId == currentUser.UserId!.Value;
    }
}
