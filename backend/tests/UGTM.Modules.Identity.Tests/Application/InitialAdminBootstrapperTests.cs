using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Bootstrap;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure.Bootstrap;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class InitialAdminBootstrapperTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BootstrapAdminAsync_succeeds_when_valid_credentials_and_no_admin_exists()
    {
        var repo = new FakeUserRepository();
        var auditLogs = new FakeAuditLogWriter();
        var bootstrapper = CreateBootstrapper(repo, auditLogs: auditLogs);

        var result = await bootstrapper.BootstrapAdminAsync(
            "admin@ugtm.edu.vn",
            "StrongAdmin123!",
            "System Admin",
            "ADMIN01",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        var user = repo.Users.Single(u => u.Id == result.Value);
        Assert.Equal("admin@ugtm.edu.vn", user.Email);
        Assert.Equal("ADMIN@UGTM.EDU.VN", user.NormalizedEmail);
        Assert.Equal("System Admin", user.FullName);
        Assert.Equal("ADMIN01", user.UserCode);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal("hashed-StrongAdmin123!", user.PasswordHash);
        Assert.Contains(user.UserRoles, ur => ur.Role.RoleName == SystemRoleName.Admin);
        Assert.Equal(1, repo.AdminMutationLockCount);

        var auditEntry = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.UserCreated, auditEntry.Action);
        Assert.Equal(user.Id.ToString("D"), auditEntry.TargetId);
    }

    [Fact]
    public async Task BootstrapAdminAsync_fails_when_admin_already_exists()
    {
        var existingAdmin = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([existingAdmin]);
        var bootstrapper = CreateBootstrapper(repo);

        var result = await bootstrapper.BootstrapAdminAsync(
            "newadmin@ugtm.edu.vn",
            "StrongAdmin123!",
            "Another Admin",
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.AdminAlreadyExists", result.Error.Code);
        Assert.Single(repo.Users);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("admin@")]
    public async Task BootstrapAdminAsync_fails_when_email_is_invalid(string invalidEmail)
    {
        var repo = new FakeUserRepository();
        var bootstrapper = CreateBootstrapper(repo);

        var result = await bootstrapper.BootstrapAdminAsync(
            invalidEmail,
            "StrongAdmin123!",
            "Admin",
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidInput", result.Error.Code);
        Assert.Empty(repo.Users);
    }

    [Fact]
    public async Task BootstrapAdminAsync_fails_when_email_already_registered()
    {
        var existingUser = CreateUserWithRole(SystemRoleName.Lecturer);
        var repo = new FakeUserRepository([existingUser]);
        var bootstrapper = CreateBootstrapper(repo);

        var result = await bootstrapper.BootstrapAdminAsync(
            existingUser.Email,
            "StrongAdmin123!",
            "Admin",
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.DuplicateAccount", result.Error.Code);
    }

    [Fact]
    public async Task BootstrapAdminAsync_fails_when_usercode_already_exists()
    {
        var existingUser = CreateUserWithRole(SystemRoleName.Lecturer);
        var repo = new FakeUserRepository([existingUser]);
        var bootstrapper = CreateBootstrapper(repo);

        var result = await bootstrapper.BootstrapAdminAsync(
            "admin@ugtm.edu.vn",
            "StrongAdmin123!",
            "Admin",
            existingUser.UserCode,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.DuplicateAccount", result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    public async Task BootstrapAdminAsync_fails_when_password_is_weak_or_empty(string weakPassword)
    {
        var repo = new FakeUserRepository();
        var bootstrapper = CreateBootstrapper(repo);

        var result = await bootstrapper.BootstrapAdminAsync(
            "admin@ugtm.edu.vn",
            weakPassword,
            "Admin",
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.WeakPassword", result.Error.Code);
        Assert.Empty(repo.Users);
    }

    [Fact]
    public async Task BootstrapAdminAsync_uses_default_fullname_when_fullname_is_null_or_empty()
    {
        var repo = new FakeUserRepository();
        var bootstrapper = CreateBootstrapper(repo);

        var result = await bootstrapper.BootstrapAdminAsync(
            "admin@ugtm.edu.vn",
            "StrongAdmin123!",
            null,
            null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = repo.Users.Single(u => u.Id == result.Value);
        Assert.Equal("System Administrator", user.FullName);
    }

    [Fact]
    public async Task BootstrapAdminAsync_fails_when_fullname_or_usercode_exceeds_max_length()
    {
        var repo = new FakeUserRepository();
        var bootstrapper = CreateBootstrapper(repo);

        var tooLongNameResult = await bootstrapper.BootstrapAdminAsync(
            "admin@ugtm.edu.vn",
            "StrongAdmin123!",
            new string('A', 151),
            "ADM01",
            CancellationToken.None);

        Assert.False(tooLongNameResult.IsSuccess);
        Assert.Equal("Identity.InvalidInput", tooLongNameResult.Error.Code);

        var tooLongCodeResult = await bootstrapper.BootstrapAdminAsync(
            "admin@ugtm.edu.vn",
            "StrongAdmin123!",
            "System Admin",
            new string('C', 21),
            CancellationToken.None);

        Assert.False(tooLongCodeResult.IsSuccess);
        Assert.Equal("Identity.InvalidInput", tooLongCodeResult.Error.Code);
        Assert.Empty(repo.Users);
    }

    [Fact]
    public async Task InitialAdminBootstrapHostedService_bootstraps_when_env_vars_configured()
    {
        var repo = new FakeUserRepository();
        var auditLogs = new FakeAuditLogWriter();
        var bootstrapper = CreateBootstrapper(repo, auditLogs: auditLogs);

        var services = new ServiceCollection();
        services.AddSingleton<IInitialAdminBootstrapper>(bootstrapper);
        var serviceProvider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "envadmin@ugtm.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "StrongAdminPass123!",
                ["BOOTSTRAP_ADMIN_FULLNAME"] = "Root Admin"
            })
            .Build();

        var hostedService = new InitialAdminBootstrapHostedService(
            serviceProvider,
            config,
            NullLogger<InitialAdminBootstrapHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        Assert.Single(repo.Users);
        var admin = repo.Users.First();
        Assert.Equal("envadmin@ugtm.edu.vn", admin.Email);
        Assert.Equal("Root Admin", admin.FullName);
        Assert.Null(config["BOOTSTRAP_ADMIN_PASSWORD"]);
    }

    [Fact]
    public async Task InitialAdminBootstrapHostedService_skips_when_credentials_not_provided()
    {
        var repo = new FakeUserRepository();
        var bootstrapper = CreateBootstrapper(repo);

        var services = new ServiceCollection();
        services.AddSingleton<IInitialAdminBootstrapper>(bootstrapper);
        var serviceProvider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var hostedService = new InitialAdminBootstrapHostedService(
            serviceProvider,
            config,
            NullLogger<InitialAdminBootstrapHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        Assert.Empty(repo.Users);
    }

    [Fact]
    public async Task InitialAdminBootstrapHostedService_does_not_throw_when_admin_already_exists()
    {
        var existingAdmin = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([existingAdmin]);
        var bootstrapper = CreateBootstrapper(repo);

        var services = new ServiceCollection();
        services.AddSingleton<IInitialAdminBootstrapper>(bootstrapper);
        var serviceProvider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "anotheradmin@ugtm.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "StrongAdminPass123!"
            })
            .Build();

        var hostedService = new InitialAdminBootstrapHostedService(
            serviceProvider,
            config,
            NullLogger<InitialAdminBootstrapHostedService>.Instance);

        var exception = await Record.ExceptionAsync(() => hostedService.StartAsync(CancellationToken.None));
        Assert.Null(exception);
        Assert.Single(repo.Users);
    }

    private static InitialAdminBootstrapper CreateBootstrapper(
        FakeUserRepository repo,
        FakeAuditLogWriter? auditLogs = null)
    {
        return new InitialAdminBootstrapper(
            repo,
            new DirectUnitOfWork(),
            new FakePasswordPolicy(),
            new FakePasswordService(),
            new FakeClock(Now),
            auditLogs ?? new FakeAuditLogWriter(),
            NullLogger<InitialAdminBootstrapper>.Instance);
    }

    private static User CreateUserWithRole(SystemRoleName role, AccountStatus status = AccountStatus.Active)
    {
        var user = User.Create(
            Guid.NewGuid(),
            $"{role.ToString().ToLowerInvariant()}@ugtm.edu.vn",
            $"{role.ToString().ToUpperInvariant()}@UGTM.EDU.VN",
            role.ToString(),
            role.ToString().ToUpperInvariant(),
            null,
            "hash",
            status,
            Now);
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = (int)role + 1,
            Role = new Role { Id = (int)role + 1, RoleName = role },
            AssignedAt = Now,
            User = user
        });
        return user;
    }

    private sealed class FakeUserRepository(IEnumerable<User>? initialUsers = null) : IUserRepository
    {
        public List<User> Users { get; } = [.. initialUsers ?? []];
        public int AdminMutationLockCount { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.UserCode == userCode));

        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail || u.UserCode == normalizedUserCode));

        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail || u.UserCode == userCode));

        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.UserCode == userCode && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(Users
                .Where(user => normalizedEmails.Contains(user.NormalizedEmail))
                .Select(user => user.NormalizedEmail)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(Users
                .Where(user => user.UserCode is not null && userCodes.Contains(user.UserCode))
                .Select(user => user.UserCode!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        public Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = (int)role + 1,
                Role = new Role { Id = (int)role + 1, RoleName = role },
                AssignedAt = assignedAt,
                User = user
            });
            return Task.CompletedTask;
        }

        public async Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
        {
            foreach (var user in users)
            {
                Users.Add(user);
                await AddToRoleAsync(user, role, assignedAt, cancellationToken);
            }
        }

        public Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken)
        {
            user.UserRoles.Clear();
            foreach (var role in roles)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = (int)role + 1,
                    Role = new Role { Id = (int)role + 1, RoleName = role },
                    AssignedAt = assignedAt,
                    User = user
                });
            }
            return Task.CompletedTask;
        }

        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken)
        {
            var count = Users
                .Where(u => u.Status == AccountStatus.Active && u.UserRoles.Any(ur => ur.Role.RoleName == SystemRoleName.Admin))
                .Where(u => !excludeUserId.HasValue || u.Id != excludeUserId.Value)
                .Count();
            return Task.FromResult(count);
        }

        public Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken)
        {
            AdminMutationLockCount++;
            return Task.CompletedTask;
        }

        public Task AcquireAccountProvisioningLockAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken)
        {
            var exists = Users.Any(u => u.UserRoles.Any(ur => ur.Role.RoleName == SystemRoleName.Admin));
            return Task.FromResult(exists);
        }

        public void Add(User user) => Users.Add(user);
    }

    private sealed class FakePasswordPolicy : IPasswordPolicy
    {
        public PasswordPolicyResult Validate(string password) => new(password.Length >= 6, []);
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public string Hash(User user, string password) => "hashed-" + password;
        public bool Verify(User user, string passwordHash, string suppliedPassword) => passwordHash == "hashed-" + suppliedPassword;
    }

    private sealed class DirectUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Add(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
