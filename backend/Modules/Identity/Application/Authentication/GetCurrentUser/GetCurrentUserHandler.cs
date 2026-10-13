using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Application.Authentication.GetCurrentUser;

public sealed class GetCurrentUserHandler(
    ICurrentUser currentUser,
    IUserProfileReader profiles)
{
    public async Task<Result<UserProfileDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserId is not Guid userId)
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.InvalidSession);
        }

        var profile = await profiles.GetAsync(userId, cancellationToken);
        return profile is null
            ? Result.Failure<UserProfileDto>(IdentityErrors.InvalidSession)
            : Result.Success(profile);
    }

}
