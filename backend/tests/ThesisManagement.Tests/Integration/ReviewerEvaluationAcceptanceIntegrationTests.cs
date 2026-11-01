using System.Net;
using Microsoft.EntityFrameworkCore;
using UGTM.Database.Migrations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Theory]
    [InlineData("Draft")]
    [InlineData("Archived")]
    public async Task Evaluation_cp4_non_open_batch_blocks_draft_edit_and_submit_but_preserves_private_read(string status)
    {
        var parent = await CreateForCancellationAsync();
        using var draft = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, comment: "Nháp trước khi đổi đợt"));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var original = await EvaluationDataAsync(draft);
        var version = original.GetProperty("evaluationVersion").GetUInt32();
        await using (var db = Db<UGTMDbContext>())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status={status} WHERE id={_batch}");

        foreach (var action in new[] { "SaveDraft", "Submit" })
        {
            using var blocked = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, version, action, 8, "Góp ý mới", "Approved"));
            await EvaluationErrorAsync(blocked, HttpStatusCode.Conflict, "BatchNotOpen");
        }

        using var read = await ReadEvaluationAsync(parent.Id);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var retained = await EvaluationDataAsync(read);
        Assert.Equal(version, retained.GetProperty("evaluationVersion").GetUInt32());
        Assert.Equal("Nháp trước khi đổi đợt", retained.GetProperty("comment").GetString());
        Assert.Equal("Draft", retained.GetProperty("status").GetString());
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData("ambiguous", "ConfigurationAmbiguous")]
    [InlineData("invalid", "ConfigurationInvalid")]
    public async Task Evaluation_cp4_invalid_active_configuration_does_not_block_submission_or_feedback_but_blocks_gate(string configuration, string reason)
    {
        var parent = await CreateForCancellationAsync();
        await PublishFoundationRuleAsync(await SeedFoundationRuleDraftAsync());
        await using (var db = Db<UGTMDbContext>())
        {
            if (configuration == "ambiguous")
                await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET minimum_score=6 WHERE component_code='R_OUT'");
            else
                await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET applicability='Unknown' WHERE component_code='R_OUT'");
        }

        using var submitted = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: "Submit", score: 8,
            comment: "Góp ý vẫn được phát hành", decision: "Approved"));
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var final = await EvaluationDataAsync(submitted);
        Assert.Equal("Submitted", final.GetProperty("status").GetString());
        Assert.True(final.GetProperty("isLocked").GetBoolean());
        using var gate = await ReadAsync(DefenseGateRoute());
        await Cp3GateAsync(gate, reason);
        using var feedback = await ReadAsync(FeedbackRoute(), _student, "Student");
        await Cp3FeedbackAsync(feedback, "Góp ý vẫn được phát hành");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }
}
