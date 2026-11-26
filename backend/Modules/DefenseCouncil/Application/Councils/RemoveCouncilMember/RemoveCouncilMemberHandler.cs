using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.RemoveCouncilMember;

public sealed record RemoveCouncilMemberCommand(
    Guid CouncilId,
    Guid UserId);

public sealed class RemoveCouncilMemberHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICouncilUserInfoReader userInfoReader,
    ICurrentUser currentUser,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null,
    TimeProvider? timeProvider = null)
{
    public async Task<Result<IReadOnlyList<CouncilMemberResponseDto>>> HandleAsync(
        RemoveCouncilMemberCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!CouncilMemberAssignmentHelper.IsCouncilStaffOrAdmin(currentUser))
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.Forbidden);
        }

        var council = await councilRepository.GetByIdWithDetailsAsync(command.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.NotFound);
        }

        if (council.Status != CouncilStatus.Draft)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.NotInDraftStatus);
        }

        var memberToRemove = council.Members.FirstOrDefault(m => m.UserId == command.UserId);
        if (memberToRemove is null)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.MemberNotInCouncil(command.UserId));
        }

        var removedRoleLabel = DefenseCouncilNotificationFormatting.FormatCouncilRole(memberToRemove.RoleInCouncil);
        var scheduleText = DefenseCouncilNotificationFormatting.FormatSchedule(
            council.DefenseDate, council.TimeSlot, council.Room);

        council.ResetSubmission();
        await councilRepository.RemoveMemberAsync(memberToRemove, cancellationToken);

        if (notificationOutbox is not null)
        {
            var nowUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "ASSIGNMENT_CANCELLED",
                    Title: $"Rút tên khỏi Hội đồng {council.CouncilCode}",
                    Content: $"Phân công {removedRoleLabel} tại Hội đồng {council.CouncilCode} của thầy/cô đã được gỡ.",
                    RecipientUserIds: [command.UserId],
                    ResourceType: "Council",
                    ResourceId: council.Id,
                    AcademicBatchId: council.BatchId,
                    ActionPath: "/councils/grading",
                    Priority: "Critical",
                    EnqueuePush: true,
                    EnqueueEmail: true,
                    EmailSubject: $"[UGTM] Gỡ phân công Hội đồng {council.CouncilCode}",
                    EmailCategoryLabel: "Thay đổi phân công",
                    EmailCtaLabel: "Xem danh sách Hội đồng",
                    EmailMetadata:
                    [
                        new("Loại phân công", $"Thành viên Hội đồng ({removedRoleLabel})"),
                        new("Mã hội đồng", council.CouncilCode),
                        new("Lịch bảo vệ", scheduleText)
                    ]),
                nowUtc);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var stillInOtherCouncils = await councilRepository.GetUserIdsWithAnyCouncilMembershipAsync(
            [command.UserId],
            council.Id,
            cancellationToken);

        if (!stillInOtherCouncils.Contains(command.UserId))
        {
            await userInfoReader.RevokeCouncilMemberRoleAsync([command.UserId], cancellationToken);
        }

        await DefenseCouncilCacheKeys.InvalidateCouncilAsync(cache, council.Id, council.BatchId, cancellationToken);

        var remainingMembers = council.Members
            .Where(m => m.Id != memberToRemove.Id && m.UserId != command.UserId)
            .ToList();

        var remainingUserIds = remainingMembers.Select(m => m.UserId).Distinct().ToList();
        var usersInfo = remainingUserIds.Count > 0
            ? await userInfoReader.GetUsersByIdsAsync(remainingUserIds, cancellationToken)
            : new Dictionary<Guid, CouncilUserInfoContractDto>();

        var resultDtos = CouncilMemberAssignmentHelper.MapMemberDtos(remainingMembers, usersInfo);
        return Result.Success(resultDtos);
    }
}
