using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentThesisReader(ThesisManagementDbContext context) : IReviewerAssignmentThesisReader
{
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
