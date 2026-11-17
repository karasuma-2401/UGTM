using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusReviewQueue;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class SyllabusReviewQueryReader(ThesisManagementDbContext db) : ISyllabusReviewQueryReader
{
    public async Task<PagedResult<SyllabusReviewRecord>> GetPageAsync(
        SyllabusReviewFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Syllabuses.AsNoTracking().Where(syllabus =>
            syllabus.Status == SyllabusStatus.Submitted || syllabus.Status == SyllabusStatus.NeedRevision
            || syllabus.Status == SyllabusStatus.Approved || syllabus.Status == SyllabusStatus.Rejected);
        if (filter.Status.HasValue) query = query.Where(syllabus => syllabus.Status == filter.Status.Value);
        if (filter.BatchTopicIds is not null)
        {
            var topicIds = filter.BatchTopicIds.ToArray();
            query = query.Where(syllabus => db.StudentGroups.Any(group =>
                group.Id == syllabus.GroupId && topicIds.Contains(group.TopicId)));
        }
        if (filter.KeywordTopicIds is not null || filter.KeywordStudentIds is not null)
        {
            var topicIds = filter.KeywordTopicIds?.ToArray() ?? [];
            var studentIds = filter.KeywordStudentIds?.ToArray() ?? [];
            query = query.Where(syllabus => db.StudentGroups.Any(group =>
                    group.Id == syllabus.GroupId && topicIds.Contains(group.TopicId))
                || db.StudentGroupMembers.Any(member =>
                    member.GroupId == syllabus.GroupId && studentIds.Contains(member.StudentId)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = ((long)pageNumber - 1) * pageSize;
        if (offset >= totalCount)
            return new PagedResult<SyllabusReviewRecord>([], totalCount, pageNumber, pageSize);

        var page = await query.Select(syllabus => new
        {
            syllabus.Id,
            syllabus.GroupId,
            syllabus.CurrentVersionNo,
            syllabus.Status,
            File = db.SubmissionFiles.Where(file => file.EntityType == SubmissionEntityType.Syllabus
                    && file.EntityId == syllabus.Id && file.VersionNo == syllabus.CurrentVersionNo)
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
        var records = page.Select(row => new SyllabusReviewRecord(row.Id, row.GroupId, row.CurrentVersionNo,
            row.Status, groups.GetValueOrDefault(row.GroupId), row.File)).ToArray();
        return new PagedResult<SyllabusReviewRecord>(records, totalCount, pageNumber, pageSize);
    }
}
