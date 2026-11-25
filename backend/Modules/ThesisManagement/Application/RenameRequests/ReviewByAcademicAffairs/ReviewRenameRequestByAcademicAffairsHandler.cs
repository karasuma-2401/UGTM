using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;

public sealed class ReviewRenameRequestByAcademicAffairsHandler(
    IRenameRequestRepository renameRequestRepository,
    IStudentGroupRepository groupRepository,
    ITopicRenameContract topicRenameContract,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IRenameRequestTransactionCoordinator coordinator,
    IThesisNotificationOutbox? notificationOutbox = null)
{
    public async Task<Result<RenameRequestDto>> HandleAsync(
        ReviewRenameRequestByAcademicAffairsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        if (!ThesisAuthorization.CanReviewRenameRequestAcademicAffairs(currentUser))
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        var isApprove = string.Equals(command.Decision, "Approve", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(command.Decision, "Reject", StringComparison.OrdinalIgnoreCase);

        if (!isApprove && !isReject)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidDecision);
        }

        if (isReject && string.IsNullOrWhiteSpace(command.RejectionReason))
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.RejectionReasonRequired);
        }

        return await coordinator.ExecuteAsync(
            token => HandleCoreAsync(command, isApprove, currentUser.UserId.Value, token), cancellationToken);
    }

    private async Task<Result<RenameRequestDto>> HandleCoreAsync(
        ReviewRenameRequestByAcademicAffairsCommand command,
        bool isApprove,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var request = await renameRequestRepository.LockByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.RequestNotFound);
        }

        if (request.Status != RenameRequestStatus.FacultyApproved)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidReviewState);
        }

        var group = await groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.GroupNotFound);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (isApprove)
        {
            var updateResult = await topicRenameContract.UpdateTopicTitlesAsync(
                group.TopicId,
                request.NewTitleVi,
                request.NewTitleEn,
                cancellationToken);

            if (!updateResult.IsSuccess)
            {
                return Result.Failure<RenameRequestDto>(
                    Error.Conflict(
                        updateResult.ErrorCode ?? "RenameRequest.TopicUpdateFailed",
                        updateResult.ErrorMessage ?? "Không thể cập nhật tên đề tài."));
            }

            request.ApproveByAcademicAffairs(nowUtc);
            group.EnableSyllabusResubmission();

            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "RenameRequest",
                    request.Id.ToString(),
                    IntegrationAuditActions.RenameRequestApproved,
                    "Succeeded",
                    null,
                    new
                    {
                        request.GroupId,
                        group.TopicId,
                        OldTitleVi = request.OldTitleVi,
                        NewTitleVi = request.NewTitleVi,
                        OldTitleEn = request.OldTitleEn,
                        NewTitleEn = request.NewTitleEn,
                        ApprovedBy = actorId
                    }),
                nowUtc);
        }
        else
        {
            request.RejectByAcademicAffairs(command.RejectionReason!.Trim(), actorId, nowUtc);
        }

        if (notificationOutbox is not null)
        {
            var batchId = group.Members.Select(m => (Guid?)m.BatchId).FirstOrDefault();
            var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(group);
            var topicSnapshot = await topicRenameContract.GetTopicSnapshotAsync(group.TopicId, cancellationToken);
            var shortNewTitle = ThesisNotificationFormatting.Shorten(request.NewTitleVi, 60);

            if (isApprove)
            {
                if (confirmedStudents.Count > 0)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "RENAME_REQUEST_APPROVED",
                            Title: "Đã phê duyệt đổi tên đề tài KLTN",
                            Content: "PĐT đã duyệt đổi tên đề tài và mở quyền nộp lại Đề cương cập nhật cho nhóm.",
                            RecipientUserIds: confirmedStudents,
                            ResourceType: "RenameRequest",
                            ResourceId: request.Id,
                            AcademicBatchId: batchId,
                            ActionPath: "/thesis/proposal",
                            Priority: "Normal",
                            EnqueuePush: true,
                            EnqueueEmail: false),
                        nowUtc);
                }

                if (topicSnapshot is { AdvisorId: var advisorId } && advisorId != Guid.Empty)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "RENAME_REQUEST_APPROVED",
                            Title: "Đề tài hướng dẫn đã cập nhật tên mới",
                            Content: $"PĐT đã phê duyệt đổi tên đề tài sang \"{shortNewTitle}\".",
                            RecipientUserIds: [advisorId],
                            ResourceType: "RenameRequest",
                            ResourceId: request.Id,
                            AcademicBatchId: batchId,
                            ActionPath: "/topics/advising",
                            Priority: "Normal",
                            EnqueuePush: true,
                            EnqueueEmail: false),
                        nowUtc);
                }
            }
            else
            {
                if (confirmedStudents.Count > 0)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "RENAME_REQUEST_REJECTED",
                            Title: "PĐT từ chối yêu cầu đổi tên đề tài",
                            Content: $"PĐT không phê duyệt yêu cầu đổi tên đề tài. Lý do: {ThesisNotificationFormatting.Shorten(command.RejectionReason, 80)}",
                            RecipientUserIds: confirmedStudents,
                            ResourceType: "RenameRequest",
                            ResourceId: request.Id,
                            AcademicBatchId: batchId,
                            ActionPath: "/topics/rename",
                            Priority: "Normal",
                            EnqueuePush: true,
                            EnqueueEmail: false),
                        nowUtc);
                }

                if (topicSnapshot is { AdvisorId: var advisorId } && advisorId != Guid.Empty)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "RENAME_REQUEST_REJECTED",
                            Title: "PĐT từ chối đổi tên đề tài hướng dẫn",
                            Content: $"PĐT không phê duyệt đổi tên đề tài sang \"{shortNewTitle}\".",
                            RecipientUserIds: [advisorId],
                            ResourceType: "RenameRequest",
                            ResourceId: request.Id,
                            AcademicBatchId: batchId,
                            ActionPath: "/topics/advising",
                            Priority: "Normal",
                            EnqueuePush: true,
                            EnqueueEmail: false),
                        nowUtc);
                }
            }
        }

        var saveError = await unitOfWork.TrySaveChangesAsync(
            RenameRequestErrors.ConcurrencyConflict,
            RenameRequestErrors.ConcurrencyConflict,
            cancellationToken);

        if (saveError is not null)
        {
            return Result.Failure<RenameRequestDto>(saveError);
        }

        return Result.Success(RenameRequestMapper.ToDto(request));
    }
}
