using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class GetMyTopicsHandlerTests
{
    // ==========================================
    // 1. AUTHENTICATION & ROLE AUTHORIZATION TESTS
    // ==========================================

    [Fact]
    public async Task GetMyTopics_fails_when_user_is_not_authenticated()
    {
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.Unauthenticated();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, null, 1, 10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
        Assert.Null(stubReader.LastGetMyQuery);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("AcademicAffairs")]
    [InlineData("Guest")]
    [InlineData("Hacker")]
    [InlineData("Unknown")]
    public async Task GetMyTopics_fails_when_user_has_unauthorized_role(string unauthorizedRole)
    {
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsRole(unauthorizedRole);
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, null, 1, 10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
        Assert.Null(stubReader.LastGetMyQuery);
    }

    [Fact]
    public async Task GetMyTopics_lecturer_only_sees_their_own_topics()
    {
        var lecturerId = Guid.NewGuid();
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsLecturer(lecturerId);
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, null, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetMyQuery);
        Assert.Equal(lecturerId, stubReader.LastEffectiveAdvisorId);
        Assert.False(stubReader.LastIsPrivileged);
    }

    [Fact]
    public async Task GetMyTopics_lecturer_tampering_advisor_id_is_overridden_by_own_id()
    {
        var lecturerId = Guid.NewGuid();
        var otherLecturerId = Guid.NewGuid();
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsLecturer(lecturerId);
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        // Attempt to pass otherLecturerId to sneakily view their topics
        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, otherLecturerId, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetMyQuery);
        // Security check: Must enforce currentUser.UserId, ignoring otherLecturerId!
        Assert.Equal(lecturerId, stubReader.LastEffectiveAdvisorId);
        Assert.NotEqual(otherLecturerId, stubReader.LastEffectiveAdvisorId);
        Assert.False(stubReader.LastIsPrivileged);
    }

    [Fact]
    public async Task GetMyTopics_admin_can_view_all_topics_when_advisor_id_is_omitted()
    {
        var adminId = Guid.NewGuid();
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsAdmin(adminId);
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, null, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetMyQuery);
        Assert.Null(stubReader.LastEffectiveAdvisorId); // Can view all!
        Assert.True(stubReader.LastIsPrivileged);
    }

    [Fact]
    public async Task GetMyTopics_admin_can_filter_by_specific_advisor_id()
    {
        var adminId = Guid.NewGuid();
        var targetLecturerId = Guid.NewGuid();
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsAdmin(adminId);
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, targetLecturerId, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetMyQuery);
        Assert.Equal(targetLecturerId, stubReader.LastEffectiveAdvisorId);
        Assert.True(stubReader.LastIsPrivileged);
    }

    [Fact]
    public async Task GetMyTopics_faculty_can_view_all_and_filter_by_advisor_id_for_review()
    {
        var facultyId = Guid.NewGuid();
        var targetLecturerId = Guid.NewGuid();
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsFaculty(facultyId);
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var resultAll = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, "Submitted", null, null, 1, 10),
            CancellationToken.None);

        Assert.True(resultAll.IsSuccess);
        Assert.Null(stubReader.LastEffectiveAdvisorId);
        Assert.True(stubReader.LastIsPrivileged);

        var resultFiltered = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, "Approved", null, targetLecturerId, 1, 10),
            CancellationToken.None);

        Assert.True(resultFiltered.IsSuccess);
        Assert.Equal(targetLecturerId, stubReader.LastEffectiveAdvisorId);
        Assert.True(stubReader.LastIsPrivileged);
    }

    [Fact]
    public async Task GetMyTopics_council_member_can_view_all_and_filter_by_advisor_id()
    {
        var councilId = Guid.NewGuid();
        var targetLecturerId = Guid.NewGuid();
        var stubReader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsCouncilMember(councilId);
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var resultAll = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, null, 1, 10),
            CancellationToken.None);

        Assert.True(resultAll.IsSuccess);
        Assert.Null(stubReader.LastEffectiveAdvisorId);
        Assert.True(stubReader.LastIsPrivileged);

        var resultFiltered = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, targetLecturerId, 1, 10),
            CancellationToken.None);

        Assert.True(resultFiltered.IsSuccess);
        Assert.Equal(targetLecturerId, stubReader.LastEffectiveAdvisorId);
        Assert.True(stubReader.LastIsPrivileged);
    }

    // ==========================================
    // 2. INPUT VALIDATION & FILTER EDGE CASES
    // ==========================================

    [Theory]
    [InlineData("InvalidStatus")]
    [InlineData("999")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("Occupied")]
    [InlineData("Available")]
    [InlineData("Partial")]
    [InlineData("!@#$%")]
    public async Task GetMyTopics_fails_when_registration_status_filter_is_invalid(string invalidStatus)
    {
        var currentUser = FakeCurrentUser.AsLecturer();
        var stubReader = new StubTopicQueryReader();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, invalidStatus, null, 1, 10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidRegistrationStatusFilter.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("Empty")]
    [InlineData("empty")]
    [InlineData("Registered")]
    [InlineData("registered")]
    [InlineData("Full")]
    [InlineData("full")]
    [InlineData(" FULL ")]
    public async Task GetMyTopics_accepts_valid_registration_status_case_insensitively(string validStatus)
    {
        var currentUser = FakeCurrentUser.AsLecturer();
        var stubReader = new StubTopicQueryReader();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, validStatus, null, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetMyQuery);
        Assert.Equal(validStatus.Trim(), stubReader.LastGetMyQuery.RegistrationStatus);
    }

    [Theory]
    [InlineData("InvalidTopicStatus")]
    [InlineData("Deleted")]
    [InlineData("Terminated")]
    [InlineData("Cancelled")]
    [InlineData("Pending")]
    [InlineData("999")]
    [InlineData("Unknown")]
    [InlineData("null")]
    public async Task GetMyTopics_fails_when_topic_status_is_invalid(string invalidStatus)
    {
        var currentUser = FakeCurrentUser.AsLecturer();
        var stubReader = new StubTopicQueryReader();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, invalidStatus, null, null, 1, 10),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidTopicStatusFilter.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("draft")]
    [InlineData("Submitted")]
    [InlineData("NeedRevision")]
    [InlineData("Approved")]
    [InlineData("approved")]
    [InlineData("Rejected")]
    [InlineData("Published")]
    [InlineData("Closed")]
    [InlineData("Archived")]
    [InlineData("archived")]
    [InlineData(" APPROVED ")]
    public async Task GetMyTopics_accepts_valid_topic_status_case_insensitively(string validStatus)
    {
        var currentUser = FakeCurrentUser.AsLecturer();
        var stubReader = new StubTopicQueryReader();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, validStatus, null, null, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetMyQuery);
        Assert.Equal(validStatus.Trim(), stubReader.LastGetMyQuery.Status);
    }

    // ==========================================
    // 3. PAGINATION & BOUNDARY SANITIZATION
    // ==========================================

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-100, 1)]
    public async Task GetMyTopics_sanitizes_page_number_to_minimum_one(int rawPage, int expectedPage)
    {
        var currentUser = FakeCurrentUser.AsLecturer();
        var stubReader = new StubTopicQueryReader();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, null, rawPage, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPage, stubReader.LastGetMyQuery!.PageNumber);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-10, 10)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    public async Task GetMyTopics_clamps_page_size_between_1_and_100(int rawSize, int expectedSize)
    {
        var currentUser = FakeCurrentUser.AsLecturer();
        var stubReader = new StubTopicQueryReader();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(null, null, null, null, null, 1, rawSize),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedSize, stubReader.LastGetMyQuery!.PageSize);
    }

    // ==========================================
    // 4. SPECIAL CHARACTERS & SQL INJECTION TESTS
    // ==========================================

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    [InlineData("100% Guaranteed Topic")]
    [InlineData("topic_name_v2")]
    [InlineData("' OR '1'='1")]
    [InlineData("Special!@#$%^&*()_+{}[]|\":;?/>.<,~`")]
    [InlineData("Ứng dụng Trí tuệ nhân tạo (AI) trong Y tế 🏥")]
    public async Task GetMyTopics_handles_special_characters_safely(string specialKeyword)
    {
        var currentUser = FakeCurrentUser.AsLecturer();
        var stubReader = new StubTopicQueryReader();
        var handler = new GetMyTopicsHandler(stubReader, currentUser);

        var result = await handler.HandleAsync(
            new GetMyTopicsQuery(specialKeyword, null, null, null, null, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(specialKeyword, stubReader.LastGetMyQuery!.Keyword);
    }
}
