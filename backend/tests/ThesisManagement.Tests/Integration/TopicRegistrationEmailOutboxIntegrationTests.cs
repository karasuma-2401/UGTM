using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invitation_outbox_retries_without_penalty_and_starts_deadline_after_delivery(bool confirm)
    {
        var topicId = await SeedRegistrationTopicAsync();
        var clock = new RegistrationClock(Now);
        var sender = new RegistrationEmailSender { Fail = true };
        await using var provider = RegistrationProvider(clock, sender);
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        var invitation = new TopicRegistrationInvitation(token, "partner@example.test", "Partner", "SV02", "inviter@example.test", "Inviter");

        Guid groupId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ITopicRegistrationStore>()
                .RegisterTopicAsync(topicId, _actor, _otherReviewer, Now, TimeSpan.FromHours(24), hash, invitation);
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.ConfirmationExpiresAtUtc);
            groupId = result.Value.GroupId;
        }
        await using (var verify = Db<ThesisManagementDbContext>())
        {
            var queued = await verify.TopicRegistrationEmails.SingleAsync();
            Assert.DoesNotContain(token, queued.ProtectedPayload, StringComparison.Ordinal);
            Assert.Null((await verify.StudentGroupMembers.SingleAsync(m => m.StudentId == _otherReviewer)).ConfirmationExpiresAtUtc);
        }

        await DispatchRegistrationEmailAsync(provider);
        clock.Advance(TimeSpan.FromHours(25));
        await using (var scope = provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ITopicRegistrationStore>();
            Assert.Empty(await store.ExpirePendingRegistrationsAsync(clock.UtcNow, 3));
            var denied = await store.RegisterTopicAsync(topicId, _reviewer, null, clock.UtcNow,
                TimeSpan.FromHours(24), string.Empty, null);
            Assert.Equal(TopicRegistrationErrors.TopicAlreadyTaken.Code, denied.Error.Code);
        }
        sender.Fail = false;
        await DispatchRegistrationEmailAsync(provider);
        Assert.Single(sender.Messages);
        Assert.Contains(token, sender.Messages[0].TextBody, StringComparison.Ordinal);
        await using (var verify = Db<ThesisManagementDbContext>())
        {
            var member = await verify.StudentGroupMembers.SingleAsync(m => m.StudentId == _otherReviewer);
            Assert.Equal(clock.UtcNow.AddHours(24), member.ConfirmationExpiresAtUtc);
            var sent = await verify.TopicRegistrationEmails.SingleAsync(e => e.Type == "Invite");
            Assert.NotNull(sent.ProcessedAtUtc);
            Assert.Equal(1, sent.AttemptCount);
            Assert.Empty(sent.ProtectedPayload);
        }
        await DispatchRegistrationEmailAsync(provider);
        Assert.Equal(2, sender.Messages.Count);
        Assert.Equal("inviter@example.test", sender.Messages[1].To);

        await using (var scope = provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ITopicRegistrationStore>();
            if (confirm)
            {
                var result = await store.ConfirmRegistrationAsync(hash, _otherReviewer, clock.UtcNow);
                Assert.True(result.IsSuccess);
            }
            else
            {
                clock.Advance(TimeSpan.FromHours(25));
                var expired = await store.ExpirePendingRegistrationsAsync(clock.UtcNow, 3);
                Assert.Equal(_otherReviewer, Assert.Single(expired).StudentId);
            }
        }
        await using var final = Db<ThesisManagementDbContext>();
        var savedMember = await final.StudentGroupMembers.AsNoTracking().SingleOrDefaultAsync(m => m.GroupId == groupId && m.StudentId == _otherReviewer);
        Assert.Equal(confirm ? MemberConfirmationStatus.Confirmed : null, savedMember?.ConfirmationStatus);
        Assert.Equal(confirm ? 0 : 1, await final.StudentRegistrationPenalties.CountAsync(p => p.StudentId == _otherReviewer));
    }

    [Fact]
    public async Task Registration_rolls_back_when_invitation_cannot_be_enqueued()
    {
        var topicId = await SeedRegistrationTopicAsync();
        await using var db = Db<ThesisManagementDbContext>();
        var store = new TopicRegistrationStore(db, new OpenRegistrationBatchReader(), new RejectingInvitationOutbox());
        var invitation = new TopicRegistrationInvitation("token", "partner@example.test", "Partner", "SV02", null, "Inviter");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.RegisterTopicAsync(
            topicId, _actor, _otherReviewer, Now, TimeSpan.FromHours(24), "hash", invitation));

        await using var verify = Db<ThesisManagementDbContext>();
        Assert.False(await verify.StudentGroups.AnyAsync(group => group.TopicId == topicId));
        Assert.Empty(await verify.TopicRegistrationEmails.ToListAsync());
    }

    [Fact]
    public async Task Advisor_assignment_email_is_sent_from_outbox_for_current_member()
    {
        var clock = new RegistrationClock(Now);
        var sender = new RegistrationEmailSender();
        await using var provider = RegistrationProvider(clock, sender);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
            var member = await db.StudentGroupMembers.SingleAsync(m => m.StudentId == _student);
            scope.ServiceProvider.GetRequiredService<IAdvisorAssignmentEmailOutbox>()
                .Enqueue(member.Id, "student@example.test", "Student", "SV01", Now);
            await db.SaveChangesAsync();
        }

        await DispatchRegistrationEmailAsync(provider);
        Assert.Equal("student@example.test", Assert.Single(sender.Messages).To);
        await using var verify = Db<ThesisManagementDbContext>();
        var sent = await verify.TopicRegistrationEmails.SingleAsync(e => e.Type == "AdvisorAssigned");
        Assert.NotNull(sent.ProcessedAtUtc);
        Assert.Empty(sent.ProtectedPayload);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Registration_adds_new_members_to_an_existing_empty_group(bool withPartner)
    {
        var topicId = await SeedRegistrationTopicAsync();
        await using (var db = Db<ThesisManagementDbContext>())
        {
            db.StudentGroups.Add(new UGTM.Modules.ThesisManagement.Domain.Entities.StudentGroup(Guid.NewGuid(), topicId, Now));
            await db.SaveChangesAsync();
        }
        await using var provider = RegistrationProvider(new RegistrationClock(Now), new RegistrationEmailSender());
        await using (var scope = provider.CreateAsyncScope())
        {
            var invitation = withPartner
                ? new TopicRegistrationInvitation("token", "partner@example.test", "Partner", "SV02", null, "Inviter")
                : null;
            var result = await scope.ServiceProvider.GetRequiredService<ITopicRegistrationStore>()
                .RegisterTopicAsync(topicId, _actor, withPartner ? _otherReviewer : null, Now,
                    TimeSpan.FromHours(24), "hash", invitation);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        }
        await using var verify = Db<ThesisManagementDbContext>();
        var group = await verify.StudentGroups.Include(g => g.Members).SingleAsync(g => g.TopicId == topicId);
        Assert.Equal(withPartner ? 2 : 1, group.Members.Count);
        Assert.Equal(withPartner ? 1 : 0, await verify.TopicRegistrationEmails.CountAsync());
    }

    private async Task<Guid> SeedRegistrationTopicAsync()
    {
        await using var topics = Db<TopicCatalogDbContext>();
        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            BatchId = _batch,
            AdvisorId = _advisor,
            TitleVi = "Registration outbox topic",
            StudentNumber = 2,
            Status = TopicStatus.Published,
            CreatedAt = Now
        };
        topics.Topics.Add(topic);
        await topics.SaveChangesAsync();
        return topic.Id;
    }

    private ServiceProvider RegistrationProvider(RegistrationClock clock, RegistrationEmailSender sender)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = fixture.ConnectionString,
            ["ThesisManagement:Email:PublicWebBaseUrl"] = "https://workflow.test",
            ["ThesisManagement:Email:OutboxPartition"] = "outbox-test"
        }).Build();
        new ThesisManagementModule().RegisterModule(services, configuration);
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IEmailSender>(sender);
        services.AddSingleton<IKltnBatchReader, OpenRegistrationBatchReader>();
        return services.BuildServiceProvider();
    }

    private static async Task DispatchRegistrationEmailAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ITopicRegistrationEmailDispatcher>().ProcessOneAsync());
    }

    private sealed class RegistrationClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; private set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }

    private sealed class RegistrationEmailSender : IEmailSender
    {
        public bool Fail { get; set; }
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Fail) throw new IOException("SMTP unavailable");
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RejectingInvitationOutbox : ITopicRegistrationInvitationOutbox
    {
        public void Enqueue(Guid memberId, TopicRegistrationInvitation invitation, TimeSpan confirmationWindow, DateTime createdAtUtc)
            => throw new InvalidOperationException("Outbox unavailable");
    }

    private sealed class OpenRegistrationBatchReader : IKltnBatchReader
    {
        public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchRegistrationWindowContractDto?>(new(batchId, "Open", true, null));
        public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchTopicPreparationWindowContractDto?>(new(batchId, "Open", true, null));
        public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
