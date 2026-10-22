using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportSubmissionHistory;

public sealed class GetMidtermReportSubmissionHistoryHandler(
    IStudentGroupRepository groupRepository,
    IMidtermReportRepository midtermReportRepository,
    ISubmissionFileRepository submissionFileRepository,
    ISubmissionReviewRepository submissionReviewRepository,
    ICurrentUser currentUser)
{
    public async Task<Result<MidtermReportSubmissionHistoryDto>> HandleAsync(
        GetMidtermReportSubmissionHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<MidtermReportSubmissionHistoryDto>(MidtermReportSubmissionErrors.AccessDenied);
        }

        if (query.GroupId == Guid.Empty)
        {
            return Result.Failure<MidtermReportSubmissionHistoryDto>(MidtermReportSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<MidtermReportSubmissionHistoryDto>(MidtermReportSubmissionErrors.GroupNotFound);
        }

        var isMember = group.Members.Any(member => member.StudentId == currentUser.UserId.Value);
        if (!isMember && !ThesisAuthorization.CanReviewGates(currentUser))
        {
            return Result.Failure<MidtermReportSubmissionHistoryDto>(MidtermReportSubmissionErrors.AccessDenied);
        }

        var report = await midtermReportRepository.GetByGroupIdAsync(query.GroupId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<MidtermReportSubmissionHistoryDto>(MidtermReportSubmissionErrors.SubmissionNotFound);
        }

        var files = await submissionFileRepository.GetByEntityAsync(
            SubmissionEntityType.MidtermReport,
            report.Id,
            cancellationToken);
        var reviews = await submissionReviewRepository.GetBySubmissionAsync(
            ReviewGateType.Midterm,
            report.Id,
            cancellationToken);
        var reviewByVersion = reviews.ToDictionary(review => review.VersionNo);
        var versions = files
            .GroupBy(file => file.VersionNo)
            .Select(grouping => grouping.OrderByDescending(file => file.UploadedAt).First())
            .OrderBy(file => file.VersionNo)
            .Select(file => new MidtermReportSubmissionHistoryItemDto(
                file.VersionNo,
                SyllabusSubmissionMapper.ToDto(file),
                reviewByVersion.TryGetValue(file.VersionNo, out var review)
                    ? SyllabusSubmissionMapper.ToDto(review)
                    : null))
            .ToArray();

        return Result.Success(new MidtermReportSubmissionHistoryDto(
            report.Id,
            report.GroupId,
            report.CurrentVersionNo,
            report.Status.ToString(),
            versions));
    }
}
