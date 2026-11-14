using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportAttachmentDownload;

public sealed class GetFinalReportAttachmentDownloadHandler(
    ISubmissionAttachmentRepository attachmentRepository,
    ISubmissionFileRepository submissionFileRepository,
    IFinalReportRepository finalReportRepository,
    IStudentGroupRepository groupRepository,
    FinalReportReadAccessChecker accessChecker,
    IThesisSubmissionObjectStorage objectStorage,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IReviewerAssignmentWorkflowContract? reviewerWorkflow = null)
{
    private static readonly TimeSpan DownloadUriLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<SubmissionFileDownloadDto>> HandleAsync(
        GetFinalReportAttachmentDownloadQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.AuthenticationRequired);
        }

        if (query.AttachmentId == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.InvalidRequest);
        }

        var attachment = await attachmentRepository.GetByIdAsync(query.AttachmentId, cancellationToken);
        if (attachment is null || attachment.EntityType != SubmissionEntityType.FinalReport)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.FileNotFound);
        }

        var report = await finalReportRepository.GetByIdAsync(attachment.EntityId, cancellationToken);
        var group = report is null ? null : await groupRepository.GetByIdAsync(report.GroupId, cancellationToken);
        if (report is null || group is null || attachment.VersionNo < 1 || attachment.VersionNo > report.CurrentVersionNo)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.FileNotFound);
        }

        if (!await accessChecker.CanReadAsync(group, cancellationToken))
        {
            var versionFiles = await submissionFileRepository.GetByEntityAsync(
                SubmissionEntityType.FinalReport,
                report.Id,
                cancellationToken);
            var versionFile = versionFiles.FirstOrDefault(f => f.VersionNo == attachment.VersionNo);
            var access = versionFile is null || reviewerWorkflow is null
                ? null
                : await reviewerWorkflow.GetActiveReportAccessAsync(currentUser.UserId.Value, versionFile.Id, cancellationToken);
            if (access is null
                || access.Binding.GroupId != group.Id
                || access.Binding.FinalReportId != report.Id
                || access.Binding.SubmissionFileId != versionFile!.Id
                || access.Binding.ReportVersionNo != attachment.VersionNo
                || !string.Equals(access.Binding.SubmissionChecksumSha256, versionFile.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.ReadAccessDenied);
            }
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var uri = await objectStorage.CreateDownloadUriAsync(attachment.ObjectKey, DownloadUriLifetime, cancellationToken);
        return Result.Success(new SubmissionFileDownloadDto(attachment.Id, uri.AbsoluteUri, nowUtc.Add(DownloadUriLifetime)));
    }
}
