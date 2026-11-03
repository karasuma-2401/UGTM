using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    private const string ScoresRoute = "/api/thesis/scores";
    private readonly Guid _scoreReviewerId = Guid.NewGuid();
    private readonly Guid _scoreMemberId = Guid.NewGuid();
    private readonly Guid _scoreFacultyId = Guid.NewGuid();
    private readonly Guid _scoreAcademicId = Guid.NewGuid();
    private Guid _scoreAssignmentId;
    private Guid _scoreCriteriaId;
    private async Task PrepareScoreCalculationAsync(bool inside = false, bool seal = true)
    {
        using var initial = await SubmitAsync("pdf", _studentId);
        await ArchivalDataAsync(initial);
        Guid reportId; Guid fileId; string checksum;
        await using (var db = Db<ThesisManagementDbContext>())
        { var report = await db.FinalReports.SingleAsync(); var file = await db.SubmissionFiles.SingleAsync(); reportId = report.Id; fileId = file.Id; checksum = file.ChecksumSha256!; }
        using var begin = await BeginAsync(reportId, 1, _advisorId);
        await ArchivalDataAsync(begin);
        await PrepareArchivalAsync();
        await using (var identity = Db<IdentityDbContext>())
        {
            var roles = await identity.Roles.ToDictionaryAsync(r => r.RoleName, r => r.Id);
            foreach (var pair in new[] { (_scoreReviewerId, "Lecturer"), (_scoreMemberId, "Lecturer"), (_scoreFacultyId, "Faculty"), (_scoreAcademicId, "AcademicAffairs") })
            {
                identity.Users.Add(User.Create(pair.Item1, $"cp3-{pair.Item1:N}@test.example", $"CP3-{pair.Item1:N}@TEST.EXAMPLE", "Score workflow", null, null, null, AccountStatus.Active, SeedTime));
                identity.UserRoles.Add(new UserRole { UserId = pair.Item1, RoleId = roles[pair.Item2], AssignedAt = SeedTime });
            }
            identity.UserRoles.Add(new UserRole { UserId = _replacementAdvisorId, RoleId = roles["Lecturer"], AssignedAt = SeedTime });
            await identity.SaveChangesAsync();
        }
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var advisor = AdvisorEvaluation.Create(Guid.NewGuid(), _groupId, reportId, 1, fileId, checksum, 8, "Advisor feedback", _advisorId, _clock.UtcNow.AddDays(-3));
            Assert.True(advisor.TryDecide(true, null, _advisorId, _clock.UtcNow.AddDays(-3)));
            defense.AdvisorEvaluations.Add(advisor);
            var reviewer = ReviewerAssignment.Create(Guid.NewGuid(), _groupId, _scoreReviewerId, _advisorId, advisor.Id, reportId, 1, fileId, checksum, _adminId, _clock.UtcNow.AddDays(-2));
            var evaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), reviewer.Id, 7, "Reviewer feedback", DefenseApprovalStatus.Approved, _scoreReviewerId, _clock.UtcNow.AddDays(-2));
            Assert.True(evaluation.TrySubmit(7, "Reviewer feedback", DefenseApprovalStatus.Approved, _scoreReviewerId, _clock.UtcNow.AddDays(-2)));
            defense.ReviewerAssignments.Add(reviewer); defense.ReviewerEvaluations.Add(evaluation);
            var assignment = await defense.CouncilAssignments.SingleAsync(); _scoreAssignmentId = assignment.Id;
            var member = new CouncilMember { Id = Guid.NewGuid(), CouncilId = assignment.CouncilId, UserId = inside ? _scoreReviewerId : _scoreMemberId, RoleInCouncil = CouncilMemberRole.Member };
            defense.CouncilMembers.Add(member);
            var roster = await defense.CouncilMembers.ToListAsync(); roster.Add(member);
            _scoreCriteriaId = Guid.NewGuid();
            defense.GradingCriterias.Add(new GradingCriteria { Id = _scoreCriteriaId, BatchId = _batchId, CriteriaCode = "TOTAL", CriteriaName = "Score", MaxScore = 10, OrderIndex = 1 });
            foreach (var m in roster.Where(m => m.UserId != _scoreReviewerId))
            {
                var score = m.RoleInCouncil == CouncilMemberRole.Chairman ? 9m : m.RoleInCouncil == CouncilMemberRole.Secretary ? 8m : 6m;
                var s = new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = assignment.Id, CouncilMemberId = m.Id, Score = score, EvaluatedAt = _clock.UtcNow.AddDays(-1) };
                defense.CouncilScores.Add(s); defense.EvaluationDetails.Add(new EvaluationDetail { Id = Guid.NewGuid(), EvaluationId = s.Id, CriteriaId = _scoreCriteriaId, Score = score });
            }
            await defense.SaveChangesAsync();
        }
        await ReplaceScoreConfigurationAsync();
        if (seal)
        {
            using var submitted = await ArchiveAsync(); var current = await ArchivalDataAsync(submitted);
            using var advisor = await ConfirmArchivalAsync(current); var reviewed = await ArchivalDataAsync(advisor);
            using var faculty = await ConfirmArchivalAsync(reviewed, "FacultyConfirm", _adminId, "Admin"); await ArchivalDataAsync(faculty);
        }
    }
    private async Task ReplaceScoreConfigurationAsync(ScoreCalculationMethod method = ScoreCalculationMethod.IntegerCoefficient, IReadOnlyList<ScoreComponentDefinition>? components = null)
    {
        await using var db = Db<AcademicPeriodsDbContext>();
        var current = await db.ScoreWeightConfigs.SingleAsync(c => c.BatchId == _batchId && c.Status == ConfigurationVersionStatus.Active);
        current.Retire(_clock.UtcNow); await db.SaveChangesAsync();
        var definitions = components ?? new[]{
            new ScoreComponentDefinition("A","Advisor",ScoreComponentSource.Advisor,ScoreComponentApplicability.Always,2,true,null,false,1),
            new ScoreComponentDefinition("R","Reviewer",ScoreComponentSource.Reviewer,ScoreComponentApplicability.Always,2,true,null,false,2),
            new ScoreComponentDefinition("C","Chairman",ScoreComponentSource.CouncilChairman,ScoreComponentApplicability.Always,1,true,null,false,3),
            new ScoreComponentDefinition("S","Secretary",ScoreComponentSource.CouncilSecretary,ScoreComponentApplicability.Always,1,true,null,false,4),
            new ScoreComponentDefinition("M","Member",ScoreComponentSource.CouncilMember,ScoreComponentApplicability.ReviewerOutsideCouncil,1,true,null,false,5)};
        var next = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), _batchId, current.VersionNo + 1, method, definitions, _adminId, _clock.UtcNow, "Score workflow configuration");
        next.Publish(_adminId, _clock.UtcNow); db.ScoreWeightConfigs.Add(next); await db.SaveChangesAsync();
    }
    private async Task<HttpResponseMessage> CalculateAsync(uint? expected = null, Guid? key = null, Guid? actor = null, string role = "Admin", int? config = null, string? raw = null, string? suffix = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ScoresRoute}/calculate/{_groupId}{suffix}")
        { Content = raw is null ? JsonContent.Create(new { expectedSummaryVersion = expected, expectedActiveConfigVersion = config }) : new StringContent(raw, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actor ?? _adminId:N}.{role}");
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await _client.SendAsync(request);
    }
    [Theory]
    [InlineData(false, 7.57, 7)]
    [InlineData(true, 7.83, 6)]
    public async Task ScoreSummary_real_archival_to_dynamic_calculation_revisions_snapshot_and_receipt(bool inside, double score, int weight)
    {
        await PrepareScoreCalculationAsync(inside);
        var key = Guid.NewGuid();
        using var calculated = await CalculateAsync(key: key, actor: _scoreFacultyId, role: "Faculty");
        var first = await ArchivalDataAsync(calculated);
        Assert.Equal((decimal)score, first.GetProperty("finalScore").GetDecimal());
        Assert.Equal(weight, first.GetProperty("totalWeight").GetDecimal());
        Assert.False(first.GetProperty("isLegacy").GetBoolean()); Assert.False(first.GetProperty("isStale").GetBoolean());
        Assert.True(first.GetProperty("summaryVersion").GetUInt32() > 0);
        using var replay = await CalculateAsync(key: key, actor: _scoreFacultyId, role: "Faculty");
        Assert.Equal(await calculated.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var readiness = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/readiness/{_groupId}", _scoreAcademicId, "AcademicAffairs");
        Assert.True((await ArchivalDataAsync(readiness)).GetProperty("isReadyForPublication").GetBoolean());
        using var recalculated = await CalculateAsync(first.GetProperty("summaryVersion").GetUInt32());
        var second = await ArchivalDataAsync(recalculated); Assert.Equal(2, second.GetProperty("revisions").GetArrayLength());
        await using var db = Db<DefenseCouncilDbContext>();
        Assert.Equal(2, await db.ScoreSummaryRevisions.CountAsync()); Assert.Equal(2, await db.ScoreSummaryIdempotencyRecords.CountAsync());
        var revisions = await db.ScoreSummaryRevisions.OrderBy(r => r.RevisionNo).ToArrayAsync();
        Assert.Equal(revisions[0].InputFingerprint, revisions[1].InputFingerprint);
        Assert.Contains(_scoreReviewerId.ToString("N"), revisions[0].SourceSnapshot);
        using var raw = JsonDocument.Parse(revisions[0].SourceSnapshot); Assert.Equal(JsonValueKind.Array, raw.RootElement.ValueKind);
        await using var migration = MigrationDb(); Assert.False(migration.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task ScoreSummary_actual_council_average_keeps_raw_decimal_precision_in_database()
    {
        await PrepareScoreCalculationAsync();
        await ReplaceScoreConfigurationAsync(components: [
            new("A","Advisor",ScoreComponentSource.Advisor,ScoreComponentApplicability.Always,2,true,null,false,1),
            new("R","Reviewer",ScoreComponentSource.Reviewer,ScoreComponentApplicability.Always,2,true,null,false,2),
            new("AVG","Council average",ScoreComponentSource.CouncilAverage,ScoreComponentApplicability.Always,1,true,null,false,3)]);
        using var response = await CalculateAsync(); var result = await ArchivalDataAsync(response); Assert.Equal(7.53m, result.GetProperty("finalScore").GetDecimal());
        await using var db = Db<DefenseCouncilDbContext>(); var average = await db.ScoreSummaryComponents.SingleAsync(c => c.Source == "CouncilAverage");
        Assert.Equal(23m / 3m, average.Score); Assert.NotEqual(decimal.Round(average.Score, 4), average.Score);
        using var read = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/group/{_groupId}", _adminId, "Admin"); Assert.False((await ArchivalDataAsync(read)).GetProperty("isStale").GetBoolean());
    }
}
