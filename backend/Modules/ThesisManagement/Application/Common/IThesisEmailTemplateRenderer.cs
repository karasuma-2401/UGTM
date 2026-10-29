using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public interface IThesisEmailTemplateRenderer
{
    EmailMessage RenderTopicRegistrationInvite(
        string to,
        string partnerFullName,
        string studentAName,
        string rawToken,
        DateTime expiresAtUtc);

    EmailMessage RenderTopicRegistrationConfirmationForInviter(
        string to,
        string studentAFullName,
        string partnerFullName,
        string partnerUserCode,
        DateTime expiresAtUtc);

    EmailMessage RenderAdvisorAssignedNotification(
        string to,
        string studentFullName,
        string studentUserCode);

    EmailMessage RenderTopicRegistrationWarning(
        string to,
        string studentFullName,
        string studentUserCode,
        int violationCount,
        int maxViolations);

    EmailMessage RenderTopicRegistrationBlocked(
        string to,
        string studentFullName,
        string studentUserCode,
        int violationCount);
}
