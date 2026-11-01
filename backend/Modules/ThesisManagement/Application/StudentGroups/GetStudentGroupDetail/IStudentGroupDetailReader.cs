using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.StudentGroups.GetStudentGroupDetail;

public interface IStudentGroupDetailReader
{
    Task<StudentGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken = default);
}
