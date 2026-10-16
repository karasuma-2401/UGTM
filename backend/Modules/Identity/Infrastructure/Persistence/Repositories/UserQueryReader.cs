using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserQueryReader(IdentityDbContext db) : IUserQueryReader
{
    public async Task<PagedResult<UserSummaryDto>> GetUsersAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        var usersQuery = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            var normalized = keyword.ToUpperInvariant();
            var escapedKeyword = keyword
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
            usersQuery = usersQuery.Where(u =>
                u.NormalizedEmail.Contains(normalized)
                || EF.Functions.ILike(u.FullName, $"%{escapedKeyword}%", @"\")
                || (u.UserCode != null && EF.Functions.ILike(u.UserCode, $"%{escapedKeyword}%", @"\")));
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && !int.TryParse(query.Status.Trim(), out _)
            && Enum.TryParse<AccountStatus>(query.Status.Trim(), ignoreCase: true, out var statusEnum)
            && Enum.IsDefined(statusEnum))
        {
            usersQuery = usersQuery.Where(u => u.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(query.Role)
            && !int.TryParse(query.Role.Trim(), out _)
            && Enum.TryParse<SystemRoleName>(query.Role.Trim(), ignoreCase: true, out var roleEnum)
            && Enum.IsDefined(roleEnum))
        {
            usersQuery = usersQuery.Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == roleEnum));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);

        var pagedUsers = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.UserCode,
                u.PhoneNumber,
                Status = u.Status.ToString(),
                Roles = u.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList(),
                u.CreatedAt,
                u.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        var items = pagedUsers.Select(u => new UserSummaryDto(
            u.Id,
            u.Email,
            u.FullName,
            u.UserCode,
            u.PhoneNumber,
            u.Status,
            u.Roles,
            u.CreatedAt,
            u.LastLoginAt)).ToList();

        return new PagedResult<UserSummaryDto>(items, totalCount, query.PageNumber, query.PageSize);
    }
}
