namespace UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;

public sealed record AcademicResultMidtermSnapshotContractDto(
    Guid ReportId,
    string Status,
    int CurrentVersionNo,
    string? FacultyFeedback,
    Guid? ReviewedBy,
    DateTime? ReviewedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record AcademicResultGroupSnapshotContractDto(
    Guid GroupId,
    Guid TopicId,
    Guid BatchId,
    string GroupStatus,
    DateTime CreatedAtUtc,
    IReadOnlyList<DefenseGroupMemberContractDto> Members,
    AcademicResultMidtermSnapshotContractDto? MidtermReport,
    string? FinalSubmissionStatus);

public interface IAcademicResultsThesisReader
{
    Task<IReadOnlyList<AcademicResultGroupSnapshotContractDto>> GetGroupsByBatchIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<AcademicResultGroupSnapshotContractDto?> GetGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
