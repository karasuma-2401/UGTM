using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.PublishTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class TopicProposalAndReviewTests
{
    private static readonly Guid ValidBatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherBatchId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ==========================================
    // 1. CREATE TOPIC TESTS (GVHD TẠO ĐỀ TÀI)
    // ==========================================

    [Fact]
    public async Task CreateTopic_fails_when_unauthenticated()
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.Unauthenticated());

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Xây dựng hệ thống quản lý khóa luận",
            "Thesis Management System",
            "Mục tiêu",
            "Yêu cầu",
            2));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
        Assert.Equal(0, uow.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Faculty")]
    [InlineData("CouncilMember")]
    [InlineData("AcademicAffairs")]
    [InlineData("Guest")]
    public async Task CreateTopic_fails_for_non_lecturer_and_non_admin_roles(string role)
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsRole(role));

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Xây dựng hệ thống AI",
            null,
            null,
            null,
            1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
        Assert.Equal(0, uow.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(100)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task CreateTopic_fails_when_student_number_is_not_1_or_2(int? invalidStudentNumber)
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer());

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Đề tài kiểm tra chỉ tiêu sinh viên",
            null,
            null,
            null,
            invalidStudentNumber));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidStudentNumber.Code, result.Error.Code);
        Assert.Equal(0, uow.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CreateTopic_succeeds_when_student_number_is_1_or_2(int validStudentNumber)
    {
        var lecturerId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "  Xây dựng ứng dụng học sâu  ",
            "  Deep Learning Application  ",
            "  Nghiên cứu mô hình CNN  ",
            "  Python, PyTorch  ",
            validStudentNumber,
            SubmitImmediately: false));

        Assert.True(result.IsSuccess);
        Assert.Equal(lecturerId, result.Value.AdvisorId);
        Assert.Equal("Xây dựng ứng dụng học sâu", result.Value.TitleVi);
        Assert.Equal("Deep Learning Application", result.Value.TitleEn);
        Assert.Equal("Nghiên cứu mô hình CNN", result.Value.Description);
        Assert.Equal("Python, PyTorch", result.Value.Requirements);
        Assert.Equal(validStudentNumber, result.Value.StudentNumber);
        Assert.Equal("Draft", result.Value.Status);
        Assert.False(result.Value.IsPublished);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task CreateTopic_fails_when_title_vi_is_empty_or_whitespace(string emptyTitle)
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer());

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            emptyTitle,
            null,
            null,
            null,
            2));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidTitleVi.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateTopic_fails_when_title_vi_exceeds_300_chars_or_title_en_exceeds_350_chars()
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer());

        var overLengthVi = new string('A', 301);
        var resultVi = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            overLengthVi,
            null,
            null,
            null,
            2));

        Assert.False(resultVi.IsSuccess);
        Assert.Equal(TopicErrors.InvalidTitleVi.Code, resultVi.Error.Code);

        var overLengthEn = new string('B', 351);
        var resultEn = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Tên tiếng Việt hợp lệ",
            overLengthEn,
            null,
            null,
            2));

        Assert.False(resultEn.IsSuccess);
        Assert.Equal(TopicErrors.InvalidTitleEn.Code, resultEn.Error.Code);
    }

    [Fact]
    public async Task CreateTopic_fails_when_batch_id_is_empty_or_not_in_db()
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer());

        var emptyBatchResult = await handler.HandleAsync(new CreateTopicCommand(
            Guid.Empty,
            "Tên đề tài",
            null,
            null,
            null,
            2));

        Assert.False(emptyBatchResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, emptyBatchResult.Error.Type);
        Assert.Equal(TopicErrors.InvalidBatchId.Code, emptyBatchResult.Error.Code);

        var missingBatchResult = await handler.HandleAsync(new CreateTopicCommand(
            Guid.NewGuid(),
            "Tên đề tài",
            null,
            null,
            null,
            2));

        Assert.False(missingBatchResult.IsSuccess);
        Assert.Equal(ErrorType.NotFound, missingBatchResult.Error.Type);
        Assert.Equal(TopicErrors.BatchNotFound.Code, missingBatchResult.Error.Code);
    }

    [Fact]
    public async Task CreateTopic_fails_when_duplicate_title_vi_exists_for_same_advisor_in_same_batch()
    {
        var lecturerId = Guid.NewGuid();
        var repo = TopicRepository.CreateInMemory([]);
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId, OtherBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        var first = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Hệ thống phát hiện gian lận thi cử",
            null,
            null,
            null,
            2));
        Assert.True(first.IsSuccess);

        // Duplicate with different casing and extra spaces in same batch -> Conflict 409
        var duplicate = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "  hệ thống phát hiện gian lận thi cử  ",
            null,
            null,
            null,
            1));
        Assert.False(duplicate.IsSuccess);
        Assert.Equal(ErrorType.Conflict, duplicate.Error.Type);
        Assert.Equal(TopicErrors.DuplicateTitle.Code, duplicate.Error.Code);

        // Same title in a DIFFERENT batch -> Allowed
        var otherBatch = await handler.HandleAsync(new CreateTopicCommand(
            OtherBatchId,
            "Hệ thống phát hiện gian lận thi cử",
            null,
            null,
            null,
            2));
        Assert.True(otherBatch.IsSuccess);
    }

    [Fact]
    public async Task CreateTopic_lecturer_cannot_spoof_other_advisor_id_and_submit_immediately_sets_submitted_status()
    {
        var lecturerId = Guid.NewGuid();
        var otherAdvisorId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new CreateTopicHandler(repo, uow, batchReader, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Đề tài gửi duyệt ngay",
            "Immediate Submit Topic",
            "Mục tiêu",
            "C#, .NET 10, PostgreSQL",
            2,
            SubmitImmediately: true,
            AdvisorId: otherAdvisorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(lecturerId, result.Value.AdvisorId);
        Assert.Equal("Submitted", result.Value.Status);
        Assert.False(result.Value.IsPublished);
    }

    // ==========================================
    // 2. UPDATE TOPIC TESTS (GVHD SỬA ĐỀ TÀI)
    // ==========================================

    [Theory]
    [InlineData(TopicStatus.Draft)]
    [InlineData(TopicStatus.NeedRevision)]
    public async Task UpdateTopic_succeeds_when_topic_is_in_draft_or_need_revision(TopicStatus editableStatus)
    {
        var lecturerId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = lecturerId,
            TitleVi = "Tên cũ",
            Status = editableStatus,
            StudentNumber = 1,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new UpdateTopicHandler(repo, uow, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new UpdateTopicCommand(
            topicId,
            "Tên mới sau khi chỉnh sửa",
            "Updated English Title",
            "Mục tiêu mới",
            "Yêu cầu mới",
            2,
            SubmitImmediately: false));

        Assert.True(result.IsSuccess);
        Assert.Equal("Tên mới sau khi chỉnh sửa", result.Value.TitleVi);
        Assert.Equal("Updated English Title", result.Value.TitleEn);
        Assert.Equal(2, result.Value.StudentNumber);
        Assert.Equal(editableStatus.ToString(), result.Value.Status);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(TopicStatus.Submitted)]
    [InlineData(TopicStatus.Approved)]
    [InlineData(TopicStatus.Rejected)]
    [InlineData(TopicStatus.Published)]
    [InlineData(TopicStatus.Closed)]
    [InlineData(TopicStatus.Archived)]
    public async Task UpdateTopic_fails_when_topic_is_not_in_draft_or_need_revision(TopicStatus lockedStatus)
    {
        var lecturerId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = lecturerId,
            TitleVi = "Đề tài đã khóa chỉnh sửa",
            Status = lockedStatus,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new UpdateTopicHandler(repo, uow, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new UpdateTopicCommand(
            topicId,
            "Cố tình sửa đề tài đã gửi/duyệt",
            null,
            null,
            null,
            1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidStatusTransition.Code, result.Error.Code);
        Assert.Equal(0, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateTopic_fails_when_lecturer_tries_to_edit_another_lecturers_topic()
    {
        var ownerLecturerId = Guid.NewGuid();
        var attackerLecturerId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = ownerLecturerId,
            TitleVi = "Đề tài của giảng viên A",
            Status = TopicStatus.Draft,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new UpdateTopicHandler(repo, uow, FakeCurrentUser.AsLecturer(attackerLecturerId));

        var result = await handler.HandleAsync(new UpdateTopicCommand(
            topicId,
            "Giảng viên B cố tình sửa",
            null,
            null,
            null,
            2));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
    }

    // ==========================================
    // 3. SUBMIT TOPIC TESTS (GVHD GỬI DUYỆT)
    // ==========================================

    [Theory]
    [InlineData(TopicStatus.Draft)]
    [InlineData(TopicStatus.NeedRevision)]
    public async Task SubmitTopic_transitions_draft_or_need_revision_to_submitted(TopicStatus initialStatus)
    {
        var lecturerId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = lecturerId,
            TitleVi = "Đề tài chuẩn bị gửi duyệt",
            Status = initialStatus,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new SubmitTopicHandler(repo, uow, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new SubmitTopicCommand(topicId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Submitted", result.Value.Status);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task SubmitTopic_fails_when_legacy_draft_has_null_or_invalid_student_number()
    {
        var lecturerId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = lecturerId,
            TitleVi = "Đề tài nháp chưa điền chỉ tiêu",
            Status = TopicStatus.Draft,
            StudentNumber = null,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new SubmitTopicHandler(repo, uow, FakeCurrentUser.AsLecturer(lecturerId));

        var result = await handler.HandleAsync(new SubmitTopicCommand(topicId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidStudentNumber.Code, result.Error.Code);
    }

    // ==========================================
    // 4. REVIEW TOPIC TESTS (VĂN PHÒNG KHOA THẨM ĐỊNH)
    // ==========================================

    [Theory]
    [InlineData("Lecturer")]
    [InlineData("Student")]
    [InlineData("CouncilMember")]
    [InlineData("AcademicAffairs")]
    public async Task ReviewTopic_fails_for_non_faculty_roles_including_lecturer(string unauthorizedRole)
    {
        var advisorId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = advisorId,
            TitleVi = "Đề tài chờ Khoa duyệt",
            Status = TopicStatus.Submitted,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new ReviewTopicHandler(repo, uow, FakeCurrentUser.AsRole(unauthorizedRole, advisorId));

        var result = await handler.HandleAsync(new ReviewTopicCommand(topicId, "Approved"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
        Assert.Equal(0, uow.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("Approved", TopicStatus.Approved)]
    [InlineData("approved", TopicStatus.Approved)]
    [InlineData("NeedRevision", TopicStatus.NeedRevision)]
    [InlineData("needrevision", TopicStatus.NeedRevision)]
    [InlineData("Rejected", TopicStatus.Rejected)]
    [InlineData(" REJECTED ", TopicStatus.Rejected)]
    public async Task ReviewTopic_faculty_can_approve_request_revision_or_reject_submitted_topic(
        string decisionInput,
        TopicStatus expectedStatus)
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

        var reviewNote = expectedStatus is TopicStatus.Approved ? null : "Lý do thẩm định của Khoa";
        var result = await handler.HandleAsync(new ReviewTopicCommand(topicId, decisionInput, reviewNote));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus.ToString(), result.Value.Status);
        Assert.False(result.Value.IsPublished);
        Assert.Equal(reviewNote, result.Value.ReviewNote);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    [InlineData("Published")]
    [InlineData("Closed")]
    [InlineData("Archived")]
    [InlineData("3")]
    [InlineData("999")]
    [InlineData("InvalidDecision")]
    public async Task ReviewTopic_fails_when_decision_is_invalid(string invalidDecision)
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

        var result = await handler.HandleAsync(new ReviewTopicCommand(topicId, invalidDecision));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidReviewDecision.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(TopicStatus.Draft)]
    [InlineData(TopicStatus.NeedRevision)]
    [InlineData(TopicStatus.Approved)]
    [InlineData(TopicStatus.Rejected)]
    [InlineData(TopicStatus.Published)]
    public async Task ReviewTopic_fails_when_topic_is_not_in_submitted_status(TopicStatus nonSubmittedStatus)
    {
        var topicId = Guid.NewGuid();
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = topicId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài không ở trạng thái Submitted",
            Status = nonSubmittedStatus,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var handler = new ReviewTopicHandler(repo, uow, FakeCurrentUser.AsFaculty());

        var result = await handler.HandleAsync(new ReviewTopicCommand(topicId, "Approved"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(TopicErrors.InvalidStatusTransition.Code, result.Error.Code);
    }

    // ==========================================
    // 5. PUBLISH TOPICS & END-TO-END WORKFLOW TESTS
    // ==========================================

    [Theory]
    [InlineData("Lecturer")]
    [InlineData("Student")]
    [InlineData("CouncilMember")]
    [InlineData("AcademicAffairs")]
    public async Task PublishTopics_fails_for_non_faculty_roles(string unauthorizedRole)
    {
        var repo = new FakeTopicRepository();
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new PublishTopicsHandler(repo, uow, batchReader, FakeCurrentUser.AsRole(unauthorizedRole));

        var result = await handler.HandleAsync(new PublishTopicsCommand(ValidBatchId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TopicErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task PublishTopics_fails_when_no_approved_topics_exist_in_batch()
    {
        var repo = new FakeTopicRepository();
        repo.Add(new Topic
        {
            Id = Guid.NewGuid(),
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài mới đang chờ duyệt",
            Status = TopicStatus.Submitted,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new PublishTopicsHandler(repo, uow, batchReader, FakeCurrentUser.AsFaculty());

        var result = await handler.HandleAsync(new PublishTopicsCommand(ValidBatchId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TopicErrors.NoApprovedTopicsToPublish.Code, result.Error.Code);
    }

    [Fact]
    public async Task PublishTopics_specific_topic_ids_fails_if_any_topic_is_not_approved_or_not_in_batch()
    {
        var approvedId = Guid.NewGuid();
        var submittedId = Guid.NewGuid();
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
            Id = submittedId,
            BatchId = ValidBatchId,
            AdvisorId = Guid.NewGuid(),
            TitleVi = "Đề tài chưa duyệt",
            Status = TopicStatus.Submitted,
            StudentNumber = 2,
            CreatedAt = DateTime.UtcNow
        });

        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);
        var handler = new PublishTopicsHandler(repo, uow, batchReader, FakeCurrentUser.AsFaculty());

        var invalidStateResult = await handler.HandleAsync(
            new PublishTopicsCommand(ValidBatchId, [approvedId, submittedId]));

        Assert.False(invalidStateResult.IsSuccess);
        Assert.Equal(ErrorType.Conflict, invalidStateResult.Error.Type);
        Assert.Equal(TopicErrors.InvalidStatusTransition.Code, invalidStateResult.Error.Code);

        var missingIdResult = await handler.HandleAsync(
            new PublishTopicsCommand(ValidBatchId, [approvedId, Guid.NewGuid()]));

        Assert.False(missingIdResult.IsSuccess);
        Assert.Equal(ErrorType.NotFound, missingIdResult.Error.Type);
        Assert.Equal(TopicErrors.TopicNotFound.Code, missingIdResult.Error.Code);
    }

    [Fact]
    public async Task FullLifecycle_Draft_Submit_NeedRevision_Update_Resubmit_Approve_Publish_And_StudentSearch()
    {
        var lecturerId = Guid.NewGuid();
        var topicsStore = new List<Topic>();
        var repo = TopicRepository.CreateInMemory(topicsStore);
        var uow = new FakeTopicUnitOfWork();
        var batchReader = new FakeKltnBatchReader(ValidBatchId);

        var lecturerUser = FakeCurrentUser.AsLecturer(lecturerId);
        var facultyUser = FakeCurrentUser.AsFaculty();

        var createHandler = new CreateTopicHandler(repo, uow, batchReader, lecturerUser);
        var submitHandler = new SubmitTopicHandler(repo, uow, lecturerUser);
        var reviewHandler = new ReviewTopicHandler(repo, uow, facultyUser);
        var updateHandler = new UpdateTopicHandler(repo, uow, lecturerUser);
        var publishHandler = new PublishTopicsHandler(repo, uow, batchReader, facultyUser);

        // Step 1: Lecturer creates a Draft topic
        var created = await createHandler.HandleAsync(new CreateTopicCommand(
            ValidBatchId,
            "Nghiên cứu mô hình ngôn ngữ lớn tiếng Việt",
            "Vietnamese Large Language Model Research",
            "Xây dựng mô hình LLM hỗ trợ hỏi đáp giáo dục",
            "Python, PyTorch, HuggingFace",
            2,
            SubmitImmediately: false));
        Assert.True(created.IsSuccess);
        Assert.Equal("Draft", created.Value.Status);
        Assert.False(created.Value.IsPublished);
        var topicId = created.Value.Id;

        // Step 2: Lecturer submits the topic -> Submitted
        var submitted = await submitHandler.HandleAsync(new SubmitTopicCommand(topicId));
        Assert.True(submitted.IsSuccess);
        Assert.Equal("Submitted", submitted.Value.Status);

        // Step 3: Faculty reviews and requests revision -> NeedRevision
        var needRevision = await reviewHandler.HandleAsync(new ReviewTopicCommand(topicId, "NeedRevision", "Cần bổ sung mục tiêu chi tiết hơn"));
        Assert.True(needRevision.IsSuccess);
        Assert.Equal("NeedRevision", needRevision.Value.Status);
        Assert.Equal("Cần bổ sung mục tiêu chi tiết hơn", needRevision.Value.ReviewNote);

        // Step 4: Lecturer updates the topic and immediately resubmits -> Submitted
        var updatedAndResubmitted = await updateHandler.HandleAsync(new UpdateTopicCommand(
            topicId,
            "Nghiên cứu và tối ưu hóa mô hình ngôn ngữ lớn tiếng Việt",
            "Vietnamese LLM Optimization Research",
            "Mục tiêu đã bổ sung theo yêu cầu của Khoa",
            "Python, PyTorch, vLLM",
            2,
            SubmitImmediately: true));
        Assert.True(updatedAndResubmitted.IsSuccess);
        Assert.Equal("Submitted", updatedAndResubmitted.Value.Status);
        Assert.Equal("Cần bổ sung mục tiêu chi tiết hơn", updatedAndResubmitted.Value.ReviewNote);

        // Step 5: Faculty approves the topic -> Approved (not yet published! Previous note is cleared)
        var approved = await reviewHandler.HandleAsync(new ReviewTopicCommand(topicId, "Approved"));
        Assert.True(approved.IsSuccess);
        Assert.Equal("Approved", approved.Value.Status);
        Assert.False(approved.Value.IsPublished);
        Assert.Null(approved.Value.ReviewNote);

        // Before Publish: Students searching GetAllTopicsAsync MUST NOT see the topic yet!
        var queryReaderBeforePublish = TopicQueryReader.CreateInMemory(
            topicsStore,
            new EmptyRegistrationContract(),
            new EmptyStudentInfoReader());
        var studentViewBefore = await queryReaderBeforePublish.GetAllTopicsAsync(
            new GetAllTopicsQuery(null, ValidBatchId, 1, 10));
        Assert.Empty(studentViewBefore.Items);

        // Step 6: Faculty clicks "Công bố danh mục đề tài" -> Published (IsPublished = true)
        var published = await publishHandler.HandleAsync(new PublishTopicsCommand(ValidBatchId));
        Assert.True(published.IsSuccess);
        Assert.Equal(1, published.Value.PublishedCount);
        Assert.Contains(topicId, published.Value.PublishedTopicIds);

        // Step 7: Students searching GetAllTopicsAsync NOW see the published topic with IsPublished = true!
        var queryReaderAfterPublish = TopicQueryReader.CreateInMemory(
            topicsStore,
            new EmptyRegistrationContract(),
            new EmptyStudentInfoReader());
        var studentViewAfter = await queryReaderAfterPublish.GetAllTopicsAsync(
            new GetAllTopicsQuery(null, ValidBatchId, 1, 10));
        var visibleTopic = Assert.Single(studentViewAfter.Items);
        Assert.Equal(topicId, visibleTopic.Id);
        Assert.Equal("Published", visibleTopic.Status);
        Assert.True(visibleTopic.IsPublished);
    }

    private sealed class EmptyRegistrationContract : IThesisRegistrationContract
    {
        public Task<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>> GetRegistrationsByTopicIdsAsync(
            IEnumerable<Guid> topicIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>>(
                new Dictionary<Guid, TopicRegistrationContractDto>());

        public Task<TopicRegistrationContractDto?> GetRegistrationByTopicIdAsync(
            Guid topicId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TopicRegistrationContractDto?>(null);
    }

    private sealed class EmptyStudentInfoReader : IStudentInfoReader
    {
        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(
            IEnumerable<Guid> studentIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(
                new Dictionary<Guid, StudentInfoContractDto>());

        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(
            Guid? studentId,
            string? userCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<StudentInfoContractDto?>(null);
    }
}
