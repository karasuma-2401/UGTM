using System.Text;
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

    public void RecordReview(FinalSubmissionReviewAction action, Guid actor, DateTime atUtc, string? reason)
    {
        if (IsLegacy || CurrentPackageId is null || SubmittedAtUtc is null || Status == FinalSubmissionStatus.ArchiveConfirmed)
            throw new InvalidOperationException("A nonlegacy, mutable package is required.");
        if (actor == Guid.Empty || atUtc.Kind != DateTimeKind.Utc || atUtc < SubmittedAtUtc
            || (AdvisorConfirmedAt.HasValue && atUtc < AdvisorConfirmedAt))
            throw new ArgumentException("Invalid review actor/time.");
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (action == FinalSubmissionReviewAction.RequestRevision)
        {
            if (Status is not (FinalSubmissionStatus.Submitted or FinalSubmissionStatus.AdvisorConfirmed))
                throw new InvalidOperationException("Revision cannot be requested in this state.");
            if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim() || reason.Contains('\0')
                || reason.EnumerateRunes().Count() > 1000)
                throw new ArgumentException("A bounded revision reason is required.", nameof(reason));
            for (var i = 0; i < reason.Length; i++)
                if (char.IsSurrogate(reason[i]) && (!char.IsHighSurrogate(reason[i]) || i + 1 >= reason.Length || !char.IsLowSurrogate(reason[++i])))
                    throw new ArgumentException("Invalid revision reason encoding.", nameof(reason));
            FacultyFeedback = reason;
            AdvisorConfirmedBy = ArchivedConfirmedBy = null;
            AdvisorConfirmedAt = ArchivedConfirmedAt = null;
            Status = FinalSubmissionStatus.NeedRevision;
            return;
        }
        if (reason is not null) throw new ArgumentException("Confirmation cannot contain a revision reason.", nameof(reason));
        if (action == FinalSubmissionReviewAction.AdvisorConfirm && Status == FinalSubmissionStatus.Submitted)
        {
            AdvisorConfirmedBy = actor;
            AdvisorConfirmedAt = atUtc;
            FacultyFeedback = null;
            Status = FinalSubmissionStatus.AdvisorConfirmed;
        }
        else if (action == FinalSubmissionReviewAction.FacultyConfirm && Status == FinalSubmissionStatus.AdvisorConfirmed)
        {
            ArchivedConfirmedBy = actor;
            ArchivedConfirmedAt = atUtc;
            Status = FinalSubmissionStatus.ArchiveConfirmed;
        }
        else throw new InvalidOperationException("Invalid archival review transition.");
    }
}
