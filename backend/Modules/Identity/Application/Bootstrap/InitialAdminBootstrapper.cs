using Microsoft.Extensions.Logging;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Bootstrap;

public sealed class InitialAdminBootstrapper(
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    IPasswordPolicy passwordPolicy,
    IPasswordService passwordService,
    IClock clock,
    IAuditLogWriter auditLogs,
    ILogger<InitialAdminBootstrapper> logger) : IInitialAdminBootstrapper
{
    public Task<Result<Guid>> BootstrapAdminAsync(
        string email,
        string password,
        string? fullName = null,
        string? userCode = null,
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await users.AcquireAdminMutationLockAsync(ct);
            await users.AcquireAccountProvisioningLockAsync(ct);

            if (await users.AnyAdminExistsAsync(ct))
            {
                logger.LogInformation("Khởi tạo Admin bị bỏ qua: Hệ thống đã tồn tại tài khoản Quản trị viên (Admin).");
                return Result.Failure<Guid>(IdentityErrors.AdminAlreadyExists);
            }

            if (string.IsNullOrWhiteSpace(email) || !EmailValidator.IsValid(email))
            {
                return Result.Failure<Guid>(IdentityErrors.InvalidInput);
            }

            var adminFullName = string.IsNullOrWhiteSpace(fullName)
                ? "System Administrator"
                : fullName.Trim();

            var normalizedUserCode = string.IsNullOrWhiteSpace(userCode)
                ? null
                : userCode.Trim().ToUpperInvariant();

            if (adminFullName.Length > EmailValidator.MaxFullNameLength
                || (normalizedUserCode is not null && normalizedUserCode.Length > EmailValidator.MaxUserCodeLength))
            {
                return Result.Failure<Guid>(IdentityErrors.InvalidInput);
            }

            var normalizedEmail = email.Trim().ToUpperInvariant();
            if (await users.ExistsByEmailAsync(normalizedEmail, null, ct))
            {
                return Result.Failure<Guid>(IdentityErrors.DuplicateAccount);
            }

            if (normalizedUserCode != null && await users.ExistsByUserCodeAsync(normalizedUserCode, null, ct))
            {
                return Result.Failure<Guid>(IdentityErrors.DuplicateAccount);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return Result.Failure<Guid>(IdentityErrors.WeakPassword);
            }

            var policyResult = passwordPolicy.Validate(password);
            if (!policyResult.IsValid)
            {
                return Result.Failure<Guid>(IdentityErrors.WeakPassword);
            }

            var now = clock.UtcNow;

            var user = User.Create(
                Guid.NewGuid(),
                email.Trim(),
                normalizedEmail,
                adminFullName,
                normalizedUserCode,
                phoneNumber: null,
                passwordHash: null,
                AccountStatus.PendingActivation,
                now);

            var passwordHash = passwordService.Hash(user, password);
            user.Activate(passwordHash, now);

            users.Add(user);
            await users.AddToRoleAsync(user, SystemRoleName.Admin, now, ct);

            auditLogs.Add(new AuditEntry(
                TargetType: "User",
                TargetId: user.Id.ToString("D"),
                Action: AuditActions.UserCreated,
                Outcome: AuditOutcomes.Success,
                ReasonCode: "InitialAdminBootstrap",
                Metadata: new
                {
                    Email = user.Email,
                    FullName = user.FullName,
                    Roles = new[] { nameof(SystemRoleName.Admin) }
                }));

            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation("Khởi tạo tài khoản Quản trị viên (Admin) đầu tiên thành công: {Email}", user.Email);
            return Result.Success(user.Id);
        }, cancellationToken);
    }
}
