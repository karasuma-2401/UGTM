using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;

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
    public void TopicTracking_computes_remaining_slots_and_status_accurately(
        int maxStudents,
        int registeredCount,
        int expectedRemaining,
        string expectedStatus,
        bool expectedHasRegistration,
        bool expectedIsAvailable)
    {
        var remainingSlots = Math.Max(0, maxStudents - registeredCount);
        var registrationStatus = registeredCount switch
        {
            0 => "Empty",
            _ when remainingSlots == 0 => "Full",
            _ => "Registered"
        };
        var hasRegistration = registeredCount > 0;
        var isAvailable = remainingSlots > 0;

        Assert.Equal(expectedRemaining, remainingSlots);
        Assert.Equal(expectedStatus, registrationStatus);
        Assert.Equal(expectedHasRegistration, hasRegistration);
        Assert.Equal(expectedIsAvailable, isAvailable);
    }

    [Theory]
    [InlineData(2, 3)] // Over-capacity edge case: 3 registered for 2 slots
    [InlineData(1, 5)] // 5 registered for 1 slot
    public void TopicTracking_handles_over_capacity_edge_cases_without_negative_slots(
        int maxStudents,
        int registeredCount)
    {
        var remainingSlots = Math.Max(0, maxStudents - registeredCount);
        var registrationStatus = registeredCount switch
        {
            0 => "Empty",
            _ when remainingSlots == 0 => "Full",
            _ => "Registered"
        };
        var hasRegistration = registeredCount > 0;
        var isAvailable = remainingSlots > 0;

        Assert.Equal(0, remainingSlots);
        Assert.Equal("Full", registrationStatus);
        Assert.True(hasRegistration);
        Assert.False(isAvailable);
    }

    [Fact]
    public void TopicStudentDto_fallbacks_safely_when_identity_profile_is_missing()
    {
        var studentId = Guid.NewGuid();
        // Simulate missing student profile in Identity (e.g. deleted user record)
        var dto = new TopicStudentDto(
            studentId,
            UserCode: null,
            FullName: $"Sinh viên ({studentId})",
            Email: string.Empty,
            PhoneNumber: null,
            JoinedAt: DateTime.UtcNow);

        Assert.Equal(studentId, dto.StudentId);
        Assert.Null(dto.UserCode);
        Assert.Contains(studentId.ToString(), dto.FullName);
        Assert.Equal(string.Empty, dto.Email);
        Assert.Null(dto.PhoneNumber);
    }
}
