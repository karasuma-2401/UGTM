using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionFileDownload;

public sealed class GetSyllabusSubmissionFileDownloadHandler(
    ISubmissionFileRepository submissionFileRepository,
    ISyllabusRepository syllabusRepository,
    IStudentGroupRepository groupRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IThesisSubmissionObjectStorage objectStorage,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DownloadUriLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<SubmissionFileDownloadDto>> HandleAsync(
        GetSyllabusSubmissionFileDownloadQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId.Value == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(SyllabusSubmissionErrors.AccessDenied);
        }

        if (query.FileId == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(SyllabusSubmissionErrors.InvalidRequest);
        }

        var file = await submissionFileRepository.GetByIdAsync(query.FileId, cancellationToken);
        if (file is null || file.EntityType != SubmissionEntityType.Syllabus)
        {
            return Result.Failure<SubmissionFileDownloadDto>(SyllabusSubmissionErrors.FileNotFound);
        }

        var syllabus = await syllabusRepository.GetByIdAsync(file.EntityId, cancellationToken);
        if (syllabus is null)
        {
            return Result.Failure<SubmissionFileDownloadDto>(SyllabusSubmissionErrors.FileNotFound);
        }

        var group = await groupRepository.GetByIdAsync(syllabus.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SubmissionFileDownloadDto>(SyllabusSubmissionErrors.FileNotFound);
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
            return Result.Failure<SubmissionFileDownloadDto>(SyllabusSubmissionErrors.AccessDenied);
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
