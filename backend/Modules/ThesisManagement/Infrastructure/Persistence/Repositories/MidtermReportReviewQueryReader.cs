using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportReviewQueue;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class MidtermReportReviewQueryReader(ThesisManagementDbContext db) : IMidtermReportReviewQueryReader
{
    public async Task<PagedResult<MidtermReportReviewRecord>> GetPageAsync(
        MidtermReportReviewFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.MidtermReports.AsNoTracking().Where(report =>
            report.Status == MidtermReportStatus.Submitted || report.Status == MidtermReportStatus.NeedRevision
            || report.Status == MidtermReportStatus.Approved || report.Status == MidtermReportStatus.Rejected);
        if (filter.Status.HasValue) query = query.Where(report => report.Status == filter.Status.Value);
        if (filter.BatchTopicIds is not null)
        {
            var topicIds = filter.BatchTopicIds.ToArray();
            query = query.Where(report => db.StudentGroups.Any(group =>
                group.Id == report.GroupId && topicIds.Contains(group.TopicId)));
        }
        if (filter.KeywordTopicIds is not null || filter.KeywordStudentIds is not null)
        {
            var topicIds = filter.KeywordTopicIds?.ToArray() ?? [];
            var studentIds = filter.KeywordStudentIds?.ToArray() ?? [];
            query = query.Where(report => db.StudentGroups.Any(group =>
                    group.Id == report.GroupId && topicIds.Contains(group.TopicId))
                || db.StudentGroupMembers.Any(member =>
                    member.GroupId == report.GroupId && studentIds.Contains(member.StudentId)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = ((long)pageNumber - 1) * pageSize;
        if (offset >= totalCount)
            return new PagedResult<MidtermReportReviewRecord>([], totalCount, pageNumber, pageSize);

        var page = await query.Select(report => new
        {
            report.Id,
            report.GroupId,
            report.CurrentVersionNo,
            report.Status,
            File = db.SubmissionFiles.Where(file => file.EntityType == SubmissionEntityType.MidtermReport
                    && file.EntityId == report.Id && file.VersionNo == report.CurrentVersionNo)
                .OrderByDescending(file => file.UploadedAt).ThenByDescending(file => file.Id).FirstOrDefault()
        })
            .OrderByDescending(row => row.File != null)
            .ThenByDescending(row => row.File!.UploadedAt)
            .ThenBy(row => row.Id)
            .Skip((int)offset).Take(pageSize)
            .ToListAsync(cancellationToken);

        var groupIds = page.Select(row => row.GroupId).Distinct().ToArray();
        var groups = await db.StudentGroups.AsNoTracking().Include(group => group.Members)
            .Where(group => groupIds.Contains(group.Id)).ToDictionaryAsync(group => group.Id, cancellationToken);
        var records = page.Select(row => new MidtermReportReviewRecord(row.Id, row.GroupId, row.CurrentVersionNo,
            row.Status, groups.GetValueOrDefault(row.GroupId), row.File)).ToArray();
        return new PagedResult<MidtermReportReviewRecord>(records, totalCount, pageNumber, pageSize);
    }
}
