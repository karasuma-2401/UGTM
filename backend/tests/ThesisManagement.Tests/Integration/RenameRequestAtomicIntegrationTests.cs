using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Infrastructure.Auditing;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("conflict")]
    [InlineData("exception")]
    public async Task Rename_approval_commits_or_rolls_back_both_modules_and_audit(string outcome)
    {
        var requestId = await SeedRenameRequestAsync();
        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var thesis = services.GetRequiredService<ThesisManagementDbContext>();
        var actor = new Actor(_actor);
        IThesisUnitOfWork unitOfWork = outcome == "success" ? thesis : new FailingRenameUnitOfWork(outcome);
        var handler = new ReviewRenameRequestByAcademicAffairsHandler(
            services.GetRequiredService<IRenameRequestRepository>(),
            services.GetRequiredService<IStudentGroupRepository>(),
            services.GetRequiredService<ITopicRenameContract>(),
            new ThesisManagementAuditOutbox(thesis, actor, new HttpContextAccessor()),
            unitOfWork, actor, TimeProvider.System,
            services.GetRequiredService<IRenameRequestTransactionCoordinator>());

        if (outcome == "exception")
            await Assert.ThrowsAsync<IOException>(() => handler.HandleAsync(new(requestId, "Approve", null)));
        else
        {
            var result = await handler.HandleAsync(new(requestId, "Approve", null));
            if (outcome == "success") Assert.True(result.IsSuccess);
            else Assert.Equal(RenameRequestErrors.ConcurrencyConflict.Code, result.Error.Code);
        }

        await using var verifyThesis = Db<ThesisManagementDbContext>();
        await using var verifyTopics = Db<TopicCatalogDbContext>();
        var request = await verifyThesis.RenameRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
        var group = await verifyThesis.StudentGroups.AsNoTracking().SingleAsync(x => x.Id == _group);
        var topic = await verifyTopics.Topics.AsNoTracking().SingleAsync(x => x.Id == _topic);
        var auditCount = await verifyThesis.AuditOutboxMessages.CountAsync(x => x.Action == IntegrationAuditActions.RenameRequestApproved);
        Assert.Equal(outcome == "success" ? "Tên đề tài mới" : "Reviewer transaction", topic.TitleVi);
        Assert.Equal(outcome == "success" ? RenameRequestStatus.Completed : RenameRequestStatus.FacultyApproved, request.Status);
        Assert.Equal(outcome == "success", group.AllowSyllabusResubmission);
        Assert.Equal(outcome == "success" ? 1 : 0, auditCount);
    }

    [Fact]
    public async Task Rename_request_rejects_stale_reviews_and_multiple_active_requests()
    {
        var requestId = await SeedRenameRequestAsync();
        await using var first = Db<ThesisManagementDbContext>();
        await using var stale = Db<ThesisManagementDbContext>();
        var firstRequest = await first.RenameRequests.SingleAsync(x => x.Id == requestId);
        var staleRequest = await stale.RenameRequests.SingleAsync(x => x.Id == requestId);
        firstRequest.RejectByAcademicAffairs("Không duyệt", _actor, Now);
        staleRequest.ApproveByAcademicAffairs(Now);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<ThesisManagementConcurrencyException>(() => stale.SaveChangesAsync());

        await using var duplicate = Db<ThesisManagementDbContext>();
        var one = RenameRequest.Create(Guid.NewGuid(), _group, _student, "Reviewer transaction", "Mới 1", null, null, "Lý do", Now);
        var two = RenameRequest.Create(Guid.NewGuid(), _group, _student, "Reviewer transaction", "Mới 2", null, null, "Lý do", Now);
        duplicate.RenameRequests.AddRange(one, two);
        var error = await Assert.ThrowsAsync<ThesisManagementUniqueConstraintException>(() => duplicate.SaveChangesAsync());
        Assert.Equal("ux_rename_request_active_group", error.ConstraintName);
    }

    private async Task<Guid> SeedRenameRequestAsync()
    {
        await using var db = Db<ThesisManagementDbContext>();
        var request = RenameRequest.Create(Guid.NewGuid(), _group, _student, "Reviewer transaction", "Tên đề tài mới", null, null, "Lý do", Now);
        request.ApproveByAdvisor(Now);
        request.ApproveByFaculty(Now);
        db.RenameRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    private sealed class FailingRenameUnitOfWork(string outcome) : IThesisUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => outcome == "conflict"
                ? Task.FromException<int>(new ThesisManagementConcurrencyException(new InvalidOperationException("Stale request")))
                : Task.FromException<int>(new IOException("Connection lost after topic save"));
    }
}
