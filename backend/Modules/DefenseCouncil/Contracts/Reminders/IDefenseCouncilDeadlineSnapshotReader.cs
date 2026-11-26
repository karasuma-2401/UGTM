namespace UGTM.Modules.DefenseCouncil.Contracts.Reminders;

public sealed record PendingReviewerAssignmentReminderDto(
    Guid AssignmentId,
    Guid GroupId,
    Guid ReviewerId);

public sealed record ActiveCouncilReminderDto(
    Guid CouncilId,
    string CouncilCode,
    DateOnly DefenseDate,
    string TimeSlot,
    string Room,
    IReadOnlyList<Guid> MemberUserIds,
    IReadOnlyList<Guid> ChairmanAndSecretaryIds,
    IReadOnlyList<Guid> AssignedGroupIds,
    IReadOnlyList<Guid> UnclosedMinuteGroupIds);

public sealed record DefenseCouncilBatchReminderSnapshotDto(
    IReadOnlySet<Guid> AdvisorEvaluatedGroupIds,
    IReadOnlyList<PendingReviewerAssignmentReminderDto> PendingReviewerAssignments,
    IReadOnlySet<Guid> ReviewerVisibleGroupIds,
    IReadOnlyList<ActiveCouncilReminderDto> ActiveCouncils,
    IReadOnlySet<Guid> PublishedScoreGroupIds);

public interface IDefenseCouncilDeadlineSnapshotReader
{
    Task<DefenseCouncilBatchReminderSnapshotDto> GetBatchSnapshotAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
