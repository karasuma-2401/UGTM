using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Security;

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string passwordHash, string suppliedPassword);
}