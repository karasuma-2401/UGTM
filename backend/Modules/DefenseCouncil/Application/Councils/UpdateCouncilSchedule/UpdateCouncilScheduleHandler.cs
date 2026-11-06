using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;

public sealed class UpdateCouncilScheduleHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICouncilUserInfoReader userInfoReader,
    ICurrentUser currentUser)
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

        // Chống trùng phòng thi (Anti-Double Booking)
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

        // Chống trùng lịch thành viên
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

        // Cập nhật lịch dự thảo (tự động reset isSubmittedToAcademicAffairs = false)
        council.UpdateSchedule(command.DefenseDate, command.TimeSlot, command.Room);

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
