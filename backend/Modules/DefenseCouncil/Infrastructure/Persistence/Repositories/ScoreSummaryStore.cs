using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class ScoreSummaryStore(DefenseCouncilDbContext db) : IScoreSummaryStore
{
    private void RequireTransaction() { if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Shared score transaction required."); }
    public async Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token)
    {
        RequireTransaction(); var scope = $"{actor:N}:{operation}:{key:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope},0))", token);
    }
    public Task<ScoreSummaryIdempotency?> ReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token)
        => db.ScoreSummaryIdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor && r.Operation == operation && r.IdempotencyKey == key, token);
    public async Task LockAdvisorsAsync(IReadOnlyCollection<Guid> groups, CancellationToken token)
    {
        RequireTransaction();
        foreach (var id in groups.Order()) await db.AdvisorEvaluations.FromSqlInterpolated($"SELECT e.*,e.xmin FROM councils.advisor_evaluation e WHERE e.group_id={id} FOR SHARE").AsNoTracking().ToListAsync(token);
    }
    public async Task<IReadOnlyDictionary<Guid, ScoreFacts>> FactsAsync(IReadOnlyCollection<Guid> groups, bool locked, CancellationToken token)
    {
        if (locked)
        {
            RequireTransaction();
            foreach (var id in groups.Order())
            {
                var reviewers = await db.ReviewerAssignments.FromSqlInterpolated($"SELECT r.*,r.xmin FROM councils.reviewer_assignment r WHERE r.group_id={id} AND r.is_active ORDER BY r.id FOR SHARE").AsNoTracking().ToListAsync(token);
                foreach (var r in reviewers) await db.ReviewerEvaluations.FromSqlInterpolated($"SELECT e.*,e.xmin FROM councils.reviewer_evaluation e WHERE e.assignment_id={r.Id} ORDER BY e.id FOR SHARE").AsNoTracking().ToListAsync(token);
            }
            var links = await db.CouncilAssignments.AsNoTracking().Where(a => groups.Contains(a.GroupId)).ToListAsync(token);
            foreach (var id in links.Select(a => a.CouncilId).Distinct().Order()) await db.Councils.FromSqlInterpolated($"SELECT c.* FROM councils.council c WHERE c.id={id} FOR SHARE").AsNoTracking().ToListAsync(token);
            foreach (var a in links.OrderBy(a => a.Id)) await db.CouncilAssignments.FromSqlInterpolated($"SELECT a.*,a.xmin FROM councils.council_assignment a WHERE a.id={a.Id} FOR SHARE").AsNoTracking().ToListAsync(token);
            foreach (var id in links.Select(a => a.CouncilId).Distinct().Order()) await db.CouncilMembers.FromSqlInterpolated($"SELECT m.*,m.xmin FROM councils.council_member m WHERE m.council_id={id} ORDER BY m.id FOR SHARE").AsNoTracking().ToListAsync(token);
            foreach (var a in links.OrderBy(a => a.Id))
            {
                await db.CouncilMinutes.FromSqlInterpolated($"SELECT m.*,m.xmin FROM councils.council_minute m WHERE m.council_assignment_id={a.Id} FOR SHARE").AsNoTracking().ToListAsync(token);
                var scores = await db.CouncilScores.FromSqlInterpolated($"SELECT s.*,s.xmin FROM councils.council_score s WHERE s.council_assignment_id={a.Id} ORDER BY s.id FOR SHARE").AsNoTracking().ToListAsync(token);
                foreach (var s in scores) await db.EvaluationDetails.FromSqlInterpolated($"SELECT d.* FROM councils.evaluation_detail d WHERE d.evaluation_id={s.Id} ORDER BY d.id FOR SHARE").AsNoTracking().ToListAsync(token);
            }
        }
        var advisors = await db.AdvisorEvaluations.AsNoTracking().Where(a => groups.Contains(a.GroupId)).ToListAsync(token);
        var assignments = await db.CouncilAssignments.AsNoTracking().Where(a => groups.Contains(a.GroupId)).ToListAsync(token);
        var councilIds = assignments.Select(a => a.CouncilId).Distinct().ToArray(); var assignmentIds = assignments.Select(a => a.Id).ToArray();
        var councils = await db.Councils.AsNoTracking().Where(c => councilIds.Contains(c.Id)).ToListAsync(token);
        var members = await db.CouncilMembers.AsNoTracking().Where(m => councilIds.Contains(m.CouncilId)).ToListAsync(token);
        var minutes = await db.CouncilMinutes.AsNoTracking().Where(m => assignmentIds.Contains(m.CouncilAssignmentId)).ToListAsync(token);
        var scoresAll = await db.CouncilScores.AsNoTracking().Where(s => assignmentIds.Contains(s.CouncilAssignmentId)).ToListAsync(token);
        var scoreIds = scoresAll.Select(s => s.Id).ToArray();
        var details = await db.EvaluationDetails.AsNoTracking().Where(d => scoreIds.Contains(d.EvaluationId)).ToListAsync(token);
        var reviewersAll = await db.ReviewerAssignments.AsNoTracking().Where(r => groups.Contains(r.GroupId) && r.IsActive).ToListAsync(token);
        var reviewerIds = reviewersAll.Select(r => r.Id).ToArray();
        var evaluations = await db.ReviewerEvaluations.AsNoTracking().Where(e => reviewerIds.Contains(e.AssignmentId)).ToListAsync(token);
        return groups.ToDictionary(g => g, g =>
        {
            var aa = assignments.Where(a => a.GroupId == g).ToArray(); var cc = aa.Select(a => a.CouncilId).ToHashSet(); var ai = aa.Select(a => a.Id).ToHashSet();
            var rr = reviewersAll.Where(r => r.GroupId == g).ToArray(); var ri = rr.Select(r => r.Id).ToHashSet(); var ss = scoresAll.Where(s => ai.Contains(s.CouncilAssignmentId)).ToArray(); var si = ss.Select(s => s.Id).ToHashSet();
            return new ScoreFacts(advisors.Where(a => a.GroupId == g).ToArray(), rr, evaluations.Where(e => ri.Contains(e.AssignmentId)).ToArray(), aa, councils.Where(c => cc.Contains(c.Id)).ToArray(), members.Where(m => cc.Contains(m.CouncilId)).ToArray(), minutes.Where(m => ai.Contains(m.CouncilAssignmentId)).ToArray(), ss, details.Where(d => si.Contains(d.EvaluationId)).ToArray());
        });
    }
    public Task<ScoreSummary?> SummaryAsync(Guid group, bool locked, CancellationToken token)
    {
        if (!locked) return db.ScoreSummarys.AsNoTracking().SingleOrDefaultAsync(s => s.GroupId == group, token);
        RequireTransaction(); return db.ScoreSummarys.FromSqlInterpolated($"SELECT s.*,s.xmin FROM councils.score_summary s WHERE s.group_id={group} FOR UPDATE").SingleOrDefaultAsync(token);
    }
    public async Task<IReadOnlyDictionary<Guid, ScoreHistory>> HistoriesAsync(IReadOnlyCollection<Guid> groups, CancellationToken token)
    {
        var rows = await db.ScoreSummarys.AsNoTracking().Where(s => groups.Contains(s.GroupId)).ToListAsync(token); var ids = rows.Select(s => s.Id).ToArray();
        var revisions = await db.ScoreSummaryRevisions.AsNoTracking().Where(r => ids.Contains(r.SummaryId)).OrderByDescending(r => r.RevisionNo).ToListAsync(token);
        var components = await db.ScoreSummaryComponents.AsNoTracking().Where(c => ids.Contains(c.SummaryId)).ToListAsync(token);
        return rows.ToDictionary(s => s.GroupId, s => new ScoreHistory(s, revisions.Where(r => r.SummaryId == s.Id).ToArray(), components.Where(c => c.SummaryId == s.Id).ToArray()));
    }
    public Task<Guid?> AssignmentGroupAsync(Guid assignment, CancellationToken token) => db.CouncilAssignments.AsNoTracking().Where(a => a.Id == assignment).Select(a => (Guid?)a.GroupId).SingleOrDefaultAsync(token);
    public async Task<IReadOnlyList<Guid>> CouncilGroupsAsync(Guid council, CancellationToken token) => await db.CouncilAssignments.AsNoTracking().Where(a => a.CouncilId == council).Select(a => a.GroupId).Order().ToListAsync(token);
    public Task<bool> PublishedAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => db.ScoreSummarys.AsNoTracking().AnyAsync(s => groups.Contains(s.GroupId) && s.Status == Domain.Enums.ScoreSummaryStatus.PublishedToStudent, token);
    public void Add(ScoreSummary summary) => db.ScoreSummarys.Add(summary);
    public void Add(ScoreSummaryRevision revision, IReadOnlyList<ScoreSummaryComponent> components) { db.ScoreSummaryRevisions.Add(revision); db.ScoreSummaryComponents.AddRange(components); }
    public void Add(ScoreSummaryIdempotency receipt) => db.ScoreSummaryIdempotencyRecords.Add(receipt);
}
