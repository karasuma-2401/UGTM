using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using UGTM.Modules.Notifications.Application.Notifications.Abstractions;

namespace UGTM.Modules.Notifications.Infrastructure.Security;

public sealed class DataProtectionDeviceTokenProtector(IDataProtectionProvider dataProtectionProvider)
    : IDeviceTokenProtector
{
    private const string Purpose = "UGTM.Notifications.DeviceToken.v1";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    public string Protect(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return _protector.Protect(rawToken);
    }

    public string Unprotect(string protectedToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedToken);
        return _protector.Unprotect(protectedToken);
    }

    public string ComputeHash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hashBytes);
    }
}
