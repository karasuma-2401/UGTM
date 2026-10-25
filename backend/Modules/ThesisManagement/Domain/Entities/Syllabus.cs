using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class Syllabus
{
    private Syllabus()
    {
    }

    private Syllabus(Guid id, Guid groupId)
    {
        if (id == Guid.Empty || groupId == Guid.Empty)
        {
            throw new ArgumentException("Syllabus and group identifiers must be nonempty.");
        }

        Id = id;
        GroupId = groupId;
        CurrentVersionNo = 1;
        Status = SyllabusStatus.Submitted;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public int CurrentVersionNo { get; private set; }
    public SyllabusStatus Status { get; private set; }
    public string? FacultyFeedback { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    public StudentGroup Group { get; private set; } = null!;

    public static Syllabus CreateSubmitted(Guid id, Guid groupId)
        => new(id, groupId);

    public void SubmitRevision()
    {
        if (Status != SyllabusStatus.NeedRevision)
        {
            throw new InvalidOperationException("Only a syllabus requiring revision can be submitted again.");
        }

        CurrentVersionNo = checked(CurrentVersionNo + 1);
        Status = SyllabusStatus.Submitted;
        FacultyFeedback = null;
        ReviewedBy = null;
        ReviewedAt = null;
    }

    public void SubmitUpdatedAfterRename()
    {
        CurrentVersionNo = checked(CurrentVersionNo + 1);
    }

    public void RecordReview(
        ReviewDecision decision,
        string? feedback,
        Guid reviewedBy,
        DateTime reviewedAtUtc)
    {
        if (Status != SyllabusStatus.Submitted)
        {
            throw new InvalidOperationException("Only a submitted syllabus can be reviewed.");
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
            ReviewDecision.Approved => SyllabusStatus.Approved,
            ReviewDecision.NeedRevision => SyllabusStatus.NeedRevision,
            ReviewDecision.Rejected => SyllabusStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };
        FacultyFeedback = normalizedFeedback;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAtUtc;
    }
}
