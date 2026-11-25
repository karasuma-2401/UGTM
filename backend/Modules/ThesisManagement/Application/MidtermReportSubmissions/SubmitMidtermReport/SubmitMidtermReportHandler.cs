using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.SubmitMidtermReport;

public sealed class SubmitMidtermReportHandler(
    IStudentGroupRepository groupRepository,
    IMidtermReportRepository midtermReportRepository,
    IReviewGateConfigurationRepository gateConfigurationRepository,
    ISubmissionFileRepository submissionFileRepository,
    IThesisSubmissionFileValidator fileValidator,
    IThesisSubmissionObjectKeyFactory objectKeyFactory,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IThesisUploadLifecycle uploadLifecycle,
    IKltnBatchReader batchReader,
    IThesisNotificationOutbox? notificationOutbox = null,
    ITopicSummaryReader? topicSummaryReader = null,
    IStudentInfoReader? studentInfoReader = null)
{
    public async Task<Result<MidtermReportSubmissionDto>> HandleAsync(
        SubmitMidtermReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.AccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.FileName)
            || command.Content is null
            || !command.Content.CanRead)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.GroupNotFound);
        }

        var membership = group.Members.SingleOrDefault(member => member.StudentId == currentUser.UserId.Value);
        if (membership is null || membership.ConfirmationStatus != MemberConfirmationStatus.Confirmed)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.AccessDenied);
        }

        var report = await midtermReportRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        if (report is null)
        {
            if (group.Status != StudentGroupStatus.InProgress)
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidSubmissionState);
            }
        }
        else if (report.Status != MidtermReportStatus.NeedRevision
            || group.Status != StudentGroupStatus.InProgress)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidSubmissionState);
        }

        var stageWindow = await batchReader.GetMidtermSubmissionWindowAsync(
            membership.BatchId,
            occurredAt,
            cancellationToken);
        if (stageWindow is null || !stageWindow.IsStageWindowOpen)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.StageWindowClosed);
        }

        var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
            membership.BatchId,
            ReviewGateType.Midterm,
            cancellationToken);
        if (gateConfiguration is null)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.GateConfigurationNotFound);
        }

        if (report is null)
        {
            if (occurredAt > gateConfiguration.InitialSubmissionDeadlineUtc)
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InitialSubmissionDeadlinePassed);
            }
        }
        else if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.FinalClosureDeadlinePassed);
        }

        ValidatedThesisSubmissionFile validatedFile;
        try
        {
            validatedFile = await fileValidator.ValidateAsync(
                command.FileName,
                command.Content,
                cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidFile);
        }

        var isFirstSubmission = report is null;
        var versionNo = isFirstSubmission ? 1 : checked(report!.CurrentVersionNo + 1);
        var reportId = report?.Id ?? Guid.NewGuid();
        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.MidtermReport,
            reportId,
            versionNo,
            validatedFile.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.MidtermReport,
            EntityId = reportId,
            VersionNo = versionNo,
            FileName = validatedFile.FileName,
            ObjectKey = objectKey,
            ContentType = validatedFile.ContentType,
            ChecksumSha256 = validatedFile.ChecksumSha256,
            FileSize = validatedFile.FileSize,
            FileExtension = validatedFile.FileExtension,
            UploadedBy = currentUser.UserId.Value,
            UploadedAt = occurredAt
        };

        var uploadAttempt = await uploadLifecycle.BeginAsync(objectKey, cancellationToken);
        try
        {
            await using var content = new MemoryStream(validatedFile.Content.ToArray(), writable: false);
            await objectStorage.UploadAsync(new ThesisSubmissionObjectWriteRequest(
                objectKey, validatedFile.ContentType, validatedFile.FileSize), content, cancellationToken);
            if (isFirstSubmission)
            {
                report = MidtermReport.CreateSubmitted(reportId, command.GroupId);
                midtermReportRepository.Add(report);
            }
            else
            {
                report!.SubmitRevision();
            }

            if (!group.TrySubmitMidtermReport())
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidSubmissionState);
            }

            submissionFileRepository.Add(submissionFile);
            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "MidtermReport",
                    report.Id.ToString(),
                    IntegrationAuditActions.MidtermReportSubmitted,
                    "Succeeded",
                    null,
                    new
                    {
                        report.GroupId,
                        report.CurrentVersionNo,
                        FileId = submissionFile.Id,
                        submissionFile.FileName,
                        submissionFile.FileSize,
                        submissionFile.ContentType,
                        IsResubmission = !isFirstSubmission
                    }),
                occurredAt);

            if (notificationOutbox is not null)
            {
                await EnqueueSubmissionNotificationsAsync(
                    notificationOutbox,
                    topicSummaryReader,
                    studentInfoReader,
                    group,
                    membership.BatchId,
                    report,
                    submissionFile,
                    currentUser.UserId.Value,
                    occurredAt,
                    cancellationToken);
            }

            var saveError = await uploadLifecycle.SaveAsync(uploadAttempt,
                token => unitOfWork.TrySaveChangesAsync(MidtermReportSubmissionErrors.ConcurrencyConflict,
                    MidtermReportSubmissionErrors.ConcurrencyConflict, token),
                MidtermReportSubmissionErrors.ConcurrencyConflict, cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<MidtermReportSubmissionDto>(saveError);
            }

            return Result.Success(MidtermReportSubmissionMapper.ToSubmissionDto(
                report,
                SyllabusSubmissionMapper.ToDto(submissionFile)));
        }
        finally
        {
            await uploadLifecycle.FinishAsync(uploadAttempt);
        }
    }

    private static async Task EnqueueSubmissionNotificationsAsync(
        IThesisNotificationOutbox notificationOutbox,
        ITopicSummaryReader? topicSummaryReader,
        IStudentInfoReader? studentInfoReader,
        StudentGroup group,
        Guid batchId,
        MidtermReport report,
        SubmissionFile submissionFile,
        Guid uploaderId,
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

        string? uploaderName = null;
        if (studentInfoReader is not null)
        {
            var studentInfo = await studentInfoReader.GetStudentByIdOrCodeAsync(uploaderId, null, cancellationToken);
            uploaderName = studentInfo?.FullName;
        }

        var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(group);
        var versionLabel = $"v{report.CurrentVersionNo}";
        var shortTopic = ThesisNotificationFormatting.Shorten(topicTitle, 65);

        if (confirmedStudents.Count > 0)
        {
            var metadata = new List<NotificationEmailMetaItem>
            {
                new("Mốc nộp", "Báo cáo giữa kỳ (Cổng 2)")
            };
            if (!string.IsNullOrWhiteSpace(topicTitle))
            {
                metadata.Add(new NotificationEmailMetaItem("Đề tài", topicTitle.Trim()));
            }
            metadata.Add(new NotificationEmailMetaItem("Phiên bản", versionLabel));
            if (!string.IsNullOrWhiteSpace(uploaderName))
            {
                metadata.Add(new NotificationEmailMetaItem("Người nộp", uploaderName.Trim()));
            }
            metadata.Add(new NotificationEmailMetaItem(
                "Tệp ghi nhận",
                $"{submissionFile.FileName} ({ThesisNotificationFormatting.FormatFileSize(submissionFile.FileSize)})"));
            metadata.Add(new NotificationEmailMetaItem(
                "Thời gian nộp (VN)",
                ThesisNotificationFormatting.FormatVietnamTime(occurredAtUtc)));

            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "SUBMISSION_CONFIRMED",
                    Title: $"Xác nhận nộp Báo cáo giữa kỳ ({versionLabel})",
                    Content: $"Ghi nhận tệp Báo cáo giữa kỳ {versionLabel} của nhóm. Khoa sẽ thẩm định và phản hồi sớm theo kế hoạch.",
                    RecipientUserIds: confirmedStudents,
                    ResourceType: "MidtermReport",
                    ResourceId: report.Id,
                    AcademicBatchId: batchId,
                    ActionPath: "/thesis/midterm",
                    Priority: "Critical",
                    EnqueuePush: true,
                    EnqueueEmail: true,
                    EmailSubject: $"[UGTM] Xác nhận nộp Báo cáo giữa kỳ KLTN ({versionLabel})",
                    EmailCategoryLabel: "Xác nhận nộp bài",
                    EmailCtaLabel: "Xem trang Giữa kỳ",
                    EmailNotice: "Vui lòng theo dõi kết quả thẩm định của Khoa trên hệ thống.",
                    EmailMetadata: metadata),
                occurredAtUtc);
        }

        notificationOutbox.Enqueue(
            new NotificationOutboxEntry(
                NotificationType: "MIDTERM_REPORT_SUBMITTED",
                Title: $"Báo cáo giữa kỳ {versionLabel} chờ thẩm định",
                Content: $"Nhóm đề tài \"{shortTopic}\" vừa nộp Báo cáo giữa kỳ {versionLabel} chờ Khoa thẩm định.",
                RecipientRoles: ["Faculty"],
                RecipientPermissions: [PermissionCodes.ThesisReviewGatesReview],
                ExcludeUserIds: [uploaderId],
                ResourceType: "MidtermReport",
                ResourceId: report.Id,
                AcademicBatchId: batchId,
                ActionPath: "/thesis/review/midterm",
                Priority: "Normal",
                EnqueuePush: true,
                EnqueueEmail: false),
            occurredAtUtc);

        if (advisorId.HasValue && advisorId.Value != Guid.Empty)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "MIDTERM_REPORT_SUBMITTED",
                    Title: $"Nhóm hướng dẫn đã nộp Giữa kỳ {versionLabel}",
                    Content: $"Nhóm đề tài \"{shortTopic}\" vừa nộp tệp Báo cáo giữa kỳ phiên bản {versionLabel}.",
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
