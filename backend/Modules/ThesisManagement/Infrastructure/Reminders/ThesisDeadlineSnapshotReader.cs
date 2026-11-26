using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.Reminders;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Reminders;

internal sealed class ThesisDeadlineSnapshotReader(ThesisManagementDbContext db)
    : IThesisDeadlineSnapshotReader
{
    public async Task<ThesisBatchReminderSnapshotDto> GetBatchSnapshotAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var gates = await db.ReviewGateConfigurations
            .AsNoTracking()
            .Where(g => g.BatchId == batchId)
            .Select(g => new ReviewGateReminderDto(
                g.GateType.ToString(),
                g.InitialSubmissionDeadlineUtc,
                g.FacultyResponseDeadlineUtc,
                g.FinalClosureDeadlineUtc))
            .ToListAsync(cancellationToken);

        var memberRows = await db.StudentGroupMembers
            .AsNoTracking()
            .Where(m => m.BatchId == batchId && m.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
            .Select(m => new { m.GroupId, m.StudentId })
            .ToListAsync(cancellationToken);

        var membersByGroup = memberRows
            .GroupBy(x => x.GroupId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(x => x.StudentId).Distinct().ToArray());

        var groupIds = membersByGroup.Keys.ToArray();
        if (groupIds.Length == 0)
        {
            return new ThesisBatchReminderSnapshotDto(gates, [], []);
        }

        var groups = await db.StudentGroups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id) && g.Status != StudentGroupStatus.Stopped)
            .Select(g => new { g.Id, g.TopicId, g.Status })
            .ToListAsync(cancellationToken);

        var activeGroupIds = groups.Select(g => g.Id).ToArray();
        if (activeGroupIds.Length == 0)
        {
            return new ThesisBatchReminderSnapshotDto(gates, [], []);
        }

        var syllabuses = await db.Syllabuses
            .AsNoTracking()
            .Where(s => activeGroupIds.Contains(s.GroupId))
            .ToDictionaryAsync(s => s.GroupId, s => s.Status.ToString(), cancellationToken);

        var midterms = await db.MidtermReports
            .AsNoTracking()
            .Where(m => activeGroupIds.Contains(m.GroupId))
            .ToDictionaryAsync(m => m.GroupId, m => m.Status.ToString(), cancellationToken);

        var finalReportTypes = await db.FinalReports
            .AsNoTracking()
            .Where(f => activeGroupIds.Contains(f.GroupId))
            .Select(f => new { f.GroupId, f.VersionType })
            .ToListAsync(cancellationToken);

        var finalByGroup = finalReportTypes
            .GroupBy(x => x.GroupId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    HasInitial = g.Any(x => x.VersionType == FinalReportVersionType.InitialFinal),
                    HasRevised = g.Any(x => x.VersionType == FinalReportVersionType.RevisedAfterReviewer)
                });

        var archivals = await db.FinalSubmissions
            .AsNoTracking()
            .Where(s => activeGroupIds.Contains(s.GroupId))
            .ToDictionaryAsync(s => s.GroupId, s => s.Status.ToString(), cancellationToken);

        var groupDtos = groups
            .Select(g =>
            {
                finalByGroup.TryGetValue(g.Id, out var finals);
                return new ThesisGroupReminderDto(
                    g.Id,
                    g.TopicId,
                    g.Status.ToString(),
                    membersByGroup[g.Id],
                    syllabuses.GetValueOrDefault(g.Id),
                    midterms.GetValueOrDefault(g.Id),
                    finals?.HasInitial ?? false,
                    finals?.HasRevised ?? false,
                    archivals.GetValueOrDefault(g.Id));
            })
            .ToArray();

        var milestones = await db.ProgressMilestones
            .AsNoTracking()
            .Where(m => activeGroupIds.Contains(m.GroupId))
            .Select(m => new { m.Id, m.GroupId, m.Title, m.Deadline })
            .ToListAsync(cancellationToken);

        var milestoneIds = milestones.Select(m => m.Id).ToArray();
        var reportStatuses = milestoneIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.ProgressReports
                .AsNoTracking()
                .Where(r => milestoneIds.Contains(r.MilestoneId))
                .ToDictionaryAsync(r => r.MilestoneId, r => r.Status.ToString(), cancellationToken);

        var pendingMilestones = milestones
            .Where(m => !reportStatuses.TryGetValue(m.Id, out var status)
                        || string.Equals(status, nameof(ProgressReportStatus.NeedRevision), StringComparison.Ordinal))
            .Select(m => new MilestoneReminderDto(
                m.Id,
                m.GroupId,
                m.Title,
                m.Deadline,
                reportStatuses.GetValueOrDefault(m.Id)))
            .ToArray();

        return new ThesisBatchReminderSnapshotDto(gates, groupDtos, pendingMilestones);
    }
}
