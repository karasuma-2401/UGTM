using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetTopicTrackingDetail;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class GetTopicTrackingDetailHandlerTests
{
    private static Topic CreateSampleTopic(Guid topicId, Guid advisorId) => new()
    {
        Id = topicId,
        BatchId = Guid.NewGuid(),
        AdvisorId = advisorId,
        TitleVi = "Nghiên cứu thị giác máy tính",
        TitleEn = "Computer Vision Research",
        Description = "Mô tả chi tiết",
        Requirements = "Yêu cầu",
        Status = TopicStatus.Approved,
        StudentNumber = 2,
        CreatedAt = DateTime.UtcNow
    };

    // ==========================================
    // 1. INPUT VALIDATION TESTS
    // ==========================================

    [Fact]
    public async Task GetTopicTrackingDetail_fails_when_topic_id_is_empty_guid()
    {
        var repo = new FakeTopicRepository();
        var reader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsLecturer();
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(Guid.Empty),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidInput.Code, result.Error.Code);
    }

    // ==========================================
    // 2. AUTHENTICATION & ROLE AUTHORIZATION TESTS
    // ==========================================

    [Fact]
    public async Task GetTopicTrackingDetail_fails_when_user_is_not_authenticated()
    {
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(CreateSampleTopic(topicId, advisorId));

        var reader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.Unauthenticated();
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(topicId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Faculty")]
    [InlineData("AcademicAffairs")]
    [InlineData("Guest")]
    [InlineData("RandomHacker")]
    public async Task GetTopicTrackingDetail_fails_when_user_has_unauthorized_role(string role)
    {
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(CreateSampleTopic(topicId, advisorId));

        var reader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsRole(role);
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(topicId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetTopicTrackingDetail_fails_when_lecturer_attempts_to_view_other_lecturers_topic()
    {
        var topicId = Guid.NewGuid();
        var topicAdvisorId = Guid.NewGuid();
        var currentLecturerId = Guid.NewGuid(); // Different from topicAdvisorId!

        var repo = new FakeTopicRepository();
        repo.Add(CreateSampleTopic(topicId, topicAdvisorId));

        var reader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsLecturer(currentLecturerId);
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(topicId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetTopicTrackingDetail_succeeds_when_lecturer_views_their_own_topic()
    {
        var topicId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var repo = new FakeTopicRepository();
        repo.Add(CreateSampleTopic(topicId, lecturerId));

        var expectedDetail = new TopicTrackingDetailDto(
            topicId,
            Guid.NewGuid(),
            lecturerId,
            "Nghiên cứu thị giác máy tính",
            "Computer Vision Research",
            "Mô tả chi tiết",
            "Yêu cầu",
            "Approved",
            2,
            DateTime.UtcNow,
            0,
            2,
            "Empty",
            false,
            true,
            null);

        var reader = new StubTopicQueryReader
        {
            GetTopicTrackingDetailResponse = expectedDetail
        };
        var currentUser = FakeCurrentUser.AsLecturer(lecturerId);
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(topicId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(topicId, result.Value.Id);
        Assert.Equal("Empty", result.Value.RegistrationStatus);
        Assert.False(result.Value.HasRegistration);
        Assert.Null(result.Value.Group);
    }

    [Fact]
    public async Task GetTopicTrackingDetail_admin_can_view_any_topic_bypassing_advisor_check()
    {
        var topicId = Guid.NewGuid();
        var anotherAdvisorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var repo = new FakeTopicRepository();
        repo.Add(CreateSampleTopic(topicId, anotherAdvisorId));

        var expectedDetail = new TopicTrackingDetailDto(
            topicId,
            Guid.NewGuid(),
            anotherAdvisorId,
            "Đề tài của GV khác",
            null,
            null,
            null,
            "Approved",
            2,
            DateTime.UtcNow,
            2,
            0,
            "Full",
            true,
            false,
            new TopicGroupDto(
                Guid.NewGuid(),
                "Approved",
                DateTime.UtcNow,
                [
                    new TopicStudentDto(Guid.NewGuid(), "SV001", "Nguyễn Văn A", "a@student.edu.vn", "0901234567", DateTime.UtcNow),
                    new TopicStudentDto(Guid.NewGuid(), "SV002", "Trần Thị B", "b@student.edu.vn", "0901234568", DateTime.UtcNow)
                ]));

        var reader = new StubTopicQueryReader
        {
            GetTopicTrackingDetailResponse = expectedDetail
        };
        var currentUser = FakeCurrentUser.AsAdmin(adminId);
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(topicId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(topicId, result.Value.Id);
        Assert.True(result.Value.HasRegistration);
        Assert.Equal(2, result.Value.RegisteredStudentsCount);
        Assert.NotNull(result.Value.Group);
        Assert.Equal(2, result.Value.Group.Students.Count);
    }

    [Fact]
    public async Task GetTopicTrackingDetail_council_member_can_view_any_topic_bypassing_advisor_check()
    {
        var topicId = Guid.NewGuid();
        var anotherAdvisorId = Guid.NewGuid();
        var councilId = Guid.NewGuid();

        var repo = new FakeTopicRepository();
        repo.Add(CreateSampleTopic(topicId, anotherAdvisorId));

        var expectedDetail = new TopicTrackingDetailDto(
            topicId,
            Guid.NewGuid(),
            anotherAdvisorId,
            "Đề tài độc lập",
            null,
            null,
            null,
            "Approved",
            2,
            DateTime.UtcNow,
            1,
            1,
            "Registered",
            true,
            true,
            null);

        var reader = new StubTopicQueryReader
        {
            GetTopicTrackingDetailResponse = expectedDetail
        };
        var currentUser = FakeCurrentUser.AsCouncilMember(councilId);
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(topicId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(topicId, result.Value.Id);
        Assert.Equal("Registered", result.Value.RegistrationStatus);
    }

    // ==========================================
    // 3. DATABASE NOT FOUND & MISSING DATA TESTS
    // ==========================================

    [Fact]
    public async Task GetTopicTrackingDetail_returns_not_found_when_topic_does_not_exist_in_db()
    {
        var nonExistentId = Guid.NewGuid();
        var repo = new FakeTopicRepository(); // Empty DB
        var reader = new StubTopicQueryReader();
        var currentUser = FakeCurrentUser.AsLecturer();
        var handler = new GetTopicTrackingDetailHandler(repo, reader, currentUser);

        var result = await handler.HandleAsync(
            new GetTopicTrackingDetailQuery(nonExistentId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(TopicErrors.TopicNotFound.Code, result.Error.Code);
    }
}
