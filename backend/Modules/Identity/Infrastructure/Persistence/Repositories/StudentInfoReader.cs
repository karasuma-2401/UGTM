using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;

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
}
