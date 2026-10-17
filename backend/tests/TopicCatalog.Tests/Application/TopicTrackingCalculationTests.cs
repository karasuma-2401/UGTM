using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class TopicTrackingCalculationTests
{
    [Theory]
    [InlineData(2, 0, 2, "Empty", false, true)]
    [InlineData(2, 1, 1, "Registered", true, true)]
    [InlineData(2, 2, 0, "Full", true, false)]
    [InlineData(3, 1, 2, "Registered", true, true)]
    [InlineData(3, 3, 0, "Full", true, false)]
    [InlineData(1, 0, 1, "Empty", false, true)]
    [InlineData(1, 1, 0, "Full", true, false)]
    [InlineData(null, 0, 1, "Empty", false, true)]
    [InlineData(null, 1, 0, "Full", true, false)]
    public void ComputeRegistrationMetrics_computes_remaining_slots_and_status_accurately(
        int? maxStudents,
        int registeredCount,
        int expectedRemaining,
        string expectedStatus,
        bool expectedHasRegistration,
        bool expectedIsAvailable)
    {
        var metrics = TopicQueryReader.ComputeRegistrationMetrics(maxStudents, registeredCount);

        Assert.Equal(registeredCount, metrics.RegisteredCount);
        Assert.Equal(expectedRemaining, metrics.RemainingSlots);
        Assert.Equal(expectedStatus, metrics.RegistrationStatus);
        Assert.Equal(expectedHasRegistration, metrics.HasRegistration);
        Assert.Equal(expectedIsAvailable, metrics.IsAvailable);
    }

    [Theory]
    [InlineData(2, 3)] // Over-capacity edge case: 3 registered for 2 slots
    [InlineData(1, 5)] // 5 registered for 1 slot
    [InlineData(0, 1)] // Zero maxStudents corrupted data edge case
    [InlineData(-1, 0)] // Negative maxStudents corrupted data edge case
    public void ComputeRegistrationMetrics_handles_over_capacity_and_invalid_max_slots_safely(
        int maxStudents,
        int registeredCount)
    {
        var metrics = TopicQueryReader.ComputeRegistrationMetrics(maxStudents, registeredCount);

        Assert.True(metrics.RemainingSlots >= 0);
        if (registeredCount > 0 && metrics.RemainingSlots == 0)
        {
            Assert.Equal("Full", metrics.RegistrationStatus);
            Assert.True(metrics.HasRegistration);
            Assert.False(metrics.IsAvailable);
        }
    }

    [Theory]
    [InlineData("100% AI_Topic\\2026", @"100\% AI\_Topic\\2026")]
    [InlineData("%_\\", @"\%\_\\")]
    [InlineData("Normal Keyword", "Normal Keyword")]
    public void EscapeIlike_escapes_postgres_wildcards_and_backslashes(string raw, string expected)
    {
        var escaped = TopicQueryReader.EscapeIlike(raw);
        Assert.Equal(expected, escaped);
    }

    [Fact]
    public void BuildTrackingDetail_fallbacks_safely_when_identity_profile_is_missing()
    {
        var topicId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = topicId,
            BatchId = Guid.NewGuid(),
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài kiểm thử",
            Status = TopicStatus.Approved,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        };
        var registration = new TopicRegistrationContractDto(
            Guid.NewGuid(),
            topicId,
            "Registered",
            DateTime.UtcNow,
            [new TopicMemberContractDto(studentId, topic.BatchId, DateTime.UtcNow)]);

        // Empty student map simulates missing/deleted student profile in Identity module
        var emptyStudentsMap = new Dictionary<Guid, StudentInfoContractDto>();

        var detail = TopicQueryReader.BuildTrackingDetail(topic, registration, emptyStudentsMap);

        Assert.Equal("Registered", detail.RegistrationStatus);
        Assert.Equal(1, detail.RegisteredStudentsCount);
        Assert.Equal(1, detail.RemainingSlots);
        Assert.True(detail.HasRegistration);
        Assert.True(detail.IsAvailable);
        Assert.NotNull(detail.Group);
        var student = Assert.Single(detail.Group.Students);
        Assert.Equal(studentId, student.StudentId);
        Assert.Null(student.UserCode);
        Assert.Equal($"Sinh viên ({studentId})", student.FullName);
        Assert.Equal(string.Empty, student.Email);
        Assert.Null(student.PhoneNumber);
    }
}
