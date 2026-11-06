using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class FinalReportPlagiarismScanRepository(ThesisManagementDbContext context) : IFinalReportPlagiarismScanRepository
{
    public Task<FinalReportPlagiarismScan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.FinalReportPlagiarismScans
            .Include(x => x.FinalReport)
            .Include(x => x.SubmissionFile)
            .SingleOrDefaultAsync(scan => scan.Id == id, cancellationToken);

    public Task<FinalReportPlagiarismScan?> GetByReportAndVersionAsync(
        Guid finalReportId,
        int versionNo,
        CancellationToken cancellationToken = default)
        => context.FinalReportPlagiarismScans
            .Include(x => x.FinalReport)
            .Include(x => x.SubmissionFile)
            .SingleOrDefaultAsync(
                scan => scan.FinalReportId == finalReportId && scan.VersionNo == versionNo,
                cancellationToken);

    public Task<FinalReportPlagiarismScan?> GetLatestByReportIdAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default)
        => context.FinalReportPlagiarismScans
            .Include(x => x.FinalReport)
            .Include(x => x.SubmissionFile)
            .Where(scan => scan.FinalReportId == finalReportId)
            .OrderByDescending(scan => scan.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FinalReportPlagiarismScan>> GetByReportIdAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default)
        => await context.FinalReportPlagiarismScans
            .Include(x => x.FinalReport)
            .Include(x => x.SubmissionFile)
            .Where(scan => scan.FinalReportId == finalReportId)
            .OrderBy(scan => scan.VersionNo)
            .ToListAsync(cancellationToken);

    public Task<FinalReportPlagiarismScan?> GetByJobIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
        => context.FinalReportPlagiarismScans
            .Include(x => x.FinalReport)
            .Include(x => x.SubmissionFile)
            .SingleOrDefaultAsync(scan => scan.JobId == jobId, cancellationToken);

    public async Task<IReadOnlyList<FinalReportPlagiarismScan>> GetActiveOrPendingScansAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            PlagiarismScanStatus.PendingSubmission,
            PlagiarismScanStatus.Queued,
            PlagiarismScanStatus.Processing,
            PlagiarismScanStatus.WaitingForCompute
        };

        var now = DateTime.UtcNow;

        return await context.FinalReportPlagiarismScans
            .Include(x => x.FinalReport)
            .Include(x => x.SubmissionFile)
            .Where(scan => activeStatuses.Contains(scan.Status)
                && (scan.NextCheckAfterUtc == null || scan.NextCheckAfterUtc <= now))
            .OrderBy(scan => scan.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinalReportPlagiarismScan>> GetResumableScansAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var resumableStatuses = new[]
        {
            PlagiarismScanStatus.ComputeUnavailable
        };

        return await context.FinalReportPlagiarismScans
            .Include(x => x.FinalReport)
            .Include(x => x.SubmissionFile)
            .Where(scan => resumableStatuses.Contains(scan.Status))
            .OrderBy(scan => scan.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public void Add(FinalReportPlagiarismScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        context.FinalReportPlagiarismScans.Add(scan);
    }

    public void Update(FinalReportPlagiarismScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        context.FinalReportPlagiarismScans.Update(scan);
    }
}

