using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);

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

    public void Add(User user) => db.Users.Add(user);

    public async Task AddToRoleAsync(
        User user,
        SystemRoleName role,
        DateTime assignedAt,
        CancellationToken ct)
    {
        var roleId = await db.Roles.Where(x => x.RoleName == role)
            .Select(x => x.Id).SingleAsync(ct);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = roleId,
            AssignedAt = assignedAt
        });
    }
}