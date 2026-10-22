using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class MidtermReport
{
    private MidtermReport()
    {
    }

    private MidtermReport(Guid id, Guid groupId)
    {
        if (id == Guid.Empty || groupId == Guid.Empty)
        {
            throw new ArgumentException("Midterm report and group identifiers must be nonempty.");
        }

        Id = id;
        GroupId = groupId;
        CurrentVersionNo = 1;
        Status = MidtermReportStatus.Submitted;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public int CurrentVersionNo { get; private set; }
    public MidtermReportStatus Status { get; private set; }
    public string? FacultyFeedback { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    public StudentGroup Group { get; private set; } = null!;

    public static MidtermReport CreateSubmitted(Guid id, Guid groupId)
        => new(id, groupId);

    public void SubmitRevision()
    {
        if (Status != MidtermReportStatus.NeedRevision)
        {
            throw new InvalidOperationException("Only a midterm report requiring revision can be submitted again.");
        }

        CurrentVersionNo = checked(CurrentVersionNo + 1);
        Status = MidtermReportStatus.Submitted;
        FacultyFeedback = null;
        ReviewedBy = null;
        ReviewedAt = null;
    }

    public void RecordReview(
        ReviewDecision decision,
        string? feedback,
        Guid reviewedBy,
        DateTime reviewedAtUtc)
    {
        if (Status != MidtermReportStatus.Submitted)
        {
            throw new InvalidOperationException("Only a submitted midterm report can be reviewed.");
        }

        if (reviewedBy == Guid.Empty)
        {
            throw new ArgumentException("Reviewer identifier must be nonempty.", nameof(reviewedBy));
        }

        if (reviewedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Review time must be UTC.", nameof(reviewedAtUtc));
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), "Review decision is invalid.");
        }

        var normalizedFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        if (decision is ReviewDecision.NeedRevision or ReviewDecision.Rejected
            && normalizedFeedback is null)
        {
            throw new ArgumentException(
                "Feedback is required when revision is requested or a submission is rejected.",
                nameof(feedback));
        }

        Status = decision switch
        {
            ReviewDecision.Approved => MidtermReportStatus.Approved,
            ReviewDecision.NeedRevision => MidtermReportStatus.NeedRevision,
            ReviewDecision.Rejected => MidtermReportStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };
        FacultyFeedback = normalizedFeedback;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAtUtc;
    }
}
