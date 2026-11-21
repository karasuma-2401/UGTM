using System.Security.Cryptography;
using UGTM.Modules.Identity.Application.Security;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class CryptoTemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const int PasswordLength = 16;
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Specials = "!@#$%^&*-_=+";
    private const string AllCharacters = Uppercase + Lowercase + Digits + Specials;

    public string Generate()
    {
        Span<char> buffer = stackalloc char[PasswordLength];
        var index = 0;

        buffer[index++] = NextChar(Uppercase);
        buffer[index++] = NextChar(Uppercase);
        buffer[index++] = NextChar(Lowercase);
        buffer[index++] = NextChar(Lowercase);
        buffer[index++] = NextChar(Digits);
        buffer[index++] = NextChar(Digits);
        buffer[index++] = NextChar(Specials);
        buffer[index++] = NextChar(Specials);

        while (index < PasswordLength)
        {
            buffer[index++] = NextChar(AllCharacters);
        }

        // Fisher-Yates shuffle with CSPRNG to remove positional predictability.
        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }

    private static char NextChar(string alphabet)
        => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
