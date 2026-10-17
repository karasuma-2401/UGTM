using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SeedIdentityPermissionsAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE _existing_role_permissions AS
                SELECT rp.role_id, p.permission_code, rp.assigned_at
                FROM identity.role_permission rp
                JOIN identity.permission p ON p.id = rp.permission_id
                WHERE p.permission_code IN (
                    'identity.users.read',
                    'identity.users.manage',
                    'identity.users.import',
                    'identity.permissions.read',
                    'identity.roles.manage',
                    'identity.sessions.revoke'
                ) OR p.id IN (1, 2, 3, 4, 5, 6);

                DELETE FROM identity.permission
                WHERE permission_code IN (
                    'identity.users.read',
                    'identity.users.manage',
                    'identity.users.import',
                    'identity.permissions.read',
                    'identity.roles.manage',
                    'identity.sessions.revoke'
                ) OR id IN (1, 2, 3, 4, 5, 6);

                INSERT INTO identity.permission (id, module, permission_code, permission_name)
                VALUES
                    (1, 'Identity', 'identity.users.read', 'Xem danh sách người dùng'),
                    (2, 'Identity', 'identity.users.manage', 'Quản lý người dùng'),
                    (3, 'Identity', 'identity.users.import', 'Nhập danh sách người dùng'),
                    (4, 'Identity', 'identity.permissions.read', 'Xem danh mục quyền'),
                    (5, 'Identity', 'identity.roles.manage', 'Quản lý vai trò và phân quyền'),
                    (6, 'Identity', 'identity.sessions.revoke', 'Thu hồi phiên đăng nhập')
                ON CONFLICT (id) DO UPDATE
                SET module = EXCLUDED.module,
                    permission_code = EXCLUDED.permission_code,
                    permission_name = EXCLUDED.permission_name;

                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                VALUES
                    (1, 1, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (1, 2, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (1, 3, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (1, 4, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (1, 5, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (1, 6, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (2, 1, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (2, 3, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (3, 1, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (3, 2, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (3, 3, TIMESTAMPTZ '2026-01-01 00:00:00Z'),
                    (3, 4, TIMESTAMPTZ '2026-01-01 00:00:00Z')
                ON CONFLICT (role_id, permission_id) DO NOTHING;

                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                SELECT erp.role_id, p.id, erp.assigned_at
                FROM _existing_role_permissions erp
                JOIN identity.permission p ON p.permission_code = erp.permission_code
                ON CONFLICT (role_id, permission_id) DO NOTHING;

                DROP TABLE _existing_role_permissions;

                SELECT setval(pg_get_serial_sequence('identity.permission', 'id'), (SELECT COALESCE(MAX(id), 1) FROM identity.permission));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 3, 1 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 4, 1 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 5, 1 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 1, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 2, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 3, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 4, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permission",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permission",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permission",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permission",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permission",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permission",
                keyColumn: "id",
                keyValue: 6);
        }
    }
}
