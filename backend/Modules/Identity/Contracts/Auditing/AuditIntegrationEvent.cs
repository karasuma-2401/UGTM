namespace UGTM.Modules.Identity.Contracts.Auditing;

public sealed record AuditIntegrationEntry(
    string TargetType,
    string TargetId,
    string Action,
    string Outcome,
    string? ReasonCode,
    object? Metadata);

public sealed record AuditIntegrationEvent(
    Guid EventId,
    Guid? ActorUserId,
    Guid? ActorSessionId,
    string RequestId,
    string TargetType,
    string TargetId,
    string Action,
    string Outcome,
    string? ReasonCode,
    string? MetadataJson,
    DateTime OccurredAtUtc);

public static class AuditIntegrationEventValidation
{
    public static void Validate(
        AuditIntegrationEntry entry,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsureValue(entry.TargetType, 100, nameof(entry.TargetType));
        EnsureValue(entry.TargetId, 100, nameof(entry.TargetId));
        EnsureValue(entry.Action, 100, nameof(entry.Action));
        EnsureValue(entry.Outcome, 50, nameof(entry.Outcome));

        if (entry.ReasonCode is not null)
        {
            EnsureValue(entry.ReasonCode, 100, nameof(entry.ReasonCode));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Audit event time must be UTC.",
                nameof(occurredAtUtc));
        }
    }

    private static void EnsureValue(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maxLength} characters.",
                parameterName);
        }
    }
}

public static class IntegrationAuditActions
{
    public const string BatchCreated =
        "AcademicPeriods.Batch.Created";

    public const string BatchScheduleUpdated =
        "AcademicPeriods.Batch.ScheduleUpdated";

    public const string BatchOpened =
        "AcademicPeriods.Batch.Opened";

    public const string BatchClosed =
        "AcademicPeriods.Batch.Closed";

    public const string BatchArchived =
        "AcademicPeriods.Batch.Archived";

    public const string ScoreConfigurationDraftCreated =
        "AcademicPeriods.ScoreConfiguration.DraftCreated";

    public const string ScoreConfigurationDraftUpdated =
        "AcademicPeriods.ScoreConfiguration.DraftUpdated";

    public const string ScoreConfigurationPublished =
        "AcademicPeriods.ScoreConfiguration.Published";

    public const string ScoreConfigurationRetired =
        "AcademicPeriods.ScoreConfiguration.Retired";

    public const string BatchConfigurationUpdated =
        "AcademicPeriods.Batch.ConfigurationUpdated";

    public const string ScoreConfigurationUpdated =
        "AcademicPeriods.Score.ConfigurationUpdated";

    public const string RenameRequestApproved =
        "ThesisManagement.RenameRequest.Approved";

    public const string ReviewGateConfigurationCreated =
        "ThesisManagement.ReviewGateConfiguration.Created";

    public const string ReviewGateConfigurationUpdated =
        "ThesisManagement.ReviewGateConfiguration.Updated";

    public const string SyllabusSubmitted =
        "ThesisManagement.Syllabus.Submitted";

    public const string SyllabusReviewed =
        "ThesisManagement.Syllabus.Reviewed";

    public const string MidtermReportSubmitted =
        "ThesisManagement.MidtermReport.Submitted";

    public const string FinalReportSubmitted =
        "ThesisManagement.FinalReport.Submitted";

    public const string MidtermReportReviewed =
        "ThesisManagement.MidtermReport.Reviewed";

    public const string ReviewGateDeadlineClosed =
        "ThesisManagement.ReviewGate.DeadlineClosed";

    public const string ProgressMilestoneCreated =
        "ThesisManagement.ProgressMilestone.Created";

    public const string ProgressReportSubmitted =
        "ThesisManagement.ProgressReport.Submitted";

    public const string ProgressReportReviewed =
        "ThesisManagement.ProgressReport.Reviewed";

    public const string CouncilMinuteUnlockGranted =
        "DefenseCouncil.CouncilMinute.UnlockGranted";

    public const string ScoreAdjusted =
        "DefenseCouncil.Score.Adjusted";

    public const string ScoreExcluded =
        "DefenseCouncil.Score.Excluded";
}
