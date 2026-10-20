using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class UserAccountGroupTests
{
    [Fact]
    public void Groups_are_disjoint_and_preserve_staff_with_multiple_or_missing_roles()
    {
        var student = CreateUser("student", SystemRoleName.Student);
        var lecturer = CreateUser("lecturer", SystemRoleName.Lecturer, SystemRoleName.CouncilMember);
        var admin = CreateUser("admin", SystemRoleName.Admin);
        var roleless = CreateUser("roleless");
        var users = new[] { student, lecturer, admin, roleless }.AsQueryable();

        Assert.Equal([student.Id], users.FilterByAccountGroup("Student").Select(user => user.Id));
        Assert.Equal([lecturer.Id, admin.Id, roleless.Id], users.FilterByAccountGroup("Staff").Select(user => user.Id));
        Assert.Equal(4, users.FilterByAccountGroup(null).Count());
    }

    [Fact]
    public void Group_filter_precedes_counts_pagination_and_other_filters()
    {
        var users = Enumerable.Range(1, 50)
            .Select(index => CreateUser($"student{index}", SystemRoleName.Student))
            .Concat([CreateUser("lecturer", SystemRoleName.Lecturer)])
            .AsQueryable();
        var staff = users.FilterByAccountGroup("Staff");
        Assert.Equal(1, staff.Count());
        Assert.Single(staff.Take(20));
        Assert.Equal(50, users.FilterByAccountGroup("Student").Count());
        Assert.Empty(staff.Where(user => user.UserRoles.Any(role => role.Role.RoleName == SystemRoleName.Student)));
    }

    [Theory]
    [InlineData("Student", "EXISTS")]
    [InlineData("Staff", "NOT EXISTS")]
    public void Group_filter_translates_to_postgres_before_limit(string group, string expected)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var db = new IdentityDbContext(options);
        var sql = db.Users.FilterByAccountGroup(group)
            .OrderBy(user => user.CreatedAt).ThenBy(user => user.Id).Take(20).ToQueryString();
        Assert.Contains(expected, sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static User CreateUser(string code, params SystemRoleName[] roles)
    {
        var user = User.Create(Guid.NewGuid(), $"{code}@example.test", $"{code}@example.test".ToUpperInvariant(),
            code, code, null, null, AccountStatus.PendingActivation, DateTime.UtcNow);
        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, Role = new Role { RoleName = role } });
        }
        return user;
    }
}
