namespace UGTM.Modules.Notifications.Contracts.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}