using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class SyllabusRepository(ThesisManagementDbContext context) : ISyllabusRepository
{
    public Task<Syllabus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Syllabuses.SingleOrDefaultAsync(syllabus => syllabus.Id == id, cancellationToken);

    public Task<Syllabus?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        => context.Syllabuses.SingleOrDefaultAsync(syllabus => syllabus.GroupId == groupId, cancellationToken);

    public void Add(Syllabus syllabus)
    {
        ArgumentNullException.ThrowIfNull(syllabus);
        context.Syllabuses.Add(syllabus);
    }
}
