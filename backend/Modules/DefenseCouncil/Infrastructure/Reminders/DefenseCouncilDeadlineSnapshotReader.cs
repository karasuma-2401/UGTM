using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Contracts.Reminders;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Reminders;

internal sealed class DefenseCouncilDeadlineSnapshotReader(DefenseCouncilDbContext db)
    : IDefenseCouncilDeadlineSnapshotReader
{
    public async Task<DefenseCouncilBatchReminderSnapshotDto> GetBatchSnapshotAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var advisorEvaluatedGroupIds = (await db.AdvisorEvaluations
                .AsNoTracking()
                .Where(e => e.AllowsReviewerAssignment != null)
                .Select(e => e.GroupId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var activeAssignments = await db.ReviewerAssignments
            .AsNoTracking()
            .Where(a => a.IsActive)
            .Select(a => new { a.Id, a.GroupId, a.ReviewerId })
            .ToListAsync(cancellationToken);

        var assignmentIds = activeAssignments.Select(a => a.Id).ToArray();
        var submittedEvalAssignmentIds = assignmentIds.Length == 0
            ? new HashSet<Guid>()
            : (await db.ReviewerEvaluations
                .AsNoTracking()
                .Where(e => assignmentIds.Contains(e.AssignmentId) && e.Status == ReviewerEvaluationStatus.Submitted)
                .Select(e => e.AssignmentId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var pendingReviewerAssignments = activeAssignments
            .Where(a => !submittedEvalAssignmentIds.Contains(a.Id))
            .Select(a => new PendingReviewerAssignmentReminderDto(a.Id, a.GroupId, a.ReviewerId))
            .ToArray();

        var visibleEvalAssignmentIds = assignmentIds.Length == 0
            ? new HashSet<Guid>()
            : (await db.ReviewerEvaluations
                .AsNoTracking()
                .Where(e => assignmentIds.Contains(e.AssignmentId) && e.IsVisibleToStudent)
                .Select(e => e.AssignmentId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var reviewerVisibleGroupIds = activeAssignments
            .Where(a => visibleEvalAssignmentIds.Contains(a.Id))
            .Select(a => a.GroupId)
            .ToHashSet();

        var councils = await db.Councils
            .AsNoTracking()
            .Where(c => c.BatchId == batchId && c.Status == CouncilStatus.Active)
            .Include(c => c.Members)
            .Include(c => c.Assignments)
            .ThenInclude(a => a.Minute)
            .ToListAsync(cancellationToken);

        var activeCouncilDtos = councils
            .Select(c => new ActiveCouncilReminderDto(
                c.Id,
                c.CouncilCode,
                c.DefenseDate,
                c.TimeSlot,
                c.Room,
                c.Members.Select(m => m.UserId).Distinct().ToArray(),
                c.Members
                    .Where(m => m.RoleInCouncil is CouncilMemberRole.Chairman or CouncilMemberRole.Secretary)
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToArray(),
                c.Assignments.Select(a => a.GroupId).Distinct().ToArray(),
                c.Assignments
                    .Where(a => a.Minute is null || a.Minute.LockStatus != CouncilMinuteLockStatus.Locked)
                    .Select(a => a.GroupId)
                    .Distinct()
                    .ToArray()))
            .ToArray();

        var publishedScoreGroupIds = (await db.ScoreSummarys
                .AsNoTracking()
                .Where(s => s.Status == ScoreSummaryStatus.PublishedToStudent)
                .Select(s => s.GroupId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return new DefenseCouncilBatchReminderSnapshotDto(
            advisorEvaluatedGroupIds,
            pendingReviewerAssignments,
            reviewerVisibleGroupIds,
            activeCouncilDtos,
            publishedScoreGroupIds);
    }
}
