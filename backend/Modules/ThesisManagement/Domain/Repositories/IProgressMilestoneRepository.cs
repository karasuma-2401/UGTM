using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IProgressMilestoneRepository
{
    Task<ProgressMilestone?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgressMilestone>> GetByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    void Add(ProgressMilestone milestone);
}
