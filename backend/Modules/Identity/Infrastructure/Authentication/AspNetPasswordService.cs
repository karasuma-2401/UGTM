using Microsoft.AspNetCore.Identity;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class AspNetPasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password)
    => _hasher.HashPassword(user, password);

    public bool Verify(User user, string passwordHash, string suppliedPassword)
        => _hasher.VerifyHashedPassword(user, passwordHash, suppliedPassword)
            is PasswordVerificationResult.SuccessRehashNeeded;
}