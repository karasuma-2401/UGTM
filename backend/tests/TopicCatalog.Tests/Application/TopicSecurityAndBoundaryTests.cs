using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.PublishTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class TopicSecurityAndBoundaryTests
{
    private static readonly Guid ValidBatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherBatchId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // =========================================================================
    // 1. BOUNDARY VALUE ANALYSIS (BVA) - LENGTHS & NUMERIC LIMITS
    // =========================================================================

    [Fact]
    public async Task CreateTopic_TitleVi_exact_300_chars_succeeds_301_chars_fails()
    {
        var lecturerId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        // Exactly 300 characters -> Valid
        var exact300Title = new string('A', 300);
        var successResult = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            exact300Title,
            null,
            null,
            null,
            1));
        Assert.True(successResult.IsSuccess);
        Assert.Equal(300, successResult.Value.TitleVi.Length);

        // 301 characters -> Invalid
        var over301Title = new string('B', 301);
        var failResult = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            over301Title,
            null,
            null,
            null,
            1));
        Assert.False(failResult.IsSuccess);
        Assert.Equal(TopicErrors.InvalidTitleVi.Code, failResult.Error.Code);
    }

    [Fact]
    public async Task CreateTopic_TitleEn_exact_350_chars_succeeds_351_chars_fails()
    {
        var lecturerId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        // Exactly 350 characters -> Valid
        var exact350TitleEn = new string('E', 350);
        var successResult = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Tiêu đề tiếng Việt hợp lệ",
            exact350TitleEn,
            null,
            null,
            1));
        Assert.True(successResult.IsSuccess);
        Assert.Equal(350, successResult.Value.TitleEn?.Length);

        // 351 characters -> Invalid
        var over351TitleEn = new string('F', 351);
        var failResult = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Tiêu đề tiếng Việt khác",
            over351TitleEn,
            null,
            null,
            1));
        Assert.False(failResult.IsSuccess);
        Assert.Equal(TopicErrors.InvalidTitleEn.Code, failResult.Error.Code);
    }

    // =========================================================================
    // 2. EQUIVALENCE PARTITIONING & ABNORMAL DATA (SQLi, XSS, UNICODE, EMOJIS)
    // =========================================================================

    [Theory]
    [InlineData("Robert'); DROP TABLE topics.topic;--", "SQL Injection in TitleVi")]
    [InlineData("<script>alert('XSS Attack');</script>", "Cross Site Scripting Title")]
    [InlineData("🤖 Hệ thống AI hỗ trợ phát hiện sâu bệnh bằng Deep Learning 🌿🌾", "Emoji and Vietnamese Accents")]
    [InlineData("Đề tài với ký tự đặc biệt: & < > \" ' % _ / \\ ; --", "Special characters")]
    public async Task CreateTopic_safely_accepts_and_stores_special_characters_without_exception(string titleVi, string titleEn)
    {
        var lecturerId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            titleVi,
            titleEn,
            "Description with 'quotes' & symbols <tag>",
            "Requirements: C#, .NET 10, PostgreSQL",
            2));

        Assert.True(result.IsSuccess);
        Assert.Equal(titleVi.Trim(), result.Value.TitleVi);
        Assert.Equal(titleEn.Trim(), result.Value.TitleEn);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateTopic_converts_whitespace_only_optional_fields_to_null()
    {
        var lecturerId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Tiêu đề đề tài hợp lệ",
            "   ",      // whitespace TitleEn
            "\t\r\n",   // whitespace Description
            "  ",       // whitespace Requirements
            1));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.TitleEn);
        Assert.Null(result.Value.Description);
        Assert.Null(result.Value.Requirements);
    }

    // =========================================================================
    // 3. SECURITY & PRIVILEGE ENFORCEMENT (IDOR / BOLA, ROLE ESCALATION)
    // =========================================================================

    [Fact]
    public async Task Admin_can_create_topic_specifying_any_advisor_id()
    {
        var specifiedAdvisorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsAdmin(adminId));

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Đề tài do Admin tạo hộ giảng viên",
            null,
            null,
            null,
            2,
            SubmitImmediately: false,
            AdvisorId: specifiedAdvisorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(specifiedAdvisorId, result.Value.AdvisorId);
    }

    [Fact]
    public async Task Lecturer_cannot_spoof_advisor_id_when_creating_topic()
    {
        var lecturerId = Guid.NewGuid();
        var spoofedAdvisorId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Đề tài do Lecturer cố gắng gán cho người khác",
            null,
            null,
            null,
            1,
            SubmitImmediately: false,
            AdvisorId: spoofedAdvisorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(lecturerId, result.Value.AdvisorId); // Still owned by lecturerId!
    }

    [Fact]
    public async Task Admin_can_update_any_lecturers_draft_topic()
    {
        var lecturerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = lecturerId,
            TitleVi = "Đề tài gốc của GVHD",
            Status = TopicStatus.Draft,
            StudentNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new UpdateTopicHandler(repo, uow, FakeCurrentUser.AsAdmin(adminId));

        var result = await handler.HandleAsync(new UpdateTopicCommand(
            topicId,
            "Đề tài đã được Admin hỗ trợ cập nhật",
            null,
            null,
            null,
            2));

        Assert.True(result.IsSuccess);
        Assert.Equal("Đề tài đã được Admin hỗ trợ cập nhật", result.Value.TitleVi);
        Assert.Equal(2, result.Value.StudentNumber);
        Assert.Equal(lecturerId, result.Value.AdvisorId);
    }

    // =========================================================================
    // 4. REVIEW DECISION VALIDATION & CASE INSENSITIVITY
    // =========================================================================

    [Theory]
    [InlineData("ApPrOvEd", TopicStatus.Approved)]
    [InlineData("  APPROVED  ", TopicStatus.Approved)]
    [InlineData("needrevision", TopicStatus.NeedRevision)]
    [InlineData("  NeedRevision  ", TopicStatus.NeedRevision)]
    [InlineData("REJECTED", TopicStatus.Rejected)]
    [InlineData("  rejected  ", TopicStatus.Rejected)]
    public async Task ReviewTopic_handles_mixed_case_and_whitespace_correctly(string decision, TopicStatus expectedStatus)
    {
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài thẩm định",
            Status = TopicStatus.Submitted,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new ReviewTopicHandler(repo, uow, FakeCurrentUser.AsFaculty());

        var note = expectedStatus is TopicStatus.Approved ? null : "Lý do thẩm định hợp lệ";
        var result = await handler.HandleAsync(new ReviewTopicCommand(topicId, decision, note));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus.ToString(), result.Value.Status);
        Assert.Equal(note, result.Value.ReviewNote);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("Accepted")]
    [InlineData("Declined")]
    public async Task ReviewTopic_rejects_numeric_strings_and_unsupported_terms(string illegalDecision)
    {
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài thẩm định",
            Status = TopicStatus.Submitted,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new ReviewTopicHandler(repo, uow, FakeCurrentUser.AsFaculty());

        var result = await handler.HandleAsync(new ReviewTopicCommand(topicId, illegalDecision));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidReviewDecision.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("NeedRevision", null)]
    [InlineData("NeedRevision", "")]
    [InlineData("NeedRevision", "   ")]
    [InlineData("Rejected", null)]
    [InlineData("Rejected", "")]
    [InlineData("Rejected", "   ")]
    public async Task ReviewTopic_fails_when_note_is_missing_for_need_revision_or_rejected(
        string decision,
        string? emptyNote)
    {
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài thẩm định",
            Status = TopicStatus.Submitted,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new ReviewTopicHandler(repo, uow, FakeCurrentUser.AsFaculty());

        var result = await handler.HandleAsync(new ReviewTopicCommand(topicId, decision, emptyNote));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.ReviewNoteRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task ReviewTopic_note_exact_2000_chars_succeeds_2001_chars_fails()
    {
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài thẩm định biên độ dài note",
            Status = TopicStatus.Submitted,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new ReviewTopicHandler(repo, uow, FakeCurrentUser.AsFaculty());

        // 2001 chars -> Fails
        var overLengthNote = new string('N', 2001);
        var failResult = await handler.HandleAsync(new ReviewTopicCommand(topicId, "NeedRevision", overLengthNote));
        Assert.False(failResult.IsSuccess);
        Assert.Equal(TopicErrors.InvalidReviewNote.Code, failResult.Error.Code);

        // 2000 chars -> Succeeds
        var exact2000Note = new string('N', 2000);
        var successResult = await handler.HandleAsync(new ReviewTopicCommand(topicId, "NeedRevision", exact2000Note));
        Assert.True(successResult.IsSuccess);
        Assert.Equal(2000, successResult.Value.ReviewNote?.Length);
    }

    [Fact]
    public async Task ReviewTopic_trims_review_note_and_clears_previous_note_when_approved_without_note()
    {
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var existingTopic = new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài đã từng bị yêu cầu sửa",
            Status = TopicStatus.Submitted,
            ReviewNote = "Lý do cũ cần sửa",
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        };
        repo.Add(existingTopic);

        var uow = new FakeTopicUnitOfWork();
        var handler = new ReviewTopicHandler(repo, uow, FakeCurrentUser.AsFaculty());

        // Approved without note -> clears previous ReviewNote
        var approvedResult = await handler.HandleAsync(new ReviewTopicCommand(topicId, "Approved", null));
        Assert.True(approvedResult.IsSuccess);
        Assert.Null(approvedResult.Value.ReviewNote);
        Assert.Null(existingTopic.ReviewNote);

        // Reset to Submitted for testing approved WITH note
        existingTopic.Status = TopicStatus.Submitted;
        var approvedWithNote = await handler.HandleAsync(new ReviewTopicCommand(topicId, "Approved", "  Ghi chú khen ngợi  "));
        Assert.True(approvedWithNote.IsSuccess);
        Assert.Equal("Ghi chú khen ngợi", approvedWithNote.Value.ReviewNote);
        Assert.Equal("Ghi chú khen ngợi", existingTopic.ReviewNote);
    }

    [Fact]
    public async Task Domain_Topic_TryReview_validates_review_note_consistently()
    {
        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            Status = TopicStatus.Submitted,
            StudentNumber = 1
        };

        // Rejected without note -> false
        Assert.False(topic.TryReview(TopicStatus.Rejected, null));
        Assert.False(topic.TryReview(TopicStatus.Rejected, "   "));

        // NeedRevision without note -> false
        Assert.False(topic.TryReview(TopicStatus.NeedRevision, ""));

        // Rejected with note -> true, trimmed
        Assert.True(topic.TryReview(TopicStatus.Rejected, "  Trùng lặp đề tài  "));
        Assert.Equal("Trùng lặp đề tài", topic.ReviewNote);

        // Reset to Submitted
        topic.Status = TopicStatus.Submitted;
        // Approved with null -> clears note
        Assert.True(topic.TryReview(TopicStatus.Approved, null));
        Assert.Null(topic.ReviewNote);
    }

    // =========================================================================
    // 5. PUBLISH TOPICS EDGE CASES (DUPLICATES, CROSS-BATCH, EMPTY ARRAY)
    // =========================================================================

    [Fact]
    public async Task PublishTopics_deduplicates_requested_topic_ids()
    {
        var topicId1 = Guid.NewGuid();
        var topicId2 = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId1,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài duyệt 1",
            Status = TopicStatus.Approved,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });
        repo.Add(new Topic
        {
            Id = topicId2,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài duyệt 2",
            Status = TopicStatus.Approved,
            StudentNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new PublishTopicsHandler(repo, uow, batchReader, FakeCurrentUser.AsFaculty());

        // Pass duplicates in array [topicId1, topicId1, topicId2, topicId2]
        var result = await handler.HandleAsync(new PublishTopicsCommand(
            ValidBatchId,
            [topicId1, topicId1, topicId2, topicId2]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.PublishedCount);
        Assert.Contains(topicId1, result.Value.PublishedTopicIds);
        Assert.Contains(topicId2, result.Value.PublishedTopicIds);
    }

    [Fact]
    public async Task PublishTopics_fails_when_topic_belongs_to_different_batch()
    {
        var foreignBatchTopicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = foreignBatchTopicId,
            BatchId = OtherBatchId, // Different batch!
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài thuộc đợt khác",
            Status = TopicStatus.Approved,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId, OtherBatchId);
        var handler = new PublishTopicsHandler(repo, uow, batchReader, FakeCurrentUser.AsFaculty());

        var result = await handler.HandleAsync(new PublishTopicsCommand(
            ValidBatchId,
            [foreignBatchTopicId]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(TopicErrors.TopicNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task PublishTopics_fails_when_any_guid_in_list_is_empty()
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new PublishTopicsHandler(repo, uow, batchReader, FakeCurrentUser.AsFaculty());

        var result = await handler.HandleAsync(new PublishTopicsCommand(
            ValidBatchId,
            [Guid.NewGuid(), Guid.Empty]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact]
    public async Task PublishTopics_empty_list_acts_like_null_and_publishes_all_approved_topics_in_batch()
    {
        var approvedId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = approvedId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài đã duyệt",
            Status = TopicStatus.Approved,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });
        repo.Add(new Topic
        {
            Id = draftId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài nháp",
            Status = TopicStatus.Draft,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new PublishTopicsHandler(repo, uow, batchReader, FakeCurrentUser.AsFaculty());

        // Pass empty list []
        var result = await handler.HandleAsync(new PublishTopicsCommand(
            ValidBatchId,
            Array.Empty<Guid>()));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PublishedCount);
        Assert.Contains(approvedId, result.Value.PublishedTopicIds);
        Assert.DoesNotContain(draftId, result.Value.PublishedTopicIds);
    }
}
