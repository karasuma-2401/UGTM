using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed class DefenseGroupContract(ThesisManagementDbContext db) : IDefenseGroupContract
{
    public async Task<DefenseGroupSnapshotContractDto?> GetGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await db.StudentGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        return group is null ? null : MapToDto(group);
    }

    public async Task<IReadOnlyDictionary<Guid, DefenseGroupSnapshotContractDto>> GetGroupsByIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken cancellationToken = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, DefenseGroupSnapshotContractDto>();
        }

        var groups = await db.StudentGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => idList.Contains(g.Id))
            .ToListAsync(cancellationToken);

        return groups.ToDictionary(g => g.Id, MapToDto);
    }

    public async Task<DefenseGroupSnapshotContractDto?> GetActiveGroupByStudentIdAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var group = await db.StudentGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => g.Status != StudentGroupStatus.Stopped
                        && g.Members.Any(m => m.StudentId == studentId
                                              && m.ConfirmationStatus == MemberConfirmationStatus.Confirmed))
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return group is null ? null : MapToDto(group);
    }

    private static DefenseGroupSnapshotContractDto MapToDto(Domain.Entities.StudentGroup group)
    {
        var batchId = group.Members.FirstOrDefault()?.BatchId ?? Guid.Empty;
        var members = group.Members.Select(m => new DefenseGroupMemberContractDto(
            m.StudentId,
            m.BatchId,
            m.ConfirmationStatus.ToString())).ToList();

        return new DefenseGroupSnapshotContractDto(
            group.Id,
            group.TopicId,
            batchId,
            group.Status.ToString(),
            group.CreatedAt,
            members);
    }
}
