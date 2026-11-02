using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class FinalSubmissionArchiveProofContract(ThesisManagementDbContext db, IFinalSubmissionStore store)
    : IFinalSubmissionArchiveProofContract
{
    public Task<FinalSubmissionArchiveProofContractDto> GetArchiveProofAsync(Guid groupId, CancellationToken cancellationToken = default)
        => ReadAsync(groupId, false, cancellationToken);
    public Task<FinalSubmissionArchiveProofContractDto> LockArchiveProofForCalculationAsync(Guid groupId, CancellationToken cancellationToken = default)
        => ReadAsync(groupId, true, cancellationToken);
    private async Task<FinalSubmissionArchiveProofContractDto> ReadAsync(Guid groupId, bool locked, CancellationToken token)
    {
        if (locked && db.Database.CurrentTransaction is null) throw new InvalidOperationException("Shared archival transaction and caller-held group/context guards required.");
        // The consumer holds earlier group/topic/batch guards before entering this owner phase.
        // Never acquire those root locks again here in the opposite order.
        var group = await db.StudentGroups.AsNoTracking().Include(g => g.Members).SingleOrDefaultAsync(g => g.Id == groupId, token);
        var batchIds = group?.Members.Select(m => m.BatchId).Distinct().ToArray() ?? [];
        var batchId = batchIds.Length == 1 ? batchIds[0] : Guid.Empty;
        FinalSubmissionArchiveProofContractDto Failed(ArchiveProofOutcome outcome)
            => new(outcome, groupId, batchId, null, null, null, null, null, null, null, null, null, null, null, []);
        if (group is null) return Failed(ArchiveProofOutcome.Missing);
        if (batchId == Guid.Empty) return Failed(ArchiveProofOutcome.ContextInvalid);
        var row = locked
            ? await db.FinalSubmissions.FromSqlInterpolated($"SELECT s.*, s.xmin FROM thesis.final_submission s WHERE s.group_id={groupId} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(token)
            : await store.GetByGroupAsync(groupId, false, token);
        if (row is null) return Failed(ArchiveProofOutcome.Missing);
        if (row.IsLegacy || row.CurrentPackageId is null) return Failed(ArchiveProofOutcome.Legacy);
        if (row.Status != FinalSubmissionStatus.ArchiveConfirmed) return Failed(ArchiveProofOutcome.NotConfirmed);
        var graph = locked ? await store.LockCurrentPackageAsync(row.Id, row.CurrentPackageId.Value, token) : await store.GetPackagesAsync(row.Id, token);
        if (!FinalSubmissionPackageValidation.TryValidate(row, graph, batchId, group.TopicId, out var verified))
            return Failed(ArchiveProofOutcome.ContextInvalid);
        // A sealed historical proof does not depend on today's lecturer grants, advisor or minute.
        var p = verified!.Package;
        return new(ArchiveProofOutcome.Succeeded, groupId, batchId, row.Id, row.Version, p.Id, p.VersionNo, p.ManifestSha256,
            verified.AdvisorReview!.Id, verified.FacultyReview!.Id, row.AdvisorConfirmedBy, row.AdvisorConfirmedAt,
            row.ArchivedConfirmedBy, row.ArchivedConfirmedAt, verified.Files.OrderBy(FinalSubmissionMapping.Role, StringComparer.Ordinal)
                .Select(f => new ArchiveFileProofContractDto(f.Id, p.Id, p.VersionNo, FinalSubmissionMapping.Role(f), f.ContentType, f.FileSize, f.ChecksumSha256!)).ToArray());
    }
}
