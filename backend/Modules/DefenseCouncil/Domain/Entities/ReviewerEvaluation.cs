using System.Text;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public sealed class ReviewerEvaluation
{
    public const int CommentMaximumLength = 2000;
    private ReviewerEvaluation() { }
    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public decimal? Score { get; private set; }
    public string? Comment { get; private set; }
    public string? RevisionFeedback { get; private set; }
    public bool IsIgnoredInCalculation { get; private set; }
    public string? ExclusionReason { get; private set; }
    public DefenseApprovalStatus? DefenseApproval { get; private set; }
    public bool IsVisibleToStudent { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
    public ReviewerEvaluationStatus? Status { get; private set; }
    public bool IsLegacy { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? CreatedAtUtc { get; private set; }
    public Guid? EvaluatedBy { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public uint Version { get; private set; }
    public bool IsLocked => !IsLegacy && Status == ReviewerEvaluationStatus.Submitted;
    public ReviewerAssignment Assignment { get; private set; } = null!;

    // The caller must hold the assignment workflow lock until this row commits.
    public static ReviewerEvaluation CreateDraft(Guid id, Guid assignmentId, decimal? score, string? comment,
        DefenseApprovalStatus? defenseApproval, Guid actorId, DateTime atUtc)
    {
        RequireId(id, nameof(id));
        RequireId(assignmentId, nameof(assignmentId));
        var content = NormalizeContent(score, comment, defenseApproval, false);
        RequireId(actorId, nameof(actorId));
        RequireUtc(atUtc);
        var value = new ReviewerEvaluation
        {
            Id = id,
            AssignmentId = assignmentId,
            Status = ReviewerEvaluationStatus.Draft,
            IsLegacy = false,
            CreatedBy = actorId,
            CreatedAtUtc = atUtc
        };
        value.Apply(content, actorId, atUtc);
        return value;
    }

    public bool TrySaveDraft(decimal? score, string? comment, DefenseApprovalStatus? defenseApproval,
        Guid actorId, DateTime atUtc)
    {
        if (!CanChange()) return false;
        var content = NormalizeContent(score, comment, defenseApproval, false);
        ValidateActorTime(actorId, atUtc);
        Apply(content, actorId, atUtc);
        return true;
    }

    public bool TrySubmit(decimal? score, string? comment, DefenseApprovalStatus? defenseApproval,
        Guid actorId, DateTime atUtc)
    {
        if (!CanChange()) return false;
        var content = NormalizeContent(score, comment, defenseApproval, true);
        ValidateActorTime(actorId, atUtc);
        Apply(content, actorId, atUtc);
        Status = ReviewerEvaluationStatus.Submitted;
        SubmittedBy = actorId;
        SubmittedAtUtc = atUtc;
        IsVisibleToStudent = true;
        return true;
    }

    public static (decimal? Score, string? Comment, DefenseApprovalStatus? DefenseApproval) NormalizeContent(
        decimal? score, string? comment, DefenseApprovalStatus? defenseApproval, bool submitting)
    {
        if (score is < 0m or > 10m) throw new ArgumentOutOfRangeException(nameof(score));
        if (defenseApproval.HasValue && !Enum.IsDefined(defenseApproval.Value))
            throw new ArgumentOutOfRangeException(nameof(defenseApproval));
        var normalized = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalized is not null)
        {
            if (normalized.Contains('\0')) throw new ArgumentException("PostgreSQL text cannot contain NUL.", nameof(comment));
            // Rune enumeration replaces invalid UTF-16; reject it rather than changing public text.
            for (var index = 0; index < normalized.Length; index++)
            {
                if (!char.IsSurrogate(normalized[index])) continue;
                if (!char.IsHighSurrogate(normalized[index]) || index + 1 >= normalized.Length
                    || !char.IsLowSurrogate(normalized[index + 1])) throw new ArgumentException("Invalid Unicode text.", nameof(comment));
                index++;
            }
            if (normalized.EnumerateRunes().Count() > CommentMaximumLength)
                throw new ArgumentOutOfRangeException(nameof(comment));
        }
        if (submitting && (!score.HasValue || normalized is null || !defenseApproval.HasValue))
            throw new ArgumentException("Score, public feedback and a decision are required to submit.");
        return (score.HasValue ? decimal.Round(score.Value, 2, MidpointRounding.AwayFromZero) : null, normalized, defenseApproval);
    }

    private bool CanChange() => !IsLegacy && Status == ReviewerEvaluationStatus.Draft;
    private void ValidateActorTime(Guid actorId, DateTime atUtc)
    {
        RequireId(actorId, nameof(actorId));
        RequireUtc(atUtc);
        if (atUtc < EvaluatedAt) throw new ArgumentOutOfRangeException(nameof(atUtc), "A save cannot precede the previous save.");
    }
    private void Apply((decimal? Score, string? Comment, DefenseApprovalStatus? DefenseApproval) content,
        Guid actorId, DateTime atUtc)
    {
        Score = content.Score;
        Comment = content.Comment;
        DefenseApproval = content.DefenseApproval;
        EvaluatedBy = actorId;
        EvaluatedAt = atUtc;
    }
    private static void RequireId(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("A nonempty identifier is required.", name);
    }
    private static void RequireUtc(DateTime atUtc)
    {
        if (atUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(atUtc));
    }
}
