using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

public sealed class CouncilRepository(
    DefenseCouncilDbContext db,
    IReviewerDefenseEligibilityContract reviewerEligibility) : ICouncilRepository
{
    public Task<Council?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Councils.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Council?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => db.Councils
            .Include(c => c.Members)
            .Include(c => c.Assignments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Council>> GetCouncilsForOverviewAsync(
        Guid? batchId = null,
        CouncilStatus? status = null,
        CancellationToken ct = default)
    {
        var query = db.Councils
            .Include(c => c.Members)
            .Include(c => c.Assignments)
                .ThenInclude(a => a.Minute)
            .Include(c => c.Assignments)
                .ThenInclude(a => a.Scores)
            .AsNoTracking();

        if (batchId.HasValue)
        {
            query = query.Where(c => c.BatchId == batchId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return await query.OrderBy(c => c.DefenseDate).ThenBy(c => c.CouncilCode).ToListAsync(ct);
    }

    public async Task<bool> ExistsByCodeInBatchAsync(
        Guid batchId,
        string councilCode,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var normalizedCode = councilCode.Trim().ToLower();
        return await db.Councils.AnyAsync(
            c => c.BatchId == batchId
                 && (excludeCouncilId == null || c.Id != excludeCouncilId)
                 && c.CouncilCode.ToLower() == normalizedCode,
            ct);
    }

    public async Task<bool> HasRoomScheduleConflictAsync(
        DateOnly defenseDate,
        string timeSlot,
        string room,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var normalizedSlot = timeSlot.Trim().ToLower();
        var normalizedRoom = room.Trim().ToLower();

        return await db.Councils.AnyAsync(
            c => c.DefenseDate == defenseDate
                 && (excludeCouncilId == null || c.Id != excludeCouncilId)
                 && c.TimeSlot.ToLower() == normalizedSlot
                 && c.Room.ToLower() == normalizedRoom,
            ct);
    }

    public async Task<IReadOnlyList<Guid>> GetConflictingMemberUserIdsAsync(
        DateOnly defenseDate,
        string timeSlot,
        IEnumerable<Guid> userIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var memberIdList = userIds.Distinct().ToList();
        if (memberIdList.Count == 0) return [];

        var normalizedSlot = timeSlot.Trim().ToLower();

        return await db.Councils
            .Where(c => c.DefenseDate == defenseDate
                        && (excludeCouncilId == null || c.Id != excludeCouncilId)
                        && c.TimeSlot.ToLower() == normalizedSlot)
            .SelectMany(c => c.Members)
            .Where(m => memberIdList.Contains(m.UserId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetAssignedGroupCouncilsAsync(
        IEnumerable<Guid> groupIds,
        Guid? excludeCouncilId = null,
        CancellationToken ct = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, Guid>();

        return await db.CouncilAssignments
            .Where(a => idList.Contains(a.GroupId) && (excludeCouncilId == null || a.CouncilId != excludeCouncilId))
            .ToDictionaryAsync(a => a.GroupId, a => a.CouncilId, ct);
    }

    public async Task<IReadOnlySet<Guid>> GetRejectedDefenseGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (idList.Count == 0) return new HashSet<Guid>();

        var rejected = await db.ReviewerAssignments
            .Where(r => r.IsActive
                        && idList.Contains(r.GroupId)
                        && r.Evaluation != null
                        && r.Evaluation.DefenseApproval == DefenseApprovalStatus.Rejected)
            .Select(r => r.GroupId)
            .Distinct()
            .ToListAsync(ct);

        return rejected.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetDefenseEligibleGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default)
        => await GetEligibilityAsync(groupIds, false, ct);

    public async Task<IReadOnlySet<Guid>> LockDefenseEligibleGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default)
        => await GetEligibilityAsync(groupIds, true, ct);

    private async Task<IReadOnlySet<Guid>> GetEligibilityAsync(IEnumerable<Guid> groupIds, bool locked, CancellationToken ct)
    {
        var eligible = new HashSet<Guid>();
        foreach (var groupId in groupIds.Distinct().Order())
        {
            var result = locked
                ? await reviewerEligibility.LockForSchedulingAsync(groupId, ct)
                : await reviewerEligibility.GetAsync(groupId, ct);
            if (result?.CanProceed == true)
                eligible.Add(groupId);
        }

        return eligible;
    }

    public Task<Council?> GetCouncilByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => db.CouncilAssignments
            .Where(a => a.GroupId == groupId)
            .Select(a => a.Council)
            .Include(c => c.Members)
            .Include(c => c.Assignments)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetActiveReviewerIdsByGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken ct = default)
    {
        var idList = groupIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, Guid>();

        return await db.ReviewerAssignments
            .Where(r => r.IsActive && idList.Contains(r.GroupId))
            .ToDictionaryAsync(r => r.GroupId, r => r.ReviewerId, ct);
    }

    public async Task AddAsync(Council council, CancellationToken ct = default)
        => await db.Councils.AddAsync(council, ct);

    public async Task AddMembersAsync(IEnumerable<CouncilMember> members, CancellationToken ct = default)
        => await db.CouncilMembers.AddRangeAsync(members, ct);

    public async Task AddAssignmentsAsync(IEnumerable<CouncilAssignment> assignments, CancellationToken ct = default)
        => await db.CouncilAssignments.AddRangeAsync(assignments, ct);

    public Task<CouncilAssignment?> GetAssignmentWithCouncilAndMembersAsync(Guid assignmentId, CancellationToken ct = default)
        => db.CouncilAssignments
            .Include(a => a.Council)
                .ThenInclude(c => c.Members)
            .Include(a => a.Minute)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);

    public async Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken ct = default)
    {
        await GradingRubricLock.AcquireAsync(db, batchId, ct);
        return await db.GradingCriterias
            .Where(g => g.BatchId == batchId)
            .OrderBy(g => g.OrderIndex)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CouncilScore>> GetCouncilScoresWithDetailsByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default)
        => await db.CouncilScores
            .Include(s => s.CouncilMember)
            .Where(s => s.CouncilAssignmentId == assignmentId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken ct = default)
        => await db.EvaluationDetails
            .Include(d => d.Criteria)
            .Where(d => d.EvaluationId == evaluationId)
            .ToListAsync(ct);

    public Task<CouncilScore?> GetCouncilScoreAsync(Guid assignmentId, Guid councilMemberId, CancellationToken ct = default)
        => db.CouncilScores
            .FirstOrDefaultAsync(s => s.CouncilAssignmentId == assignmentId && s.CouncilMemberId == councilMemberId, ct);

    public async Task SaveCouncilScoreAsync(CouncilScore score, IEnumerable<EvaluationDetail> details, CancellationToken ct = default)
    {
        var existingScore = await db.CouncilScores.FirstOrDefaultAsync(
            s => s.CouncilAssignmentId == score.CouncilAssignmentId && s.CouncilMemberId == score.CouncilMemberId, ct);

        if (existingScore != null)
        {
            existingScore.Score = score.Score;
            existingScore.Feedback = score.Feedback;
            existingScore.EvaluatedAt = score.EvaluatedAt;
            score.Id = existingScore.Id;
        }
        else
        {
            await db.CouncilScores.AddAsync(score, ct);
        }

        var existingDetails = await db.EvaluationDetails
            .Where(d => d.EvaluationId == score.Id)
            .ToListAsync(ct);

        if (existingDetails.Count > 0)
        {
            db.EvaluationDetails.RemoveRange(existingDetails);
        }

        foreach (var detail in details)
        {
            detail.EvaluationId = score.Id;
            await db.EvaluationDetails.AddAsync(detail, ct);
        }
    }

    public Task<ReviewerAssignment?> GetActiveReviewerAssignmentByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => db.ReviewerAssignments
            .Include(r => r.Evaluation)
            .FirstOrDefaultAsync(r => r.GroupId == groupId && r.IsActive, ct);

    public Task<AdvisorEvaluation?> GetAdvisorEvaluationByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => db.AdvisorEvaluations
            .FirstOrDefaultAsync(a => a.GroupId == groupId, ct);

    public Task<ScoreSummary?> GetScoreSummaryByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => db.ScoreSummarys
            .Include(s => s.Components)
            .FirstOrDefaultAsync(s => s.GroupId == groupId, ct);

    public async Task UpdateMinuteLockStatusAsync(Guid assignmentId, CouncilMinuteLockStatus status, CancellationToken ct = default)
    {
        var minute = await db.CouncilMinutes
            .FirstOrDefaultAsync(m => m.CouncilAssignmentId == assignmentId, ct);

        if (minute != null)
        {
            // Do not downgrade UnlockedByOverride to AllScoresEntered
            if (minute.LockStatus == CouncilMinuteLockStatus.UnlockedByOverride && status == CouncilMinuteLockStatus.AllScoresEntered)
            {
                return;
            }

            minute.LockStatus = status;
        }
        else
        {
            await db.CouncilMinutes.AddAsync(new CouncilMinute
            {
                Id = Guid.NewGuid(),
                CouncilAssignmentId = assignmentId,
                LockStatus = status
            }, ct);
        }
    }

    public Task<CouncilMinute?> GetMinuteByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default)
        => db.CouncilMinutes.FirstOrDefaultAsync(m => m.CouncilAssignmentId == assignmentId, ct);

    public async Task SaveMinuteAsync(CouncilMinute minute, CancellationToken ct = default)
    {
        var existing = await db.CouncilMinutes.FirstOrDefaultAsync(m => m.CouncilAssignmentId == minute.CouncilAssignmentId, ct);
        if (existing == null)
        {
            await db.CouncilMinutes.AddAsync(minute, ct);
        }
    }

    public async Task SaveCouncilMinuteDraftAsync(Guid assignmentId, string? studentFeedback, string? overrideMarker, CancellationToken ct = default)
    {
        var minute = await db.CouncilMinutes
            .FirstOrDefaultAsync(m => m.CouncilAssignmentId == assignmentId, ct);

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
            await db.CouncilMinutes.AddAsync(new CouncilMinute
            {
                Id = Guid.NewGuid(),
                CouncilAssignmentId = assignmentId,
                RequiredRevisions = feedback,
                GeneralFeedback = feedback,
                OverrideReason = marker,
                LockStatus = CouncilMinuteLockStatus.Open
            }, ct);
        }
    }
}
