using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class SubmissionReviewTests
{
    private static readonly DateTime ReviewedAt = new(2026, 10, 22, 7, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ReviewDecision.NeedRevision)]
    [InlineData(ReviewDecision.Rejected)]
    public void Create_requires_feedback_for_non_approval_decisions(ReviewDecision decision)
    {
        Assert.Throws<ArgumentException>(() => SubmissionReview.Create(
            Guid.NewGuid(),
            ReviewGateType.Syllabus,
            Guid.NewGuid(),
            1,
            decision,
            " ",
            Guid.NewGuid(),
            ReviewedAt));
    }

    [Fact]
    public void Create_allows_approved_submission_without_feedback()
    {
        var review = SubmissionReview.Create(
            Guid.NewGuid(),
            ReviewGateType.Midterm,
            Guid.NewGuid(),
            2,
            ReviewDecision.Approved,
            null,
            Guid.NewGuid(),
            ReviewedAt);

        Assert.Equal(2, review.VersionNo);
        Assert.Equal(ReviewDecision.Approved, review.Decision);
        Assert.Null(review.Feedback);
    }

    [Fact]
    public void Create_normalizes_feedback_and_requires_utc_review_time()
    {
        var review = SubmissionReview.Create(
            Guid.NewGuid(),
            ReviewGateType.Syllabus,
            Guid.NewGuid(),
            1,
            ReviewDecision.NeedRevision,
            "  Cần bổ sung phạm vi nghiên cứu.  ",
            Guid.NewGuid(),
            ReviewedAt);

        Assert.Equal("Cần bổ sung phạm vi nghiên cứu.", review.Feedback);
        Assert.Throws<ArgumentException>(() => SubmissionReview.Create(
            Guid.NewGuid(),
            ReviewGateType.Syllabus,
            Guid.NewGuid(),
            1,
            ReviewDecision.Approved,
            null,
            Guid.NewGuid(),
            DateTime.SpecifyKind(ReviewedAt, DateTimeKind.Local)));
    }
}
