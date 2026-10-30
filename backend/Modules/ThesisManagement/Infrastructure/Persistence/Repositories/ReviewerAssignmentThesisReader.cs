using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentThesisReader(ThesisManagementDbContext context) : IReviewerAssignmentThesisReader
{
    public async Task<IReadOnlyList<ReviewerAssignmentGroupLinkContractDto>> GetGroupLinksAsync(
        IReadOnlyCollection<Guid>? groupIds, IReadOnlyCollection<Guid>? topicIds, CancellationToken cancellationToken = default)
    {
        if (groupIds is null && topicIds is null) throw new ArgumentException("A group or topic scope is required.");
        var query = context.StudentGroups.AsNoTracking().AsQueryable();
        if (groupIds is not null) query = query.Where(group => groupIds.Contains(group.Id));
        if (topicIds is not null) query = query.Where(group => topicIds.Contains(group.TopicId));
        return await query.Select(group => new ReviewerAssignmentGroupLinkContractDto(group.Id, group.TopicId)).ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ReviewerAssignmentThesisContractDto>> GetManyAsync(IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken = default)
        => ReadManyAsync(context.StudentGroups.AsNoTracking().Where(group => groupIds.Contains(group.Id)), cancellationToken);

    public Task<IReadOnlyList<ReviewerAssignmentThesisContractDto>> GetByTopicIdsAsync(IReadOnlyCollection<Guid> topicIds,
        CancellationToken cancellationToken = default)
        => ReadManyAsync(context.StudentGroups.AsNoTracking().Where(group => topicIds.Contains(group.TopicId)), cancellationToken);

    private async Task<IReadOnlyList<ReviewerAssignmentThesisContractDto>> ReadManyAsync(IQueryable<StudentGroup> query, CancellationToken token)
    {
        var groups = await query.Select(group => new { group.Id, group.TopicId, group.Status }).ToListAsync(token);
        if (groups.Count == 0) return [];
        var ids = groups.Select(group => group.Id).ToArray();
        var members = await context.StudentGroupMembers.AsNoTracking().Where(member => ids.Contains(member.GroupId))
            .Select(member => new { member.GroupId, member.BatchId }).Distinct().ToListAsync(token);
        var batches = members.GroupBy(member => member.GroupId).ToDictionary(group => group.Key, group => group.Select(member => member.BatchId).ToArray());
        var reports = await context.FinalReports.AsNoTracking().Where(report => ids.Contains(report.GroupId)
                && report.VersionType == FinalReportVersionType.InitialFinal)
            .Select(report => new { report.Id, report.GroupId, report.CurrentVersionNo, report.Status }).ToDictionaryAsync(report => report.GroupId, token);
        var files = await (from file in context.SubmissionFiles.AsNoTracking()
                           join report in context.FinalReports.AsNoTracking() on file.EntityId equals report.Id
                           where ids.Contains(report.GroupId) && report.VersionType == FinalReportVersionType.InitialFinal
                               && file.EntityType == SubmissionEntityType.FinalReport && file.VersionNo == report.CurrentVersionNo
                           select new { report.GroupId, file.Id, file.FileName, file.ContentType, file.FileSize, file.VersionNo, file.ChecksumSha256 })
            .ToDictionaryAsync(file => file.GroupId, token);
        return groups.Select(group =>
        {
            var report = reports.GetValueOrDefault(group.Id);
            var file = files.GetValueOrDefault(group.Id);
            var batchIds = batches.GetValueOrDefault(group.Id);
            return new ReviewerAssignmentThesisContractDto(group.Id, group.TopicId, batchIds is { Length: 1 } ? batchIds[0] : null,
                group.Status == StudentGroupStatus.ReadyForReviewerAssignment, report?.Id,
                report?.Status == FinalReportStatus.UnderEvaluation, report?.CurrentVersionNo, file?.Id, file?.ChecksumSha256,
                file is null ? null : new(file.Id, file.FileName, file.ContentType, file.FileSize, file.VersionNo));
        }).ToArray();
    }
    public Task<ReviewerAssignmentThesisContractDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
        => ReadAsync(groupId, false, cancellationToken);

    public Task<ReviewerAssignmentThesisContractDto?> LockAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        return ReadAsync(groupId, true, cancellationToken);
    }

    private async Task<ReviewerAssignmentThesisContractDto?> ReadAsync(Guid groupId, bool locked, CancellationToken token)
    {
        var group = locked
            ? await context.StudentGroups.FromSqlInterpolated($"""
                SELECT g.*, g.xmin FROM thesis.student_group g WHERE g.id = {groupId} FOR UPDATE
                """).AsNoTracking().SingleOrDefaultAsync(token)
            : await context.StudentGroups.AsNoTracking().SingleOrDefaultAsync(value => value.Id == groupId, token);
        if (group is null) return null;
        var members = locked
            ? await context.StudentGroupMembers.FromSqlInterpolated($"""
                SELECT m.* FROM thesis.student_group_member m WHERE m.group_id = {groupId}
                ORDER BY m.student_id FOR SHARE
                """).AsNoTracking().ToListAsync(token)
            : await context.StudentGroupMembers.AsNoTracking().Where(value => value.GroupId == groupId).ToListAsync(token);
        var batchIds = members.Select(value => value.BatchId).Distinct().ToArray();
        var report = locked
            ? await context.FinalReports.FromSqlInterpolated($"""
                SELECT r.*, r.xmin FROM thesis.final_report r
                WHERE r.group_id = {groupId} AND r.version_type = 'InitialFinal' FOR SHARE
                """).AsNoTracking().SingleOrDefaultAsync(token)
            : await context.FinalReports.AsNoTracking().SingleOrDefaultAsync(value =>
                value.GroupId == groupId && value.VersionType == FinalReportVersionType.InitialFinal, token);
        SubmissionFile? file = null;
        if (report is not null)
        {
            file = locked
                ? await context.SubmissionFiles.FromSqlInterpolated($"""
                    SELECT f.* FROM thesis.submission_file f
                    WHERE f.entity_type = 'FinalReport' AND f.entity_id = {report.Id}
                        AND f.version_no = {report.CurrentVersionNo} FOR SHARE
                    """).AsNoTracking().SingleOrDefaultAsync(token)
                : await context.SubmissionFiles.AsNoTracking().SingleOrDefaultAsync(value =>
                    value.EntityType == SubmissionEntityType.FinalReport && value.EntityId == report.Id
                    && value.VersionNo == report.CurrentVersionNo, token);
        }
        return new(group.Id, group.TopicId, batchIds.Length == 1 ? batchIds[0] : null,
            group.Status == StudentGroupStatus.ReadyForReviewerAssignment, report?.Id,
            report?.Status == FinalReportStatus.UnderEvaluation, report?.CurrentVersionNo, file?.Id, file?.ChecksumSha256);
    }
}
