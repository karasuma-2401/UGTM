using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface ICouncilRepository
{
    Task<Council?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Council?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByCodeInBatchAsync(
        Guid batchId,
        string councilCode,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<bool> HasRoomScheduleConflictAsync(
        DateOnly defenseDate,
        string timeSlot,
        string room,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetConflictingMemberUserIdsAsync(
        DateOnly defenseDate,
        string timeSlot,
        IEnumerable<Guid> userIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, Guid>> GetAssignedGroupCouncilsAsync(
        IEnumerable<Guid> groupIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> GetRejectedDefenseGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default);

    Task<Council?> GetCouncilByGroupIdAsync(
        Guid groupId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, Guid>> GetActiveReviewerIdsByGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default);

    Task AddAsync(Council council, CancellationToken ct = default);

    Task AddMembersAsync(IEnumerable<CouncilMember> members, CancellationToken ct = default);

    Task AddAssignmentsAsync(IEnumerable<CouncilAssignment> assignments, CancellationToken ct = default);
}
