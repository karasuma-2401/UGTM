namespace UGTM.Modules.Identity.Contracts.Users;

public sealed record NotificationRecipientContractDto(
    Guid UserId,
    string FullName,
    string Email);

public interface INotificationRecipientDirectory
{
    Task<IReadOnlyList<NotificationRecipientContractDto>> ResolveActiveRecipientsAsync(
        IReadOnlyCollection<Guid>? userIds = null,
        IReadOnlyCollection<string>? roleNames = null,
        IReadOnlyCollection<string>? permissionCodes = null,
        CancellationToken cancellationToken = default);
}
