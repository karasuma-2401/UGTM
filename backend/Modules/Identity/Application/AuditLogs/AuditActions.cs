namespace UGTM.Modules.Identity.Application.AuditLogs;

public static class AuditActions
{
    public const string UserCreated = "Identity.User.Created";
    public const string UserUpdated = "Identity.User.Updated";
    public const string UserStatusChanged = "Identity.User.StatusChanged";
    public const string UserRolesChanged = "Identity.User.RolesChanged";
    public const string RolePermissionsChanged = "Identity.Role.PermissionsChanged";
}

public static class AuditOutcomes
{
    public const string Success = "Success";
    public const string Denied = "Denied";
    public const string Failed = "Failed";
}
