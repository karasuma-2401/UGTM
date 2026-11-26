namespace UGTM.Modules.ThesisManagement.Contracts.Reminders;

public sealed record ReviewGateReminderDto(
    string GateType,
    DateTime InitialSubmissionDeadlineUtc,
    DateTime FacultyResponseDeadlineUtc,
    DateTime FinalClosureDeadlineUtc);

public sealed record ThesisGroupReminderDto(
    Guid GroupId,
    Guid TopicId,
    string Status,
    IReadOnlyList<Guid> ConfirmedStudentIds,
    string? SyllabusStatus,
    string? MidtermStatus,
    bool HasInitialFinalReport,
    bool HasRevisedFinalReport,
    string? ArchivalStatus);

public sealed record MilestoneReminderDto(
    Guid MilestoneId,
    Guid GroupId,
    string Title,
    DateTime DeadlineUtc,
    string? LatestReportStatus);

public sealed record ThesisBatchReminderSnapshotDto(
    IReadOnlyList<ReviewGateReminderDto> ReviewGates,
    IReadOnlyList<ThesisGroupReminderDto> Groups,
    IReadOnlyList<MilestoneReminderDto> PendingMilestones);

public interface IThesisDeadlineSnapshotReader
{
    Task<ThesisBatchReminderSnapshotDto> GetBatchSnapshotAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
