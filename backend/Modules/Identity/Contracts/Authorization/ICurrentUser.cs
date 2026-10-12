namespace UGTM.Modules.Identity.Contracts.Authorization;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid?  UserId { get; }
    Guid?  SessionId { get; }
    IReadOnlySet<string> Roles { get; }
    IReadOnlySet<string> Permissions { get; }
}