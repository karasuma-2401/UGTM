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
    Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken);
    Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken);
    Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken);
    Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken);
    Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken);
    void Add(User user);
}
