using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;
using Microsoft.Extensions.Logging;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.StudentGroups.Dtos;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportReviewQueue;

public sealed class GetMidtermReportReviewQueueHandler(
    IMidtermReportReviewQueryReader queryReader,
    ITopicSearchReader topicSearch,
    IStudentSearchReader studentSearch,
    ITopicSummaryReader topicReader,
    IBatchSummaryReader batchReader,
    IStudentInfoReader studentReader,
    IAdvisorNameReader advisorReader,
    ICurrentUser currentUser,
    ILogger<GetMidtermReportReviewQueueHandler> logger)
{
    public async Task<Result<PagedResult<MidtermReportReviewItemDto>>> HandleAsync(
        GetMidtermReportReviewQueueQuery query, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
            return Result.Failure<PagedResult<MidtermReportReviewItemDto>>(MidtermReportReviewQueueErrors.AuthenticationRequired);
        if (!ThesisAuthorization.CanReviewGates(currentUser))
            return Result.Failure<PagedResult<MidtermReportReviewItemDto>>(MidtermReportReviewQueueErrors.AccessDenied);

        MidtermReportStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var value = query.Status.Trim();
            if (!Enum.GetNames<MidtermReportStatus>().Any(name => name != nameof(MidtermReportStatus.Draft)
                    && string.Equals(name, value, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<PagedResult<MidtermReportReviewItemDto>>(MidtermReportReviewQueueErrors.InvalidRequest);
            status = Enum.Parse<MidtermReportStatus>(value, true);
        }
        if (query.BatchId == Guid.Empty)
            return Result.Failure<PagedResult<MidtermReportReviewItemDto>>(MidtermReportReviewQueueErrors.InvalidRequest);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();
        var batchTopicIds = query.BatchId.HasValue
            ? await topicSearch.FindIdsAsync(query.BatchId, null, cancellationToken) : null;
        var keywordTopicIds = keyword is not null
            ? await topicSearch.FindIdsAsync(query.BatchId, keyword, cancellationToken) : null;
        var keywordStudentIds = keyword is not null
            ? await studentSearch.FindIdsByNameOrCodeAsync(keyword, cancellationToken) : null;
        var page = await queryReader.GetPageAsync(
            new MidtermReportReviewFilter(status, batchTopicIds, keywordTopicIds, keywordStudentIds),
            pageNumber, pageSize, cancellationToken);
        if (page.Items.Count == 0)
            return Result.Success(new PagedResult<MidtermReportReviewItemDto>([], page.TotalCount, pageNumber, pageSize));

        var topics = await topicReader.GetByIdsAsync(
            page.Items.Where(row => row.Group is not null).Select(row => row.Group!.TopicId), cancellationToken);
        foreach (var row in page.Items)
        {
            if (row.Group is null || !topics.TryGetValue(row.Group.TopicId, out var topic)
                || topic.BatchId == Guid.Empty || topic.AdvisorId == Guid.Empty
                || row.Group.Members.Any(member => member.BatchId != topic.BatchId))
            {
                logger.LogError("Midterm report {MidtermReportId} has missing group/topic data or inconsistent batch links.", row.MidtermReportId);
                return Result.Failure<PagedResult<MidtermReportReviewItemDto>>(MidtermReportReviewQueueErrors.InconsistentData);
            }
        }

        var batches = await batchReader.GetByIdsAsync(topics.Values.Select(topic => topic.BatchId), cancellationToken);
        var advisors = await advisorReader.GetNamesByIdsAsync(topics.Values.Select(topic => topic.AdvisorId), cancellationToken);
        var students = await studentReader.GetStudentsByIdsAsync(
            page.Items.SelectMany(row => row.Group!.Members).Select(member => member.StudentId), cancellationToken);
        var items = page.Items.Select(row =>
        {
            var group = row.Group!;
            var topic = topics[group.TopicId];
            if (!batches.TryGetValue(topic.BatchId, out var batch))
                logger.LogWarning("Batch {BatchId} for midterm report {MidtermReportId} was not found.", topic.BatchId, row.MidtermReportId);
            if (!advisors.TryGetValue(topic.AdvisorId, out var advisorName))
                logger.LogWarning("Advisor {AdvisorId} for midterm report {MidtermReportId} was not found.", topic.AdvisorId, row.MidtermReportId);
            if (row.LatestFile is null)
                logger.LogWarning("Midterm report {MidtermReportId} has no file for current version {VersionNo}.", row.MidtermReportId, row.CurrentVersionNo);
            var members = group.Members.OrderBy(member => member.JoinedAt).ThenBy(member => member.StudentId)
                .Select(member =>
                {
                    if (!students.TryGetValue(member.StudentId, out var student))
                        logger.LogWarning("Student {StudentId} for midterm report {MidtermReportId} was not found.", member.StudentId, row.MidtermReportId);
                    return new MidtermReportReviewStudentDto(member.StudentId, student?.UserCode, student?.FullName);
                }).ToArray();
            return new MidtermReportReviewItemDto(row.MidtermReportId, group.Id, topic.TopicId, topic.TitleVi, topic.TitleEn,
                topic.BatchId, batch?.BatchName, members, new StudentGroupAdvisorDto(topic.AdvisorId, advisorName),
                row.CurrentVersionNo, row.Status.ToString(), row.LatestFile is null ? null : SyllabusSubmissionMapper.ToDto(row.LatestFile));
        }).ToArray();
        return Result.Success(new PagedResult<MidtermReportReviewItemDto>(items, page.TotalCount, pageNumber, pageSize));
    }
}
