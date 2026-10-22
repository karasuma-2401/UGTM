using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionHistory;

public sealed class GetSyllabusSubmissionHistoryHandler(
    IStudentGroupRepository groupRepository,
    ISyllabusRepository syllabusRepository,
    ISubmissionFileRepository submissionFileRepository,
    ISubmissionReviewRepository submissionReviewRepository,
    ICurrentUser currentUser)
{
    public async Task<Result<SyllabusSubmissionHistoryDto>> HandleAsync(
        GetSyllabusSubmissionHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<SyllabusSubmissionHistoryDto>(SyllabusSubmissionErrors.AccessDenied);
        }

        if (query.GroupId == Guid.Empty)
        {
            return Result.Failure<SyllabusSubmissionHistoryDto>(SyllabusSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SyllabusSubmissionHistoryDto>(SyllabusSubmissionErrors.GroupNotFound);
        }

        var isMember = group.Members.Any(member => member.StudentId == currentUser.UserId.Value);
        if (!isMember && !ThesisAuthorization.CanReviewGates(currentUser))
        {
            return Result.Failure<SyllabusSubmissionHistoryDto>(SyllabusSubmissionErrors.AccessDenied);
        }

        var syllabus = await syllabusRepository.GetByGroupIdAsync(query.GroupId, cancellationToken);
        if (syllabus is null)
        {
            return Result.Failure<SyllabusSubmissionHistoryDto>(SyllabusSubmissionErrors.SubmissionNotFound);
        }

        var files = await submissionFileRepository.GetByEntityAsync(
            SubmissionEntityType.Syllabus,
            syllabus.Id,
            cancellationToken);
        var reviews = await submissionReviewRepository.GetBySubmissionAsync(
            ReviewGateType.Syllabus,
            syllabus.Id,
            cancellationToken);
        var reviewByVersion = reviews.ToDictionary(review => review.VersionNo);
        var versions = files
            .GroupBy(file => file.VersionNo)
            .Select(grouping => grouping.OrderByDescending(file => file.UploadedAt).First())
            .OrderBy(file => file.VersionNo)
            .Select(file => new SyllabusSubmissionHistoryItemDto(
                file.VersionNo,
                SyllabusSubmissionMapper.ToDto(file),
                reviewByVersion.TryGetValue(file.VersionNo, out var review)
                    ? SyllabusSubmissionMapper.ToDto(review)
                    : null))
            .ToArray();

        return Result.Success(new SyllabusSubmissionHistoryDto(
            syllabus.Id,
            syllabus.GroupId,
            syllabus.CurrentVersionNo,
            syllabus.Status.ToString(),
            versions));
    }
}
