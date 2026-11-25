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

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.ReviewMidtermReport;

public sealed class ReviewMidtermReportHandler(
    IStudentGroupRepository groupRepository,
    IMidtermReportRepository midtermReportRepository,
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
        ReviewMidtermReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!ThesisAuthorization.CanReviewGates(currentUser))
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.ReviewAccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || !Enum.TryParse<ReviewDecision>(command.Decision, ignoreCase: true, out var decision)
            || !Enum.IsDefined(decision))
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.GroupNotFound);
        }

        var batchId = group.Members.Select(member => (Guid?)member.BatchId).FirstOrDefault();
        if (!batchId.HasValue)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
            batchId.Value,
            ReviewGateType.Midterm,
            cancellationToken);
        if (gateConfiguration is null)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.GateConfigurationNotFound);
        }

        var report = await midtermReportRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.SubmissionNotFound);
        }

        if (report.Status != MidtermReportStatus.Submitted
            || group.Status != StudentGroupStatus.MidtermSubmitted)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.FinalClosureDeadlinePassed);
        }

        if (report.CurrentVersionNo == 1
            && occurredAt > gateConfiguration.FacultyResponseDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.FacultyResponseDeadlinePassed);
        }

        SubmissionReview review;
        try
        {
            report.RecordReview(
                decision,
                command.Feedback,
                currentUser.UserId!.Value,
                occurredAt);
            review = SubmissionReview.Create(
                Guid.NewGuid(),
                ReviewGateType.Midterm,
                report.Id,
                report.CurrentVersionNo,
                decision,
                command.Feedback,
                currentUser.UserId.Value,
                occurredAt);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidRequest);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        var groupTransitioned = decision switch
        {
            ReviewDecision.Approved => group.TryApproveMidtermReport(),
            ReviewDecision.Rejected => group.TryStop(),
            ReviewDecision.NeedRevision => group.TryOpenMidtermReportRevision(),
            _ => false
        };
        if (!groupTransitioned)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        submissionReviewRepository.Add(review);
        auditOutbox.Add(
            new AuditIntegrationEntry(
                "MidtermReport",
                report.Id.ToString(),
                IntegrationAuditActions.MidtermReportReviewed,
                "Succeeded",
                null,
                new
                {
                    report.GroupId,
                    report.CurrentVersionNo,
                    Decision = decision.ToString(),
                    ResultingMidtermReportStatus = report.Status.ToString(),
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
                report,
                decision,
                command.Feedback,
                occurredAt,
                cancellationToken);
        }

        var saveError = await unitOfWork.TrySaveChangesAsync(
            MidtermReportSubmissionErrors.ConcurrencyConflict,
            MidtermReportSubmissionErrors.ConcurrencyConflict,
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
        MidtermReport report,
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
                new("Giai đoạn dừng", "Cổng thẩm định 2 — Báo cáo giữa kỳ")
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
                        Content: "Báo cáo giữa kỳ không đạt Cổng thẩm định 2, nhóm bị dừng thực hiện đề tài trong đợt này.",
                        RecipientUserIds: confirmedStudents,
                        ResourceType: "StudentGroup",
                        ResourceId: group.Id,
                        AcademicBatchId: batchId,
                        ActionPath: "/thesis/midterm",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: "[UGTM] Thông báo dừng thực hiện đề tài KLTN (Cổng 2)",
                        EmailCategoryLabel: "Cổng thẩm định 2",
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
                        Title: "Nhóm hướng dẫn bị dừng đề tài tại Cổng 2",
                        Content: $"Báo cáo giữa kỳ đề tài \"{shortTopic}\" không đạt Cổng 2, nhóm sinh viên đã dừng thực hiện.",
                        RecipientUserIds: [advisorId.Value],
                        ResourceType: "StudentGroup",
                        ResourceId: group.Id,
                        AcademicBatchId: batchId,
                        ActionPath: "/topics/advising",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Dừng thực hiện đề tài tại Cổng 2: {shortTopic}",
                        EmailCategoryLabel: "Cổng thẩm định 2",
                        EmailCtaLabel: "Xem nhóm hướng dẫn",
                        EmailMetadata: metadata),
                    occurredAtUtc);
            }

            return;
        }

        var (notifType, studentTitle, studentContent, advisorTitle, advisorContent) = decision == ReviewDecision.Approved
            ? (
                "MIDTERM_REPORT_APPROVED",
                "Báo cáo giữa kỳ đã được phê duyệt",
                "Báo cáo giữa kỳ đã đạt Cổng thẩm định 2. Nhóm tiếp tục hoàn thiện báo cáo cuối kỳ.",
                "Báo cáo giữa kỳ nhóm hướng dẫn đã đạt",
                $"Báo cáo giữa kỳ đề tài \"{shortTopic}\" đã được Khoa phê duyệt qua Cổng 2.")
            : (
                "MIDTERM_REPORT_REVISION_REQUIRED",
                "Yêu cầu chỉnh sửa Báo cáo giữa kỳ",
                "Khoa yêu cầu nhóm chỉnh sửa Báo cáo giữa kỳ. Vui lòng xem nhận xét và nộp lại đúng hạn.",
                "Nhóm hướng dẫn cần sửa Báo cáo giữa kỳ",
                $"Khoa yêu cầu chỉnh sửa Báo cáo giữa kỳ đề tài \"{shortTopic}\".");

        if (confirmedStudents.Count > 0)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: notifType,
                    Title: studentTitle,
                    Content: studentContent,
                    RecipientUserIds: confirmedStudents,
                    ResourceType: "MidtermReport",
                    ResourceId: report.Id,
                    AcademicBatchId: batchId,
                    ActionPath: "/thesis/midterm",
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
                    ResourceType: "MidtermReport",
                    ResourceId: report.Id,
                    AcademicBatchId: batchId,
                    ActionPath: "/topics/advising",
                    Priority: "Normal",
                    EnqueuePush: true,
                    EnqueueEmail: false),
                occurredAtUtc);
        }
    }
}
