using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class StudentGroupRepository(ThesisManagementDbContext context) : IStudentGroupRepository
{
    public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.StudentGroups.Include(group => group.Members)
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

    public void Add(StudentGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        context.StudentGroups.Add(group);
    }
}
