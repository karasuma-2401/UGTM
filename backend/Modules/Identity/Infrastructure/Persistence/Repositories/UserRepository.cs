using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    // A transaction-scoped PostgreSQL advisory lock serializes bootstrap, demotion,
    // suspension, and disabling decisions that could otherwise remove the last active Admin.
    private const long AdminMutationLockKey = 8_127_049_532_100_001;
    private const long AccountProvisioningLockKey = 8_127_049_532_100_002;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct)
        => db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<User?> GetByNormalizedEmailAsync(string email, CancellationToken ct)
        => db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == email, ct);

    public Task<User?> GetByUserCodeAsync(string code, CancellationToken ct)
        => db.Users.SingleOrDefaultAsync(x => x.UserCode == code, ct);

    public Task<User?> GetByIdentifierAsync(string email, string code, CancellationToken ct)
        => db.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == email || x.UserCode == code,
            ct);

    public Task<bool> ExistsByEmailOrCodeAsync(string email, string code, CancellationToken ct)
        => db.Users.AnyAsync(x => x.NormalizedEmail == email || x.UserCode == code, ct);

    public Task<bool> ExistsByUserCodeAsync(string code, Guid? excludeUserId, CancellationToken ct)
    {
        var query = db.Users.Where(x => x.UserCode == code);
        if (excludeUserId.HasValue)
        {
            query = query.Where(x => x.Id != excludeUserId.Value);
        }
        return query.AnyAsync(ct);
    }

    public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken ct)
    {
        var query = db.Users.Where(x => x.NormalizedEmail == normalizedEmail);
        if (excludeUserId.HasValue)
        {
            query = query.Where(x => x.Id != excludeUserId.Value);
        }
        return query.AnyAsync(ct);
    }

    public async Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(
        IReadOnlyCollection<string> normalizedEmails,
        CancellationToken ct)
        => (await db.Users
                .Where(user => normalizedEmails.Contains(user.NormalizedEmail))
                .Select(user => user.NormalizedEmail)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlySet<string>> GetExistingUserCodesAsync(
        IReadOnlyCollection<string> userCodes,
        CancellationToken ct)
        => (await db.Users
                .Where(user => user.UserCode != null && userCodes.Contains(user.UserCode))
                .Select(user => user.UserCode!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Add(User user) => db.Users.Add(user);

    public async Task AddToRoleAsync(
        User user,
        SystemRoleName role,
        DateTime assignedAt,
        CancellationToken ct)
    {
        var roleName = role.ToString();
        var roleId = await db.Roles.Where(x => x.RoleName == roleName)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Vai trò '{role}' chưa được khởi tạo trong bảng identity.role của cơ sở dữ liệu.");
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = roleId,
            AssignedAt = assignedAt
        });
    }

    public async Task AddRangeToRoleAsync(
        IReadOnlyCollection<User> users,
        SystemRoleName role,
        DateTime assignedAt,
        CancellationToken ct)
    {
        var roleName = role.ToString();
        var roleId = await db.Roles
            .Where(entity => entity.RoleName == roleName)
            .Select(entity => (int?)entity.Id)
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Vai trò '{role}' chưa được khởi tạo trong bảng identity.role của cơ sở dữ liệu.");

        db.Users.AddRange(users);
        db.UserRoles.AddRange(users.Select(user => new UserRole
        {
            UserId = user.Id,
            RoleId = roleId,
            AssignedAt = assignedAt
        }));
    }

    public async Task UpdateRolesAsync(
        User user,
        IEnumerable<SystemRoleName> roles,
        DateTime assignedAt,
        CancellationToken ct)
    {
        var targetRoleNames = roles.Select(r => r.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentRoles = await db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync(ct);

        var rolesToRemove = currentRoles
            .Where(ur => !targetRoleNames.Contains(ur.Role.RoleName))
            .ToList();
        if (rolesToRemove.Count > 0)
        {
            db.UserRoles.RemoveRange(rolesToRemove);
        }

        var existingRoleNames = currentRoles
            .Select(ur => ur.Role.RoleName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rolesToAdd = targetRoleNames
            .Where(r => !existingRoleNames.Contains(r))
            .ToList();

        if (rolesToAdd.Count > 0)
        {
            var roleEntities = await db.Roles
                .Where(r => rolesToAdd.Contains(r.RoleName))
                .ToListAsync(ct);

            if (roleEntities.Count != rolesToAdd.Count)
            {
                throw new InvalidOperationException("Một hoặc nhiều vai trò chưa được khởi tạo trong bảng identity.role của cơ sở dữ liệu.");
            }

            foreach (var role in roleEntities)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    AssignedAt = assignedAt
                });
            }
        }
    }

    public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken ct)
    {
        var adminRoleName = nameof(SystemRoleName.Admin);
        var query = db.Users
            .Where(u => u.Status == AccountStatus.Active && u.UserRoles.Any(ur => ur.Role.RoleName == adminRoleName));

        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludeUserId.Value);
        }

        return query.CountAsync(ct);
    }

    public Task AcquireAdminMutationLockAsync(CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({AdminMutationLockKey})",
            ct);

    public Task AcquireAccountProvisioningLockAsync(CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({AccountProvisioningLockKey})",
            ct);

    public Task<bool> AnyAdminExistsAsync(CancellationToken ct)
        => db.UserRoles.AnyAsync(ur => ur.Role.RoleName == nameof(SystemRoleName.Admin), ct);
}
