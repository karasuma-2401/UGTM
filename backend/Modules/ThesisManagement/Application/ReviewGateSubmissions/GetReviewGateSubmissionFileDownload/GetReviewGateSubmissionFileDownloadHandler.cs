using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.ReviewGateSubmissions.GetReviewGateSubmissionFileDownload;

public sealed class GetReviewGateSubmissionFileDownloadHandler(
    ISubmissionFileRepository submissionFileRepository,
    ISyllabusRepository syllabusRepository,
    IMidtermReportRepository midtermReportRepository,
    IStudentGroupRepository groupRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IThesisSubmissionObjectStorage objectStorage,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DownloadUriLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<SubmissionFileDownloadDto>> HandleAsync(
        GetReviewGateSubmissionFileDownloadQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId.Value == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.AccessDenied);
        }

        if (query.FileId == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.InvalidRequest);
        }

        var file = await submissionFileRepository.GetByIdAsync(query.FileId, cancellationToken);
        if (file is null)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.FileNotFound);
        }

        var groupId = file.EntityType switch
        {
            SubmissionEntityType.Syllabus => (await syllabusRepository.GetByIdAsync(
                file.EntityId,
                cancellationToken))?.GroupId,
            SubmissionEntityType.MidtermReport => (await midtermReportRepository.GetByIdAsync(
                file.EntityId,
                cancellationToken))?.GroupId,
            _ => null
        };
        if (!groupId.HasValue)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.FileNotFound);
        }

        var group = await groupRepository.GetByIdAsync(groupId.Value, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.FileNotFound);
        }

        var isMember = ThesisAuthorization.IsConfirmedMember(group, currentUser.UserId.Value);
        var canAccess = isMember || ThesisAuthorization.CanReviewGates(currentUser);
        if (!canAccess)
        {
            var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
            canAccess = assignment is not null && assignment.AdvisorId == currentUser.UserId.Value;
        }

        if (!canAccess)
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.AccessDenied);
        }

        var downloadUrl = await ThesisDownloadUriHelper.CreateSignedUriAsync(
            objectStorage,
            file.ObjectKey,
            file.FileName,
            file.FileExtension,
            DownloadUriLifetime,
            query.Inline,
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(new SubmissionFileDownloadDto(
            file.Id,
            downloadUrl.AbsoluteUri,
            now.Add(DownloadUriLifetime)));
    }
}
