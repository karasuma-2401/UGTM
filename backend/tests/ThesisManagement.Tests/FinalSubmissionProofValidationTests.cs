using System.Text.Json;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class FinalSubmissionProofValidationTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 4, 0, 0, DateTimeKind.Utc);
    private static (FinalSubmission Row, FinalSubmissionPackageGraph Graph, Guid Batch, Guid Topic) Valid()
    {
        var batch = Guid.NewGuid(); var topic = Guid.NewGuid(); var actor = Guid.NewGuid(); var advisor = Guid.NewGuid();
        var row = new FinalSubmission { Id = Guid.NewGuid(), GroupId = Guid.NewGuid(), Version = 10 };
        var p = new FinalSubmissionPackage
        {
            Id = Guid.NewGuid(),
            SubmissionId = row.Id,
            VersionNo = 1,
            AbstractVi = "vi",
            AbstractEn = "en",
            KeywordsVi = "kv",
            KeywordsEn = "ke",
            SubmittedBy = actor,
            SubmittedAtUtc = Now
        };
        var files = new[] { (".pdf", "application/pdf"), (".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document") }
            .Select(f => new SubmissionFile
            {
                Id = Guid.NewGuid(),
                EntityType = SubmissionEntityType.FinalSubmission,
                EntityId = row.Id,
                VersionNo = 1,
                FileName = "report" + f.Item1,
                FileExtension = f.Item1,
                ContentType = f.Item2,
                FileSize = 123,
                ChecksumSha256 = new string('a', 64),
                ObjectKey = "test/" + Guid.NewGuid(),
                UploadedBy = actor,
                UploadedAt = Now
            }).ToArray();
        p.ManifestSha256 = FinalSubmissionMapping.Manifest(p, files);
        row.Submit(p);
        var links = files.Select(f => new FinalSubmissionPackageFile
        {
            PackageId = p.Id,
            SubmissionId = row.Id,
            VersionNo = 1,
            SubmissionFileId = f.Id,
            FileRole = FinalSubmissionMapping.Role(f)
        }).ToArray();
        var snapshot = new FinalSubmissionDefenseSnapshot(topic, batch, advisor, new(LockedDefenseOutcome.Succeeded, row.GroupId, batch,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4, actor, Now.AddDays(-1), new string('b', 64)));
        FinalSubmissionReview Review(string action, string stage, string before, string after) => new()
        {
            Id = Guid.NewGuid(),
            SubmissionId = row.Id,
            PackageId = p.Id,
            PackageVersionNo = 1,
            Action = action,
            Stage = stage,
            ActorId = actor,
            AssignedAdvisorId = advisor,
            DecidedAtUtc = Now,
            ManifestSha256 = p.ManifestSha256,
            DefenseProofSnapshot = JsonSerializer.Serialize(snapshot),
            IdempotencyKey = Guid.NewGuid(),
            PreviousStatus = before,
            ResultStatus = after
        };
        row.RecordReview(FinalSubmissionReviewAction.AdvisorConfirm, actor, Now, null);
        row.RecordReview(FinalSubmissionReviewAction.FacultyConfirm, actor, Now, null);
        return (row, new([p], links, files, [Review("AdvisorConfirm", "Advisor", "Submitted", "AdvisorConfirmed"), Review("FacultyConfirm", "Faculty", "AdvisorConfirmed", "ArchiveConfirmed")]), batch, topic);
    }
    [Fact]
    public void Archival_proof_accepts_complete_sealed_package_with_real_binding()
    {
        var (row, graph, batch, topic) = Valid();
        Assert.True(FinalSubmissionPackageValidation.TryValidate(row, graph, batch, topic, out var valid));
        Assert.NotNull(valid!.AdvisorReview);
        Assert.NotNull(valid.FacultyReview);
    }
    [Theory]
    [InlineData("legacy")]
    [InlineData("version")]
    [InlineData("metadata")]
    [InlineData("fileChecksum")]
    [InlineData("fileSize")]
    [InlineData("mime")]
    [InlineData("linkBinding")]
    [InlineData("role")]
    [InlineData("reviewVersion")]
    [InlineData("reviewManifest")]
    [InlineData("reviewActor")]
    [InlineData("reviewDate")]
    [InlineData("reviewAction")]
    [InlineData("snapshot")]
    [InlineData("snapshotDuplicates")]
    [InlineData("snapshotUnknown")]
    [InlineData("snapshotEmptyProof")]
    [InlineData("projection")]
    public void Archival_proof_fails_closed_on_corrupt_or_cross_package_evidence(string corruption)
    {
        var (row, graph, batch, topic) = Valid();
        switch (corruption)
        {
            case "legacy": row.IsLegacy = true; break;
            case "version": row.CurrentVersionNo++; break;
            case "metadata": row.AbstractEn = "changed"; break;
            case "fileChecksum": graph.Files[0].ChecksumSha256 = new string('c', 64); break;
            case "fileSize": graph.Files[0].FileSize = 0; break;
            case "mime": graph.Files[0].ContentType = "application/octet-stream"; break;
            case "linkBinding": graph.Links[0].SubmissionId = Guid.NewGuid(); break;
            case "role": graph.Links[0].FileRole = graph.Links[1].FileRole; break;
            case "reviewVersion": graph.Reviews[1].PackageVersionNo++; break;
            case "reviewManifest": graph.Reviews[1].ManifestSha256 = new string('d', 64); break;
            case "reviewActor": graph.Reviews[1].ActorId = Guid.Empty; break;
            case "reviewDate": graph.Reviews[1].DecidedAtUtc = Now.AddDays(-2); break;
            case "reviewAction": graph.Reviews[1].Action = "RequestRevision"; break;
            case "snapshot": graph.Reviews[1].DefenseProofSnapshot = "{}"; break;
            case "snapshotDuplicates": graph.Reviews[1].DefenseProofSnapshot = graph.Reviews[1].DefenseProofSnapshot.Insert(1, "\"TopicId\":null,"); break;
            case "snapshotUnknown": graph.Reviews[1].DefenseProofSnapshot = graph.Reviews[1].DefenseProofSnapshot.Insert(1, "\"extra\":true,"); break;
            case "snapshotEmptyProof":
                var s = FinalSubmissionDefenseSnapshot.Read(graph.Reviews[1].DefenseProofSnapshot)!;
                graph.Reviews[1].DefenseProofSnapshot = JsonSerializer.Serialize(s with { Defense = s.Defense with { ProofSha256 = null } }); break;
            case "projection": row.ArchivedConfirmedBy = Guid.NewGuid(); break;
        }
        Assert.False(FinalSubmissionPackageValidation.TryValidate(row, graph, batch, topic, out _));
    }
    [Theory]
    [InlineData(FinalSubmissionReviewAction.AdvisorConfirm)]
    [InlineData(FinalSubmissionReviewAction.FacultyConfirm)]
    [InlineData(FinalSubmissionReviewAction.RequestRevision)]
    public void Archival_sealed_domain_rejects_every_review_action(FinalSubmissionReviewAction action)
    {
        var (row, _, _, _) = Valid();
        Assert.Throws<InvalidOperationException>(() => row.RecordReview(action, Guid.NewGuid(), Now, action == FinalSubmissionReviewAction.RequestRevision ? "reason" : null));
    }
}
