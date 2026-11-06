using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class FakeReauthenticationVerifier : IReauthenticationVerifier
{
    public ReauthenticationOutcome DefaultOutcome { get; set; } = ReauthenticationOutcome.Verified;
    public Dictionary<(Guid UserId, string Password), ReauthenticationOutcome> CustomOutcomes { get; } = new();

    public Task<ReauthenticationOutcome> VerifyPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        if (CustomOutcomes.TryGetValue((userId, password), out var outcome))
        {
            return Task.FromResult(outcome);
        }

        return Task.FromResult(DefaultOutcome);
    }
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public Guid? SessionId { get; set; } = Guid.NewGuid();
    public IReadOnlySet<string> Roles { get; set; } = new HashSet<string> { "Admin" };
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();
}

public sealed class FakeBatchReader : IKltnBatchReader
{
    public HashSet<Guid> ExistingBatches { get; } = [];

    public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingBatches.Contains(batchId));

    public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<BatchRegistrationWindowContractDto?>(null);

    public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<BatchTopicPreparationWindowContractDto?>(null);

    public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<BatchRenameRequestWindowContractDto?>(null);

    public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<BatchFinalReportSubmissionWindowContractDto?>(null);
}

public sealed class FakeCouncilUserInfoReader : ICouncilUserInfoReader
{
    public Dictionary<Guid, CouncilUserInfoContractDto> Users { get; } = [];

    public Task<CouncilUserInfoContractDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.GetValueOrDefault(userId));

    public Task<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var result = userIds
            .Distinct()
            .Where(id => Users.ContainsKey(id))
            .ToDictionary(id => id, id => Users[id]);

        return Task.FromResult<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>>(result);
    }
}

public sealed class FakeStudentInfoReader : IStudentInfoReader
{
    public Dictionary<Guid, StudentInfoContractDto> Students { get; } = [];

    public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken cancellationToken = default)
    {
        var result = studentIds
            .Distinct()
            .Where(id => Students.ContainsKey(id))
            .ToDictionary(id => id, id => Students[id]);

        return Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(result);
    }

    public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(Guid? studentId, string? userCode, CancellationToken cancellationToken = default)
        => Task.FromResult(studentId.HasValue ? Students.GetValueOrDefault(studentId.Value) : null);
}

public sealed class FakeDefenseGroupContract : IDefenseGroupContract
{
    public Dictionary<Guid, DefenseGroupSnapshotContractDto> Groups { get; } = [];

    public Task<DefenseGroupSnapshotContractDto?> GetGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        => Task.FromResult(Groups.GetValueOrDefault(groupId));

    public Task<IReadOnlyDictionary<Guid, DefenseGroupSnapshotContractDto>> GetGroupsByIdsAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken = default)
    {
        var result = groupIds
            .Distinct()
            .Where(id => Groups.ContainsKey(id))
            .ToDictionary(id => id, id => Groups[id]);

        return Task.FromResult<IReadOnlyDictionary<Guid, DefenseGroupSnapshotContractDto>>(result);
    }

    public Task<DefenseGroupSnapshotContractDto?> GetActiveGroupByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var group = Groups.Values.FirstOrDefault(g =>
            g.Status != "Stopped" && g.Members.Any(m => m.StudentId == studentId && m.ConfirmationStatus == "Confirmed"));

        return Task.FromResult(group);
    }
}

public sealed class FakeTopicDefenseContract : ITopicDefenseContract
{
    public Dictionary<Guid, TopicDefenseDetailsContractDto> Topics { get; } = [];

    public Task<TopicDefenseDetailsContractDto?> GetTopicDetailsAsync(Guid topicId, CancellationToken cancellationToken = default)
        => Task.FromResult(Topics.GetValueOrDefault(topicId));

    public Task<IReadOnlyDictionary<Guid, TopicDefenseDetailsContractDto>> GetTopicsDetailsAsync(IEnumerable<Guid> topicIds, CancellationToken cancellationToken = default)
    {
        var result = topicIds
            .Distinct()
            .Where(id => Topics.ContainsKey(id))
            .ToDictionary(id => id, id => Topics[id]);

        return Task.FromResult<IReadOnlyDictionary<Guid, TopicDefenseDetailsContractDto>>(result);
    }
}

public sealed class FakeDefenseCouncilAuditOutbox : IDefenseCouncilAuditOutbox
{
    public List<(AuditIntegrationEntry Entry, DateTime OccurredAtUtc)> Entries { get; } = [];

    public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc)
    {
        Entries.Add((entry, occurredAtUtc));
    }
}

public sealed class FakeDefenseCouncilUnitOfWork : IDefenseCouncilUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

