using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.ReviewProgressReport;

public sealed class ReviewProgressReportHandler(
    IProgressMilestoneRepository milestoneRepository,
    IProgressReportRepository progressReportRepository,
    IStudentGroupRepository groupRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IThesisNotificationOutbox? notificationOutbox = null)
{
    public async Task<Result<ProgressReportReviewDto>> HandleAsync(
        ReviewProgressReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.ReviewAccessDenied);
        }

        if (command.MilestoneId == Guid.Empty
            || !TryParseDecision(command.Decision, out var decision)
            || (decision == ProgressReportStatus.NeedRevision && string.IsNullOrWhiteSpace(command.Feedback)))
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.InvalidRequest);
        }

        var milestone = await milestoneRepository.GetByIdAsync(command.MilestoneId, cancellationToken);
        if (milestone is null)
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.MilestoneNotFound);
        }

        var group = await groupRepository.GetByIdAsync(milestone.GroupId, cancellationToken);
        if (group is null || !await IsAssignedAdvisorOrAdministratorAsync(group.TopicId, cancellationToken))
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.ReviewAccessDenied);
        }

        var report = await progressReportRepository.GetByMilestoneIdAsync(milestone.Id, cancellationToken);
        if (report is null)
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.ReportNotFound);
        }

        var version = await progressReportRepository.GetVersionAsync(report.Id, report.CurrentVersionNo, cancellationToken);
        if (version is null
            || !report.TryReview(decision, command.Feedback, timeProvider.GetUtcNow().UtcDateTime)
            || !version.TryReview(decision, command.Feedback, currentUser.UserId.Value, report.ReviewedAt!.Value))
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.InvalidReviewState);
        }

        auditOutbox.Add(
            new AuditIntegrationEntry(
                "ProgressReport",
                report.Id.ToString(),
                IntegrationAuditActions.ProgressReportReviewed,
                "Succeeded",
                null,
                new
                {
                    report.MilestoneId,
                    report.GroupId,
                    version.VersionNo,
                    Decision = decision.ToString(),
                    version.IsLate
                }),
            version.ReviewedAt!.Value);

        if (notificationOutbox is not null)
        {
            var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(group);
            if (confirmedStudents.Count > 0)
            {
                var batchId = group.Members.Select(m => (Guid?)m.BatchId).FirstOrDefault();
                var shortMilestone = ThesisNotificationFormatting.Shorten(milestone.Title, 55);
                var (notifType, title, content) = decision == ProgressReportStatus.Accepted
                    ? (
                        "PROGRESS_REPORT_ACCEPTED",
                        "Mốc tiến độ đã được thông qua",
                        $"GVHD đã chấp nhận báo cáo mốc \"{shortMilestone}\" (v{version.VersionNo}) của nhóm.")
                    : (
                        "PROGRESS_REPORT_REVISION_REQUIRED",
                        "Yêu cầu chỉnh sửa mốc tiến độ",
                        $"GVHD yêu cầu cập nhật lại mốc \"{shortMilestone}\". Vui lòng xem nhận xét và nộp lại sớm.");

                var isRevisionRequired = decision != ProgressReportStatus.Accepted;
                List<NotificationEmailMetaItem>? revisionMetadata = null;
                if (isRevisionRequired)
                {
                    revisionMetadata =
                    [
                        new("Mốc tiến độ", milestone.Title),
                        new("Phiên bản", $"v{version.VersionNo}")
                    ];
                    if (!string.IsNullOrWhiteSpace(version.AdvisorFeedback))
                    {
                        revisionMetadata.Add(new NotificationEmailMetaItem("Nhận xét của GVHD", version.AdvisorFeedback.Trim()));
                    }
                }

                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: notifType,
                        Title: title,
                        Content: content,
                        RecipientUserIds: confirmedStudents,
                        ResourceType: "ProgressReport",
                        ResourceId: report.Id,
                        AcademicBatchId: batchId,
                        ActionPath: "/thesis/milestones",
                        Priority: isRevisionRequired ? "High" : "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: isRevisionRequired,
                        EmailSubject: isRevisionRequired ? $"[UGTM] Yêu cầu chỉnh sửa mốc tiến độ: {shortMilestone}" : null,
                        EmailCategoryLabel: isRevisionRequired ? "Tiến độ thực hiện" : null,
                        EmailCtaLabel: isRevisionRequired ? "Xem nhận xét & nộp lại" : null,
                        EmailNotice: isRevisionRequired ? "Vui lòng chỉnh sửa theo góp ý của GVHD và nộp lại báo cáo." : null,
                        EmailMetadata: revisionMetadata),
                    version.ReviewedAt!.Value);
            }
        }

        var saveError = await unitOfWork.TrySaveChangesAsync(
            ProgressReportErrors.ConcurrencyConflict,
            ProgressReportErrors.ConcurrencyConflict,
            cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<ProgressReportReviewDto>(saveError);
        }

        return Result.Success(new ProgressReportReviewDto(
            version.VersionNo,
            decision.ToString(),
            version.AdvisorFeedback,
            version.ReviewedBy!.Value,
            version.ReviewedAt!.Value));
    }

    private async Task<bool> IsAssignedAdvisorOrAdministratorAsync(Guid topicId, CancellationToken cancellationToken)
    {
        if (ThesisAuthorization.IsAdministrator(currentUser))
        {
            return true;
        }

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(topicId, cancellationToken);
        return assignment?.AdvisorId == currentUser.UserId!.Value;
    }

    private static bool TryParseDecision(string? value, out ProgressReportStatus decision)
        => Enum.TryParse(value, ignoreCase: true, out decision)
           && decision is ProgressReportStatus.Accepted or ProgressReportStatus.NeedRevision;
}
