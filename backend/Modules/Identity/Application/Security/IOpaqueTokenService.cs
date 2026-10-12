namespace UGTM.Modules.Identity.Application.Security;

public sealed record OpaqueToken(
    string RawToken,
    string Selector,
    byte[] SecretHash);

public sealed record NumericCode(
    string Selector,
    string Code,
    byte[] SecretHash);

public interface IOpaqueTokenService
{
    OpaqueToken CreateToken();
    NumericCode CreateNumericCode(int digits);
    bool TryParse(string rawToken, out string selector, out string validator);
    bool Verify(string suppliedSecret, byte[] expectedHash);
}