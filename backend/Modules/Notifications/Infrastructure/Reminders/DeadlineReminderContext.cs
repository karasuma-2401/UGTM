using UGTM.Modules.AcademicPeriods.Contracts.Reminders;
using UGTM.Modules.DefenseCouncil.Contracts.Reminders;
using UGTM.Modules.ThesisManagement.Contracts.Reminders;
using UGTM.Modules.TopicCatalog.Contracts.Reminders;

namespace UGTM.Modules.Notifications.Infrastructure.Reminders;

internal sealed record StaffDigestSectionAccumulator(
    string Title,
    string ActionPath,
    List<string> Items);

internal sealed record BatchReminderScanContext(
    OpenBatchReminderSnapshotDto Batch,
    TopicBatchReminderSnapshotDto Topics,
    ThesisBatchReminderSnapshotDto Thesis,
    DefenseCouncilBatchReminderSnapshotDto Council,
    DateTime NowUtc)
{
    public OpenBatchStageReminderDto? Stage1 { get; } =
        DeadlineReminderFormatting.GetStage(Batch, "TOPIC_PREPARATION");

    public OpenBatchStageReminderDto? Stage5 { get; } =
        DeadlineReminderFormatting.GetStage(Batch, "FINAL_EVALUATION");

    public OpenBatchStageReminderDto? Stage6 { get; } =
        DeadlineReminderFormatting.GetStage(Batch, "DEFENSE");

    public OpenBatchStageReminderDto? Stage7 { get; } =
        DeadlineReminderFormatting.GetStage(Batch, "ARCHIVAL_AND_PUBLICATION");

    public ReviewGateReminderDto? SyllabusGate { get; } =
        Thesis.ReviewGates.FirstOrDefault(g => string.Equals(g.GateType, "Syllabus", StringComparison.OrdinalIgnoreCase));

    public ReviewGateReminderDto? MidtermGate { get; } =
        Thesis.ReviewGates.FirstOrDefault(g => string.Equals(g.GateType, "Midterm", StringComparison.OrdinalIgnoreCase));
}
