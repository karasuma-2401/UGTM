using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Notifications;

internal sealed class TopicRegistrationEmailDispatcher(
    ThesisManagementDbContext db,
    TopicRegistrationEmailOutbox outbox,
    IDataProtectionProvider dataProtection,
    IOptions<ThesisEmailOptions> options,
    IThesisEmailTemplateRenderer templates,
    IEmailSender sender,
    TimeProvider clock,
    ILogger<TopicRegistrationEmailDispatcher> logger) : ITopicRegistrationEmailDispatcher
{
    public async Task<bool> ProcessOneAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var purpose = TopicRegistrationEmailOutbox.PurposeFor(options.Value.OutboxPartition);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.TopicRegistrationEmails.FromSqlInterpolated($"""
            SELECT e.* FROM thesis.topic_registration_email e
            WHERE e.protection_purpose = {purpose} AND e.processed_at_utc IS NULL
              AND e.next_attempt_at_utc <= {now}
            ORDER BY e.created_at_utc, e.id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);
        if (message is null) return false;

        try
        {
            var json = dataProtection.CreateProtector(purpose).Unprotect(message.ProtectedPayload);
            var payload = JsonSerializer.Deserialize<TopicRegistrationEmailPayload>(json)
                ?? throw new InvalidOperationException("Invalid registration email payload.");
            if (message.Type == TopicRegistrationEmailOutbox.InviteType)
            {
                var member = await db.StudentGroupMembers.FromSqlInterpolated($"""
                    SELECT m.* FROM thesis.student_group_member m WHERE m.id = {message.MemberId} FOR UPDATE
                    """).SingleOrDefaultAsync(cancellationToken);
                if (member is not null && member.ConfirmationStatus == MemberConfirmationStatus.Pending)
                {
                    if (string.IsNullOrWhiteSpace(payload.RawToken) || payload.WindowSeconds <= 0)
                        throw new InvalidOperationException("Invalid registration invitation payload.");
                    var deadline = clock.GetUtcNow().UtcDateTime.AddSeconds(payload.WindowSeconds);
                    await sender.SendAsync(templates.RenderTopicRegistrationInvite(
                        payload.To, payload.RecipientName, payload.InviterName, payload.RawToken, deadline), cancellationToken);
                    member.SetConfirmationDeadline(deadline);
                    if (!string.IsNullOrWhiteSpace(payload.InviterEmail))
                    {
                        outbox.Add(member.Id, TopicRegistrationEmailOutbox.InviterType,
                            new TopicRegistrationEmailPayload(payload.InviterEmail, payload.InviterName,
                                payload.PartnerName, payload.PartnerUserCode, null, null, payload.InviterName,
                                payload.WindowSeconds, deadline), clock.GetUtcNow().UtcDateTime);
                    }
                }
            }
            else if (message.Type == TopicRegistrationEmailOutbox.InviterType && payload.DeadlineUtc.HasValue)
            {
                await sender.SendAsync(templates.RenderTopicRegistrationConfirmationForInviter(
                    payload.To, payload.RecipientName, payload.PartnerName, payload.PartnerUserCode,
                    payload.DeadlineUtc.Value), cancellationToken);
            }
            else if (message.Type == TopicRegistrationEmailOutbox.AdvisorAssignedType)
            {
                if (await db.StudentGroupMembers.AsNoTracking().AnyAsync(
                    member => member.Id == message.MemberId && member.ConfirmationStatus == MemberConfirmationStatus.Confirmed,
                    cancellationToken))
                {
                    await sender.SendAsync(templates.RenderAdvisorAssignedNotification(
                        payload.To, payload.RecipientName, payload.PartnerUserCode), cancellationToken);
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported registration email type.");
            }

            message.ProcessedAtUtc = clock.GetUtcNow().UtcDateTime;
            message.ProtectedPayload = string.Empty;
            message.LastErrorCode = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            message.AttemptCount++;
            message.LastErrorCode = error.GetType().Name;
            message.NextAttemptAtUtc = clock.GetUtcNow().UtcDateTime.AddSeconds(
                Math.Min(3600, Math.Pow(2, Math.Min(message.AttemptCount, 11))));
            logger.LogWarning("Topic registration email {MessageId} failed with {ErrorCode}; retry scheduled.",
                message.Id, message.LastErrorCode);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}

internal sealed class TopicRegistrationEmailWorker(
    IServiceScopeFactory scopes,
    ILogger<TopicRegistrationEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ITopicRegistrationEmailDispatcher>().ProcessOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Topic registration email dispatch failed."); }
        }
    }
}
