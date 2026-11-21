using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken);

    Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode,
        CancellationToken cancellationToken);
    Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken cancellationToken);
    Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken);
    Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken cancellationToken);
    Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken);
    Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken);
    Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetRolesByNamesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken)
    {
        var results = new List<Role>();
        foreach (var name in roleNames)
        {
            if (!string.IsNullOrWhiteSpace(name)
                && !long.TryParse(name.Trim(), out _)
                && Enum.TryParse<SystemRoleName>(name.Trim(), ignoreCase: true, out var sysRole)
                && Enum.IsDefined(sysRole))
            {
                results.Add(new Role
                {
                    Id = (int)sysRole + 1,
                    RoleName = sysRole.ToString(),
                    Description = sysRole.ToString(),
                    IsHidden = false
                });
            }
        }
        return Task.FromResult<IReadOnlyList<Role>>(results);
    }

    async Task AddToRolesByNameAsync(
        User user,
        IReadOnlyCollection<string> roleNames,
        DateTime assignedAt,
        CancellationToken cancellationToken)
    {
        foreach (var roleName in roleNames)
        {
            var sysRole = Enum.Parse<SystemRoleName>(roleName, ignoreCase: true);
            await AddToRoleAsync(user, sysRole, assignedAt, cancellationToken);
        }
    }

    Task UpdateRolesByNameAsync(
        User user,
        IReadOnlyCollection<string> roleNames,
        DateTime assignedAt,
        CancellationToken cancellationToken)
    {
        var sysRoles = roleNames.Select(r => Enum.Parse<SystemRoleName>(r, ignoreCase: true)).ToList();
        return UpdateRolesAsync(user, sysRoles, assignedAt, cancellationToken);
    }
    Task AcquireAccountProvisioningLockAsync(CancellationToken cancellationToken);
    Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken);
    Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken);
    Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken);
    void Add(User user);
}
