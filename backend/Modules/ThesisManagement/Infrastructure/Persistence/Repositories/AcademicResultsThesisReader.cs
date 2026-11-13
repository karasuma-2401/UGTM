using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed class AcademicResultsThesisReader(ThesisManagementDbContext db) : IAcademicResultsThesisReader
{
    public async Task<IReadOnlyList<AcademicResultGroupSnapshotContractDto>> GetGroupsByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var groups = await db.StudentGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.BatchId == batchId))
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
        {
            return [];
        }

        var groupIds = groups.Select(g => g.Id).ToList();

        var midterms = await db.MidtermReports
            .AsNoTracking()
            .Where(m => groupIds.Contains(m.GroupId))
            .ToDictionaryAsync(m => m.GroupId, cancellationToken);

        var finalSubmissions = await db.FinalSubmissions
            .AsNoTracking()
            .Where(f => groupIds.Contains(f.GroupId))
            .ToDictionaryAsync(f => f.GroupId, cancellationToken);

        return groups.Select(g =>
        {
            midterms.TryGetValue(g.Id, out var midterm);
            finalSubmissions.TryGetValue(g.Id, out var finalSub);

            var members = g.Members.Select(m => new DefenseGroupMemberContractDto(
                m.StudentId,
                m.BatchId,
                m.ConfirmationStatus.ToString())).ToList();

            var midtermDto = midterm != null
                ? new AcademicResultMidtermSnapshotContractDto(
                    midterm.Id,
                    midterm.Status.ToString(),
                    midterm.CurrentVersionNo,
                    midterm.FacultyFeedback,
                    midterm.ReviewedBy,
                    midterm.ReviewedAt,
                    midterm.ReviewedAt ?? g.CreatedAt)
                : null;

            return new AcademicResultGroupSnapshotContractDto(
                g.Id,
                g.TopicId,
                batchId,
                g.Status.ToString(),
                g.CreatedAt,
                members,
                midtermDto,
                finalSub?.Status.ToString());
        }).ToList();
    }

    public async Task<AcademicResultGroupSnapshotContractDto?> GetGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await db.StudentGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        if (group == null)
        {
            return null;
        }

        var midterm = await db.MidtermReports
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.GroupId == groupId, cancellationToken);

        var finalSub = await db.FinalSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.GroupId == groupId, cancellationToken);

        var batchId = group.Members.FirstOrDefault()?.BatchId ?? Guid.Empty;

        var members = group.Members.Select(m => new DefenseGroupMemberContractDto(
            m.StudentId,
            m.BatchId,
            m.ConfirmationStatus.ToString())).ToList();

        var midtermDto = midterm != null
            ? new AcademicResultMidtermSnapshotContractDto(
                midterm.Id,
                midterm.Status.ToString(),
                midterm.CurrentVersionNo,
                midterm.FacultyFeedback,
                midterm.ReviewedBy,
                midterm.ReviewedAt,
                midterm.ReviewedAt ?? group.CreatedAt)
            : null;

        return new AcademicResultGroupSnapshotContractDto(
            group.Id,
            group.TopicId,
            batchId,
            group.Status.ToString(),
            group.CreatedAt,
            members,
            midtermDto,
            finalSub?.Status.ToString());
    }
}
