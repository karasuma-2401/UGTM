using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public static class FinalSubmissionMapping
{
    public static string? Normalize(string? text, int maximum)
    {
        if (text is null) return null;
        // Reject malformed UTF-16 rather than silently replacing surrogate code units.
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsSurrogate(text[i])) continue;
            if (!char.IsHighSurrogate(text[i]) || i + 1 >= text.Length || !char.IsLowSurrogate(text[++i])) return null;
        }
        text = text.Trim();
        return text.Length == 0 || text.Contains('\0') || text.EnumerateRunes().Count() > maximum ? null : text;
    }
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    public static string Intent(Guid groupId, uint? version, string vi, string en, string kvi, string ken,
        ValidatedThesisSubmissionFile pdf, ValidatedThesisSubmissionFile docx)
        => Hash(new object?[] { groupId.ToString("N"), version, vi, en, kvi, ken,
            new object[] { "ThesisDocx", docx.ChecksumSha256, docx.FileSize, docx.ContentType },
            new object[] { "ThesisPdf", pdf.ChecksumSha256, pdf.FileSize, pdf.ContentType } });
    public static string Manifest(FinalSubmissionPackage package, IReadOnlyList<SubmissionFile> files)
        => Hash(new object[] { package.AbstractVi, package.AbstractEn, package.KeywordsVi, package.KeywordsEn,
            files.OrderBy(f => Role(f), StringComparer.Ordinal).Select(f => new object[] {
                Role(f), f.Id.ToString("N"), f.FileSize, f.ContentType, f.ChecksumSha256! }).ToArray() });
    public static string Role(SubmissionFile file) => file.FileExtension == ".pdf" ? "ThesisPdf" : "ThesisDocx";
    public static FinalSubmissionFileDto File(SubmissionFile file, string role)
        => new(file.Id, role, file.FileName, file.FileSize, file.ContentType, file.ChecksumSha256, file.UploadedAt);
    public static FinalSubmissionDto Dto(FinalSubmission row, FinalSubmissionPackageGraph graph)
    {
        var links = graph.Links.Where(l => l.PackageId == row.CurrentPackageId).OrderBy(l => l.FileRole, StringComparer.Ordinal);
        var files = graph.Files.ToDictionary(f => f.Id);
        return new(row.Id, row.GroupId, row.Status.ToString(), row.CurrentVersionNo, row.AbstractVi, row.AbstractEn,
            row.KeywordsVi, row.KeywordsEn, row.SubmittedBy, row.SubmittedAtUtc, row.Version, row.AdvisorConfirmedBy,
            row.AdvisorConfirmedAt, row.ArchivedConfirmedBy, row.ArchivedConfirmedAt, row.FacultyFeedback, row.IsLegacy,
            links.Select(l => File(files[l.SubmissionFileId], l.FileRole)).ToArray());
    }
    public static IReadOnlyList<FinalSubmissionPackageDto> Packages(FinalSubmissionPackageGraph graph)
    {
        var files = graph.Files.ToDictionary(f => f.Id);
        return graph.Packages.Select(p => new FinalSubmissionPackageDto(p.Id, p.VersionNo, p.AbstractVi, p.AbstractEn, p.KeywordsVi,
            p.KeywordsEn, p.SubmittedBy, p.SubmittedAtUtc,
            graph.Links.Where(l => l.PackageId == p.Id).OrderBy(l => l.FileRole, StringComparer.Ordinal).Select(l => File(files[l.SubmissionFileId], l.FileRole)).ToArray(),
            graph.Reviews.Where(r => r.PackageId == p.Id).Select(r => new FinalSubmissionReviewDto(r.Id, r.Action, r.Stage,
                r.ActorId, r.DecidedAtUtc, r.Reason, r.PreviousStatus, r.ResultStatus)).ToArray())).ToArray();
    }
}
