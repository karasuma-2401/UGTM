using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IStudentGroupRepository
{
    Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(StudentGroup group);
}
