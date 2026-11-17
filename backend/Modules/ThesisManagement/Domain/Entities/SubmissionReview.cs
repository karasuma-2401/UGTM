using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class SubmissionReview
{
    private SubmissionReview()
    {
    }

    private SubmissionReview(
        Guid id,
        ReviewGateType gateType,
        Guid submissionId,
        int versionNo,
        ReviewDecision decision,
        string? feedback,
        Guid reviewedBy,
        DateTime reviewedAtUtc)
    {
        if (id == Guid.Empty || submissionId == Guid.Empty || reviewedBy == Guid.Empty)
        {
            throw new ArgumentException("Review, submission, and reviewer identifiers must be nonempty.");
        }

        if (versionNo < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNo), "Version number must be positive.");
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), "Review decision is invalid.");
        }

        if (reviewedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Review time must be UTC.", nameof(reviewedAtUtc));
        }

        var normalizedFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        if (decision is ReviewDecision.NeedRevision or ReviewDecision.Rejected
            && normalizedFeedback is null)
        {
            throw new ArgumentException("Feedback is required when revision is requested or a submission is rejected.", nameof(feedback));
        }

        Id = id;
        GateType = gateType;
        SubmissionId = submissionId;
        VersionNo = versionNo;
        Decision = decision;
        Feedback = normalizedFeedback;
        ReviewedBy = reviewedBy;
        ReviewedAtUtc = reviewedAtUtc;
    }

    public Guid Id { get; private set; }
    public ReviewGateType GateType { get; private set; }
    public Guid SubmissionId { get; private set; }
    public int VersionNo { get; private set; }
    public ReviewDecision Decision { get; private set; }
    public string? Feedback { get; private set; }
    public Guid ReviewedBy { get; private set; }
    public DateTime ReviewedAtUtc { get; private set; }

    public static SubmissionReview Create(
        Guid id,
        ReviewGateType gateType,
        Guid submissionId,
        int versionNo,
        ReviewDecision decision,
        string? feedback,
        Guid reviewedBy,
        DateTime reviewedAtUtc)
        => new(id, gateType, submissionId, versionNo, decision, feedback, reviewedBy, reviewedAtUtc);
}
