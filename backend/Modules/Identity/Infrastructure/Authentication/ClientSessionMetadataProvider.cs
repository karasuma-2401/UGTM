using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed record ClientSessionMetadata(
    string? UserAgentSummary,
    string? IpAddressHash);

public sealed class ClientSessionMetadataProvider(IdentityClientMetadataOptions options)
{
    private readonly byte[] _ipHashKey = Encoding.UTF8.GetBytes(options.IpHashKey);

    public ClientSessionMetadata Capture(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new ClientSessionMetadata(
            NormalizeUserAgent(
                context.Request.Headers.UserAgent.ToString(),
                options.UserAgentMaxLength),
            HashIpAddress(context.Connection.RemoteIpAddress));
    }

    internal static string? NormalizeUserAgent(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength <= 0)
        {
            return null;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        var pendingSpace = false;
        foreach (var character in value)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace && builder.Length < maxLength)
            {
                builder.Append(' ');
            }

            pendingSpace = false;
            if (builder.Length >= maxLength)
            {
                break;
            }

            builder.Append(character);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    internal string? HashIpAddress(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        var canonicalAddress = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
        var digest = HMACSHA256.HashData(
            _ipHashKey,
            Encoding.UTF8.GetBytes(canonicalAddress));
        return Convert.ToHexStringLower(digest);
    }
}
