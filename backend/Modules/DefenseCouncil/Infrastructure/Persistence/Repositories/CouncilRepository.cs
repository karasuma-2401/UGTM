using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

public sealed class CouncilRepository(DefenseCouncilDbContext db) : ICouncilRepository
{
    public Task<Council?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Councils.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Council?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => db.Councils
            .Include(c => c.Members)
            .Include(c => c.Assignments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<bool> ExistsByCodeInBatchAsync(
        Guid batchId,
        string councilCode,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var normalizedCode = councilCode.Trim().ToLower();
        return await db.Councils.AnyAsync(
            c => c.BatchId == batchId
                 && (excludeCouncilId == null || c.Id != excludeCouncilId)
                 && c.CouncilCode.ToLower() == normalizedCode,
            ct);
    }

    public async Task<bool> HasRoomScheduleConflictAsync(
        DateOnly defenseDate,
        string timeSlot,
        string room,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var normalizedSlot = timeSlot.Trim().ToLower();
        var normalizedRoom = room.Trim().ToLower();

        return await db.Councils.AnyAsync(
            c => c.DefenseDate == defenseDate
                 && (excludeCouncilId == null || c.Id != excludeCouncilId)
                 && c.TimeSlot.ToLower() == normalizedSlot
                 && c.Room.ToLower() == normalizedRoom,
            ct);
    }

    public async Task<IReadOnlyList<Guid>> GetConflictingMemberUserIdsAsync(
        DateOnly defenseDate,
        string timeSlot,
        IEnumerable<Guid> userIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var memberIdList = userIds.Distinct().ToList();
        if (memberIdList.Count == 0) return [];

        var normalizedSlot = timeSlot.Trim().ToLower();

        return await db.Councils
            .Where(c => c.DefenseDate == defenseDate
                        && (excludeCouncilId == null || c.Id != excludeCouncilId)
                        && c.TimeSlot.ToLower() == normalizedSlot)
            .SelectMany(c => c.Members)
            .Where(m => memberIdList.Contains(m.UserId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetAssignedGroupCouncilsAsync(
        IEnumerable<Guid> groupIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, Guid>();

        return await db.CouncilAssignments
            .Where(a => idList.Contains(a.GroupId) && (excludeCouncilId == null || a.CouncilId != excludeCouncilId))
            .ToDictionaryAsync(a => a.GroupId, a => a.CouncilId, ct);
    }

    public async Task<IReadOnlySet<Guid>> GetRejectedDefenseGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (idList.Count == 0) return new HashSet<Guid>();

        var rejected = await db.ReviewerAssignments
            .Where(r => r.IsActive
                        && idList.Contains(r.GroupId)
                        && r.Evaluation != null
                        && r.Evaluation.DefenseApproval == DefenseApprovalStatus.Rejected)
            .Select(r => r.GroupId)
            .Distinct()
            .ToListAsync(ct);

        return rejected.ToHashSet();
    }

    public Task<Council?> GetCouncilByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => db.CouncilAssignments
            .Where(a => a.GroupId == groupId)
            .Select(a => a.Council)
            .Include(c => c.Members)
            .Include(c => c.Assignments)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetActiveReviewerIdsByGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, Guid>();

        return await db.ReviewerAssignments
            .Where(r => r.IsActive && idList.Contains(r.GroupId))
            .ToDictionaryAsync(r => r.GroupId, r => r.ReviewerId, ct);
    }

    public async Task AddAsync(Council council, CancellationToken ct = default)
        => await db.Councils.AddAsync(council, ct);

    public async Task AddMembersAsync(IEnumerable<CouncilMember> members, CancellationToken ct = default)
        => await db.CouncilMembers.AddRangeAsync(members, ct);

    public async Task AddAssignmentsAsync(IEnumerable<CouncilAssignment> assignments, CancellationToken ct = default)
        => await db.CouncilAssignments.AddRangeAsync(assignments, ct);
}
