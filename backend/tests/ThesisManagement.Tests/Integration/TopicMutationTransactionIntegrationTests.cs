using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;
using UGTM.Modules.TopicCatalog.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Fact]
    public async Task Creating_topic_commits_assignment_and_queues_email_in_same_transaction()
    {
        var sender = new RegistrationEmailSender { Fail = true };
        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var result = await CreateTopicWithStudentAsync(services, sender);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        await using var topics = Db<TopicCatalogDbContext>();
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.True(await topics.Topics.AnyAsync(t => t.Id == result.Value.Id));
        var group = await thesis.StudentGroups.Include(g => g.Members).SingleAsync(g => g.TopicId == result.Value.Id);
        Assert.Equal(_reviewer, Assert.Single(group.Members).StudentId);
        Assert.Single(await thesis.TopicRegistrationEmails.Where(e => e.Type == "AdvisorAssigned").ToListAsync());
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Creating_topic_rolls_back_when_assignment_email_cannot_be_enqueued()
    {
        await using var scope = _app.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTopicWithStudentAsync(
            scope.ServiceProvider, new RegistrationEmailSender(), new RejectingAdvisorOutbox()));
        await using var topics = Db<TopicCatalogDbContext>();
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.False(await topics.Topics.AnyAsync(t => t.TitleVi == "Atomic assignment topic"));
        Assert.False(await thesis.StudentGroupMembers.AnyAsync(m => m.StudentId == _reviewer));
        Assert.Empty(await thesis.TopicRegistrationEmails.ToListAsync());
    }

    [Fact]
    public async Task Updating_topic_rolls_back_assignment_and_email_when_topic_save_fails()
    {
        Guid topicId;
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var result = await CreateTopicWithStudentAsync(scope.ServiceProvider, new RegistrationEmailSender());
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
            topicId = result.Value.Id;
        }

        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new UpdateTopicHandler(services.GetRequiredService<ITopicRepository>(),
                new FailingTopicUnitOfWork(), new Actor(_actor), CreateAssignmentContract(services, new RegistrationEmailSender()),
                services.GetRequiredService<ITopicMutationTransactionCoordinator>());
            await Assert.ThrowsAsync<IOException>(() => handler.HandleAsync(new UpdateTopicCommand(
                topicId, "Updated title", null, null, null, 2, StudentIds: [_otherReviewer])));
        }

        await using var topics = Db<TopicCatalogDbContext>();
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.Equal("Atomic assignment topic", (await topics.Topics.SingleAsync(t => t.Id == topicId)).TitleVi);
        var group = await thesis.StudentGroups.Include(g => g.Members).SingleAsync(g => g.TopicId == topicId);
        Assert.Equal(_reviewer, Assert.Single(group.Members).StudentId);
        Assert.Single(await thesis.TopicRegistrationEmails.Where(e => e.Type == "AdvisorAssigned").ToListAsync());
    }

    private Task<UGTM.BuildingBlocks.Domain.Result<UGTM.Modules.TopicCatalog.Application.Topics.Dtos.TopicSummaryDto>>
        CreateTopicWithStudentAsync(IServiceProvider services, IEmailSender sender, IAdvisorAssignmentEmailOutbox? outbox = null)
    {
        var handler = new CreateTopicHandler(services.GetRequiredService<ITopicRepository>(),
            services.GetRequiredService<ITopicUnitOfWork>(), new OpenRegistrationBatchReader(), new Actor(_actor),
            CreateAssignmentContract(services, sender, outbox), services.GetRequiredService<ITopicMutationTransactionCoordinator>());
        return handler.HandleAsync(new CreateTopicCommand(_batch, "Atomic assignment topic", null, null, null, 2,
            AdvisorId: _advisor, StudentIds: [_reviewer]));
    }

    private static ThesisRegistrationContract CreateAssignmentContract(IServiceProvider services, IEmailSender sender,
        IAdvisorAssignmentEmailOutbox? outbox = null)
    {
        var db = services.GetRequiredService<ThesisManagementDbContext>();
        var store = new TopicRegistrationStore(db, new OpenRegistrationBatchReader(),
            services.GetRequiredService<ITopicRegistrationInvitationOutbox>());
        return new ThesisRegistrationContract(db, store, new AssignmentStudentReader(), sender, TimeProvider.System,
            advisorAssignmentEmailOutbox: outbox ?? services.GetRequiredService<IAdvisorAssignmentEmailOutbox>());
    }

    private sealed class AssignmentStudentReader : IStudentInfoReader
    {
        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(Guid? studentId, string? userCode, CancellationToken cancellationToken = default)
            => Task.FromResult(studentId.HasValue ? new StudentInfoContractDto(studentId.Value, "student@example.test", "Student", "SV01", null) : null);
        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RejectingAdvisorOutbox : IAdvisorAssignmentEmailOutbox
    {
        public void Enqueue(Guid memberId, string email, string fullName, string userCode, DateTime createdAtUtc)
            => throw new InvalidOperationException("Outbox unavailable");
    }

    private sealed class FailingTopicUnitOfWork : ITopicUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new IOException("Simulated failure");
    }
}
