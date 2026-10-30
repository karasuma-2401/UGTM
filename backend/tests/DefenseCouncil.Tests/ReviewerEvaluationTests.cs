using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class ReviewerEvaluationTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 7, 0, 0, DateTimeKind.Utc);
    private readonly Guid _actor = Guid.NewGuid();

    [Fact]
    public void Empty_draft_has_no_invented_score_decision_or_publication()
    {
        var value = Draft();
        Assert.Null(value.Score); Assert.Null(value.Comment); Assert.Null(value.DefenseApproval);
        Assert.Equal(ReviewerEvaluationStatus.Draft, value.Status);
        Assert.False(value.IsLegacy); Assert.False(value.IsLocked); Assert.False(value.IsVisibleToStudent);
        Assert.Equal(_actor, value.CreatedBy); Assert.Equal(_actor, value.EvaluatedBy);
        Assert.Equal(Now, value.CreatedAtUtc); Assert.Equal(Now, value.EvaluatedAt);
        Assert.Null(value.SubmittedBy); Assert.Null(value.SubmittedAtUtc);
    }

    [Theory]
    [InlineData(DefenseApprovalStatus.Approved)]
    [InlineData(DefenseApprovalStatus.Rejected)]
    public void Submission_preserves_creator_records_actual_editor_and_permanently_locks_both_decisions(DefenseApprovalStatus decision)
    {
        var value = Draft(); var administrator = Guid.NewGuid();
        Assert.True(value.TrySubmit(8.345m, "  Góp ý công khai  ", decision, administrator, Now.AddMinutes(1)));
        Assert.Equal(8.35m, value.Score); Assert.Equal("Góp ý công khai", value.Comment);
        Assert.Equal(_actor, value.CreatedBy); Assert.Equal(Now, value.CreatedAtUtc);
        Assert.Equal(administrator, value.EvaluatedBy); Assert.Equal(administrator, value.SubmittedBy);
        Assert.Equal(Now.AddMinutes(1), value.SubmittedAtUtc);
        Assert.True(value.IsLocked); Assert.True(value.IsVisibleToStudent);
        Assert.False(value.TrySaveDraft(10m, "Ghi đè", DefenseApprovalStatus.Approved, _actor, Now.AddMinutes(2)));
        Assert.False(value.TrySubmit(10m, "Ghi đè", DefenseApprovalStatus.Approved, administrator, Now.AddMinutes(2)));
        Assert.Equal(8.35m, value.Score); Assert.Equal(decision, value.DefenseApproval);
    }

    [Fact]
    public void Saving_a_draft_replaces_all_content_but_keeps_identity_and_creator()
    {
        var value = Draft(); var id = value.Id;
        Assert.True(value.TrySaveDraft(0m, "Nháp", DefenseApprovalStatus.Rejected, _actor, Now.AddSeconds(1)));
        Assert.Equal(0m, value.Score);
        Assert.True(value.TrySaveDraft(null, "\u00a0 \t", null, _actor, Now.AddSeconds(2)));
        Assert.Equal(id, value.Id); Assert.Equal(Now, value.CreatedAtUtc);
        Assert.Null(value.Score); Assert.Null(value.Comment); Assert.Null(value.DefenseApproval);
        Assert.False(value.IsVisibleToStudent);
    }

    [Theory]
    [InlineData(null, "Góp ý", DefenseApprovalStatus.Approved)]
    [InlineData(8, null, DefenseApprovalStatus.Approved)]
    [InlineData(8, "  ", DefenseApprovalStatus.Rejected)]
    [InlineData(8, "Góp ý", null)]
    public void Incomplete_submit_does_not_partially_change_draft(int? score, string? comment, DefenseApprovalStatus? approval)
    {
        var value = Draft();
        Assert.Throws<ArgumentException>(() => value.TrySubmit(score, comment, approval, _actor, Now.AddSeconds(1)));
        Assert.Null(value.Score); Assert.Null(value.Comment); Assert.Equal(Now, value.EvaluatedAt);
        Assert.Equal(ReviewerEvaluationStatus.Draft, value.Status);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("10", "10")]
    [InlineData("8.345", "8.35")]
    [InlineData("0.005", "0.01")]
    public void Decimal_scores_are_checked_then_rounded_away_from_zero(string input, string expected)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var normalized = ReviewerEvaluation.NormalizeContent(decimal.Parse(input, culture), null, null, false);
        Assert.Equal(decimal.Parse(expected, culture), normalized.Score);
    }

    [Theory]
    [InlineData("-0.001")]
    [InlineData("10.004")]
    public void Out_of_range_score_is_rejected_before_rounding(string input)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ReviewerEvaluation.NormalizeContent(
            decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture), null, null, false));

    [Fact]
    public void Comment_uses_unicode_trim_and_rune_length_without_cutting_public_text()
    {
        var text = string.Concat(Enumerable.Repeat("😀", 2000));
        Assert.Equal(text, ReviewerEvaluation.NormalizeContent(null, "\u00a0" + text + "\u00a0", null, false).Comment);
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewerEvaluation.NormalizeContent(null, text + "a", null, false));
        Assert.Throws<ArgumentException>(() => ReviewerEvaluation.NormalizeContent(null, "\ud800", null, false));
        Assert.Throws<ArgumentException>(() => ReviewerEvaluation.NormalizeContent(null, "Góp ý\0ẩn", null, false));
    }

    [Fact]
    public void Invalid_decision_actor_time_and_identifiers_never_mutate_a_draft()
    {
        var value = Draft();
        Assert.Throws<ArgumentOutOfRangeException>(() => value.TrySaveDraft(8, "X", (DefenseApprovalStatus)99, _actor, Now));
        Assert.Throws<ArgumentException>(() => value.TrySaveDraft(8, "X", null, Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => value.TrySaveDraft(8, "X", null, _actor, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentOutOfRangeException>(() => value.TrySaveDraft(8, "X", null, _actor, Now.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => ReviewerEvaluation.CreateDraft(Guid.Empty, Guid.NewGuid(), null, null, null, _actor, Now));
        Assert.Throws<ArgumentException>(() => ReviewerEvaluation.CreateDraft(Guid.NewGuid(), Guid.Empty, null, null, null, _actor, Now));
        Assert.Null(value.Score); Assert.Null(value.Comment); Assert.Equal(Now, value.EvaluatedAt);
    }
    private ReviewerEvaluation Draft() => ReviewerEvaluation.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), null, null, null, _actor, Now);
}
