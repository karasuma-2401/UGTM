using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IProgressReportRepository
{
    Task<ProgressReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProgressReport?> GetByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProgressReportVersion>> GetVersionsAsync(Guid progressReportId, CancellationToken cancellationToken = default);
    Task<ProgressReportVersion?> GetVersionAsync(Guid progressReportId, int versionNo, CancellationToken cancellationToken = default);
    void Add(ProgressReport report);
    void AddVersion(ProgressReportVersion version);
}
