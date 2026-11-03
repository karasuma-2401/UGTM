using System.Text.Json;
using System.Text.Json.Serialization;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public sealed record FinalSubmissionDefenseSnapshot(Guid TopicId, Guid BatchId, Guid AssignedAdvisorId,
    LockedDefenseProofContractDto Defense)
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static FinalSubmissionDefenseSnapshot? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!UniqueProperties(document.RootElement)) return null;
            return JsonSerializer.Deserialize<FinalSubmissionDefenseSnapshot>(json, StrictJson);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException) { return null; }
    }
    private static bool UniqueProperties(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
            if (!names.Add(property.Name) || (property.Value.ValueKind == JsonValueKind.Object && !UniqueProperties(property.Value))) return false;
        return true;
    }
    public bool IsValid(Guid group, Guid batch, Guid topic, Guid advisor, DateTime reviewAt)
        => TopicId == topic && topic != Guid.Empty && BatchId == batch && batch != Guid.Empty
            && AssignedAdvisorId == advisor && advisor != Guid.Empty && Defense is not null
            && Defense.Outcome == LockedDefenseOutcome.Succeeded && Defense.GroupId == group && Defense.BatchId == batch
            && Nonempty(Defense.CouncilId) && Nonempty(Defense.AssignmentId) && Nonempty(Defense.MinuteId)
            && Defense.MinuteVersion is > 0 && Nonempty(Defense.LockedBy)
            && Defense.LockedAtUtc is { Kind: DateTimeKind.Utc } locked && locked <= reviewAt
            && FinalSubmissionPackageValidation.IsSha256(Defense.ProofSha256);
    private static bool Nonempty(Guid? value) => value.HasValue && value != Guid.Empty;
}

public sealed record VerifiedArchivalPackage(FinalSubmissionPackage Package, IReadOnlyList<SubmissionFile> Files,
    FinalSubmissionReview? AdvisorReview, FinalSubmissionReview? FacultyReview, FinalSubmissionDefenseSnapshot? AdvisorSnapshot);

