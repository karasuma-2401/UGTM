using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface ISyllabusRepository
{
    Task<Syllabus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Syllabus?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);

    void Add(Syllabus syllabus);
}
