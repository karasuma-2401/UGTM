using Microsoft.Extensions.Logging;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Expiration;

namespace UGTM.Modules.ThesisManagement.Infrastructure.BackgroundServices;

public sealed class TopicRegistrationExpirationEnforcer(
    ITopicRegistrationStore store,
    IStudentInfoReader studentInfoReader,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<TopicRegistrationExpirationEnforcer> logger,
    IThesisEmailTemplateRenderer? emailTemplateRenderer = null) : ITopicRegistrationExpirationEnforcer
{
    private const int MaxViolationsBeforeBlock = 3;
    private readonly IThesisEmailTemplateRenderer emailTemplateRenderer =
        emailTemplateRenderer ?? new Notifications.ThesisEmailTemplateRenderer();

    public async Task<TopicRegistrationExpirationResult> EnforceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expiredMembers = await store.ExpirePendingRegistrationsAsync(
            nowUtc,
            MaxViolationsBeforeBlock,
            cancellationToken);

        if (expiredMembers.Count == 0)
        {
            return new TopicRegistrationExpirationResult(0, 0);
        }

        var studentIds = expiredMembers.Select(m => m.StudentId).Distinct().ToList();
        var students = await studentInfoReader.GetStudentsByIdsAsync(studentIds, cancellationToken);

        var blockedCount = 0;

        foreach (var expired in expiredMembers)
        {
            if (expired.IsNowBlocked)
            {
                blockedCount++;
            }

            students.TryGetValue(expired.StudentId, out var student);
            if (student is null || string.IsNullOrWhiteSpace(student.Email))
            {
                continue;
            }

            try
            {
                if (expired.IsNowBlocked)
                {
                    var message = emailTemplateRenderer.RenderTopicRegistrationBlocked(
                        student.Email,
                        student.FullName ?? string.Empty,
                        student.UserCode ?? string.Empty,
                        expired.NewViolationCount);

                    await emailSender.SendAsync(message, cancellationToken);
                }
                else
                {
                    var message = emailTemplateRenderer.RenderTopicRegistrationWarning(
                        student.Email,
                        student.FullName ?? string.Empty,
                        student.UserCode ?? string.Empty,
                        expired.NewViolationCount,
                        MaxViolationsBeforeBlock);

                    await emailSender.SendAsync(message, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send expiration email to student {StudentId}.", expired.StudentId);
            }
        }

        return new TopicRegistrationExpirationResult(expiredMembers.Count, blockedCount);
    }
}