public static class FinalSubmissionPackageValidation
{
    public static bool IsSha256(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool TryValidate(FinalSubmission row, FinalSubmissionPackageGraph graph, Guid batchId, Guid topicId,
        out VerifiedArchivalPackage? verified)
    {
        verified = null;
        if (row.IsLegacy || row.Id == Guid.Empty || row.GroupId == Guid.Empty || row.CurrentPackageId is null
            || row.CurrentPackageId == Guid.Empty || row.CurrentVersionNo < 1 || row.Version == 0
            || batchId == Guid.Empty || topicId == Guid.Empty) return false;
        var packages = graph.Packages.Where(p => p.Id == row.CurrentPackageId).ToArray();
        if (packages.Length != 1) return false;
        var p = packages[0];
        if (p.SubmissionId != row.Id || p.VersionNo != row.CurrentVersionNo || p.SubmittedBy == Guid.Empty
            || p.SubmittedAtUtc.Kind != DateTimeKind.Utc || row.SubmittedBy != p.SubmittedBy || row.SubmittedAtUtc != p.SubmittedAtUtc
            || !IsSha256(p.ManifestSha256) || !Metadata(p.AbstractVi, 10000) || !Metadata(p.AbstractEn, 10000)
            || !Metadata(p.KeywordsVi, 1000) || !Metadata(p.KeywordsEn, 1000)
            || row.AbstractVi != p.AbstractVi || row.AbstractEn != p.AbstractEn || row.KeywordsVi != p.KeywordsVi || row.KeywordsEn != p.KeywordsEn) return false;
        var links = graph.Links.Where(l => l.PackageId == p.Id).ToArray();
        if (links.Length != 2 || links.Select(l => l.FileRole).Distinct().Count() != 2
            || links.Select(l => l.SubmissionFileId).Distinct().Count() != 2) return false;
        var files = new List<SubmissionFile>();
        foreach (var link in links)
        {
            if (link.SubmissionId != row.Id || link.VersionNo != p.VersionNo || link.EntityType != SubmissionEntityType.FinalSubmission
                || link.SubmissionFileId == Guid.Empty || link.FileRole is not ("ThesisPdf" or "ThesisDocx")) return false;
            var matches = graph.Files.Where(f => f.Id == link.SubmissionFileId).ToArray();
            if (matches.Length != 1) return false;
            var file = matches[0];
            var isPdf = link.FileRole == "ThesisPdf";
            if (file.EntityType != SubmissionEntityType.FinalSubmission || file.EntityId != row.Id || file.VersionNo != p.VersionNo
                || file.FileSize is < 1 or > 20 * 1024 * 1024 || !IsSha256(file.ChecksumSha256)
                || file.FileExtension != (isPdf ? ".pdf" : ".docx")
                || file.ContentType != (isPdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
                || file.UploadedBy != p.SubmittedBy || file.UploadedAt != p.SubmittedAtUtc || !ValidName(file.FileName)
                || string.IsNullOrWhiteSpace(file.ObjectKey) || file.ObjectKey.Length > 1000) return false;
            files.Add(file);
        }
        if (FinalSubmissionMapping.Manifest(p, files) != p.ManifestSha256) return false;
        var reviews = graph.Reviews.Where(r => r.PackageId == p.Id || (r.SubmissionId == row.Id && r.PackageVersionNo == p.VersionNo)).ToArray();
        if (row.Status == FinalSubmissionStatus.Submitted)
        {
            if (reviews.Length != 0 || row.AdvisorConfirmedBy.HasValue || row.AdvisorConfirmedAt.HasValue
                || row.ArchivedConfirmedBy.HasValue || row.ArchivedConfirmedAt.HasValue || row.FacultyFeedback is not null) return false;
            verified = new(p, files, null, null, null);
            return true;
        }
        if (row.Status is not (FinalSubmissionStatus.AdvisorConfirmed or FinalSubmissionStatus.ArchiveConfirmed)) return false;
        var advisor = reviews.Where(r => r.Action == "AdvisorConfirm").ToArray();
        var faculty = reviews.Where(r => r.Action == "FacultyConfirm").ToArray();
        if (advisor.Length != 1 || !ValidReview(advisor[0], p, row.GroupId, batchId, topicId, "Advisor", "Submitted", "AdvisorConfirmed", out var advisorSnapshot)
            || row.AdvisorConfirmedBy != advisor[0].ActorId || row.AdvisorConfirmedAt != advisor[0].DecidedAtUtc
            || row.FacultyFeedback is not null) return false;
        if (row.Status == FinalSubmissionStatus.AdvisorConfirmed)
        {
            if (reviews.Length != 1 || faculty.Length != 0 || row.ArchivedConfirmedBy.HasValue || row.ArchivedConfirmedAt.HasValue) return false;
            verified = new(p, files, advisor[0], null, advisorSnapshot);
            return true;
        }
        if (reviews.Length != 2 || faculty.Length != 1 || advisor[0].Id == faculty[0].Id
            || !ValidReview(faculty[0], p, row.GroupId, batchId, topicId, "Faculty", "AdvisorConfirmed", "ArchiveConfirmed", out var facultySnapshot)
            || row.ArchivedConfirmedBy != faculty[0].ActorId || row.ArchivedConfirmedAt != faculty[0].DecidedAtUtc
            || faculty[0].DecidedAtUtc < advisor[0].DecidedAtUtc || facultySnapshot != advisorSnapshot) return false;
        verified = new(p, files, advisor[0], faculty[0], advisorSnapshot);
        return true;
    }
    private static bool ValidReview(FinalSubmissionReview r, FinalSubmissionPackage p, Guid group, Guid batch, Guid topic,
        string stage, string previous, string result, out FinalSubmissionDefenseSnapshot? snapshot)
    {
        snapshot = null;
        if (r.Id == Guid.Empty || r.ActorId == Guid.Empty || r.AssignedAdvisorId == Guid.Empty || r.IdempotencyKey == Guid.Empty
            || r.PackageId != p.Id || r.SubmissionId != p.SubmissionId || r.PackageVersionNo != p.VersionNo
            || r.Stage != stage || r.PreviousStatus != previous || r.ResultStatus != result || r.Reason is not null
            || r.ManifestSha256 != p.ManifestSha256 || r.DecidedAtUtc.Kind != DateTimeKind.Utc || r.DecidedAtUtc < p.SubmittedAtUtc
            || string.IsNullOrWhiteSpace(r.DefenseProofSnapshot)) return false;
        snapshot = FinalSubmissionDefenseSnapshot.Read(r.DefenseProofSnapshot);
        return snapshot is not null && snapshot.IsValid(group, batch, topic, r.AssignedAdvisorId, r.DecidedAtUtc);
    }
    private static bool Metadata(string? value, int maximum) => value is not null && FinalSubmissionMapping.Normalize(value, maximum) == value;
    private static bool ValidName(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 255
        && value == value.Trim() && !value.Any(char.IsControl) && value.IndexOfAny(['/', '\\']) < 0;
}
