namespace UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;

public sealed record DefenseGroupMemberContractDto(
    Guid StudentId,
    Guid BatchId,
    string ConfirmationStatus);

public sealed record DefenseGroupSnapshotContractDto(
    Guid GroupId,
    Guid TopicId,
    Guid BatchId,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<DefenseGroupMemberContractDto> Members);

public interface IDefenseGroupContract
{
    Task<DefenseGroupSnapshotContractDto?> GetGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, DefenseGroupSnapshotContractDto>> GetGroupsByIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken cancellationToken = default);

    Task<DefenseGroupSnapshotContractDto?> GetActiveGroupByStudentIdAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}
