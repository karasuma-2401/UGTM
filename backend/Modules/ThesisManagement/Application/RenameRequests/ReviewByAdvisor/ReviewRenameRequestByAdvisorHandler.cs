using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;

public sealed class ReviewRenameRequestByAdvisorHandler(
    IRenameRequestRepository renameRequestRepository,
    IStudentGroupRepository groupRepository,
    ITopicRenameContract topicRenameContract,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IThesisNotificationOutbox? notificationOutbox = null)
{
    public async Task<Result<RenameRequestDto>> HandleAsync(
        ReviewRenameRequestByAdvisorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
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

        var request = await renameRequestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.RequestNotFound);
        }

        if (request.Status != RenameRequestStatus.Created)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidReviewState);
        }

        var group = await groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.GroupNotFound);
        }

        var topicSnapshot = await topicRenameContract.GetTopicSnapshotAsync(group.TopicId, cancellationToken);
        if (topicSnapshot is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.TopicNotFound);
        }

        if (!ThesisAuthorization.CanReviewRenameRequestAdvisor(currentUser, topicSnapshot.AdvisorId))
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (isApprove)
        {
            request.ApproveByAdvisor(nowUtc);
        }
        else
        {
            request.RejectByAdvisor(command.RejectionReason!.Trim(), currentUser.UserId.Value, nowUtc);
        }

        if (notificationOutbox is not null)
        {
            var batchId = group.Members.Select(m => (Guid?)m.BatchId).FirstOrDefault();
            var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(group);
            var shortNewTitle = ThesisNotificationFormatting.Shorten(request.NewTitleVi, 60);

            if (isApprove)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "RENAME_REQUEST_ADVISOR_APPROVED",
                        Title: "Yêu cầu đổi tên đề tài chờ Khoa duyệt",
                        Content: $"GVHD đã đồng ý đổi tên đề tài sang \"{shortNewTitle}\". Khoa vui lòng thẩm định.",
                        RecipientRoles: ["Faculty"],
                        RecipientPermissions: [PermissionCodes.ThesisReviewGatesReview],
                        ExcludeUserIds: [currentUser.UserId.Value],
                        ResourceType: "RenameRequest",
                        ResourceId: request.Id,
                        AcademicBatchId: batchId,
                        ActionPath: "/topics/rename/review/faculty",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    nowUtc);

                if (confirmedStudents.Count > 0)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "RENAME_REQUEST_ADVISOR_APPROVED",
                            Title: "GVHD đã đồng ý đổi tên đề tài",
                            Content: "GVHD đã xác nhận yêu cầu đổi tên đề tài của nhóm, hồ sơ đã chuyển Khoa thẩm định.",
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
            }
            else if (confirmedStudents.Count > 0)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "RENAME_REQUEST_REJECTED",
                        Title: "Yêu cầu đổi tên đề tài bị từ chối",
                        Content: $"GVHD từ chối yêu cầu đổi tên đề tài. Lý do: {ThesisNotificationFormatting.Shorten(command.RejectionReason, 80)}",
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
