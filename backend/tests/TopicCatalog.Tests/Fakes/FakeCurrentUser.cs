using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.TopicCatalog.Tests.Fakes;

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public Guid? SessionId { get; set; } = Guid.NewGuid();
    public string? Email { get; set; } = "advisor@example.com";
    public string? UserCode { get; set; } = "GV001";
    public IReadOnlySet<string> Roles { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lecturer" };
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool IsAuthenticated { get; set; } = true;

    public bool IsInRole(string role) =>
        Roles.Contains(role);

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission);

    public static FakeCurrentUser Unauthenticated() => new()
    {
        IsAuthenticated = false,
        UserId = null,
        SessionId = null,
        Email = null,
        UserCode = null,
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    };

    public static FakeCurrentUser AsLecturer(Guid? userId = null) => new()
    {
        IsAuthenticated = true,
        UserId = userId ?? Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Email = "lecturer@example.com",
        UserCode = "GV101",
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lecturer" }
    };

    public static FakeCurrentUser AsFaculty(Guid? userId = null) => new()
    {
        IsAuthenticated = true,
        UserId = userId ?? Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Email = "faculty@example.com",
        UserCode = "KHOA01",
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Faculty" }
    };

    public static FakeCurrentUser AsAdmin(Guid? userId = null) => new()
    {
        IsAuthenticated = true,
        UserId = userId ?? Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Email = "admin@example.com",
        UserCode = "AD001",
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin" }
    };

    public static FakeCurrentUser AsCouncilMember(Guid? userId = null) => new()
    {
        IsAuthenticated = true,
        UserId = userId ?? Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Email = "council@example.com",
        UserCode = "HD001",
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CouncilMember" }
    };

    public static FakeCurrentUser AsStudent(Guid? userId = null) => new()
    {
        IsAuthenticated = true,
        UserId = userId ?? Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Email = "student@example.com",
        UserCode = "SV001",
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Student" }
    };

    public static FakeCurrentUser AsRole(string role, Guid? userId = null) => new()
    {
        IsAuthenticated = true,
        UserId = userId ?? Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Email = $"{role.ToLowerInvariant()}@example.com",
        UserCode = "CODE01",
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role }
    };
}
