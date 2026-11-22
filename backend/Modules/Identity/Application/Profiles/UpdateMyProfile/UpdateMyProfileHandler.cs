using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Profiles.UpdateMyProfile;

public sealed class UpdateMyProfileHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IUserProfileReader profiles,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public async Task<Result<UserProfileDto>> HandleAsync(
        UpdateMyProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.InvalidSession);
        }

        if (!PhoneNumberValidator.IsValid(command.PhoneNumber))
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.InvalidPhoneNumber);
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(command.PhoneNumber)
            ? null
            : command.PhoneNumber.Trim();

        var txResult = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await users.GetByIdAsync(userId, ct);
            if (user is null)
            {
                return Result.Failure(IdentityErrors.InvalidSession);
            }

            if (!string.Equals(user.PhoneNumber, normalizedPhone, StringComparison.Ordinal))
            {
                user.UpdateProfile(user.FullName, normalizedPhone, user.UserCode, clock.UtcNow);
                auditLogs.Add(new AuditEntry(
                    TargetType: "User",
                    TargetId: user.Id.ToString("D"),
                    Action: AuditActions.ProfileUpdated,
                    Outcome: AuditOutcomes.Success,
                    ReasonCode: null,
                    Metadata: new
                    {
                        ChangedFields = new[] { "phoneNumber" }
                    }));
                await unitOfWork.SaveChangesAsync(ct);
            }

            return Result.Success();
        }, cancellationToken);

        if (txResult.IsFailure)
        {
            return Result.Failure<UserProfileDto>(txResult.Error);
        }

        var updatedProfile = await profiles.GetAsync(userId, cancellationToken);
        return updatedProfile is null
            ? Result.Failure<UserProfileDto>(IdentityErrors.InvalidSession)
            : Result.Success(updatedProfile);
    }
}
