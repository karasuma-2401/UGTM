using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;

public sealed record AddCouncilMembersCommand(
    Guid CouncilId,
    IReadOnlyList<CouncilMemberInputDto> Members);

public sealed class AddCouncilMembersHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICouncilUserInfoReader userInfoReader,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICurrentUser currentUser,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null,
    TimeProvider? timeProvider = null)
{
    public async Task<Result<IReadOnlyList<CouncilMemberResponseDto>>> HandleAsync(
        AddCouncilMembersCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!CouncilMemberAssignmentHelper.IsCouncilStaffOrAdmin(currentUser))
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.Forbidden);
        }

        var inputValidation = CouncilMemberAssignmentHelper.ValidateMemberInputList(command.Members);
        if (inputValidation.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(inputValidation.Error);
        }

        var council = await councilRepository.GetByIdWithDetailsAsync(command.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.NotFound);
        }

        var validation = await CouncilMemberAssignmentHelper.ValidateForCouncilAsync(
            council,
            command.Members,
            isReplaceMode: false,
            councilRepository,
            userInfoReader,
            defenseGroupContract,
            topicDefenseContract,
            cancellationToken);

        if (validation.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(validation.Error);
        }

        var validated = validation.Value;
        council.ResetSubmission();
        await councilRepository.AddMembersAsync(validated.NewMembers, cancellationToken);

        if (notificationOutbox is not null)
        {
            var nowUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            var scheduleText = DefenseCouncilNotificationFormatting.FormatSchedule(
                council.DefenseDate, council.TimeSlot, council.Room);

            foreach (var member in validated.NewMembers)
            {
                var roleLabel = DefenseCouncilNotificationFormatting.FormatCouncilRole(member.RoleInCouncil);
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "COUNCIL_MEMBER_ASSIGNED",
                        Title: $"Phân công tham gia Hội đồng {council.CouncilCode}",
                        Content: $"Thầy/cô được phân công làm {roleLabel} tại Hội đồng {council.CouncilCode} ({scheduleText}).",
                        RecipientUserIds: [member.UserId],
                        ResourceType: "Council",
                        ResourceId: council.Id,
                        AcademicBatchId: council.BatchId,
                        ActionPath: "/councils/grading",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Phân công Hội đồng {council.CouncilCode} ({roleLabel})",
                        EmailCategoryLabel: "Phân công Hội đồng",
                        EmailCtaLabel: "Xem không gian Hội đồng",
                        EmailNotice: "Lịch dự kiến có thể được điều chỉnh trước khi PĐT ban hành quyết định chính thức.",
                        EmailMetadata:
                        [
                            new("Mã hội đồng", council.CouncilCode),
                            new("Vai trò", roleLabel),
                            new("Lịch bảo vệ", scheduleText)
                        ]),
                    nowUtc);
            }
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DefenseCouncilUniqueConstraintException error) when (error.ConstraintName is
            "ux_council_member_leadership_role" or "ix_council_member_council_id_user_id")
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.ConcurrentModification);
        }

        await userInfoReader.GrantCouncilMemberRoleAsync(validated.MemberUserIds, cancellationToken);
        await DefenseCouncilCacheKeys.InvalidateCouncilAsync(cache, council.Id, council.BatchId, cancellationToken);

        var resultDtos = CouncilMemberAssignmentHelper.MapMemberDtos(validated.NewMembers, validated.UsersInfo);
        return Result.Success(resultDtos);
    }
}
