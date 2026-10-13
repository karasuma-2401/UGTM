using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class AspNetPasswordServiceTests
{
    [Fact]
    public void Verify_accepts_a_password_created_by_the_same_service()
    {
        var user = CreateUser();
        var service = new AspNetPasswordService();
        var hash = service.Hash(user, "Correct-password-123!");

        Assert.True(service.Verify(user, hash, "Correct-password-123!"));
        Assert.False(service.Verify(user, hash, "Wrong-password-123!"));
    }

    [Fact]
    public void Verify_rejects_empty_password_input()
    {
        var user = CreateUser();
        var service = new AspNetPasswordService();
        var hash = service.Hash(user, "Correct-password-123!");

        Assert.False(service.Verify(user, hash, string.Empty));
    }

    private static User CreateUser()
        => User.ProvisionStudent(
            Guid.NewGuid(),
            "student@ugtm.edu.vn",
            "STUDENT@UGTM.EDU.VN",
            "Student",
            "SV001",
            null,
            new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
}
