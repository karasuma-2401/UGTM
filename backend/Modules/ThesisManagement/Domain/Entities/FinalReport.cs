using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class FinalReport
{
    private FinalReport() { }

    private FinalReport(Guid id, Guid groupId, FinalReportVersionType versionType, DateTime submittedAtUtc)
    {
        if (id == Guid.Empty || groupId == Guid.Empty)
        {
            throw new ArgumentException("Report and group identifiers must be nonempty.");
        }

        if (!Enum.IsDefined(versionType))
        {
            throw new ArgumentOutOfRangeException(nameof(versionType), "Report version type is invalid.");
        }

        if (submittedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Submission time must be UTC.", nameof(submittedAtUtc));
        }

        Id = id;
        GroupId = groupId;
        VersionType = versionType;
        CurrentVersionNo = 1;
        Status = FinalReportStatus.Submitted;
        SubmittedAt = submittedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public FinalReportVersionType VersionType { get; private set; }
    public int CurrentVersionNo { get; private set; }
    public FinalReportStatus Status { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public uint Version { get; private set; }

    public StudentGroup Group { get; private set; } = null!;

    public static FinalReport CreateSubmitted(
        Guid id,
        Guid groupId,
        FinalReportVersionType versionType,
        DateTime submittedAtUtc)
        => new(id, groupId, versionType, submittedAtUtc);

    public bool TrySubmitRevision(DateTime submittedAtUtc)
    {
        if (Status != FinalReportStatus.Submitted
            || submittedAtUtc.Kind != DateTimeKind.Utc
            || submittedAtUtc < SubmittedAt)
        {
            return false;
        }

        CurrentVersionNo = checked(CurrentVersionNo + 1);
        SubmittedAt = submittedAtUtc;
        return true;
    }

    public bool TryBeginEvaluation()
    {
        if (Status != FinalReportStatus.Submitted)
        {
            return false;
        }

        Status = FinalReportStatus.UnderEvaluation;
        return true;
    }
}
