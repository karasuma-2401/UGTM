namespace UGTM.Modules.Identity.Application.Provisioning;

public sealed record ProvisioningStudentAccountCommand (
    string Email,
    string FullName,
    string UserCode,
    string? PhoneNumber);