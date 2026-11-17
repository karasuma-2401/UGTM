using UGTM.Modules.ThesisManagement.Application.StudentGroups.GetStudentGroupDetail;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class StudentGroupDetailReader(ThesisManagementDbContext db) : IStudentGroupDetailReader
{
    public Task<StudentGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        => db.StudentGroups.AsNoTracking()
            .Include(group => group.Members)
            .SingleOrDefaultAsync(group => group.Id == groupId, cancellationToken);
}
