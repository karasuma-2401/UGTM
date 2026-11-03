using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Fact]
    public async Task ScoreSummary_cancelled_request_after_first_save_rolls_back_revision_and_receipt()
    {
        await PrepareScoreCalculationAsync();
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION councils.cancel_score_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(710104); RETURN NEW; END $$;
            CREATE TRIGGER cancel_score_receipt BEFORE INSERT ON councils.score_summary_idempotency FOR EACH ROW EXECUTE FUNCTION councils.cancel_score_receipt();
            """);
        await using var blocker = MigrationDb(); await using var gate = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(710104)");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ScoresRoute}/calculate/{_groupId}")
        { Content = JsonContent.Create(new { expectedSummaryVersion = (uint?)null }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{_adminId:N}.Admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var cancellation = new CancellationTokenSource();
        var calculating = _client.SendAsync(request, cancellation.Token);
        try
        {
            await WaitForScoreSqlLockAsync("score_summary_idempotency");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => calculating);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var observer = MigrationDb();
            while (await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE '%score_summary_idempotency%') AS \"Value\"").SingleAsync(timeout.Token))
                await Task.Delay(30, timeout.Token);
        }
        finally { cancellation.Cancel(); await gate.CommitAsync(); }
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Empty(await verify.ScoreSummarys.ToListAsync()); Assert.Empty(await verify.ScoreSummaryRevisions.ToListAsync());
        Assert.Empty(await verify.ScoreSummaryComponents.ToListAsync()); Assert.Empty(await verify.ScoreSummaryIdempotencyRecords.ToListAsync());
        Assert.Empty(await verify.AuditOutboxMessages.Where(a => a.Action == "DefenseCouncil.ScoreSummary.Calculated").ToListAsync());
    }
}
