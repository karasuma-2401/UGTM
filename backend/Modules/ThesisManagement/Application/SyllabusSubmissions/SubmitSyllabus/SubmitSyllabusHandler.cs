using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.SubmitSyllabus;

public sealed class SubmitSyllabusHandler(
    IStudentGroupRepository groupRepository,
    ISyllabusRepository syllabusRepository,
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
    public async Task<Result<SyllabusSubmissionDto>> HandleAsync(
        SubmitSyllabusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.AccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.FileName)
            || command.Content is null
            || !command.Content.CanRead)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.GroupNotFound);
        }

        var membership = group.Members.SingleOrDefault(member => member.StudentId == currentUser.UserId.Value);
        if (membership is null || membership.ConfirmationStatus != MemberConfirmationStatus.Confirmed)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.AccessDenied);
        }

        var syllabus = await syllabusRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var isRenameResubmission = group.AllowSyllabusResubmission;

        if (isRenameResubmission)
        {
            if (syllabus is null)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
            }
        }
        else
        {
            if (syllabus is null)
            {
                if (group.Status != StudentGroupStatus.Registered)
                {
                    return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
                }
            }
            else if (syllabus.Status != SyllabusStatus.NeedRevision
                || group.Status != StudentGroupStatus.SyllabusRevisionRequired)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
            }

            var stageWindow = await batchReader.GetSyllabusSubmissionWindowAsync(
                membership.BatchId,
                occurredAt,
                cancellationToken);
            if (stageWindow is null || !stageWindow.IsStageWindowOpen)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.StageWindowClosed);
            }

            var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
                membership.BatchId,
                ReviewGateType.Syllabus,
                cancellationToken);
            if (gateConfiguration is null)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.GateConfigurationNotFound);
            }

            if (syllabus is null)
            {
                if (occurredAt > gateConfiguration.InitialSubmissionDeadlineUtc)
                {
                    return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InitialSubmissionDeadlinePassed);
                }
            }
            else if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.FinalClosureDeadlinePassed);
            }
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
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidFile);
        }

        var isFirstSubmission = syllabus is null;
        var versionNo = isFirstSubmission ? 1 : checked(syllabus!.CurrentVersionNo + 1);
        var syllabusId = syllabus?.Id ?? Guid.NewGuid();
        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.Syllabus,
            syllabusId,
            versionNo,
            validatedFile.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.Syllabus,
            EntityId = syllabusId,
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
            if (isRenameResubmission)
            {
                syllabus!.SubmitUpdatedAfterRename();
                group.ConsumeSyllabusResubmission();
            }
            else if (isFirstSubmission)
            {
                syllabus = Syllabus.CreateSubmitted(syllabusId, command.GroupId);
                syllabusRepository.Add(syllabus);

                if (!group.TrySubmitSyllabus())
                {
                    return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
                }
            }
            else
            {
                syllabus!.SubmitRevision();

                if (!group.TrySubmitSyllabus())
                {
                    return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
                }
            }

            submissionFileRepository.Add(submissionFile);
            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "Syllabus",
                    syllabus.Id.ToString(),
                    IntegrationAuditActions.SyllabusSubmitted,
                    "Succeeded",
                    null,
                    new
                    {
                        syllabus.GroupId,
                        syllabus.CurrentVersionNo,
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
                    syllabus,
                    submissionFile,
                    currentUser.UserId.Value,
                    occurredAt,
                    cancellationToken);
            }

            var saveError = await uploadLifecycle.SaveAsync(uploadAttempt,
                token => unitOfWork.TrySaveChangesAsync(SyllabusSubmissionErrors.ConcurrencyConflict,
                    SyllabusSubmissionErrors.ConcurrencyConflict, token),
                SyllabusSubmissionErrors.ConcurrencyConflict, cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<SyllabusSubmissionDto>(saveError);
            }

            return Result.Success(SyllabusSubmissionMapper.ToSubmissionDto(syllabus, submissionFile));
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
        Syllabus syllabus,
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
        var versionLabel = $"v{syllabus.CurrentVersionNo}";
        var shortTopic = ThesisNotificationFormatting.Shorten(topicTitle, 65);

        if (confirmedStudents.Count > 0)
        {
            var metadata = new List<NotificationEmailMetaItem>
            {
                new("Mốc nộp", "Đề cương KLTN (Cổng 1)")
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
                    Title: $"Xác nhận nộp Đề cương KLTN ({versionLabel})",
                    Content: $"Ghi nhận tệp Đề cương chi tiết {versionLabel} của nhóm. Khoa sẽ thẩm định và phản hồi sớm theo kế hoạch.",
                    RecipientUserIds: confirmedStudents,
                    ResourceType: "Syllabus",
                    ResourceId: syllabus.Id,
                    AcademicBatchId: batchId,
                    ActionPath: "/thesis/proposal",
                    Priority: "Critical",
                    EnqueuePush: true,
                    EnqueueEmail: true,
                    EmailSubject: $"[UGTM] Xác nhận nộp Đề cương KLTN ({versionLabel})",
                    EmailCategoryLabel: "Xác nhận nộp bài",
                    EmailCtaLabel: "Xem trang Đề cương",
                    EmailNotice: "Vui lòng theo dõi kết quả thẩm định của Khoa trên hệ thống.",
                    EmailMetadata: metadata),
                occurredAtUtc);
        }

        notificationOutbox.Enqueue(
            new NotificationOutboxEntry(
                NotificationType: "SYLLABUS_SUBMITTED",
                Title: $"Đề cương {versionLabel} chờ thẩm định",
                Content: $"Nhóm đề tài \"{shortTopic}\" vừa nộp Đề cương {versionLabel} chờ Khoa thẩm định.",
                RecipientRoles: ["Faculty"],
                RecipientPermissions: [PermissionCodes.ThesisReviewGatesReview],
                ExcludeUserIds: [uploaderId],
                ResourceType: "Syllabus",
                ResourceId: syllabus.Id,
                AcademicBatchId: batchId,
                ActionPath: "/thesis/review/syllabus",
                Priority: "Normal",
                EnqueuePush: true,
                EnqueueEmail: false),
            occurredAtUtc);

        if (advisorId.HasValue && advisorId.Value != Guid.Empty)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "SYLLABUS_SUBMITTED",
                    Title: $"Nhóm hướng dẫn đã nộp Đề cương {versionLabel}",
                    Content: $"Nhóm đề tài \"{shortTopic}\" vừa nộp tệp Đề cương chi tiết phiên bản {versionLabel}.",
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
