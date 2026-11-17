using System.Net;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    // The currently missing minute-lock producer is seeded by PrepareScoreCalculationAsync.
    // Archive v1/v2, both confirmations, calculation, publication and Student GET use HTTP.
    [Fact]
    public async Task ArchivalScore_end_to_end_versioned_archive_to_published_student_snapshot()
    {
        await PrepareScoreCalculationAsync(seal: false);
        using var unsubmitted = await CalculateAsync(); Assert.Equal(HttpStatusCode.Conflict, unsubmitted.StatusCode);
        using var firstUpload = await ArchiveAsync(); var first = await ArchivalDataAsync(firstUpload);
        Assert.Equal(1, first.GetProperty("currentVersionNo").GetInt32());
        var submissionId = first.GetProperty("submissionId").GetGuid();
        using var secondUpload = await ArchiveAsync(version: first.GetProperty("submissionVersion").GetUInt32(), student: _partnerId);
        var second = await ArchivalDataAsync(secondUpload);
        Assert.Equal(submissionId, second.GetProperty("submissionId").GetGuid()); Assert.Equal(2, second.GetProperty("currentVersionNo").GetInt32());
        using var oldApproval = await ConfirmArchivalAsync(first); Assert.Equal(HttpStatusCode.Conflict, oldApproval.StatusCode);
        using var advisor = await ConfirmArchivalAsync(second); var reviewed = await ArchivalDataAsync(advisor);
        using var notYetArchived = await CalculateAsync(); Assert.Equal(HttpStatusCode.Conflict, notYetArchived.StatusCode);
        using var faculty = await ConfirmArchivalAsync(reviewed, "FacultyConfirm", _adminId, "Admin"); var archived = await ArchivalDataAsync(faculty);
        Assert.Equal("ArchiveConfirmed", archived.GetProperty("status").GetString());
        using var hidden = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student"); Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        using var calculation = await CalculateAsync(actor: _scoreFacultyId, role: "Faculty"); var calculated = await ArchivalDataAsync(calculation);
        Assert.Equal(7.57m, calculated.GetProperty("finalScore").GetDecimal());
        using (var provenance = System.Text.Json.JsonDocument.Parse(calculated.GetProperty("sourceSnapshot").GetString()!))
            Assert.Equal(2, provenance.RootElement[12][3].GetInt32());
        var selection = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = calculated.GetProperty("summaryVersion").GetUInt32() } } };
        using var publication = await PublishScoresAsync(selection, actor: _scoreFacultyId, role: "Faculty");
        Assert.Equal(1, (await ArchivalDataAsync(publication)).GetProperty("publishedCount").GetInt32());
        using var result = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student"); var snapshot = await ArchivalDataAsync(result);
        Assert.Equal(7.57m, snapshot.GetProperty("finalScore").GetDecimal()); Assert.Equal("PublishedToStudent", snapshot.GetProperty("status").GetString());
        using var history = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{submissionId}/packages", _studentId, "Student");
        Assert.Equal(new[] { 2, 1 }, (await ArchivalDataAsync(history)).EnumerateArray().Select(p => p.GetProperty("versionNo").GetInt32()).ToArray());
        await using var thesis = Db<ThesisManagementDbContext>(); Assert.Equal(2, await thesis.FinalSubmissionPackages.CountAsync());
        await using var defense = Db<DefenseCouncilDbContext>(); var summary = await defense.ScoreSummarys.SingleAsync();
        Assert.Equal(ScoreSummaryStatus.PublishedToStudent, summary.Status); Assert.Equal(summary.ActiveRevisionId, summary.PublishedRevisionId);
    }
}
