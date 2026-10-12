namespace UGTM.Modules.Identity.Application.Notifications;

public interface IIdentityEmailOutbox
{
    void EnqueueActivationEmail(
        Guid userId,
        string email,
        string fullName,
        string rawToken,
        DateTime createdAt);

    void EnqueuePasswordRecoveryEmail(
        Guid userId,
        string email,
        string fullName,
        string code,
        DateTime createdAt);
}