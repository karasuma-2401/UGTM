using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class FinalReportTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(FinalReportVersionType.InitialFinal)]
    [InlineData(FinalReportVersionType.RevisedAfterReviewer)]
    public void A_new_report_starts_at_version_one(FinalReportVersionType versionType)
    {
        var groupId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var report = FinalReport.CreateSubmitted(reportId, groupId, versionType, Timestamp);

        Assert.Equal(reportId, report.Id);
        Assert.Equal(groupId, report.GroupId);
        Assert.Equal(versionType, report.VersionType);
        Assert.Equal(1, report.CurrentVersionNo);
        Assert.Equal(FinalReportStatus.Submitted, report.Status);
        Assert.Equal(Timestamp, report.SubmittedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_report_rejects_empty_identifiers(bool emptyReportId)
    {
        Assert.Throws<ArgumentException>(() => FinalReport.CreateSubmitted(
            emptyReportId ? Guid.Empty : Guid.NewGuid(),
            emptyReportId ? Guid.NewGuid() : Guid.Empty,
            FinalReportVersionType.InitialFinal,
            Timestamp));
    }

    [Fact]
    public void A_report_rejects_an_unknown_version_type()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FinalReport.CreateSubmitted(
            Guid.NewGuid(), Guid.NewGuid(), (FinalReportVersionType)99, Timestamp));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void A_report_requires_a_utc_submission_time(DateTimeKind kind)
    {
        Assert.Throws<ArgumentException>(() => FinalReport.CreateSubmitted(
            Guid.NewGuid(), Guid.NewGuid(), FinalReportVersionType.InitialFinal,
            DateTime.SpecifyKind(Timestamp, kind)));
    }

    [Theory]
    [InlineData(FinalReportVersionType.InitialFinal)]
    [InlineData(FinalReportVersionType.RevisedAfterReviewer)]
    public void Resubmission_increments_the_sequence_without_changing_the_report_type(FinalReportVersionType versionType)
    {
        var report = FinalReport.CreateSubmitted(Guid.NewGuid(), Guid.NewGuid(), versionType, Timestamp);

        Assert.True(report.TrySubmitRevision(Timestamp.AddHours(1)));
        Assert.True(report.TrySubmitRevision(Timestamp.AddHours(2)));

        Assert.Equal(3, report.CurrentVersionNo);
        Assert.Equal(versionType, report.VersionType);
        Assert.Equal(Timestamp.AddHours(2), report.SubmittedAt);
        Assert.Equal(FinalReportStatus.Submitted, report.Status);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Invalid_revision_timestamps_leave_the_report_unchanged(DateTimeKind kind)
    {
        var report = CreateReport();

        Assert.False(report.TrySubmitRevision(DateTime.SpecifyKind(Timestamp.AddHours(1), kind)));

        Assert.Equal(1, report.CurrentVersionNo);
        Assert.Equal(Timestamp, report.SubmittedAt);
    }

    [Fact]
    public void A_revision_cannot_predate_the_current_submission()
    {
        var report = CreateReport();
        Assert.True(report.TrySubmitRevision(Timestamp.AddHours(1)));

        Assert.False(report.TrySubmitRevision(Timestamp));

        Assert.Equal(2, report.CurrentVersionNo);
        Assert.Equal(Timestamp.AddHours(1), report.SubmittedAt);
    }

    [Fact]
    public void Evaluation_locks_the_current_version_and_cannot_be_started_twice()
    {
        var report = CreateReport();
        Assert.True(report.TrySubmitRevision(Timestamp.AddHours(1)));

        Assert.True(report.TryBeginEvaluation());
        Assert.False(report.TryBeginEvaluation());
        Assert.False(report.TrySubmitRevision(Timestamp.AddHours(2)));

        Assert.Equal(FinalReportStatus.UnderEvaluation, report.Status);
        Assert.Equal(2, report.CurrentVersionNo);
        Assert.Equal(Timestamp.AddHours(1), report.SubmittedAt);
        Assert.Equal(FinalReportVersionType.InitialFinal, report.VersionType);
    }

    private static FinalReport CreateReport()
        => FinalReport.CreateSubmitted(Guid.NewGuid(), Guid.NewGuid(), FinalReportVersionType.InitialFinal, Timestamp);
}
