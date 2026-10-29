
namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

/// <summary>
/// The advisor's evaluation of the exact final-report version locked for review.
/// Legacy rows predate that binding and are deliberately unavailable to the SCRUM-103 workflow.
/// </summary>
public sealed class AdvisorEvaluation
{
    private const int CommentMaximumLength = 2_000;
    private const int RejectionReasonMaximumLength = 1_000;
    private const int Sha256HexLength = 64;

    private AdvisorEvaluation() { }

    private AdvisorEvaluation(
        Guid id,
        Guid groupId,
        Guid finalReportId,
        int reportVersionNo,
        Guid submissionFileId,
        string submissionChecksumSha256,
        decimal score,
        string comment,
        Guid evaluatedBy,
        DateTime evaluatedAtUtc)
    {
        EnsureNonempty(id, nameof(id));
        EnsureNonempty(groupId, nameof(groupId));
        EnsureNonempty(finalReportId, nameof(finalReportId));
        EnsurePositive(reportVersionNo, nameof(reportVersionNo));
        EnsureNonempty(submissionFileId, nameof(submissionFileId));

        Id = id;
        GroupId = groupId;
        FinalReportId = finalReportId;
        ReportVersionNo = reportVersionNo;
        SubmissionFileId = submissionFileId;
        SubmissionChecksumSha256 = NormalizeChecksum(submissionChecksumSha256);
        IsLegacy = false;
        SetEvaluation(score, comment, evaluatedBy, evaluatedAtUtc);
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid? FinalReportId { get; private set; }
    public int? ReportVersionNo { get; private set; }
    public Guid? SubmissionFileId { get; private set; }
    public string? SubmissionChecksumSha256 { get; private set; }
    public decimal Score { get; private set; }
    public string? Comment { get; private set; }
    public Guid? EvaluatedBy { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
    public bool? AllowsReviewerAssignment { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? DecisionBy { get; private set; }
    public DateTime? DecisionAt { get; private set; }
    public bool IsLegacy { get; private set; }
    public bool IsIgnoredInCalculation { get; private set; }
    public string? ExclusionReason { get; private set; }
    public uint Version { get; private set; }

    public bool IsLocked => !IsLegacy && AllowsReviewerAssignment is true;

    public static AdvisorEvaluation Create(
        Guid id,
        Guid groupId,
        Guid finalReportId,
        int reportVersionNo,
        Guid submissionFileId,
        string submissionChecksumSha256,
        decimal score,
        string comment,
        Guid evaluatedBy,
        DateTime evaluatedAtUtc)
        => new(id, groupId, finalReportId, reportVersionNo, submissionFileId, submissionChecksumSha256,
            score, comment, evaluatedBy, evaluatedAtUtc);

    /// <summary>
    /// Validates a draft before a workflow acquires the cross-module report lock. Keeping this
    /// rule in the aggregate prevents invalid requests from creating a concurrency-only update.
    /// </summary>
    public static void ValidateEvaluationInput(decimal score, string? comment)
    {
        _ = NormalizeScore(score);
        _ = NormalizeComment(comment!);
    }

    public bool TryUpdate(
        decimal score,
        string comment,
        Guid evaluatedBy,
        DateTime evaluatedAtUtc)
    {
        if (!CanChange())
        {
            return false;
        }

        SetEvaluation(score, comment, evaluatedBy, evaluatedAtUtc);
        return true;
    }

    public bool TryDecide(
        bool allowsReviewerAssignment,
        string? rejectionReason,
        Guid decidedBy,
        DateTime decidedAtUtc)
    {
        if (!CanChange())
        {
            return false;
        }

        EnsureNonempty(decidedBy, nameof(decidedBy));
        EnsureUtc(decidedAtUtc, nameof(decidedAtUtc));
        var normalizedRejectionReason = allowsReviewerAssignment
            ? EnsureNoRejectionReason(rejectionReason)
            : NormalizeRejectionReason(rejectionReason);

        AllowsReviewerAssignment = allowsReviewerAssignment;
        RejectionReason = normalizedRejectionReason;
        DecisionBy = decidedBy;
        DecisionAt = decidedAtUtc;
        return true;
    }

    private bool CanChange()
        => !IsLegacy
           && FinalReportId.HasValue
           && ReportVersionNo is > 0
           && SubmissionFileId.HasValue
           && EvaluatedBy.HasValue
           && !IsLocked;

    private void SetEvaluation(decimal score, string comment, Guid evaluatedBy, DateTime evaluatedAtUtc)
    {
        EnsureNonempty(evaluatedBy, nameof(evaluatedBy));
        EnsureUtc(evaluatedAtUtc, nameof(evaluatedAtUtc));

        Score = NormalizeScore(score);
        Comment = NormalizeComment(comment);
        EvaluatedBy = evaluatedBy;
        EvaluatedAt = evaluatedAtUtc;
    }

    private static decimal NormalizeScore(decimal score)
    {
        if (score is < 0m or > 10m)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Advisor score must be between 0 and 10.");
        }

        return decimal.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeComment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.EnumerateRunes().Count() > CommentMaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value),
                $"Advisor comment cannot exceed {CommentMaximumLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeRejectionReason(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.EnumerateRunes().Count() > RejectionReasonMaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value),
                $"Rejection reason cannot exceed {RejectionReasonMaximumLength} characters.");
        }

        return normalized;
    }

    private static string? EnsureNoRejectionReason(string? value)
    {
        if (value is not null)
        {
            throw new ArgumentException("An approval cannot include a rejection reason.", nameof(value));
        }

        return null;
    }

    private static string NormalizeChecksum(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length != Sha256HexLength || !normalized.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Submission checksum must be a SHA-256 hexadecimal value.", nameof(value));
        }

        return normalized.ToLowerInvariant();
    }

    private static void EnsureNonempty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be nonempty.", parameterName);
        }
    }

    private static void EnsurePositive(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Version must be positive.");
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
