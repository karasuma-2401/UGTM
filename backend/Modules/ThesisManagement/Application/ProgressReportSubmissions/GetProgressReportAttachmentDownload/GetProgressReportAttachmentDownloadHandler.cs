using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReportAttachmentDownload;

public sealed class GetProgressReportAttachmentDownloadHandler(
    ISubmissionAttachmentRepository attachmentRepository,
    IProgressReportRepository progressReportRepository,
    IProgressMilestoneRepository milestoneRepository,
    IStudentGroupRepository groupRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IThesisSubmissionObjectStorage objectStorage,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DownloadUriLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<SubmissionFileDownloadDto>> HandleAsync(
        GetProgressReportAttachmentDownloadQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ProgressReportErrors.AccessDenied);
        }

        if (query.AttachmentId == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ProgressReportErrors.InvalidRequest);
        }

        var attachment = await attachmentRepository.GetByIdAsync(query.AttachmentId, cancellationToken);
        if (attachment is null || attachment.EntityType != SubmissionEntityType.ProgressReport)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.FileNotFound);
        }

        var report = await progressReportRepository.GetByIdAsync(attachment.EntityId, cancellationToken);
        var milestone = report is null
            ? null
            : await milestoneRepository.GetByIdAsync(report.MilestoneId, cancellationToken);
        var group = milestone is null
            ? null
            : await groupRepository.GetByIdAsync(milestone.GroupId, cancellationToken);
        if (report is null || milestone is null || group is null || report.GroupId != group.Id)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.FileNotFound);
        }

        if (!await CanReadAsync(group, cancellationToken))
        {
            return Result.Failure<SubmissionFileDownloadDto>(ProgressReportErrors.AccessDenied);
        }

        var downloadUrl = await objectStorage.CreateDownloadUriAsync(attachment.ObjectKey, DownloadUriLifetime, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(new SubmissionFileDownloadDto(attachment.Id, downloadUrl.AbsoluteUri, now.Add(DownloadUriLifetime)));
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
