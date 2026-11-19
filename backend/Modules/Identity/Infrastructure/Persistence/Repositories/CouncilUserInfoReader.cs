using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class CouncilUserInfoReader(IdentityDbContext db) : ICouncilUserInfoReader
{
    public async Task<CouncilUserInfoContractDto?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var lecturerRoleName = nameof(SystemRoleName.Lecturer);
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CouncilUserInfoContractDto(
                user.Id,
                user.FullName,
                user.Email,
                user.UserCode,
                user.Status == AccountStatus.Active,
                user.UserRoles.Any(ur => ur.Role.RoleName == lecturerRoleName)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, CouncilUserInfoContractDto>();

        var lecturerRoleName = nameof(SystemRoleName.Lecturer);
        return await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new CouncilUserInfoContractDto(
                user.Id,
                user.FullName,
                user.Email,
                user.UserCode,
                user.Status == AccountStatus.Active,
                user.UserRoles.Any(ur => ur.Role.RoleName == lecturerRoleName)))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<CouncilUserInfoContractDto>> GetActiveLecturersAsync(
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var lecturerRoleName = nameof(SystemRoleName.Lecturer);
        var query = db.Users.AsNoTracking()
            .Where(user => user.Status == AccountStatus.Active
                && user.UserRoles.Any(ur => ur.Role.RoleName == lecturerRoleName));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            var normalizedEmail = trimmed.ToUpperInvariant();
            var escaped = trimmed
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
            var pattern = $"%{escaped}%";

            query = query.Where(user =>
                user.NormalizedEmail.Contains(normalizedEmail)
                || EF.Functions.ILike(user.FullName, pattern, @"\")
                || (user.UserCode != null && EF.Functions.ILike(user.UserCode, pattern, @"\")));
        }

        return await query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserCode)
            .ThenBy(user => user.Id)
            .Select(user => new CouncilUserInfoContractDto(
                user.Id,
                user.FullName,
                user.Email,
                user.UserCode,
                true,
                true))
            .ToListAsync(cancellationToken);
    }

    public async Task GrantCouncilMemberRoleAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return;

        var councilMemberRoleName = nameof(SystemRoleName.CouncilMember);
        var councilMemberRole = await db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == councilMemberRoleName, cancellationToken);

        if (councilMemberRole is null) return;

        var existingUserIds = await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == councilMemberRole.Id && ids.Contains(ur.UserId))
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        var existingSet = existingUserIds.ToHashSet();
        var usersToGrant = ids.Where(id => !existingSet.Contains(id)).ToList();
        if (usersToGrant.Count == 0) return;

        var now = DateTime.UtcNow;
        var addedAny = false;
        foreach (var userId in usersToGrant)
        {
            if (db.UserRoles.Local.Any(ur => ur.UserId == userId && ur.RoleId == councilMemberRole.Id))
            {
                continue;
            }

            db.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = councilMemberRole.Id,
                AssignedAt = now
            });
            addedAny = true;
        }

        if (addedAny)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeCouncilMemberRoleAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return;

        var councilMemberRoleName = nameof(SystemRoleName.CouncilMember);
        var toRemove = await db.UserRoles
            .Where(ur => ids.Contains(ur.UserId) && ur.Role.RoleName == councilMemberRoleName)
            .ToListAsync(cancellationToken);

        if (toRemove.Count == 0) return;

        db.UserRoles.RemoveRange(toRemove);
        await db.SaveChangesAsync(cancellationToken);
    }
}
