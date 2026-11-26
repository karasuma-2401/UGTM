using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.SubmitProgressReport;

public sealed class SubmitProgressReportHandler(
    IProgressMilestoneRepository milestoneRepository,
    IProgressReportRepository progressReportRepository,
    IStudentGroupRepository groupRepository,
    ISubmissionFileRepository submissionFileRepository,
    IThesisSubmissionFileValidator fileValidator,
    IThesisSubmissionObjectKeyFactory objectKeyFactory,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IThesisUploadLifecycle uploadLifecycle,
    ISyllabusRepository? syllabusRepository = null,
    ISubmissionProductLinkRepository? productLinkRepository = null,
    ISubmissionAttachmentRepository? attachmentRepository = null,
    IThesisSubmissionImageValidator? imageValidator = null,
    IThesisNotificationOutbox? notificationOutbox = null,
    ITopicSummaryReader? topicSummaryReader = null,
    IStudentInfoReader? studentInfoReader = null)
{
    public async Task<Result<ProgressReportSubmissionDto>> HandleAsync(
        SubmitProgressReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.AccessDenied);
        }

        if (command.MilestoneId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.FileName)
            || command.Content is null
            || !command.Content.CanRead
            || !SubmissionProductLinkValidator.TryNormalize(
                command.ProductLinks,
                command.GitUrl,
                requireAtLeastOne: true,
                out var normalizedLinks))
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidRequest);
        }

        var gitUrl = normalizedLinks[0].Url;

        var milestone = await milestoneRepository.GetByIdAsync(command.MilestoneId, cancellationToken);
        if (milestone is null)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.MilestoneNotFound);
        }

        var group = await groupRepository.GetByIdAsync(milestone.GroupId, cancellationToken);
        var isMember = group is not null && ThesisAuthorization.IsConfirmedMember(group, currentUser.UserId.Value);
        var isAdministrator = ThesisAuthorization.IsAdministrator(currentUser);
        if (group is null || (!isMember && !isAdministrator))
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.AccessDenied);
        }

        if (group.Status != StudentGroupStatus.InProgress
            && group.Status != StudentGroupStatus.MidtermSubmitted
            && group.Status != StudentGroupStatus.SyllabusApproved)
        {
            if (group.Status is not (StudentGroupStatus.Registered or StudentGroupStatus.SyllabusRevisionRequired or StudentGroupStatus.SyllabusSubmitted))
            {
                return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidSubmissionState);
            }

            var syllabus = syllabusRepository is not null
                ? await syllabusRepository.GetByGroupIdAsync(group.Id, cancellationToken)
                : null;

            if (syllabus is not null && syllabus.Status == SyllabusStatus.Approved)
            {
                if (group.Status is StudentGroupStatus.Registered or StudentGroupStatus.SyllabusRevisionRequired)
                {
                    group.TrySubmitSyllabus();
                }
                if (group.Status is StudentGroupStatus.SyllabusSubmitted)
                {
                    group.TryApproveSyllabus();
                }
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidSubmissionState);
            }
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var report = await progressReportRepository.GetByMilestoneIdAsync(milestone.Id, cancellationToken);
        var isFirstSubmission = report is null;
        var versionNo = isFirstSubmission ? 1 : checked(report!.CurrentVersionNo + 1);
        if (!isFirstSubmission && report!.Status != ProgressReportStatus.NeedRevision)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidSubmissionState);
        }

        ValidatedThesisSubmissionFile validatedFile;
        try
        {
            validatedFile = await fileValidator.ValidateAsync(command.FileName, command.Content, cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidFile);
        }

        var reportId = report?.Id ?? Guid.NewGuid();
        IReadOnlyList<SubmissionEvidenceHelper.PreparedAttachment> preparedAttachments;
        try
        {
            preparedAttachments = await SubmissionEvidenceHelper.PrepareImagesAsync(
                command.EvidenceImages,
                imageValidator,
                objectKeyFactory,
                SubmissionEntityType.ProgressReport,
                reportId,
                versionNo,
                currentUser.UserId.Value,
                occurredAt,
                cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidAttachment);
        }

        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.ProgressReport,
            reportId,
            versionNo,
            validatedFile.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.ProgressReport,
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

        var linkEntities = SubmissionEvidenceHelper.BuildProductLinks(
            normalizedLinks,
            SubmissionEntityType.ProgressReport,
            reportId,
            versionNo,
            currentUser.UserId.Value,
            occurredAt);
        var attachmentEntities = preparedAttachments.Select(item => item.Entity).ToArray();
        var uploadedExtraKeys = new List<string>(preparedAttachments.Count);

        var uploadAttempt = await uploadLifecycle.BeginAsync(objectKey, cancellationToken);
        try
        {
            await using var content = new MemoryStream(validatedFile.Content.ToArray(), writable: false);
            await objectStorage.UploadAsync(new ThesisSubmissionObjectWriteRequest(
                objectKey, validatedFile.ContentType, validatedFile.FileSize), content, cancellationToken);
            await SubmissionEvidenceHelper.UploadAllAsync(preparedAttachments, objectStorage, uploadedExtraKeys, cancellationToken);
            if (isFirstSubmission)
            {
                report = ProgressReport.CreateSubmitted(reportId, milestone.Id, group.Id, command.StudentNote, occurredAt);
                progressReportRepository.Add(report);
            }
            else if (!report!.TrySubmitRevision(command.StudentNote, occurredAt))
            {
                await TryDeleteExtraObjectsAsync(uploadedExtraKeys);
                return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidSubmissionState);
            }

            var version = ProgressReportVersion.CreateSubmitted(
                Guid.NewGuid(),
                report!.Id,
                versionNo,
                gitUrl!,
                command.StudentNote,
                currentUser.UserId.Value,
                occurredAt,
                occurredAt > milestone.Deadline);
            progressReportRepository.AddVersion(version);
            submissionFileRepository.Add(submissionFile);
            if (linkEntities.Count > 0)
            {
                productLinkRepository?.AddRange(linkEntities);
            }
            if (attachmentEntities.Length > 0)
            {
                attachmentRepository?.AddRange(attachmentEntities);
            }
            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "ProgressReport",
                    report.Id.ToString(),
                    IntegrationAuditActions.ProgressReportSubmitted,
                    "Succeeded",
                    null,
                    new
                    {
                        report.MilestoneId,
                        report.GroupId,
                        version.VersionNo,
                        version.IsLate,
                        version.GitUrl,
                        LinkCount = linkEntities.Count,
                        AttachmentCount = attachmentEntities.Length,
                        FileId = submissionFile.Id,
                        submissionFile.FileName
                    }),
                occurredAt);

            if (notificationOutbox is not null)
            {
                await EnqueueProgressSubmissionNotificationsAsync(
                    notificationOutbox,
                    topicSummaryReader,
                    studentInfoReader,
                    group,
                    milestone,
                    report,
                    version,
                    submissionFile,
                    currentUser.UserId.Value,
                    occurredAt,
                    cancellationToken);
            }

            var saveError = await uploadLifecycle.SaveAsync(uploadAttempt,
                token => unitOfWork.TrySaveChangesAsync(ProgressReportErrors.ConcurrencyConflict,
                    ProgressReportErrors.ConcurrencyConflict, token),
                ProgressReportErrors.ConcurrencyConflict, cancellationToken);
            if (saveError is not null)
            {
                await TryDeleteExtraObjectsAsync(uploadedExtraKeys);
                return Result.Failure<ProgressReportSubmissionDto>(saveError);
            }

            return Result.Success(new ProgressReportSubmissionDto(
                report.Id,
                report.MilestoneId,
                report.GroupId,
                version.VersionNo,
                report.Status.ToString(),
                version.GitUrl,
                version.StudentNote,
                version.IsLate,
                version.SubmittedAt,
                SyllabusSubmissionMapper.ToDto(submissionFile),
                linkEntities.Select(SubmissionSupplementalMapper.ToDto).ToArray(),
                attachmentEntities.Select(SubmissionSupplementalMapper.ToDto).ToArray()));
        }
        catch
        {
            await TryDeleteExtraObjectsAsync(uploadedExtraKeys);
            throw;
        }
        finally
        {
            await uploadLifecycle.FinishAsync(uploadAttempt);
        }
    }

    private static async Task EnqueueProgressSubmissionNotificationsAsync(
        IThesisNotificationOutbox notificationOutbox,
        ITopicSummaryReader? topicSummaryReader,
        IStudentInfoReader? studentInfoReader,
        StudentGroup group,
        ProgressMilestone milestone,
        ProgressReport report,
        ProgressReportVersion version,
        SubmissionFile submissionFile,
        Guid uploaderId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        Guid? advisorId = milestone.CreatedBy != Guid.Empty ? milestone.CreatedBy : null;
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

        var batchId = group.Members.Select(m => (Guid?)m.BatchId).FirstOrDefault();
        var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(group);
        var versionLabel = $"v{version.VersionNo}";
        var shortMilestone = ThesisNotificationFormatting.Shorten(milestone.Title, 55);
        var shortTopic = ThesisNotificationFormatting.Shorten(topicTitle, 55);

        if (confirmedStudents.Count > 0)
        {
            var metadata = new List<NotificationEmailMetaItem>
            {
                new("Mốc nộp", $"Mốc tiến độ: {milestone.Title}")
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
            var timeDisplay = ThesisNotificationFormatting.FormatVietnamTime(occurredAtUtc);
            metadata.Add(new NotificationEmailMetaItem(
                "Thời gian nộp (VN)",
                version.IsLate ? $"{timeDisplay} (Nộp trễ hạn)" : timeDisplay));

            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "SUBMISSION_CONFIRMED",
                    Title: $"Xác nhận nộp Mốc tiến độ ({versionLabel})",
                    Content: $"Ghi nhận báo cáo mốc \"{shortMilestone}\" ({versionLabel}). GVHD sẽ xem xét và phản hồi trên hệ thống.",
                    RecipientUserIds: confirmedStudents,
                    ResourceType: "ProgressReport",
                    ResourceId: report.Id,
                    AcademicBatchId: batchId,
                    ActionPath: "/thesis/milestones",
                    Priority: "Critical",
                    EnqueuePush: true,
                    EnqueueEmail: true,
                    EmailSubject: $"[UGTM] Xác nhận nộp báo cáo mốc tiến độ: {shortMilestone} ({versionLabel})",
                    EmailCategoryLabel: "Xác nhận nộp bài",
                    EmailCtaLabel: "Xem mốc tiến độ",
                    EmailNotice: "Vui lòng theo dõi nhận xét của GVHD tại trang Mốc tiến độ.",
                    EmailMetadata: metadata),
                occurredAtUtc);
        }

        if (advisorId.HasValue && advisorId.Value != Guid.Empty)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "PROGRESS_REPORT_SUBMITTED",
                    Title: $"Nhóm đã nộp mốc tiến độ {versionLabel}",
                    Content: $"Nhóm đề tài \"{shortTopic}\" vừa nộp mốc \"{shortMilestone}\" ({versionLabel}) chờ thầy/cô đánh giá.",
                    RecipientUserIds: [advisorId.Value],
                    ResourceType: "ProgressReport",
                    ResourceId: report.Id,
                    AcademicBatchId: batchId,
                    ActionPath: $"/advisor/milestones/{milestone.Id}/review",
                    Priority: "Normal",
                    EnqueuePush: true,
                    EnqueueEmail: false),
                occurredAtUtc);
        }
    }

    private async Task TryDeleteExtraObjectsAsync(IReadOnlyCollection<string> objectKeys)
    {
        if (objectKeys.Count == 0)
        {
            return;
        }

        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var objectKey in objectKeys)
        {
            try
            {
                await objectStorage.DeleteAsync(objectKey, cleanup.Token);
            }
            catch
            {
                // Best-effort compensation cleanup for auxiliary evidence objects.
            }
        }
    }

}
