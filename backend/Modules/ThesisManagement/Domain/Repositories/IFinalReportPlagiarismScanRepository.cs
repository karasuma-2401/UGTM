using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IFinalReportPlagiarismScanRepository
{
    Task<FinalReportPlagiarismScan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FinalReportPlagiarismScan?> GetByReportAndVersionAsync(
        Guid finalReportId,
        int versionNo,
        CancellationToken cancellationToken = default);

    Task<FinalReportPlagiarismScan?> GetLatestByReportIdAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinalReportPlagiarismScan>> GetByReportIdAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default);

    Task<FinalReportPlagiarismScan?> GetByJobIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinalReportPlagiarismScan>> GetActiveOrPendingScansAsync(
        int limit = 50,
        string? workerPartition = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinalReportPlagiarismScan>> GetResumableScansAsync(
        int limit = 50,
        string? workerPartition = null,
        CancellationToken cancellationToken = default);

    void Add(FinalReportPlagiarismScan scan);

    void Update(FinalReportPlagiarismScan scan);
}

