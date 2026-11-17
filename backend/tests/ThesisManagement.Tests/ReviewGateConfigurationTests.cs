using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ReviewGateConfigurationTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InitialSubmissionDeadline = CreatedAt.AddDays(1);
    private static readonly DateTime FacultyResponseDeadline = CreatedAt.AddDays(2);
    private static readonly DateTime FinalClosureDeadline = CreatedAt.AddDays(4);

    [Fact]
    public void Constructor_persists_a_valid_three_deadline_schedule()
    {
        var configuration = CreateConfiguration();

        Assert.Equal(ReviewGateType.Syllabus, configuration.GateType);
        Assert.Equal(InitialSubmissionDeadline, configuration.InitialSubmissionDeadlineUtc);
        Assert.Equal(FacultyResponseDeadline, configuration.FacultyResponseDeadlineUtc);
        Assert.Equal(FinalClosureDeadline, configuration.FinalClosureDeadlineUtc);
        Assert.Equal(CreatedAt, configuration.CreatedAtUtc);
        Assert.Equal(CreatedAt, configuration.UpdatedAtUtc);
    }

    [Fact]
    public void Constructor_rejects_deadlines_that_are_not_strictly_increasing()
    {
        Assert.Throws<ArgumentException>(() => new ReviewGateConfiguration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReviewGateType.Midterm,
            InitialSubmissionDeadline,
            InitialSubmissionDeadline,
            FinalClosureDeadline,
            Guid.NewGuid(),
            CreatedAt));
    }

    [Fact]
    public void UpdateDeadlines_rejects_non_utc_timestamps()
    {
        var configuration = CreateConfiguration();

        Assert.Throws<ArgumentException>(() => configuration.UpdateDeadlines(
            DateTime.SpecifyKind(InitialSubmissionDeadline, DateTimeKind.Local),
            FacultyResponseDeadline,
            FinalClosureDeadline,
            Guid.NewGuid(),
            CreatedAt.AddHours(1)));
    }

    [Fact]
    public void UpdateDeadlines_records_the_new_schedule_and_audit_metadata()
    {
        var configuration = CreateConfiguration();
        var updater = Guid.NewGuid();
        var updatedAt = CreatedAt.AddHours(1);
        var newInitial = InitialSubmissionDeadline.AddDays(7);
        var newResponse = FacultyResponseDeadline.AddDays(7);
        var newClosure = FinalClosureDeadline.AddDays(7);

        configuration.UpdateDeadlines(newInitial, newResponse, newClosure, updater, updatedAt);

        Assert.Equal(newInitial, configuration.InitialSubmissionDeadlineUtc);
        Assert.Equal(newResponse, configuration.FacultyResponseDeadlineUtc);
        Assert.Equal(newClosure, configuration.FinalClosureDeadlineUtc);
        Assert.Equal(updater, configuration.UpdatedBy);
        Assert.Equal(updatedAt, configuration.UpdatedAtUtc);
    }

    private static ReviewGateConfiguration CreateConfiguration()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReviewGateType.Syllabus,
            InitialSubmissionDeadline,
            FacultyResponseDeadline,
            FinalClosureDeadline,
            Guid.NewGuid(),
            CreatedAt);
}
