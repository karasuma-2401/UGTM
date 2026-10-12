using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class JwtSigningKeyProvider : IDisposable
{
    private readonly RSA _rsa;

    public JwtSigningKeyProvider(JwtOptions options)
    {
        _rsa = RSA.Create();
        _rsa.ImportFromPem(File.ReadAllText(options.PrivateKeyPath));
        Key = new RsaSecurityKey(_rsa) { KeyId = options.KeyId };
    }

    public RsaSecurityKey Key { get; }
    public SigningCredentials SigningCredentials => new SigningCredentials(Key, SecurityAlgorithms.RsaSha256);

    public void Dispose() => _rsa.Dispose();
}