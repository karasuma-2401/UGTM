using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Authentication.ChangePassword;

public sealed class ChangePasswordHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IAuthenticationSessionRepository sessions,
    IPasswordService passwords,
    IPasswordPolicy passwordPolicy,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
        {
            return Result.Failure(IdentityErrors.InvalidSession);
        }

        var validation = passwordPolicy.Validate(command.NewPassword);
        if (!validation.IsValid)
        {
            return Result.Failure(IdentityErrors.WeakPassword);
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await users.GetByIdAsync(userId, ct);
            if (user?.PasswordHash is null
                || !passwords.Verify(user, user.PasswordHash, command.CurrentPassword))
            {
                return Result.Failure(IdentityErrors.CurrentPasswordIncorrect);
            }

            var now = clock.UtcNow;
            user.ChangePassword(passwords.Hash(user, command.NewPassword), now);
            await sessions.RevokeAllAsync(
                user.Id,
                SessionRevocationReason.PasswordChanged,
                now,
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}