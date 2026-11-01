using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class AdvisorEvaluationTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
    private const string Checksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void A_new_evaluation_binds_to_the_locked_report_and_rounds_its_score()
    {
        var evaluation = CreateEvaluation(score: 8.345m, comment: "  Báo cáo đạt yêu cầu.  ");

        Assert.False(evaluation.IsLegacy);
        Assert.Equal(8.35m, evaluation.Score);
        Assert.Equal("Báo cáo đạt yêu cầu.", evaluation.Comment);
        Assert.Equal(Checksum, evaluation.SubmissionChecksumSha256);
        Assert.Null(evaluation.AllowsReviewerAssignment);
        Assert.Null(evaluation.DecisionBy);
        Assert.False(evaluation.IsLocked);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(10.001)]
    public void A_new_evaluation_rejects_scores_outside_the_unrounded_range(decimal score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvaluation(score: score));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_new_evaluation_requires_a_positive_report_version(int reportVersionNo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AdvisorEvaluation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reportVersionNo, Guid.NewGuid(), Checksum,
            8m, "Nhận xét", Guid.NewGuid(), Timestamp));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-checksum")]
    public void A_new_evaluation_requires_a_sha256_checksum(string checksum)
    {
        Assert.Throws<ArgumentException>(() => AdvisorEvaluation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), checksum,
            8m, "Nhận xét", Guid.NewGuid(), Timestamp));
    }

    [Fact]
    public void A_rejected_evaluation_can_be_updated_and_reconsidered()
    {
        var evaluation = CreateEvaluation();
        var rejectionActor = Guid.NewGuid();

        Assert.True(evaluation.TryDecide(false, "  Cần bổ sung phần thực nghiệm. ", rejectionActor, Timestamp.AddMinutes(1)));
        Assert.False(evaluation.AllowsReviewerAssignment);
        Assert.Equal("Cần bổ sung phần thực nghiệm.", evaluation.RejectionReason);
        Assert.Equal(rejectionActor, evaluation.DecisionBy);
        Assert.True(evaluation.TryUpdate(9.995m, "  Đã bổ sung thực nghiệm. ", Guid.NewGuid(), Timestamp.AddMinutes(2)));

        Assert.Equal(10m, evaluation.Score);
        Assert.Equal("Đã bổ sung thực nghiệm.", evaluation.Comment);
        Assert.False(evaluation.AllowsReviewerAssignment);
        Assert.Equal("Cần bổ sung phần thực nghiệm.", evaluation.RejectionReason);
        Assert.True(evaluation.TryDecide(true, null, Guid.NewGuid(), Timestamp.AddMinutes(3)));
        Assert.True(evaluation.AllowsReviewerAssignment);
        Assert.Null(evaluation.RejectionReason);
        Assert.True(evaluation.IsLocked);
    }

    [Fact]
    public void An_approved_evaluation_cannot_be_changed_or_have_its_decision_withdrawn()
    {
        var evaluation = CreateEvaluation();
        Assert.True(evaluation.TryDecide(true, null, Guid.NewGuid(), Timestamp.AddMinutes(1)));

        Assert.False(evaluation.TryUpdate(7m, "Nội dung mới", Guid.NewGuid(), Timestamp.AddMinutes(2)));
        Assert.False(evaluation.TryDecide(false, "Không còn đồng ý", Guid.NewGuid(), Timestamp.AddMinutes(2)));
        Assert.Equal(8m, evaluation.Score);
        Assert.True(evaluation.AllowsReviewerAssignment);
    }

    [Fact]
    public void A_rejection_requires_a_nonempty_reason_and_a_utc_decision_time()
    {
        var evaluation = CreateEvaluation();

        Assert.Throws<ArgumentException>(() => evaluation.TryDecide(false, " ", Guid.NewGuid(), Timestamp));
        Assert.Throws<ArgumentException>(() => evaluation.TryDecide(false, "Thiếu giờ UTC", Guid.NewGuid(),
            DateTime.SpecifyKind(Timestamp, DateTimeKind.Local)));
        Assert.Null(evaluation.AllowsReviewerAssignment);
    }

    [Fact]
    public void An_approval_rejects_a_rejection_reason_without_mutating_the_evaluation()
    {
        var evaluation = CreateEvaluation();

        Assert.Throws<ArgumentException>(() => evaluation.TryDecide(true, "Không hợp lệ", Guid.NewGuid(), Timestamp));

        Assert.Null(evaluation.AllowsReviewerAssignment);
        Assert.Null(evaluation.RejectionReason);
        Assert.Null(evaluation.DecisionBy);
    }

    [Fact]
    public void An_evaluation_requires_a_nonempty_comment_and_a_utc_evaluation_time()
    {
        Assert.Throws<ArgumentException>(() => CreateEvaluation(comment: " "));
        Assert.Throws<ArgumentException>(() => AdvisorEvaluation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), Checksum,
            8m, "Nhận xét", Guid.NewGuid(), DateTime.SpecifyKind(Timestamp, DateTimeKind.Unspecified)));
    }

    private static AdvisorEvaluation CreateEvaluation(decimal score = 8m, string comment = "Nhận xét")
        => AdvisorEvaluation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), Checksum,
            score, comment, Guid.NewGuid(), Timestamp);
}
