using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class FinalSubmissionWorkflowTests
{
    [Theory]
    [InlineData(FinalSubmissionStatus.NotSubmitted)]
    [InlineData(FinalSubmissionStatus.Submitted)]
    [InlineData(FinalSubmissionStatus.NeedRevision)]
    public void Replacement_versions_reset_confirmation_projection_and_preserve_legacy_snapshot(FinalSubmissionStatus status)
    {
        var row = new FinalSubmission
        {
            Id = Guid.NewGuid(),
            Status = status,
            CurrentVersionNo = 4,
            CurrentPackageId = Guid.NewGuid(),
            AdvisorConfirmedBy = Guid.NewGuid(),
            AdvisorConfirmedAt = DateTime.UtcNow,
            FacultyFeedback = "revise",
            LegacySnapshot = "{}"
        };
        var package = new FinalSubmissionPackage
        {
            Id = Guid.NewGuid(),
            SubmissionId = row.Id,
            VersionNo = 5,
            AbstractVi = "vi",
            AbstractEn = "en",
            KeywordsVi = "kv",
            KeywordsEn = "ke",
            SubmittedBy = Guid.NewGuid(),
            SubmittedAtUtc = DateTime.UtcNow
        };
        row.Submit(package);
        Assert.Equal(package.Id, row.CurrentPackageId);
        Assert.Equal(5, row.CurrentVersionNo);
        Assert.Equal(FinalSubmissionStatus.Submitted, row.Status);
        Assert.False(row.IsLegacy);
        Assert.Null(row.AdvisorConfirmedBy);
        Assert.Null(row.AdvisorConfirmedAt);
        Assert.Null(row.FacultyFeedback);
        Assert.Equal("{}", row.LegacySnapshot);
    }
    [Theory]
    [InlineData(FinalSubmissionStatus.AdvisorConfirmed)]
    [InlineData(FinalSubmissionStatus.ArchiveConfirmed)]
    public void Confirmation_blocks_replacement(FinalSubmissionStatus status)
    {
        var row = new FinalSubmission { Status = status };
        Assert.Throws<InvalidOperationException>(() => row.Submit(new()));
        Assert.Equal(status, row.Status);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("x\0y")]
    public void Metadata_rejects_empty_or_NUL(string? text) => Assert.Null(FinalSubmissionMapping.Normalize(text, 10));
    [Fact]
    public void Metadata_counts_runes_and_rejects_unpaired_surrogates()
    {
        Assert.Equal("😀", FinalSubmissionMapping.Normalize("  😀  ", 1));
        Assert.Null(FinalSubmissionMapping.Normalize("😀😀", 1));
        Assert.Null(FinalSubmissionMapping.Normalize("\uD800", 10));
        Assert.Null(FinalSubmissionMapping.Normalize("\uDC00", 10));
        Assert.Null(FinalSubmissionMapping.Normalize(new string('x', 10001), 10000));
    }
}
