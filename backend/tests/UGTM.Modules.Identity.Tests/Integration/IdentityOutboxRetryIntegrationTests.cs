using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Infrastructure.Notifications;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class IdentityOutboxRetryIntegrationTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now =
        new(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Failed_delivery_is_retried_after_backoff_and_clears_protected_payload()
    {
        var dataProtection = new EphemeralDataProtectionProvider();
        var clock = new MutableClock(Now);
        var sender = new FailOnceEmailSender();
        Guid messageId;

        await using (var db = CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
            await db.Database.EnsureCreatedAsync();

            var outbox = new IdentityEmailOutbox(db, dataProtection);
            outbox.EnqueueActivationEmail(
                Guid.NewGuid(),
                "student@ugtm.edu.vn",
                "Student Retry",
                "activation-token",
                clock.UtcNow);
            await db.SaveChangesAsync();
            messageId = await db.IdentityOutboxMessages.Select(value => value.Id).SingleAsync();
        }

        Assert.Equal(1, await DispatchAsync(sender, clock, dataProtection));

        await using (var db = CreateDbContext())
        {
            var failed = await db.IdentityOutboxMessages
                .AsNoTracking()
                .SingleAsync(value => value.Id == messageId);
            Assert.Null(failed.ProcessedAt);
            Assert.Equal(1, failed.AttemptCount);
            Assert.Equal(Now.AddMinutes(2), failed.NextAttemptAt);
            Assert.Equal(nameof(InvalidOperationException), failed.LastError);
            Assert.NotEmpty(failed.ProtectedPayload);
        }

        clock.UtcNow = Now.AddMinutes(1);
        Assert.Equal(0, await DispatchAsync(sender, clock, dataProtection));
        Assert.Equal(1, sender.AttemptCount);

        clock.UtcNow = Now.AddMinutes(2);
        Assert.Equal(1, await DispatchAsync(sender, clock, dataProtection));

        await using (var db = CreateDbContext())
        {
            var delivered = await db.IdentityOutboxMessages
                .AsNoTracking()
                .SingleAsync(value => value.Id == messageId);
            Assert.Equal(clock.UtcNow, delivered.ProcessedAt);
            Assert.Equal(1, delivered.AttemptCount);
            Assert.Null(delivered.LastError);
            Assert.Empty(delivered.ProtectedPayload);
        }

        Assert.Equal(2, sender.AttemptCount);
        var email = Assert.Single(sender.Delivered);
        Assert.Equal("student@ugtm.edu.vn", email.To);
        Assert.Contains("#token=activation-token", email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_dispatchers_skip_locked_messages_and_deliver_each_email_once()
    {
        var dataProtection = new EphemeralDataProtectionProvider();
        var clock = new MutableClock(Now);
        var sender = new BlockingEmailSender();

        await using (var db = CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
            await db.Database.EnsureCreatedAsync();

            var outbox = new IdentityEmailOutbox(db, dataProtection);
            outbox.EnqueueActivationEmail(
                Guid.NewGuid(), "first@ugtm.edu.vn", "First", "first-token", Now);
            outbox.EnqueueActivationEmail(
                Guid.NewGuid(), "second@ugtm.edu.vn", "Second", "second-token", Now);
            await db.SaveChangesAsync();
        }

        var firstDispatcher = DispatchAsync(sender, clock, dataProtection);
        await sender.FirstDeliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var secondDispatcher = DispatchAsync(sender, clock, dataProtection);
        Assert.Equal(0, await secondDispatcher.WaitAsync(TimeSpan.FromSeconds(10)));

        sender.AllowDelivery.SetResult();
        Assert.Equal(2, await firstDispatcher.WaitAsync(TimeSpan.FromSeconds(10)));

        await using var verificationDb = CreateDbContext();
        Assert.Equal(
            2,
            await verificationDb.IdentityOutboxMessages.CountAsync(value =>
                value.ProcessedAt != null && value.ProtectedPayload == string.Empty));
        Assert.Equal(
            ["first@ugtm.edu.vn", "second@ugtm.edu.vn"],
            sender.Delivered.Select(value => value.To).Order().ToArray());
    }

    private async Task<int> DispatchAsync(
        IEmailSender sender,
        IClock clock,
        IDataProtectionProvider dataProtection)
    {
        await using var db = CreateDbContext();
        var emailOptions = Options.Create(new IdentityEmailOptions
        {
            PublicWebBaseUrl = new Uri("https://ugtm.example")
        });
        var dispatcher = new IdentityOutboxDispatcher(
            db,
            sender,
            clock,
            dataProtection,
            new IdentityEmailTemplateRenderer(emailOptions),
            NullLogger<IdentityOutboxDispatcher>.Instance);
        return await dispatcher.ProcessBatchAsync(CancellationToken.None);
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new IdentityDbContext(options.Options);
    }

    private sealed class MutableClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; set; } = now;
    }

    private sealed class FailOnceEmailSender : IEmailSender
    {
        public int AttemptCount { get; private set; }
        public List<EmailMessage> Delivered { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            AttemptCount++;
            if (AttemptCount == 1)
            {
                throw new InvalidOperationException("Simulated SMTP failure.");
            }

            Delivered.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingEmailSender : IEmailSender
    {
        public TaskCompletionSource FirstDeliveryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowDelivery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<EmailMessage> Delivered { get; } = [];

        public async Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken)
        {
            if (Delivered.Count == 0)
            {
                FirstDeliveryStarted.TrySetResult();
                await AllowDelivery.Task.WaitAsync(cancellationToken);
            }

            Delivered.Add(message);
        }
    }
}
