using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class ProgressReport
{
    private ProgressReport() { }

    private ProgressReport(Guid id, Guid milestoneId, Guid groupId, string? studentNote, DateTime submittedAtUtc)
    {
        if (id == Guid.Empty || milestoneId == Guid.Empty || groupId == Guid.Empty)
        {
            throw new ArgumentException("Report, milestone, and group identifiers must be nonempty.");
        }

        if (submittedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Submission time must be UTC.", nameof(submittedAtUtc));
        }

        Id = id;
        MilestoneId = milestoneId;
        GroupId = groupId;
        CurrentVersionNo = 1;
        Status = ProgressReportStatus.Submitted;
        StudentNote = NormalizeNote(studentNote);
        SubmittedAt = submittedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MilestoneId { get; private set; }
    public Guid GroupId { get; private set; }
    public int CurrentVersionNo { get; private set; }
    public ProgressReportStatus Status { get; private set; }
    public string? StudentNote { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public string? AdvisorFeedback { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public uint Version { get; private set; }

    public ProgressMilestone Milestone { get; private set; } = null!;
    public StudentGroup Group { get; private set; } = null!;

    public static ProgressReport CreateSubmitted(
        Guid id,
        Guid milestoneId,
        Guid groupId,
        string? studentNote,
        DateTime submittedAtUtc)
        => new(id, milestoneId, groupId, studentNote, submittedAtUtc);

    public bool TrySubmitRevision(string? studentNote, DateTime submittedAtUtc)
    {
        if (Status != ProgressReportStatus.NeedRevision || submittedAtUtc.Kind != DateTimeKind.Utc)
        {
            return false;
        }

        CurrentVersionNo = checked(CurrentVersionNo + 1);
        Status = ProgressReportStatus.Submitted;
        StudentNote = NormalizeNote(studentNote);
        SubmittedAt = submittedAtUtc;
        AdvisorFeedback = null;
        ReviewedAt = null;
        return true;
    }

    public bool TryReview(ProgressReportStatus decision, string? feedback, DateTime reviewedAtUtc)
    {
        if (Status != ProgressReportStatus.Submitted
            || decision is not (ProgressReportStatus.Accepted or ProgressReportStatus.NeedRevision)
            || reviewedAtUtc.Kind != DateTimeKind.Utc)
        {
            return false;
        }

        var normalizedFeedback = NormalizeNote(feedback);
        if (decision == ProgressReportStatus.NeedRevision && normalizedFeedback is null)
        {
            return false;
        }

        Status = decision;
        AdvisorFeedback = normalizedFeedback;
        ReviewedAt = reviewedAtUtc;
        return true;
    }

    private static string? NormalizeNote(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
