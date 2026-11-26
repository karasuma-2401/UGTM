using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;

public sealed class UpdateCouncilScheduleHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICouncilUserInfoReader userInfoReader,
    ICurrentUser currentUser,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null,
    IDefenseGroupContract? defenseGroupContract = null,
    TimeProvider? timeProvider = null)
{
    public async Task<Result<CouncilResponseDto>> HandleAsync(
        UpdateCouncilScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.Forbidden);
        }

        var isAuthorized = currentUser.Roles.Any(r => r is "Faculty" or "Admin");
        if (!isAuthorized)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.Forbidden);
        }

        if (command.DefenseDate == default
            || string.IsNullOrWhiteSpace(command.TimeSlot)
            || string.IsNullOrWhiteSpace(command.Room))
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.InvalidSchedule);
        }

        var council = await councilRepository.GetByIdWithDetailsAsync(command.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.NotFound);
        }

        if (council.Status != CouncilStatus.Draft)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.NotInDraftStatus);
        }

        var roomConflict = await councilRepository.HasRoomScheduleConflictAsync(
            command.DefenseDate,
            command.TimeSlot,
            command.Room,
            excludeCouncilId: council.Id,
            cancellationToken);

        if (roomConflict)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.RoomScheduleConflict);
        }

        var memberUserIds = council.Members.Select(m => m.UserId).Distinct().ToList();
        if (memberUserIds.Count > 0)
        {
            var conflictingMemberIds = await councilRepository.GetConflictingMemberUserIdsAsync(
                command.DefenseDate,
                command.TimeSlot,
                memberUserIds,
                excludeCouncilId: council.Id,
                cancellationToken);

            if (conflictingMemberIds.Count > 0)
            {
                var usersInfo = await userInfoReader.GetUsersByIdsAsync(conflictingMemberIds, cancellationToken);
                var names = string.Join(", ", conflictingMemberIds.Select(id =>
                    usersInfo.TryGetValue(id, out var u) ? u.FullName : id.ToString()));

                return Result.Failure<CouncilResponseDto>(CouncilErrors.MemberScheduleConflict(names));
            }
        }

        var oldSchedule = DefenseCouncilNotificationFormatting.FormatSchedule(
            council.DefenseDate, council.TimeSlot, council.Room);

        council.UpdateSchedule(command.DefenseDate, command.TimeSlot, command.Room);

        var newSchedule = DefenseCouncilNotificationFormatting.FormatSchedule(
            council.DefenseDate, council.TimeSlot, council.Room);

        if (notificationOutbox is not null
            && !string.Equals(oldSchedule, newSchedule, StringComparison.Ordinal))
        {
            var nowUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            var metadata = new List<NotificationEmailMetaItem>
            {
                new("Mã hội đồng", council.CouncilCode),
                new("Lịch cũ", oldSchedule),
                new("Lịch mới", newSchedule)
            };

            if (memberUserIds.Count > 0)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "COUNCIL_SCHEDULE_UPDATED",
                        Title: $"Đính chính lịch bảo vệ Hội đồng {council.CouncilCode}",
                        Content: $"Lịch bảo vệ Hội đồng {council.CouncilCode} đã đổi sang {newSchedule}. Thầy/cô vui lòng cập nhật.",
                        RecipientUserIds: memberUserIds,
                        ResourceType: "Council",
                        ResourceId: council.Id,
                        AcademicBatchId: council.BatchId,
                        ActionPath: "/councils/grading",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Đính chính lịch Hội đồng {council.CouncilCode}",
                        EmailCategoryLabel: "Đính chính lịch bảo vệ",
                        EmailCtaLabel: "Xem lịch Hội đồng",
                        EmailMetadata: metadata),
                    nowUtc);
            }

            if (defenseGroupContract is not null && council.Assignments.Count > 0)
            {
                var groupIds = council.Assignments.Select(a => a.GroupId).Distinct().ToArray();
                var groups = await defenseGroupContract.GetGroupsByIdsAsync(groupIds, cancellationToken);
                var studentIds = groups.Values
                    .SelectMany(g => g.Members
                        .Where(m => string.Equals(m.ConfirmationStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
                        .Select(m => m.StudentId))
                    .Distinct()
                    .ToArray();

                if (studentIds.Length > 0)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "COUNCIL_SCHEDULE_UPDATED",
                            Title: $"Đính chính lịch bảo vệ Hội đồng {council.CouncilCode}",
                            Content: $"Lịch bảo vệ của nhóm tại Hội đồng {council.CouncilCode} đã chuyển sang {newSchedule}.",
                            RecipientUserIds: studentIds,
                            ResourceType: "Council",
                            ResourceId: council.Id,
                            AcademicBatchId: council.BatchId,
                            ActionPath: "/dashboard",
                            Priority: "Critical",
                            EnqueuePush: true,
                            EnqueueEmail: true,
                            EmailSubject: $"[UGTM] Đính chính lịch bảo vệ KLTN ({council.CouncilCode})",
                            EmailCategoryLabel: "Đính chính lịch bảo vệ",
                            EmailCtaLabel: "Xem thông tin bảo vệ",
                            EmailMetadata: metadata),
                        nowUtc);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DefenseCouncilCacheKeys.InvalidateCouncilAsync(cache, council.Id, council.BatchId, cancellationToken);

        return Result.Success(new CouncilResponseDto(
            council.Id,
            council.BatchId,
            council.CouncilCode,
            council.DefenseDate,
            council.TimeSlot,
            council.Room,
            council.Status.ToString(),
            council.DecisionNumber,
            council.SignedDate,
            council.DecisionFileUrl,
            council.ApprovedBy,
            council.ApprovedAt));
    }
}
