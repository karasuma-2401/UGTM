using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class FinalSubmission
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public FinalSubmissionStatus Status { get; set; } = FinalSubmissionStatus.NotSubmitted;
    public int CurrentVersionNo { get; set; } = 1;
    public string? AbstractVi { get; set; }
    public string? AbstractEn { get; set; }
    public string? KeywordsVi { get; set; }
    public string? KeywordsEn { get; set; }
    public Guid? AdvisorConfirmedBy { get; set; }
    public DateTime? AdvisorConfirmedAt { get; set; }
    public string? FacultyFeedback { get; set; }
    public Guid? ArchivedConfirmedBy { get; set; }
    public DateTime? ArchivedConfirmedAt { get; set; }
    public bool IsLegacy { get; set; } = true;
    public Guid? CurrentPackageId { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? LegacySnapshot { get; set; }
    public uint Version { get; set; }

    public void Submit(FinalSubmissionPackage package)
    {
        if (Status is not (FinalSubmissionStatus.NotSubmitted or FinalSubmissionStatus.Submitted or FinalSubmissionStatus.NeedRevision))
            throw new InvalidOperationException("The archival package is locked.");
        if (package.SubmissionId != Id || package.VersionNo != (CurrentPackageId.HasValue ? checked(CurrentVersionNo + 1) : 1))
            throw new ArgumentException("Package identity/version does not match the submission.", nameof(package));
        CurrentPackageId = package.Id;
        CurrentVersionNo = package.VersionNo;
        AbstractVi = package.AbstractVi;
        AbstractEn = package.AbstractEn;
        KeywordsVi = package.KeywordsVi;
        KeywordsEn = package.KeywordsEn;
        SubmittedBy = package.SubmittedBy;
        SubmittedAtUtc = package.SubmittedAtUtc;
        AdvisorConfirmedBy = ArchivedConfirmedBy = null;
        AdvisorConfirmedAt = ArchivedConfirmedAt = null;
        FacultyFeedback = null;
        Status = FinalSubmissionStatus.Submitted;
        IsLegacy = false;
    }

    public StudentGroup Group { get; set; } = null!;
}
