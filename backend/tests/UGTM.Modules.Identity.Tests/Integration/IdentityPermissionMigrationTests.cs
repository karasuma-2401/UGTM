using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class IdentityPermissionMigrationTests(PostgreSqlFixture fixture)
{
    private static readonly string[] RequiredPermissionCodes =
    [
        PermissionCodes.IdentityUsersRead,
        PermissionCodes.IdentityUsersManage,
        PermissionCodes.IdentityUsersImport,
        PermissionCodes.IdentityPermissionsRead,
        PermissionCodes.IdentityRolesManage,
        PermissionCodes.IdentitySessionsRevoke
    ];

    [Fact]
    public async Task Permission_migrations_preserve_existing_data_and_apply_the_required_role_matrix()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            var migrator = migrationDb.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20261017074158_AddCrossModuleAuditOutbox");

            await migrationDb.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO identity.permission (id, module, permission_code, permission_name)
                VALUES
                    (1, 'Legacy', 'legacy.permission', 'Legacy permission'),
                    (20, 'Legacy', 'identity.users.read', 'Old display name');

                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                SELECT id, 1, TIMESTAMPTZ '2025-01-01 00:00:00Z'
                FROM identity.role
                WHERE role_name = 'Faculty';
                """);

            await migrator.MigrateAsync("20261017145450_SeedIdentityPermissionsAndRoles");

            // Simulate the overly broad defaults deployed by the first Phase 1 version.
            await migrationDb.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                SELECT r.id, p.id, TIMESTAMPTZ '2026-01-01 00:00:00Z'
                FROM (VALUES
                    ('Faculty', 'identity.users.import'),
                    ('AcademicAffairs', 'identity.users.manage'),
                    ('AcademicAffairs', 'identity.permissions.read')
                ) AS obsolete(role_name, permission_code)
                JOIN identity.role r ON r.role_name = obsolete.role_name
                JOIN identity.permission p ON p.permission_code = obsolete.permission_code
                ON CONFLICT (role_id, permission_id) DO UPDATE
                SET assigned_at = EXCLUDED.assigned_at;
                """);

            await migrator.MigrateAsync();

            await using var identityDb = CreateIdentityDbContext();
            var permissions = await identityDb.Permissions
                .AsNoTracking()
                .OrderBy(permission => permission.Id)
                .ToListAsync();

            Assert.Contains(permissions, permission =>
                permission.Id == 1 && permission.PermissionCode == "legacy.permission");
            Assert.Contains(permissions, permission =>
                permission.Id == 20
                && permission.PermissionCode == PermissionCodes.IdentityUsersRead
                && permission.Module == "Identity"
                && permission.PermissionName == "Xem danh sách người dùng");
            Assert.All(RequiredPermissionCodes, code =>
                Assert.Single(permissions, permission => permission.PermissionCode == code));

            var assignments = await identityDb.RolePermissions
                .AsNoTracking()
                .Where(rolePermission => RequiredPermissionCodes.Contains(rolePermission.Permission.PermissionCode))
                .Select(rolePermission => new
                {
                    Role = rolePermission.Role.RoleName,
                    Permission = rolePermission.Permission.PermissionCode
                })
                .ToListAsync();

            Assert.Equal(
                RequiredPermissionCodes.Order(),
                assignments
                    .Where(assignment => assignment.Role == SystemRoleName.Admin)
                    .Select(assignment => assignment.Permission)
                    .Order());
            Assert.Equal(
                [PermissionCodes.IdentityUsersRead],
                assignments
                    .Where(assignment => assignment.Role == SystemRoleName.Faculty)
                    .Select(assignment => assignment.Permission)
                    .Order());
            Assert.Equal(
                [PermissionCodes.IdentityUsersImport, PermissionCodes.IdentityUsersRead],
                assignments
                    .Where(assignment => assignment.Role == SystemRoleName.AcademicAffairs)
                    .Select(assignment => assignment.Permission)
                    .Order());

            Assert.True(await identityDb.RolePermissions.AnyAsync(rolePermission =>
                rolePermission.Role.RoleName == SystemRoleName.Faculty
                && rolePermission.Permission.PermissionCode == "legacy.permission"));
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    [Fact]
    public async Task Concurrent_admin_suspensions_leave_one_active_admin()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            await migrationDb.Database.MigrateAsync();
            var (firstAdminId, secondAdminId) = await SeedTwoAdminsAsync();

            await using var firstDb = CreateIdentityDbContext();
            await using var secondDb = CreateIdentityDbContext();
            var firstHandler = CreateStatusHandler(firstDb);
            var secondHandler = CreateStatusHandler(secondDb);

            var results = await Task.WhenAll(
                firstHandler.HandleAsync(
                    new UpdateUserStatusCommand(firstAdminId, "Suspended"),
                    CancellationToken.None),
                secondHandler.HandleAsync(
                    new UpdateUserStatusCommand(secondAdminId, "Suspended"),
                    CancellationToken.None));

            Assert.Single(results, result => result.IsSuccess);
            var rejected = Assert.Single(results, result => result.IsFailure);
            Assert.Equal("Identity.CannotDisableLastAdmin", rejected.Error.Code);

            await using var verificationDb = CreateIdentityDbContext();
            Assert.Equal(
                1,
                await verificationDb.Users.CountAsync(user =>
                    user.Status == AccountStatus.Active
                    && user.UserRoles.Any(userRole => userRole.Role.RoleName == SystemRoleName.Admin)));
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    private async Task<(Guid First, Guid Second)> SeedTwoAdminsAsync()
    {
        await using var db = CreateIdentityDbContext();
        var adminRoleId = await db.Roles
            .Where(role => role.RoleName == SystemRoleName.Admin)
            .Select(role => role.Id)
            .SingleAsync();
        var now = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        var first = User.Create(
            Guid.NewGuid(),
            "first-admin@ugtm.edu.vn",
            "FIRST-ADMIN@UGTM.EDU.VN",
            "First Admin",
            "ADMIN01",
            null,
            "hash",
            AccountStatus.Active,
            now);
        var second = User.Create(
            Guid.NewGuid(),
            "second-admin@ugtm.edu.vn",
            "SECOND-ADMIN@UGTM.EDU.VN",
            "Second Admin",
            "ADMIN02",
            null,
            "hash",
            AccountStatus.Active,
            now);

        db.Users.AddRange(first, second);
        db.UserRoles.AddRange(
            new UserRole
            {
                UserId = first.Id,
                RoleId = adminRoleId,
                AssignedAt = now
            },
            new UserRole
            {
                UserId = second.Id,
                RoleId = adminRoleId,
                AssignedAt = now
            });
        await db.SaveChangesAsync();
        return (first.Id, second.Id);
    }

    private static UpdateUserStatusHandler CreateStatusHandler(IdentityDbContext db)
        => new(
            new UserRepository(db),
            new IdentityUnitOfWork(db),
            new FixedClock(),
            new NoOpAuditLogWriter());

    private UGTMDbContext CreateMigrationDbContext()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(
            options,
            fixture.ConnectionString,
            typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }

    private IdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new IdentityDbContext(options.Options);
    }

    private static Task ResetSchemasAsync(UGTMDbContext db)
        => db.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS identity CASCADE;
            DROP SCHEMA IF EXISTS academic CASCADE;
            DROP SCHEMA IF EXISTS topics CASCADE;
            DROP SCHEMA IF EXISTS thesis CASCADE;
            DROP SCHEMA IF EXISTS councils CASCADE;
            DROP SCHEMA IF EXISTS notifications CASCADE;
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } =
            new(2026, 10, 17, 12, 30, 0, DateTimeKind.Utc);
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public void Add(AuditEntry entry)
        {
        }
    }
}