public sealed class FakeCouncilRepository : ICouncilRepository
{
    public List<Council> Councils { get; } = [];
    public List<CouncilMember> Members { get; } = [];
    public List<CouncilAssignment> Assignments { get; } = [];
    public HashSet<Guid> RejectedGroupIds { get; } = [];
    public HashSet<Guid> IneligibleDefenseGroupIds { get; } = [];
    public HashSet<Guid>? ExplicitEligibleGroupIds { get; set; }
    public Dictionary<Guid, Guid> ActiveReviewers { get; } = [];
    public List<CouncilScore> Scores { get; } = [];
    public List<EvaluationDetail> EvaluationDetails { get; } = [];
    public List<GradingCriteria> GradingCriteriaList { get; } = [];
    public List<CouncilMinute> Minutes { get; } = [];
    public Dictionary<Guid, ReviewerAssignment> ActiveReviewerAssignments { get; } = [];
    public Dictionary<Guid, AdvisorEvaluation> AdvisorEvaluations { get; } = [];
    public Dictionary<Guid, ScoreSummary> ScoreSummaries { get; } = [];
    public List<ScoreSummaryComponent> ScoreSummaryComponents { get; } = [];

    public Task<Council?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Councils.FirstOrDefault(c => c.Id == id));

    public Task<Council?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        var council = Councils.FirstOrDefault(c => c.Id == id);
        if (council is not null)
        {
            council.Members = Members.Where(m => m.CouncilId == id).ToList();
            council.Assignments = Assignments.Where(a => a.CouncilId == id).ToList();
        }
        return Task.FromResult(council);
    }

    public Task<IReadOnlyList<Council>> GetCouncilsForOverviewAsync(
        Guid? batchId = null,
        CouncilStatus? status = null,
        CancellationToken ct = default)
    {
        var list = Councils.AsEnumerable();
        if (batchId.HasValue) list = list.Where(c => c.BatchId == batchId.Value);
        if (status.HasValue) list = list.Where(c => c.Status == status.Value);

        var result = list.Select(c =>
        {
            var clone = new Council
            {
                Id = c.Id,
                BatchId = c.BatchId,
                CouncilCode = c.CouncilCode,
                DefenseDate = c.DefenseDate,
                TimeSlot = c.TimeSlot,
                Room = c.Room,
                Status = c.Status,
                DecisionNumber = c.DecisionNumber,
                SignedDate = c.SignedDate,
                DecisionFileUrl = c.DecisionFileUrl,
                ApprovedBy = c.ApprovedBy,
                ApprovedAt = c.ApprovedAt,
                IsSubmittedToAcademicAffairs = c.IsSubmittedToAcademicAffairs,
                SubmittedAt = c.SubmittedAt,
                SubmittedBy = c.SubmittedBy,
                SubmissionNotes = c.SubmissionNotes
            };

            clone.Members = Members.Where(m => m.CouncilId == c.Id).ToList();
            var assignments = Assignments.Where(a => a.CouncilId == c.Id).ToList();
            foreach (var a in assignments)
            {
                a.Council = clone;
                a.Minute = Minutes.FirstOrDefault(m => m.CouncilAssignmentId == a.Id);
                a.Scores = Scores.Where(s => s.CouncilAssignmentId == a.Id).ToList();
            }
            clone.Assignments = assignments;
            return clone;
        }).OrderBy(c => c.DefenseDate).ThenBy(c => c.CouncilCode).ToList();

        return Task.FromResult<IReadOnlyList<Council>>(result);
    }

    public Task<bool> ExistsByCodeInBatchAsync(Guid batchId, string councilCode, Guid? excludeCouncilId = null, CancellationToken ct = default)
    {
        var normalized = councilCode.Trim().ToLower();
        var exists = Councils.Any(c =>
            c.BatchId == batchId
            && (excludeCouncilId == null || c.Id != excludeCouncilId)
            && c.CouncilCode.ToLower() == normalized);

        return Task.FromResult(exists);
    }

    public Task<bool> HasRoomScheduleConflictAsync(DateOnly defenseDate, string timeSlot, string room, Guid? excludeCouncilId = null, CancellationToken ct = default)
    {
        var slot = timeSlot.Trim().ToLower();
        var rm = room.Trim().ToLower();

        var conflict = Councils.Any(c =>
            c.DefenseDate == defenseDate
            && (excludeCouncilId == null || c.Id != excludeCouncilId)
            && c.TimeSlot.ToLower() == slot
            && c.Room.ToLower() == rm);

        return Task.FromResult(conflict);
    }

    public Task<IReadOnlyList<Guid>> GetConflictingMemberUserIdsAsync(DateOnly defenseDate, string timeSlot, IEnumerable<Guid> userIds, Guid? excludeCouncilId = null, CancellationToken ct = default)
    {
        var userSet = userIds.ToHashSet();
        var slot = timeSlot.Trim().ToLower();

        var conflictingCouncils = Councils
            .Where(c => c.DefenseDate == defenseDate
                        && (excludeCouncilId == null || c.Id != excludeCouncilId)
                        && c.TimeSlot.ToLower() == slot)
            .Select(c => c.Id)
            .ToHashSet();

        var conflictingMembers = Members
            .Where(m => conflictingCouncils.Contains(m.CouncilId) && userSet.Contains(m.UserId))
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        return Task.FromResult<IReadOnlyList<Guid>>(conflictingMembers);
    }

    public Task<IReadOnlyDictionary<Guid, Guid>> GetAssignedGroupCouncilsAsync(IEnumerable<Guid> groupIds, Guid? excludeCouncilId = null, CancellationToken ct = default)
    {
        var idSet = groupIds.ToHashSet();
        var result = Assignments
            .Where(a => idSet.Contains(a.GroupId) && (excludeCouncilId == null || a.CouncilId != excludeCouncilId))
            .ToDictionary(a => a.GroupId, a => a.CouncilId);

        return Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(result);
    }

    public Task<IReadOnlySet<Guid>> GetRejectedDefenseGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default)
    {
        var result = groupIds.Where(id => RejectedGroupIds.Contains(id)).ToHashSet();
        return Task.FromResult<IReadOnlySet<Guid>>(result);
    }

    public Task<IReadOnlySet<Guid>> GetDefenseEligibleGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (ExplicitEligibleGroupIds != null)
        {
            var filtered = idList.Where(id => ExplicitEligibleGroupIds.Contains(id)).ToHashSet();
            return Task.FromResult<IReadOnlySet<Guid>>(filtered);
        }

        var result = idList
            .Where(id => !RejectedGroupIds.Contains(id) && !IneligibleDefenseGroupIds.Contains(id))
            .ToHashSet();

        return Task.FromResult<IReadOnlySet<Guid>>(result);
    }

    public Task<Council?> GetCouncilByGroupIdAsync(Guid groupId, CancellationToken ct = default)
    {
        var assignment = Assignments.FirstOrDefault(a => a.GroupId == groupId);
        if (assignment is null) return Task.FromResult<Council?>(null);

        var council = Councils.FirstOrDefault(c => c.Id == assignment.CouncilId);
        if (council is not null)
        {
            council.Members = Members.Where(m => m.CouncilId == council.Id).ToList();
            council.Assignments = Assignments.Where(a => a.CouncilId == council.Id).ToList();
        }
        return Task.FromResult(council);
    }

    public Task<IReadOnlyDictionary<Guid, Guid>> GetActiveReviewerIdsByGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default)
    {
        var result = groupIds
            .Distinct()
            .Where(id => ActiveReviewers.ContainsKey(id))
            .ToDictionary(id => id, id => ActiveReviewers[id]);

        return Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(result);
    }

    public Task AddAsync(Council council, CancellationToken ct = default)
    {
        Councils.Add(council);
        return Task.CompletedTask;
    }

    public Task AddMembersAsync(IEnumerable<CouncilMember> members, CancellationToken ct = default)
    {
        Members.AddRange(members);
        return Task.CompletedTask;
    }

    public Task AddAssignmentsAsync(IEnumerable<CouncilAssignment> assignments, CancellationToken ct = default)
    {
        Assignments.AddRange(assignments);
        return Task.CompletedTask;
    }

    public Task<CouncilAssignment?> GetAssignmentWithCouncilAndMembersAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = Assignments.FirstOrDefault(a => a.Id == assignmentId);
        if (assignment is null) return Task.FromResult<CouncilAssignment?>(null);

        var council = Councils.FirstOrDefault(c => c.Id == assignment.CouncilId);
        if (council is not null)
        {
            council.Members = Members.Where(m => m.CouncilId == council.Id).ToList();
            assignment.Council = council;
        }

        assignment.Minute = Minutes.FirstOrDefault(m => m.CouncilAssignmentId == assignmentId);
        return Task.FromResult<CouncilAssignment?>(assignment);
    }

    public Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken ct = default)
    {
        var result = GradingCriteriaList.Where(g => g.BatchId == batchId).OrderBy(g => g.OrderIndex).ToList();
        return Task.FromResult<IReadOnlyList<GradingCriteria>>(result);
    }

    public Task<IReadOnlyList<CouncilScore>> GetCouncilScoresWithDetailsByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var result = Scores.Where(s => s.CouncilAssignmentId == assignmentId).ToList();
        foreach (var s in result)
        {
            s.CouncilMember = Members.FirstOrDefault(m => m.Id == s.CouncilMemberId)!;
        }
        return Task.FromResult<IReadOnlyList<CouncilScore>>(result);
    }

    public Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken ct = default)
    {
        var result = EvaluationDetails.Where(d => d.EvaluationId == evaluationId).ToList();
        foreach (var d in result)
        {
            d.Criteria = GradingCriteriaList.FirstOrDefault(c => c.Id == d.CriteriaId)!;
        }
        return Task.FromResult<IReadOnlyList<EvaluationDetail>>(result);
    }

    public Task<CouncilScore?> GetCouncilScoreAsync(Guid assignmentId, Guid councilMemberId, CancellationToken ct = default)
    {
        var result = Scores.FirstOrDefault(s => s.CouncilAssignmentId == assignmentId && s.CouncilMemberId == councilMemberId);
        return Task.FromResult(result);
    }

    public Task SaveCouncilScoreAsync(CouncilScore score, IEnumerable<EvaluationDetail> details, CancellationToken ct = default)
    {
        var existing = Scores.FirstOrDefault(s => s.CouncilAssignmentId == score.CouncilAssignmentId && s.CouncilMemberId == score.CouncilMemberId);
        if (existing != null)
        {
            existing.Score = score.Score;
            existing.Feedback = score.Feedback;
            existing.EvaluatedAt = score.EvaluatedAt;
            score.Id = existing.Id;
        }
        else
        {
            Scores.Add(score);
        }

        EvaluationDetails.RemoveAll(d => d.EvaluationId == score.Id);
        foreach (var d in details)
        {
            d.EvaluationId = score.Id;
            EvaluationDetails.Add(d);
        }
        return Task.CompletedTask;
    }

    public Task<ReviewerAssignment?> GetActiveReviewerAssignmentByGroupIdAsync(Guid groupId, CancellationToken ct = default)
    {
        return Task.FromResult(ActiveReviewerAssignments.GetValueOrDefault(groupId));
    }

    public Task<AdvisorEvaluation?> GetAdvisorEvaluationByGroupIdAsync(Guid groupId, CancellationToken ct = default)
    {
        return Task.FromResult(AdvisorEvaluations.GetValueOrDefault(groupId));
    }

    public Task<ScoreSummary?> GetScoreSummaryByGroupIdAsync(Guid groupId, CancellationToken ct = default)
    {
        return Task.FromResult(ScoreSummaries.GetValueOrDefault(groupId));
    }

    public Task SaveScoreSummaryAsync(ScoreSummary summary, IEnumerable<ScoreSummaryComponent> components, CancellationToken ct = default)
    {
        ScoreSummaries[summary.GroupId] = summary;
        ScoreSummaryComponents.RemoveAll(c => c.SummaryId == summary.Id);
        ScoreSummaryComponents.AddRange(components);
        return Task.CompletedTask;
    }

    public Task UpdateMinuteLockStatusAsync(Guid assignmentId, CouncilMinuteLockStatus status, CancellationToken ct = default)
    {
        var minute = Minutes.FirstOrDefault(m => m.CouncilAssignmentId == assignmentId);
        if (minute != null)
        {
            if (minute.LockStatus == CouncilMinuteLockStatus.UnlockedByOverride && status == CouncilMinuteLockStatus.AllScoresEntered)
            {
                return Task.CompletedTask;
            }

            minute.LockStatus = status;
        }
        else
        {
            Minutes.Add(new CouncilMinute
            {
                Id = Guid.NewGuid(),
                CouncilAssignmentId = assignmentId,
                LockStatus = status
            });
        }
        return Task.CompletedTask;
    }

    public Task<CouncilMinute?> GetMinuteByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default)
    {
        return Task.FromResult(Minutes.FirstOrDefault(m => m.CouncilAssignmentId == assignmentId));
    }

    public Task SaveMinuteAsync(CouncilMinute minute, CancellationToken ct = default)
    {
        var existing = Minutes.FirstOrDefault(m => m.CouncilAssignmentId == minute.CouncilAssignmentId);
        if (existing == null)
        {
            Minutes.Add(minute);
        }
        return Task.CompletedTask;
    }

    public Task SaveCouncilMinuteDraftAsync(Guid assignmentId, string? studentFeedback, string? overrideMarker, CancellationToken ct = default)
    {
        var minute = Minutes.FirstOrDefault(m => m.CouncilAssignmentId == assignmentId);
        var feedback = string.IsNullOrWhiteSpace(studentFeedback) ? null : studentFeedback.Trim();
        var marker = string.IsNullOrWhiteSpace(overrideMarker) ? null : overrideMarker.Trim();

        if (minute != null)
        {
            minute.RequiredRevisions = feedback;
            minute.GeneralFeedback = feedback;
            if (marker != null)
            {
                minute.OverrideReason = marker;
            }
        }
        else
        {
            Minutes.Add(new CouncilMinute
            {
                Id = Guid.NewGuid(),
                CouncilAssignmentId = assignmentId,
                RequiredRevisions = feedback,
                GeneralFeedback = feedback,
                OverrideReason = marker,
                LockStatus = CouncilMinuteLockStatus.Open
            });
        }
        return Task.CompletedTask;
    }
}
