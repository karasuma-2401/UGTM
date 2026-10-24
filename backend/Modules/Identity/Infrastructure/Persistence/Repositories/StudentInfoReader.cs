using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class StudentInfoReader(IdentityDbContext db) : IStudentInfoReader
{
    public async Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(
        IEnumerable<Guid> studentIds,
        CancellationToken cancellationToken = default)
    {
        var idList = studentIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, StudentInfoContractDto>();
        }

        var users = await db.Users
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new StudentInfoContractDto(
                u.Id,
                u.Email,
                u.FullName,
                u.UserCode,
                u.PhoneNumber))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id);
    }

    public async Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(
        Guid? studentId,
        string? userCode,
        CancellationToken cancellationToken = default)
    {
        var query = db.Users
            .AsNoTracking()
            .Where(u => u.Status == AccountStatus.Active &&
                        u.UserRoles.Any(ur => ur.Role.RoleName == nameof(SystemRoleName.Student)));

        if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            query = query.Where(u => u.Id == studentId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(userCode))
        {
            var trimmedCode = userCode.Trim();
            query = query.Where(u => u.UserCode == trimmedCode);
        }
        else
        {
            return null;
        }

        return await query
            .Select(u => new StudentInfoContractDto(
                u.Id,
                u.Email,
                u.FullName,
                u.UserCode,
                u.PhoneNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
