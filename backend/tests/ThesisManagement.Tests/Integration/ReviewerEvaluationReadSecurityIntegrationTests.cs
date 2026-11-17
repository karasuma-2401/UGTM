using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Fact]
    public async Task Evaluation_cp3_read_handlers_enforce_roles_and_validation_before_database_access_and_allow_real_student_branch_for_multiple_roles()
    {
        var accessor = _app.Services.GetRequiredService<IHttpContextAccessor>();
        foreach (var (role, authenticated, feedbackCode, gateCode) in new[] {
            ("Admin", false, "AuthenticationRequired", "AuthenticationRequired"),
            ("Admin", true, "AccessDenied", "InvalidRequest"),
            ("Faculty", true, "AccessDenied", "AccessDenied"),
            ("Student", true, "InvalidRequest", "AccessDenied"),
            ("Lecturer", true, "AccessDenied", "InvalidRequest") })
        {
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, _student.ToString()), new Claim(ClaimTypes.Role, role) }, authenticated ? "Cp3Test" : null))
            };
            try
            {
                await using var scope = _app.Services.CreateAsyncScope(); _readCommands.Count = 0;
                Assert.Equal("ReviewerEvaluation." + feedbackCode, (await scope.ServiceProvider.GetRequiredService<GetReviewerFeedbackHandler>().HandleAsync(Guid.Empty)).Error.Code);
                Assert.Equal("ReviewerEvaluation." + gateCode, (await scope.ServiceProvider.GetRequiredService<GetReviewerDefenseEligibilityHandler>().HandleAsync(Guid.Empty)).Error.Code);
                Assert.Equal(0, _readCommands.Count);
            }
            finally { accessor.HttpContext = null; }
        }
        await Cp3SubmittedAsync();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, _student.ToString()), new Claim(ClaimTypes.Role, "Student"), new Claim(ClaimTypes.Role, "Admin") }, "Cp3Test"))
        };
        try
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetReviewerFeedbackHandler>().HandleAsync(_group);
            Assert.True(result.IsSuccess); Assert.Equal("Góp ý công khai; KHÔNG kèm điểm", result.Value.Comment);
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task Evaluation_cp3_feedback_refuses_ambiguous_published_sources_instead_of_choosing_latest_when_database_is_corrupt()
    {
        var original = await Cp3SubmittedAsync();
        await using var raw = Db<UGTMDbContext>();
        await raw.Database.ExecuteSqlRawAsync("DROP INDEX councils.ux_reviewer_assignment_active_group");
        await using (var db = Db<DefenseCouncilDbContext>())
        {
            var other = ReviewerAssignment.Create(Guid.NewGuid(), _group, _otherReviewer, _advisor, original.AdvisorEvaluationId!.Value,
                original.FinalReportId!.Value, 2, _file, Checksum, _actor, Now);
            var evaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), other.Id, null, null, null, _actor, Now);
            Assert.True(evaluation.TrySubmit(9, "Không được tự chọn nguồn này", DefenseApprovalStatus.Approved, _actor, Now));
            db.ReviewerAssignments.Add(other); db.ReviewerEvaluations.Add(evaluation); await db.SaveChangesAsync();
        }
        using var response = await ReadAsync(FeedbackRoute(), _student, "Student");
        await EvaluationErrorAsync(response, HttpStatusCode.Conflict, "ContextInvalid");
        Assert.DoesNotContain("Không được tự chọn", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("score")]
    [InlineData("comment")]
    [InlineData("decision")]
    public async Task Evaluation_cp3_gate_fails_closed_when_submitted_content_is_corrupt_and_does_not_treat_missing_score_as_zero(string column)
    {
        await Cp3SubmittedAsync(0); await PublishFoundationRuleAsync(await Cp3RuleDraftAsync(0));
        await using var raw = Db<UGTMDbContext>();
        await raw.Database.ExecuteSqlRawAsync("ALTER TABLE councils.reviewer_evaluation DROP CONSTRAINT ck_reviewer_evaluation_workflow");
        await raw.Database.ExecuteSqlRawAsync(column switch
        {
            "score" => "UPDATE councils.reviewer_evaluation SET score=NULL",
            "comment" => "UPDATE councils.reviewer_evaluation SET comment=NULL",
            _ => "UPDATE councils.reviewer_evaluation SET defense_approval=NULL"
        });
        using var response = await ReadAsync(DefenseGateRoute()); await Cp3GateAsync(response, "ContextInvalid");
    }
}
