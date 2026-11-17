
namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

/// <summary>
/// Assigns one reviewer to the exact report approved by the advisor. Legacy assignments retain
/// their original history but cannot confer report access without a proven workflow binding.
/// </summary>
public sealed class ReviewerAssignment
{
    public const int CancellationReasonMaximumLength = 1_000;

    private ReviewerAssignment() { }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid ReviewerId { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public int AssignmentOrder { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;
    public bool IsLegacy { get; private set; }
    public Guid? AdvisorIdAtAssignment { get; private set; }
    public Guid? AdvisorEvaluationId { get; private set; }
    public Guid? FinalReportId { get; private set; }
    public int? ReportVersionNo { get; private set; }
    public Guid? SubmissionFileId { get; private set; }
    public string? SubmissionChecksumSha256 { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public uint Version { get; private set; }

    public ReviewerEvaluation? Evaluation { get; private set; }

    public static ReviewerAssignment Create(
        Guid id,
        Guid groupId,
        Guid reviewerId,
        Guid advisorIdAtAssignment,
        Guid advisorEvaluationId,
        Guid finalReportId,
        int reportVersionNo,
        Guid submissionFileId,
        string submissionChecksumSha256,
        Guid assignedBy,
        DateTime assignedAtUtc)
    {
        EnsureNonempty(id, nameof(id));
        EnsureNonempty(groupId, nameof(groupId));
        EnsureNonempty(reviewerId, nameof(reviewerId));
        EnsureNonempty(advisorIdAtAssignment, nameof(advisorIdAtAssignment));
        EnsureNonempty(advisorEvaluationId, nameof(advisorEvaluationId));
        EnsureNonempty(finalReportId, nameof(finalReportId));
        EnsureNonempty(submissionFileId, nameof(submissionFileId));
        EnsureNonempty(assignedBy, nameof(assignedBy));
        EnsureUtc(assignedAtUtc, nameof(assignedAtUtc));
        if (reviewerId == advisorIdAtAssignment)
        {
            throw new ArgumentException("The reviewer cannot be the assigned advisor.", nameof(reviewerId));
        }
        if (reportVersionNo < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(reportVersionNo));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionChecksumSha256);
        var checksum = submissionChecksumSha256.Trim();
        if (checksum.Length != 64 || !checksum.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Submission checksum must be a SHA-256 hexadecimal value.",
                nameof(submissionChecksumSha256));
        }

        return new ReviewerAssignment
        {
            Id = id,
            GroupId = groupId,
            ReviewerId = reviewerId,
            AdvisorIdAtAssignment = advisorIdAtAssignment,
            AdvisorEvaluationId = advisorEvaluationId,
            FinalReportId = finalReportId,
            ReportVersionNo = reportVersionNo,
            SubmissionFileId = submissionFileId,
            SubmissionChecksumSha256 = checksum.ToLowerInvariant(),
            AssignedBy = assignedBy,
            AssignedAt = assignedAtUtc,
            IsLegacy = false
        };
    }

    /// <summary>
    /// The caller must check persisted evaluations while holding the assignment's workflow lock
    /// until commit. A navigation that has not been loaded is not proof that no evaluation exists.
    /// Legacy assignments may be cancelled without inventing the missing report snapshot.
    /// </summary>
    public bool TryCancel(Guid cancelledBy, DateTime cancelledAtUtc, string reason, bool hasEvaluation)
    {
        if (!IsActive || hasEvaluation || Evaluation is not null)
        {
            return false;
        }

        EnsureNonempty(cancelledBy, nameof(cancelledBy));
        EnsureUtc(cancelledAtUtc, nameof(cancelledAtUtc));
        if (cancelledAtUtc < AssignedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(cancelledAtUtc),
                "Cancellation cannot precede assignment.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var normalizedReason = reason.Trim();
        if (normalizedReason.EnumerateRunes().Count() > CancellationReasonMaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        IsActive = false;
        CancelledBy = cancelledBy;
        CancelledAt = cancelledAtUtc;
        CancellationReason = normalizedReason;
        return true;
    }

    private static void EnsureNonempty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be nonempty.", parameterName);
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be UTC.", parameterName);
        }
    }
}
