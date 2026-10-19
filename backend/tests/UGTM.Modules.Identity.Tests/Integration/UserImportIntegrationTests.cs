using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Domain;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.Identity.Application.Activation;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Users.ImportUsers;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Notifications;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserImportIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Import_is_atomic_and_does_not_enqueue_activation_email()
    {
        await using var migrationDb = CreateMigrationDbContext();
        await ResetSchemasAsync(migrationDb);

        try
        {
            await migrationDb.Database.MigrateAsync();

            var firstImport = await ImportAsync(new ImportFileReadResult(
                [
                    new ImportUserRow(2, "student1@ugtm.edu.vn", "Student One", "SV001", null),
                    new ImportUserRow(3, "student2@ugtm.edu.vn", "Student Two", "SV002", "0901234567")
                ],
                []));

            Assert.True(firstImport.IsSuccess);
            Assert.Equal(2, firstImport.Value.ImportedCount);

            await using (var verificationDb = CreateIdentityDbContext())
            {
                var importedUsers = await verificationDb.Users
                    .AsNoTracking()
                    .Include(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
                    .OrderBy(user => user.UserCode)
                    .ToListAsync();
                Assert.Equal(2, importedUsers.Count);
                Assert.All(importedUsers, user =>
                {
                    Assert.Equal(AccountStatus.PendingActivation, user.Status);
                    Assert.Null(user.PasswordHash);
                    Assert.Contains(user.UserRoles, userRole =>
                        userRole.Role.RoleName == SystemRoleName.Student);
                });
                Assert.Empty(await verificationDb.OneTimeChallenges.AsNoTracking().ToListAsync());
                Assert.Empty(await verificationDb.IdentityOutboxMessages.AsNoTracking().ToListAsync());
            }

            await using (var activationDb = CreateIdentityDbContext())
            {
                var activation = new ResendActivationHandler(
                    new UserRepository(activationDb),
                    new OneTimeChallengeRepository(activationDb),
                    new OpaqueTokenService(),
                    new IdentityEmailOutbox(
                        activationDb,
                        new EphemeralDataProtectionProvider(),
                        Options.Create(new IdentityEmailOptions
                        {
                            PublicWebBaseUrl = new Uri("https://ugtm.example"),
                            OutboxPartition = "test"
                        })),
                    new IdentityUnitOfWork(activationDb),
                    new NoOpAuditLogWriter(),
                    new FixedClock(),
                    new IdentitySecurityPolicy());

                var activationResult = await activation.HandleAsync(
                    new ResendActivationCommand("student1@ugtm.edu.vn"),
                    CancellationToken.None);
                Assert.True(activationResult.IsSuccess);
            }

            await using (var activationVerificationDb = CreateIdentityDbContext())
            {
                Assert.Single(await activationVerificationDb.OneTimeChallenges.AsNoTracking().ToListAsync());
                Assert.Single(await activationVerificationDb.IdentityOutboxMessages.AsNoTracking().ToListAsync());
            }

            var conflictingImport = await ImportAsync(new ImportFileReadResult(
                [
                    new ImportUserRow(2, "student1@ugtm.edu.vn", "Duplicate", "SV099", null),
                    new ImportUserRow(3, "student3@ugtm.edu.vn", "Would Be Valid", "SV003", null)
                ],
                []));

            Assert.True(conflictingImport.IsSuccess);
            Assert.Equal(0, conflictingImport.Value.ImportedCount);
            Assert.Contains(conflictingImport.Value.Errors, error => error.Code == "DuplicateEmail");

            await using var finalDb = CreateIdentityDbContext();
            Assert.Equal(2, await finalDb.Users.CountAsync());
        }
        finally
        {
            await ResetSchemasAsync(migrationDb);
        }
    }

    private async Task<Result<ImportUsersResult>> ImportAsync(ImportFileReadResult readResult)
    {
        await using var db = CreateIdentityDbContext();
        var handler = new ImportUsersHandler(
            new StubReader(readResult),
            new UserRepository(db),
            new IdentityUnitOfWork(db),
            new FixedClock(),
            new NoOpAuditLogWriter());

        return await handler.HandleAsync(
            new ImportUsersCommand(new MemoryStream([1]), "students.xlsx", "Student"),
            CancellationToken.None);
    }

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

    private sealed class StubReader(ImportFileReadResult result) : IUserImportFileReader
    {
        public ImportFileReadResult Read(Stream stream) => result;
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } =
            new(2026, 10, 18, 2, 0, 0, DateTimeKind.Utc);
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public void Add(AuditEntry entry)
        {
        }
    }
}
