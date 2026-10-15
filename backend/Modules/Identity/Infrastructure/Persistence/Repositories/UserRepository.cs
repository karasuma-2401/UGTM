using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
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

    public void Add(User user) => db.Users.Add(user);

    public async Task AddToRoleAsync(
        User user,
        SystemRoleName role,
        DateTime assignedAt,
        CancellationToken ct)
    {
        var roleId = await db.Roles.Where(x => x.RoleName == role)
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

    public async Task UpdateRolesAsync(
        User user,
        IEnumerable<SystemRoleName> roles,
        DateTime assignedAt,
        CancellationToken ct)
    {
        var targetRoleNames = roles.ToHashSet();
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
            .ToHashSet();
        var rolesToAdd = targetRoleNames
            .Where(r => !existingRoleNames.Contains(r))
            .ToList();

        if (rolesToAdd.Count > 0)
        {
            var roleEntities = await db.Roles
                .Where(r => rolesToAdd.Contains(r.RoleName))
                .ToListAsync(ct);

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
}