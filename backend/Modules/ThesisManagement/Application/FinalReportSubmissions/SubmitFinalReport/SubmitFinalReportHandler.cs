using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;

public sealed class SubmitFinalReportHandler(
    FinalReportSubmissionEligibilityChecker eligibilityChecker,
    IFinalReportRepository finalReportRepository,
    ISubmissionFileRepository submissionFileRepository,
    IFinalReportPlagiarismScanRepository plagiarismScanRepository,
    IThesisSubmissionFileValidator fileValidator,
    IThesisSubmissionObjectKeyFactory objectKeyFactory,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ILogger<SubmitFinalReportHandler> logger,
    IOptions<PlagiarismAiOptions>? plagiarismOptions = null,
    ISubmissionProductLinkRepository? productLinkRepository = null,
    ISubmissionAttachmentRepository? attachmentRepository = null,
    IThesisSubmissionImageValidator? imageValidator = null,
    IReviewerDefenseEligibilityContract? reviewerEligibility = null,
    IThesisNotificationOutbox? notificationOutbox = null,
    ITopicSummaryReader? topicSummaryReader = null,
    IStudentInfoReader? studentInfoReader = null)
{
    public async Task<Result<FinalReportSubmissionDto>> HandleAsync(
        SubmitFinalReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!SubmissionProductLinkValidator.TryNormalize(
                command.ProductLinks,
                null,
                requireAtLeastOne: false,
                out var normalizedLinks))
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidRequest);
        }

        if (!Enum.IsDefined(command.VersionType))
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidRequest);
        }
        var isReviewerRevision = command.VersionType == FinalReportVersionType.RevisedAfterReviewer;
        var eligibility = await eligibilityChecker.CheckAsync(command.GroupId, cancellationToken,
            revisedAfterReviewer: isReviewerRevision);
        if (eligibility.IsFailure)
        {
            return Result.Failure<FinalReportSubmissionDto>(eligibility.Error);
        }

        var context = eligibility.Value;
        if (isReviewerRevision && await reviewerEligibility!.GetAsync(context.Group.Id, cancellationToken) is not { CanProceed: true })
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.ReviewerRevisionNotEligible);
        }
        var existingReport = await finalReportRepository.GetByGroupAndVersionTypeAsync(
            context.Group.Id, command.VersionType, cancellationToken);
        var isFirstSubmission = existingReport is null;
        if (existingReport is not null && existingReport.Status != FinalReportStatus.Submitted)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.EvaluationStarted);
        }

        var requiredGroupStatus = isReviewerRevision
            ? StudentGroupStatus.ReadyForReviewerAssignment
            : isFirstSubmission
            ? StudentGroupStatus.InProgress
            : StudentGroupStatus.FinalReportSubmitted;
        if (context.Group.Status != requiredGroupStatus)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidGroupState);
        }

        if (command.Content is null || !command.Content.CanRead)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidFile);
        }

        ValidatedThesisSubmissionFile file;
        try
        {
            file = await fileValidator.ValidateAsync(command.FileName, command.Content, cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidFile);
        }

        var report = existingReport ?? FinalReport.CreateSubmitted(
            Guid.NewGuid(), context.Group.Id, command.VersionType, context.SubmittedAtUtc);
        var versionNo = isFirstSubmission ? 1 : checked(report.CurrentVersionNo + 1);

        IReadOnlyList<SubmissionEvidenceHelper.PreparedAttachment> preparedAttachments;
        try
        {
            preparedAttachments = await SubmissionEvidenceHelper.PrepareImagesAsync(
                command.EvidenceImages,
                imageValidator,
                objectKeyFactory,
                SubmissionEntityType.FinalReport,
                report.Id,
                versionNo,
                context.StudentId,
                context.SubmittedAtUtc,
                cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidAttachment);
        }

        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.FinalReport, report.Id, versionNo, file.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.FinalReport,
            EntityId = report.Id,
            VersionNo = versionNo,
            FileName = file.FileName,
            ObjectKey = objectKey,
            ContentType = file.ContentType,
            ChecksumSha256 = file.ChecksumSha256,
            FileSize = file.FileSize,
            FileExtension = file.FileExtension,
            UploadedBy = context.StudentId,
            UploadedAt = context.SubmittedAtUtc
        };
        var linkEntities = SubmissionEvidenceHelper.BuildProductLinks(
            normalizedLinks,
            SubmissionEntityType.FinalReport,
            report.Id,
            versionNo,
            context.StudentId,
            context.SubmittedAtUtc);
        var attachmentEntities = preparedAttachments.Select(item => item.Entity).ToArray();
        var uploadedKeys = new List<string>(1 + preparedAttachments.Count);

        var response = new FinalReportSubmissionDto(
            report.Id,
            report.GroupId,
            report.VersionType.ToString(),
            versionNo,
            report.Status.ToString(),
            context.SubmittedAtUtc,
            SyllabusSubmissionMapper.ToDto(submissionFile),
            linkEntities.Select(SubmissionSupplementalMapper.ToDto).ToArray(),
            attachmentEntities.Select(SubmissionSupplementalMapper.ToDto).ToArray());

        var saveAttempted = false;
        try
        {
            await using var content = new MemoryStream(file.Content.ToArray(), writable: false);
            await objectStorage.UploadAsync(
                new ThesisSubmissionObjectWriteRequest(objectKey, file.ContentType, file.FileSize),
                content, cancellationToken);
            uploadedKeys.Add(objectKey);
            await SubmissionEvidenceHelper.UploadAllAsync(preparedAttachments, objectStorage, uploadedKeys, cancellationToken);

            if (isReviewerRevision && await reviewerEligibility!.GetAsync(context.Group.Id, cancellationToken) is not { CanProceed: true })
            {
                await TryDeleteObjectsAsync(uploadedKeys);
                return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.ReviewerRevisionNotEligible);
            }

            if (context.Group.Status != requiredGroupStatus
                || (isFirstSubmission && !isReviewerRevision && !context.Group.TrySubmitFinalReport()))
            {
                await TryDeleteObjectsAsync(uploadedKeys);
                return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidGroupState);
            }

            if (isFirstSubmission)
            {
                finalReportRepository.Add(report);
                if (isReviewerRevision)
                {
                    finalReportRepository.TrackGroupConcurrency(report, context.Group);
                }
            }
            else
            {
                if (!report.TrySubmitRevision(context.SubmittedAtUtc))
                {
                    await TryDeleteObjectsAsync(uploadedKeys);
                    return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.ConcurrencyConflict);
                }

                finalReportRepository.TrackGroupConcurrency(report, context.Group);
            }
            submissionFileRepository.Add(submissionFile);
            if (linkEntities.Count > 0)
            {
                productLinkRepository?.AddRange(linkEntities);
            }
            if (attachmentEntities.Length > 0)
            {
                attachmentRepository?.AddRange(attachmentEntities);
            }

            var plagiarismScan = FinalReportPlagiarismScan.Create(
                Guid.NewGuid(),
                report.Id,
                submissionFile.Id,
                versionNo,
                context.SubmittedAtUtc,
                plagiarismOptions?.Value.GetNormalizedWorkerPartition());
            plagiarismScanRepository.Add(plagiarismScan);
            auditOutbox.Add(new AuditIntegrationEntry(
                "FinalReport",
                report.Id.ToString(),
                IntegrationAuditActions.FinalReportSubmitted,
                "Succeeded",
                null,
                new
                {
                    report.GroupId,
                    context.BatchId,
                    VersionType = report.VersionType.ToString(),
                    report.CurrentVersionNo,
                    FileId = submissionFile.Id,
                    submissionFile.FileName,
                    submissionFile.FileSize,
                    submissionFile.ContentType,
                    submissionFile.ChecksumSha256,
                    LinkCount = linkEntities.Count,
                    AttachmentCount = attachmentEntities.Length,
                    IsResubmission = !isFirstSubmission
                }), context.SubmittedAtUtc);

            if (notificationOutbox is not null)
            {
                await EnqueueFinalReportNotificationsAsync(
                    notificationOutbox,
                    topicSummaryReader,
                    studentInfoReader,
                    context,
                    report,
                    submissionFile,
                    versionNo,
                    isReviewerRevision,
                    cancellationToken);
            }

            saveAttempted = true;
            var saveError = await unitOfWork.TrySaveChangesAsync(
                FinalReportSubmissionErrors.ConcurrencyConflict,
                FinalReportSubmissionErrors.ConcurrencyConflict,
                cancellationToken);
            if (saveError is not null)
            {
                await TryDeleteObjectsAsync(uploadedKeys);
                return Result.Failure<FinalReportSubmissionDto>(saveError);
            }
        }
        catch
        {
            await CompensateFailedSubmissionAsync(submissionFile, uploadedKeys, saveAttempted);
            throw;
        }

        return Result.Success(response);
    }

    private static async Task EnqueueFinalReportNotificationsAsync(
        IThesisNotificationOutbox notificationOutbox,
        ITopicSummaryReader? topicSummaryReader,
        IStudentInfoReader? studentInfoReader,
        FinalReportSubmissionContext context,
        FinalReport report,
        SubmissionFile submissionFile,
        int versionNo,
        bool isReviewerRevision,
        CancellationToken cancellationToken)
    {
        Guid? advisorId = null;
        string? topicTitle = null;
        if (topicSummaryReader is not null)
        {
            var topics = await topicSummaryReader.GetByIdsAsync([context.Group.TopicId], cancellationToken);
            if (topics.TryGetValue(context.Group.TopicId, out var topicSummary))
            {
                advisorId = topicSummary.AdvisorId;
                topicTitle = topicSummary.TitleVi;
            }
        }

        string? uploaderName = null;
        if (studentInfoReader is not null)
        {
            var studentInfo = await studentInfoReader.GetStudentByIdOrCodeAsync(context.StudentId, null, cancellationToken);
            uploaderName = studentInfo?.FullName;
        }

        var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(context.Group);
        var versionLabel = $"v{versionNo}";
        var shortLabel = isReviewerRevision ? "Báo cáo sửa sau phản biện" : "Báo cáo cuối kỳ";
        var fullLabel = isReviewerRevision ? "Báo cáo cuối kỳ (Chỉnh sửa sau phản biện)" : "Báo cáo cuối kỳ KLTN";
        var shortTopic = ThesisNotificationFormatting.Shorten(topicTitle, 60);

        if (confirmedStudents.Count > 0)
        {
            var metadata = new List<NotificationEmailMetaItem>
            {
                new("Mốc nộp", fullLabel)
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
                ThesisNotificationFormatting.FormatVietnamTime(context.SubmittedAtUtc)));

            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "SUBMISSION_CONFIRMED",
                    Title: $"Xác nhận nộp {shortLabel} ({versionLabel})",
                    Content: $"Ghi nhận tệp {shortLabel} {versionLabel} của nhóm. Vui lòng theo dõi tiến độ chấm duyệt trên hệ thống.",
                    RecipientUserIds: confirmedStudents,
                    ResourceType: "FinalReport",
                    ResourceId: report.Id,
                    AcademicBatchId: context.BatchId,
                    ActionPath: "/thesis/final-report",
                    Priority: "Critical",
                    EnqueuePush: true,
                    EnqueueEmail: true,
                    EmailSubject: $"[UGTM] Xác nhận nộp {shortLabel} ({versionLabel})",
                    EmailCategoryLabel: "Xác nhận nộp bài",
                    EmailCtaLabel: "Xem Báo cáo cuối kỳ",
                    EmailNotice: "Vui lòng theo dõi trạng thái đánh giá từ GVHD và Hội đồng trên hệ thống.",
                    EmailMetadata: metadata),
                context.SubmittedAtUtc);
        }

        if (advisorId.HasValue && advisorId.Value != Guid.Empty)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "FINAL_REPORT_SUBMITTED",
                    Title: $"Nhóm hướng dẫn đã nộp {shortLabel} {versionLabel}",
                    Content: $"Nhóm đề tài \"{shortTopic}\" vừa nộp {shortLabel} {versionLabel}. Thầy/cô có thể kiểm tra và đánh giá.",
                    RecipientUserIds: [advisorId.Value],
                    ResourceType: "FinalReport",
                    ResourceId: report.Id,
                    AcademicBatchId: context.BatchId,
                    ActionPath: isReviewerRevision ? "/topics/advising" : "/thesis/lecturer-grading",
                    Priority: "Normal",
                    EnqueuePush: true,
                    EnqueueEmail: false),
                context.SubmittedAtUtc);
        }
    }

    private async Task CompensateFailedSubmissionAsync(SubmissionFile file, IReadOnlyCollection<string> uploadedKeys, bool saveAttempted)
    {
        if (saveAttempted)
        {
            // A connection/cancellation failure can arrive after commit. Never delete an object
            // that the database already references, even when the original request reports failure.
            using var verification = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                if (await submissionFileRepository.GetByIdAsync(file.Id, verification.Token) is not null)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Could not verify final report save for object {ObjectKey}; retaining the object until its database reference can be checked.",
                    file.ObjectKey);
                return;
            }
        }

        await TryDeleteObjectsAsync(uploadedKeys.Count > 0 ? uploadedKeys : [file.ObjectKey]);
    }

    private async Task TryDeleteObjectsAsync(IEnumerable<string> objectKeys)
    {
        foreach (var objectKey in objectKeys)
        {
            await TryDeleteObjectAsync(objectKey);
        }
    }

    private async Task TryDeleteObjectAsync(string objectKey)
    {
        // Request cancellation must not cancel compensation for an uploaded object.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await objectStorage.DeleteAsync(objectKey, cleanup.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Could not delete unreferenced final report object {ObjectKey}; cleanup must be retried.", objectKey);
        }
    }
}
