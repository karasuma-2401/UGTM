using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.ReviewGateSubmissions.GetReviewGateSubmissionFileDownload;

public sealed class GetReviewGateSubmissionFileDownloadHandler(
    ISubmissionFileRepository submissionFileRepository,
    ISyllabusRepository syllabusRepository,
    IMidtermReportRepository midtermReportRepository,
    IStudentGroupRepository groupRepository,
    IThesisSubmissionObjectStorage objectStorage,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DownloadUriLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<SubmissionFileDownloadDto>> HandleAsync(
        GetReviewGateSubmissionFileDownloadQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
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

        var isMember = group.Members.Any(member => member.StudentId == currentUser.UserId.Value);
        if (!isMember && !ThesisAuthorization.CanReviewGates(currentUser))
        {
            return Result.Failure<SubmissionFileDownloadDto>(ReviewGateSubmissionFileErrors.AccessDenied);
        }

        var downloadUrl = await objectStorage.CreateDownloadUriAsync(
            file.ObjectKey,
            DownloadUriLifetime,
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(new SubmissionFileDownloadDto(
            file.Id,
            downloadUrl.AbsoluteUri,
            now.Add(DownloadUriLifetime)));
    }
}
