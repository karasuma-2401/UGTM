using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;

public sealed record CreateCouncilCommand(
    Guid BatchId,
    string CouncilCode,
    DateOnly DefenseDate,
    string TimeSlot,
    string Room);

public sealed class CreateCouncilHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    IKltnBatchReader batchReader,
    ICurrentUser currentUser)
{
    public async Task<Result<CouncilResponseDto>> HandleAsync(
        CreateCouncilCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.Forbidden);
        }

        var isStaffOrAdmin = currentUser.Roles.Any(r => r is "Admin" or "AcademicAffairs" or "Faculty");
        if (!isStaffOrAdmin)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.Forbidden);
        }

        if (command.BatchId == Guid.Empty)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.BatchNotFound);
        }

        if (string.IsNullOrWhiteSpace(command.CouncilCode)
            || string.IsNullOrWhiteSpace(command.TimeSlot)
            || string.IsNullOrWhiteSpace(command.Room))
        {
            return Result.Failure<CouncilResponseDto>(
                Error.Validation("Council.InvalidInput", "Mã hội đồng, ca thi và phòng thi không được để trống."));
        }

        var batchExists = await batchReader.ExistsAsync(command.BatchId, cancellationToken);
        if (!batchExists)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.BatchNotFound);
        }

        var codeExists = await councilRepository.ExistsByCodeInBatchAsync(
            command.BatchId,
            command.CouncilCode,
            null,
            cancellationToken);

        if (codeExists)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.DuplicateCode);
        }

        var roomConflict = await councilRepository.HasRoomScheduleConflictAsync(
            command.DefenseDate,
            command.TimeSlot,
            command.Room,
            null,
            cancellationToken);

        if (roomConflict)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.RoomScheduleConflict);
        }

        var council = Council.Create(
            command.BatchId,
            command.CouncilCode,
            command.DefenseDate,
            command.TimeSlot,
            command.Room);

        await councilRepository.AddAsync(council, cancellationToken);
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
