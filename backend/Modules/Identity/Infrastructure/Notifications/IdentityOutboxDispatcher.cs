using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.Identity.Infrastructure.Notifications;

public sealed class IdentityOutboxDispatcher(
    IdentityDbContext db,
    IEmailSender sender,
    IClock clock,
    IDataProtectionProvider dataProtection,
    IdentityEmailTemplateRenderer templates,
    ILogger<IdentityOutboxDispatcher> logger)
{
    private const int BatchSize = 20;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.IdentityOutboxMessages
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM identity.outbox_message
                WHERE processed_at IS NULL AND next_attempt_at <= {now}
                ORDER BY created_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var protector = dataProtection.CreateProtector(message.ProtectionPurpose);
                var json = protector.Unprotect(message.ProtectedPayload);
                var payload = JsonSerializer.Deserialize<IdentityEmailPayload>(json)
                    ?? throw new InvalidOperationException("Invalid outbox payload.");
                if (!string.Equals(message.Type, payload.Template, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Outbox message type does not match its payload.");
                }

                await sender.SendAsync(templates.Render(payload), cancellationToken);
                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox message {MessageId} failed.", message.Id);
                message.MarkFailed(ex.GetType().Name, clock.UtcNow);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

}
