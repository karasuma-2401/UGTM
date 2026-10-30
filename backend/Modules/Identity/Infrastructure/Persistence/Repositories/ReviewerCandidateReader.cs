using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerCandidateReader(IdentityDbContext context) : IReviewerCandidateReader
{
    public async Task<ReviewerAccountContractDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.Users.AsNoTracking().Where(value => value.Id == userId)
            .Select(value => new ReviewerAccountContractDto(value.Id, value.Status == AccountStatus.Active,
                value.UserRoles.Any(role => role.Role.RoleName == nameof(SystemRoleName.Lecturer))))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReviewerAccountContractDto?> LockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        var user = await context.Users.FromSqlInterpolated($"""
            SELECT u.* FROM identity."user" u WHERE u.id = {userId} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (user is null) return null;
        var lecturerRoles = await context.UserRoles.FromSqlInterpolated($"""
            SELECT ur.* FROM identity.user_role ur
            JOIN identity.role r ON r.id = ur.role_id
            WHERE ur.user_id = {userId} AND r.role_name = 'Lecturer'
            ORDER BY ur.role_id FOR SHARE OF ur, r
            """).AsNoTracking().ToListAsync(cancellationToken);
        return new(user.Id, user.Status == AccountStatus.Active, lecturerRoles.Count > 0);
    }

    public async Task<ReviewerCandidatePageContractDto> SearchAsync(Guid excludedAdvisorId, string? keyword,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim();
        if (keyword?.Length > 200) throw new ArgumentOutOfRangeException(nameof(keyword));
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = context.Users.AsNoTracking().Where(user => user.Id != excludedAdvisorId
            && user.Status == AccountStatus.Active
            && user.UserRoles.Any(role => role.Role.RoleName == nameof(SystemRoleName.Lecturer)));
        if (!string.IsNullOrEmpty(keyword))
        {
            var pattern = "%" + keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(user => EF.Functions.ILike(user.FullName, pattern, "\\")
                || (user.UserCode != null && EF.Functions.ILike(user.UserCode, pattern, "\\")));
        }
        var total = await query.CountAsync(cancellationToken);
        var skip = ((long)pageNumber - 1) * pageSize;
        var items = skip >= total ? [] : await query.OrderBy(user => user.FullName).ThenBy(user => user.Id)
            .Skip((int)skip).Take(pageSize).Select(user => new ReviewerCandidateContractDto(user.Id, user.FullName, user.UserCode))
            .ToListAsync(cancellationToken);
        return new(items, total, pageNumber, pageSize);
    }
}
