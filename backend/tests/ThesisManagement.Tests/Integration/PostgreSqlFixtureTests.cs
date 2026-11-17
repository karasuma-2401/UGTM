using Microsoft.EntityFrameworkCore;
using Npgsql;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlFixtureTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Restore_isolates_data_schema_and_sequences_while_preserving_migration_seeds_and_triggers()
    {
        await fixture.ResetToMigratedAsync();
        var previousConnectionString = fixture.ConnectionString;
        List<string> permissions;
        int nextPermissionId;
        await using (var db = MigrationDb())
        {
            permissions = await db.Database.SqlQueryRaw<string>(
                "SELECT permission_code AS \"Value\" FROM identity.permission ORDER BY permission_code").ToListAsync();
            Assert.NotEmpty(permissions);
        }

        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO identity.permission (module, permission_code, permission_name)
                VALUES ('Tests', 'tests.restore', 'Fixture isolation') RETURNING id
                """, connection);
            nextPermissionId = (int)(await insert.ExecuteScalarAsync())!;
            await using var damage = new NpgsqlCommand("""
                DROP TRIGGER archival_package_immutable ON thesis.final_submission_package;
                DROP SCHEMA public CASCADE;
                """, connection);
            await damage.ExecuteNonQueryAsync();
        }

        await fixture.ResetToMigratedAsync();
        Assert.NotEqual(previousConnectionString, fixture.ConnectionString);
        await using (var db = MigrationDb())
        {
            Assert.Equal(permissions, await db.Database.SqlQueryRaw<string>(
                "SELECT permission_code AS \"Value\" FROM identity.permission ORDER BY permission_code").ToListAsync());
            Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
            Assert.True(await db.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (
                    SELECT 1 FROM pg_trigger
                    WHERE tgrelid = 'thesis.final_submission_package'::regclass
                    AND tgname = 'archival_package_immutable' AND tgenabled = 'O'
                ) AS "Value"
                """).SingleAsync());
        }

        await using var restored = new NpgsqlConnection(fixture.ConnectionString);
        await restored.OpenAsync();
        await using var restoredInsert = new NpgsqlCommand("""
            INSERT INTO identity.permission (module, permission_code, permission_name)
            VALUES ('Tests', 'tests.restore', 'Fixture isolation') RETURNING id
            """, restored);
        Assert.Equal(nextPermissionId, (int)(await restoredInsert.ExecuteScalarAsync())!);
    }

    private UGTMDbContext MigrationDb()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }
}
