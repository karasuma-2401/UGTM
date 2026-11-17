using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class FinalSubmissionAuthorityReader(IdentityDbContext db) : IFinalSubmissionAuthorityReader
{
    public Task<FinalSubmissionAuthorityDto?> GetAsync(Guid userId, CancellationToken token = default) => ReadAsync(userId, false, token);
    public Task<FinalSubmissionAuthorityDto?> LockAsync(Guid userId, CancellationToken token = default) => ReadAsync(userId, true, token);
    private async Task<FinalSubmissionAuthorityDto?> ReadAsync(Guid id, bool locked, CancellationToken token)
    {
        if (locked && db.Database.CurrentTransaction is null) throw new InvalidOperationException("Shared transaction required.");
        var user = locked
            ? await db.Users.FromSqlInterpolated($"SELECT u.* FROM identity.\"user\" u WHERE u.id = {id} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(token)
            : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, token);
        if (user is null) return null;
        if (locked)
        {
            await db.UserRoles.FromSqlInterpolated($"""
                SELECT ur.* FROM identity.user_role ur JOIN identity.role r ON r.id=ur.role_id
                WHERE ur.user_id={id} ORDER BY ur.role_id FOR SHARE OF ur,r
                """).AsNoTracking().ToListAsync(token);
            await db.RolePermissions.FromSqlInterpolated($"""
                SELECT rp.* FROM identity.role_permission rp JOIN identity.permission p ON p.id=rp.permission_id
                WHERE rp.role_id IN (SELECT role_id FROM identity.user_role WHERE user_id={id})
                ORDER BY rp.role_id,rp.permission_id FOR SHARE OF rp,p
                """).AsNoTracking().ToListAsync(token);
        }
        var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == id).Select(r => r.Role.RoleName).ToListAsync(token);
        var permissions = await db.UserRoles.AsNoTracking().Where(r => r.UserId == id)
            .SelectMany(r => r.Role.RolePermissions.Select(p => p.Permission.PermissionCode)).Distinct().ToListAsync(token);
        return new(id, user.Status == AccountStatus.Active, roles, permissions);
    }
}
