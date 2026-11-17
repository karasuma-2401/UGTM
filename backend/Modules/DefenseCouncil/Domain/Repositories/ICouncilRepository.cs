using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface ICouncilRepository
{
    Task<Council?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Council?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Council>> GetCouncilsForOverviewAsync(
        Guid? batchId = null,
        UGTM.Modules.DefenseCouncil.Domain.Enums.CouncilStatus? status = null,
        CancellationToken ct = default);

    Task<bool> ExistsByCodeInBatchAsync(
        Guid batchId,
        string councilCode,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<bool> HasRoomScheduleConflictAsync(
        DateOnly defenseDate,
        string timeSlot,
        string room,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetConflictingMemberUserIdsAsync(
        DateOnly defenseDate,
        string timeSlot,
        IEnumerable<Guid> userIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, Guid>> GetAssignedGroupCouncilsAsync(
        IEnumerable<Guid> groupIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> GetRejectedDefenseGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> GetDefenseEligibleGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> LockDefenseEligibleGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default);

    Task<Council?> GetCouncilByGroupIdAsync(
        Guid groupId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, Guid>> GetActiveReviewerIdsByGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default);

    Task AddAsync(Council council, CancellationToken ct = default);

    Task AddMembersAsync(IEnumerable<CouncilMember> members, CancellationToken ct = default);

    Task AddAssignmentsAsync(IEnumerable<CouncilAssignment> assignments, CancellationToken ct = default);

    Task<CouncilAssignment?> GetAssignmentWithCouncilAndMembersAsync(Guid assignmentId, CancellationToken ct = default);

    Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken ct = default);

    Task<IReadOnlyList<CouncilScore>> GetCouncilScoresWithDetailsByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default);

    Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken ct = default);

    Task<CouncilScore?> GetCouncilScoreAsync(Guid assignmentId, Guid councilMemberId, CancellationToken ct = default);

    Task SaveCouncilScoreAsync(CouncilScore score, IEnumerable<EvaluationDetail> details, CancellationToken ct = default);

    Task<ReviewerAssignment?> GetActiveReviewerAssignmentByGroupIdAsync(Guid groupId, CancellationToken ct = default);

    Task<AdvisorEvaluation?> GetAdvisorEvaluationByGroupIdAsync(Guid groupId, CancellationToken ct = default);

    Task<ScoreSummary?> GetScoreSummaryByGroupIdAsync(Guid groupId, CancellationToken ct = default);

    Task UpdateMinuteLockStatusAsync(Guid assignmentId, UGTM.Modules.DefenseCouncil.Domain.Enums.CouncilMinuteLockStatus status, CancellationToken ct = default);

    Task<CouncilMinute?> GetMinuteByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default);

    Task SaveMinuteAsync(CouncilMinute minute, CancellationToken ct = default);

    Task SaveCouncilMinuteDraftAsync(Guid assignmentId, string? studentFeedback, string? overrideMarker, CancellationToken ct = default);
}
