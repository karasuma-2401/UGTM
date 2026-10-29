using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IFinalReportRepository
{
    Task<FinalReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FinalReport?> GetByGroupAndVersionTypeAsync(
        Guid groupId,
        FinalReportVersionType versionType,
        CancellationToken cancellationToken = default);

    void Add(FinalReport finalReport);

    /// <summary>
    /// Enlists the tracked group's concurrency token in the same save as a report state/version change.
    /// </summary>
    void TrackGroupConcurrency(FinalReport finalReport, StudentGroup group);
}
