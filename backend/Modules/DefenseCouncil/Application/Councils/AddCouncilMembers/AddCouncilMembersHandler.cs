using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
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
    ICacheService? cache = null)
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
