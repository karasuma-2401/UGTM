using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.ReviewSyllabus;

public sealed class ReviewSyllabusHandler(
    IStudentGroupRepository groupRepository,
    ISyllabusRepository syllabusRepository,
    IReviewGateConfigurationRepository gateConfigurationRepository,
    ISubmissionReviewRepository submissionReviewRepository,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IThesisNotificationOutbox? notificationOutbox = null,
    ITopicSummaryReader? topicSummaryReader = null)
{
    public async Task<Result<SubmissionReviewDto>> HandleAsync(
        ReviewSyllabusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!ThesisAuthorization.CanReviewGates(currentUser))
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.ReviewAccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || !Enum.TryParse<ReviewDecision>(command.Decision, ignoreCase: true, out var decision)
            || !Enum.IsDefined(decision))
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.GroupNotFound);
        }

        var batchId = group.Members.Select(member => (Guid?)member.BatchId).FirstOrDefault();
        if (!batchId.HasValue)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
            batchId.Value,
            ReviewGateType.Syllabus,
            cancellationToken);
        if (gateConfiguration is null)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.GateConfigurationNotFound);
        }

        var syllabus = await syllabusRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        if (syllabus is null)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.SubmissionNotFound);
        }

        if (syllabus.Status != SyllabusStatus.Submitted
            || group.Status != StudentGroupStatus.SyllabusSubmitted)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.FinalClosureDeadlinePassed);
        }

        if (syllabus.CurrentVersionNo == 1
            && occurredAt > gateConfiguration.FacultyResponseDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.FacultyResponseDeadlinePassed);
        }

        SubmissionReview review;
        try
        {
            syllabus.RecordReview(
                decision,
                command.Feedback,
                currentUser.UserId!.Value,
                occurredAt);
            review = SubmissionReview.Create(
                Guid.NewGuid(),
                ReviewGateType.Syllabus,
                syllabus.Id,
                syllabus.CurrentVersionNo,
                decision,
                command.Feedback,
                currentUser.UserId.Value,
                occurredAt);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidRequest);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        var groupTransitioned = decision switch
        {
            ReviewDecision.Approved => group.TryApproveSyllabus(),
            ReviewDecision.Rejected => group.TryStop(),
            ReviewDecision.NeedRevision => group.TryOpenSyllabusRevision(),
            _ => false
        };
        if (!groupTransitioned)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        submissionReviewRepository.Add(review);
        auditOutbox.Add(
            new AuditIntegrationEntry(
                "Syllabus",
                syllabus.Id.ToString(),
                IntegrationAuditActions.SyllabusReviewed,
                "Succeeded",
                null,
                new
                {
                    syllabus.GroupId,
                    syllabus.CurrentVersionNo,
                    Decision = decision.ToString(),
                    ResultingSyllabusStatus = syllabus.Status.ToString(),
                    ResultingGroupStatus = group.Status.ToString(),
                    FeedbackProvided = !string.IsNullOrWhiteSpace(command.Feedback)
                }),
            occurredAt);

        if (notificationOutbox is not null)
        {
            await EnqueueReviewNotificationsAsync(
                notificationOutbox,
                topicSummaryReader,
                group,
                batchId.Value,
                syllabus,
                decision,
                command.Feedback,
                occurredAt,
                cancellationToken);
        }

        var saveError = await unitOfWork.TrySaveChangesAsync(
            SyllabusSubmissionErrors.ConcurrencyConflict,
            SyllabusSubmissionErrors.ConcurrencyConflict,
            cancellationToken);
        return saveError is null
            ? Result.Success(SyllabusSubmissionMapper.ToDto(review))
            : Result.Failure<SubmissionReviewDto>(saveError);
    }

    private static async Task EnqueueReviewNotificationsAsync(
        IThesisNotificationOutbox notificationOutbox,
        ITopicSummaryReader? topicSummaryReader,
        StudentGroup group,
        Guid batchId,
        Syllabus syllabus,
        ReviewDecision decision,
        string? feedback,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        Guid? advisorId = null;
        string? topicTitle = null;
        if (topicSummaryReader is not null)
        {
            var topics = await topicSummaryReader.GetByIdsAsync([group.TopicId], cancellationToken);
            if (topics.TryGetValue(group.TopicId, out var topicSummary))
            {
                advisorId = topicSummary.AdvisorId;
                topicTitle = topicSummary.TitleVi;
            }
        }

        var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(group);
        var shortTopic = ThesisNotificationFormatting.Shorten(topicTitle, 65);

        if (decision == ReviewDecision.Rejected)
        {
            var metadata = new List<NotificationEmailMetaItem>
            {
                new("Giai đoạn dừng", "Cổng thẩm định 1 — Đề cương KLTN")
            };
            if (!string.IsNullOrWhiteSpace(topicTitle))
            {
                metadata.Add(new NotificationEmailMetaItem("Đề tài", topicTitle.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                metadata.Add(new NotificationEmailMetaItem("Lý do / Nhận xét", feedback.Trim()));
            }

            if (confirmedStudents.Count > 0)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "GROUP_STOPPED",
                        Title: "Thông báo dừng thực hiện đề tài KLTN",
                        Content: "Hồ sơ Đề cương không đạt Cổng thẩm định 1, nhóm bị dừng thực hiện đề tài trong đợt này.",
                        RecipientUserIds: confirmedStudents,
                        ResourceType: "StudentGroup",
                        ResourceId: group.Id,
                        AcademicBatchId: batchId,
                        ActionPath: "/thesis/proposal",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: "[UGTM] Thông báo dừng thực hiện đề tài KLTN (Cổng 1)",
                        EmailCategoryLabel: "Cổng thẩm định 1",
                        EmailCtaLabel: "Xem chi tiết thẩm định",
                        EmailNotice: "Vui lòng liên hệ Văn phòng Khoa hoặc GVHD nếu cần giải đáp quy chế học vụ.",
                        EmailMetadata: metadata),
                    occurredAtUtc);
            }

            if (advisorId.HasValue && advisorId.Value != Guid.Empty)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "GROUP_STOPPED",
                        Title: "Nhóm hướng dẫn bị dừng đề tài tại Cổng 1",
                        Content: $"Đề cương đề tài \"{shortTopic}\" không đạt Cổng 1, nhóm sinh viên đã dừng thực hiện.",
                        RecipientUserIds: [advisorId.Value],
                        ResourceType: "StudentGroup",
                        ResourceId: group.Id,
                        AcademicBatchId: batchId,
                        ActionPath: "/topics/advising",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Dừng thực hiện đề tài tại Cổng 1: {shortTopic}",
                        EmailCategoryLabel: "Cổng thẩm định 1",
                        EmailCtaLabel: "Xem nhóm hướng dẫn",
                        EmailMetadata: metadata),
                    occurredAtUtc);
            }

            return;
        }

        var (notifType, studentTitle, studentContent, advisorTitle, advisorContent) = decision == ReviewDecision.Approved
            ? (
                "SYLLABUS_APPROVED",
                "Đề cương KLTN đã được phê duyệt",
                "Đề cương của nhóm đã đạt Cổng thẩm định 1. Nhóm chính thức chuyển sang giai đoạn thực hiện.",
                "Đề cương nhóm hướng dẫn đã đạt",
                $"Đề cương đề tài \"{shortTopic}\" đã được Khoa phê duyệt qua Cổng 1.")
            : (
                "SYLLABUS_REVISION_REQUIRED",
                "Yêu cầu chỉnh sửa Đề cương KLTN",
                "Khoa yêu cầu nhóm chỉnh sửa Đề cương chi tiết. Vui lòng xem nhận xét và nộp lại đúng hạn.",
                "Nhóm hướng dẫn cần sửa lại Đề cương",
                $"Khoa yêu cầu chỉnh sửa Đề cương đề tài \"{shortTopic}\". Vui lòng hỗ trợ nhóm cập nhật.");

        if (confirmedStudents.Count > 0)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: notifType,
                    Title: studentTitle,
                    Content: studentContent,
                    RecipientUserIds: confirmedStudents,
                    ResourceType: "Syllabus",
                    ResourceId: syllabus.Id,
                    AcademicBatchId: batchId,
                    ActionPath: "/thesis/proposal",
                    Priority: "Normal",
                    EnqueuePush: true,
                    EnqueueEmail: false),
                occurredAtUtc);
        }

        if (advisorId.HasValue && advisorId.Value != Guid.Empty)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: notifType,
                    Title: advisorTitle,
                    Content: advisorContent,
                    RecipientUserIds: [advisorId.Value],
                    ResourceType: "Syllabus",
                    ResourceId: syllabus.Id,
                    AcademicBatchId: batchId,
                    ActionPath: "/topics/advising",
                    Priority: "Normal",
                    EnqueuePush: true,
                    EnqueueEmail: false),
                occurredAtUtc);
        }
    }
}
