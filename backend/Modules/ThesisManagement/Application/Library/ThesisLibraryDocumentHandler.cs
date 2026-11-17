using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.Library;

public sealed record ThesisLibraryDocumentDto(string DownloadUrl, DateTimeOffset ExpiresAt);

public sealed class ThesisLibraryDocumentHandler(IThesisLibraryCatalog catalog, ICurrentUser user,
    IFinalSubmissionAuthorityReader authority, IFinalSubmissionStore store, IFinalSubmissionArchiveProofContract proofs,
    IThesisSubmissionObjectStorage storage, TimeProvider clock)
{
    public async Task<Result<ThesisLibraryDocumentDto>> HandleAsync(string slug, CancellationToken token)
    {
        if (!user.IsAuthenticated || user.UserId is not { } actor || actor == Guid.Empty)
            return Result.Failure<ThesisLibraryDocumentDto>(FinalSubmissionErrors.AuthenticationRequired);
        if (await authority.GetAsync(actor, token) is not { IsActive: true })
            return Result.Failure<ThesisLibraryDocumentDto>(FinalSubmissionErrors.AccessDenied);
        var publication = ThesisLibrarySearch.Published(catalog, clock.GetUtcNow()).SingleOrDefault(p => p.Slug == slug);
        if (publication?.SubmissionId is not { } id)
            return Result.Failure<ThesisLibraryDocumentDto>(ThesisLibrarySearch.NotFound);
        var row = await store.GetByIdAsync(id, token);
        if (row is null || row.IsLegacy || row.Status != FinalSubmissionStatus.ArchiveConfirmed || row.CurrentVersionNo != publication.PackageVersion)
            return Result.Failure<ThesisLibraryDocumentDto>(ThesisLibrarySearch.NotFound);
        var proof = await proofs.GetArchiveProofAsync(row.GroupId, token);
        if (proof.Outcome != ArchiveProofOutcome.Succeeded || proof.SubmissionId != id || proof.PackageId != row.CurrentPackageId
            || proof.PackageVersionNo != publication.PackageVersion || proof.ManifestSha256 != publication.ManifestSha256)
            return Result.Failure<ThesisLibraryDocumentDto>(ThesisLibrarySearch.NotFound);
        var graph = await store.GetPackagesAsync(id, token);
        var fileProof = proof.Files.SingleOrDefault(f => f.FileRole == "ThesisPdf");
        var file = graph.Files.SingleOrDefault(f => f.Id == fileProof?.FileId && f.EntityId == id
            && f.EntityType == SubmissionEntityType.FinalSubmission && f.VersionNo == publication.PackageVersion
            && f.ContentType == "application/pdf" && f.FileSize == fileProof.SizeBytes && f.ChecksumSha256 == fileProof.ChecksumSha256);
        if (file is null || !graph.Links.Any(l => l.PackageId == proof.PackageId && l.SubmissionFileId == file.Id && l.FileRole == "ThesisPdf"))
            return Result.Failure<ThesisLibraryDocumentDto>(ThesisLibrarySearch.NotFound);
        // The explicit publication grants active authenticated readers access only to this bound PDF.
        // No DOCX, previous version, review note or arbitrary storage key is exposed.
        var ttl = TimeSpan.FromMinutes(5);
        var uri = await storage.CreateDownloadUriAsync(file.ObjectKey, file.FileName, ttl, token);
        return Result.Success(new ThesisLibraryDocumentDto(uri.AbsoluteUri, clock.GetUtcNow().Add(ttl)));
    }
}
