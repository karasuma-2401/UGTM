using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class ProgressReportVersion
{
    private ProgressReportVersion() { }

    private ProgressReportVersion(Guid id, Guid progressReportId, int versionNo, string gitUrl, string? studentNote, Guid submittedBy, DateTime submittedAtUtc, bool isLate)
    {
        if (id == Guid.Empty || progressReportId == Guid.Empty || submittedBy == Guid.Empty)
        {
            throw new ArgumentException("Version, report, and submitter identifiers must be nonempty.");
        }

        if (versionNo < 1 || string.IsNullOrWhiteSpace(gitUrl) || submittedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Progress report version data is invalid.");
        }

        Id = id;
        ProgressReportId = progressReportId;
        VersionNo = versionNo;
        GitUrl = gitUrl.Trim();
        StudentNote = Normalize(studentNote);
        SubmittedBy = submittedBy;
        SubmittedAt = submittedAtUtc;
        IsLate = isLate;
        Status = ProgressReportStatus.Submitted;
    }

    public Guid Id { get; private set; }
    public Guid ProgressReportId { get; private set; }
    public int VersionNo { get; private set; }
    public string GitUrl { get; private set; } = null!;
    public string? StudentNote { get; private set; }
    public Guid SubmittedBy { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public bool IsLate { get; private set; }
    public ProgressReportStatus Status { get; private set; }
    public string? AdvisorFeedback { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public ProgressReport ProgressReport { get; private set; } = null!;

    public static ProgressReportVersion CreateSubmitted(Guid id, Guid progressReportId, int versionNo, string gitUrl, string? studentNote, Guid submittedBy, DateTime submittedAtUtc, bool isLate)
        => new(id, progressReportId, versionNo, gitUrl, studentNote, submittedBy, submittedAtUtc, isLate);

    public bool TryReview(ProgressReportStatus decision, string? feedback, Guid reviewedBy, DateTime reviewedAtUtc)
    {
        if (Status != ProgressReportStatus.Submitted
            || decision is not (ProgressReportStatus.Accepted or ProgressReportStatus.NeedRevision)
            || reviewedBy == Guid.Empty
            || reviewedAtUtc.Kind != DateTimeKind.Utc)
        {
            return false;
        }

        var normalizedFeedback = Normalize(feedback);
        if (decision == ProgressReportStatus.NeedRevision && normalizedFeedback is null)
        {
            return false;
        }

        Status = decision;
        AdvisorFeedback = normalizedFeedback;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAtUtc;
        return true;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
