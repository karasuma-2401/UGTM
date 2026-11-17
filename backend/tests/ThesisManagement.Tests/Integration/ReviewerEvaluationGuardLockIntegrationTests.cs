using System.Net;
using Microsoft.EntityFrameworkCore;
using UGTM.Database.Migrations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Theory]
    [InlineData("advisor")]
    [InlineData("batch")]
    [InlineData("group")]
    [InlineData("binding")]
    [InlineData("account")]
    [InlineData("role")]
    public async Task Evaluation_cp2_http_writer_keeps_all_owner_guards_until_evaluation_receipt_and_audit_commit(string change)
    {
        var parent = await CreateForCancellationAsync();
        await using var blocker = Db<UGTMDbContext>(); await using var gate = await blocker.Database.BeginTransactionAsync();
        var backend = await blocker.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(10502001)");
        // This test-only trigger pauses the real handler after all guards, at its first persistence boundary.
        await using (var setup = Db<UGTMDbContext>()) await setup.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION councils.hold_evaluation_write() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(10502001); RETURN NEW; END $$;
            CREATE TRIGGER hold_evaluation_write BEFORE INSERT ON councils.reviewer_evaluation
                FOR EACH ROW EXECUTE FUNCTION councils.hold_evaluation_write();
            """);
        var writing = SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: "Submit", score: 8, comment: "Góp ý", decision: "Approved"));
        var evaluator = await FindBlockedEvaluationBackendAsync(backend);
        await using var changing = Db<UGTMDbContext>(); await changing.Database.OpenConnectionAsync();
        var changingBackend = await changing.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var mutation = changing.Database.ExecuteSqlInterpolatedAsync(Change(change));
        try { await AssertBlockedByAsync(changingBackend, evaluator); }
        finally { await gate.RollbackAsync(); }
        using var saved = await writing.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(saved.StatusCode == HttpStatusCode.Created, await saved.Content.ReadAsStringAsync());
        await mutation.WaitAsync(TimeSpan.FromSeconds(30));
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp2_waiting_batch_guard_reloads_closed_status_instead_of_using_early_open_read()
    {
        var parent = await CreateForCancellationAsync();
        await using var closer = Db<UGTMDbContext>(); await using var transaction = await closer.Database.BeginTransactionAsync();
        var backend = await closer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await closer.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
        var writing = SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version));
        try { await FindBlockedEvaluationBackendAsync(backend); } finally { await transaction.CommitAsync(); }
        using var blocked = await writing.WaitAsync(TimeSpan.FromSeconds(30));
        await EvaluationErrorAsync(blocked, HttpStatusCode.Conflict, "BatchNotOpen");
        await AssertEvaluationCountsAsync(0, 0, 0);
    }
}
