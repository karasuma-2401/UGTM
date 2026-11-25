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

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;

public sealed class ReviewRenameRequestByFacultyHandler(
    IRenameRequestRepository renameRequestRepository,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IStudentGroupRepository? groupRepository = null,
    ITopicRenameContract? topicRenameContract = null,
    IThesisNotificationOutbox? notificationOutbox = null)
{
    public async Task<Result<RenameRequestDto>> HandleAsync(
        ReviewRenameRequestByFacultyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        if (!ThesisAuthorization.CanReviewRenameRequestFaculty(currentUser))
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

        if (request.Status != RenameRequestStatus.AdvisorConfirmed)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidReviewState);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (isApprove)
        {
            request.ApproveByFaculty(nowUtc);
        }
        else
        {
            request.RejectByFaculty(command.RejectionReason!.Trim(), currentUser.UserId.Value, nowUtc);
        }

        if (notificationOutbox is not null)
        {
            var group = groupRepository is null
                ? null
                : await groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
            var batchId = group?.Members.Select(m => (Guid?)m.BatchId).FirstOrDefault();
            var shortNewTitle = ThesisNotificationFormatting.Shorten(request.NewTitleVi, 60);

            if (isApprove)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "RENAME_REQUEST_FACULTY_APPROVED",
                        Title: "Yêu cầu đổi tên đề tài chờ PĐT phê duyệt",
                        Content: $"Khoa đã thông qua đổi tên đề tài sang \"{shortNewTitle}\" chờ PĐT phê duyệt.",
                        RecipientRoles: ["AcademicAffairs"],
                        ExcludeUserIds: [currentUser.UserId.Value],
                        ResourceType: "RenameRequest",
                        ResourceId: request.Id,
                        AcademicBatchId: batchId,
                        ActionPath: "/topics/rename/review/academic-affairs",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    nowUtc);
            }
            else
            {
                var confirmedStudents = group is null
                    ? [request.RequestedByStudentId]
                    : ThesisNotificationFormatting.GetConfirmedStudentIds(group);

                if (confirmedStudents.Count > 0)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "RENAME_REQUEST_REJECTED",
                            Title: "Khoa từ chối yêu cầu đổi tên đề tài",
                            Content: $"Khoa không thông qua yêu cầu đổi tên đề tài. Lý do: {ThesisNotificationFormatting.Shorten(command.RejectionReason, 80)}",
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

                if (group is not null && topicRenameContract is not null)
                {
                    var snapshot = await topicRenameContract.GetTopicSnapshotAsync(group.TopicId, cancellationToken);
                    if (snapshot is { AdvisorId: var advisorId } && advisorId != Guid.Empty)
                    {
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "RENAME_REQUEST_REJECTED",
                                Title: "Khoa từ chối đổi tên đề tài hướng dẫn",
                                Content: $"Khoa không thông qua đổi tên đề tài sang \"{shortNewTitle}\".",
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
