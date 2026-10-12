using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using UGTM.Modules.Identity.Application.Security;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class OpaqueTokenService : IOpaqueTokenService
{
    public OpaqueToken CreateToken()
    {
        var selector = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        var validator = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new OpaqueToken(
            $"{selector}.{validator}",
            selector,
            Hash(validator));
    }

    public NumericCode CreateNumericCode(int digits)
    {
        if (digits is < 4 or > 9)
            throw new ArgumentOutOfRangeException(nameof(digits));

        var upperBound = (int)Math.Pow(10, digits);
        var code = RandomNumberGenerator.GetInt32(upperBound)
            .ToString($"D{digits}");
        var selector = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        return new NumericCode(selector, code, Hash(code));
    }

    public bool TryParse(string rawToken, out string selector, out string validator)
    {
        selector = string.Empty;
        validator = string.Empty;
        if (string.IsNullOrWhiteSpace(rawToken)) return false;

        var separator = rawToken.IndexOf('.');
        if (separator <= 0 || separator == rawToken.Length - 1) return false;
        if (rawToken.IndexOf('.', separator + 1) >= 0) return false;

        selector = rawToken[..separator];
        validator = rawToken[(separator + 1)..];
        return selector.Length <= 64 && validator.Length <= 128;
    }

    public bool Verify(string suppliedSecret, byte[] expectedHash)
    {
        if (string.IsNullOrEmpty(suppliedSecret) || expectedHash.Length == 0)
        {
            return false;
        }

        var actualHash = Hash(suppliedSecret);
        return actualHash.Length == expectedHash.Length
               && CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Hash(string value)
        => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
