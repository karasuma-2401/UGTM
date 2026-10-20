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
                SELECT setval(
                    pg_get_serial_sequence('identity.permission', 'id'),
                    COALESCE((SELECT MAX(id) FROM identity.permission), 1),
                    EXISTS (SELECT 1 FROM identity.permission));

                INSERT INTO identity.permission (module, permission_code, permission_name)
                VALUES
                    ('Identity', 'identity.users.read', 'Xem danh sách người dùng'),
                    ('Identity', 'identity.users.manage', 'Quản lý người dùng'),
                    ('Identity', 'identity.users.import', 'Nhập danh sách người dùng'),
                    ('Identity', 'identity.permissions.read', 'Xem danh mục quyền'),
                    ('Identity', 'identity.roles.manage', 'Quản lý vai trò và phân quyền'),
                    ('Identity', 'identity.sessions.revoke', 'Thu hồi phiên đăng nhập')
                ON CONFLICT (permission_code) DO UPDATE
                SET module = EXCLUDED.module,
                    permission_name = EXCLUDED.permission_name;

                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                SELECT r.id, p.id, TIMESTAMPTZ '2026-01-01 00:00:00Z'
                FROM (VALUES
                    ('Admin', 'identity.users.read'),
                    ('Admin', 'identity.users.manage'),
                    ('Admin', 'identity.users.import'),
                    ('Admin', 'identity.permissions.read'),
                    ('Admin', 'identity.roles.manage'),
                    ('Admin', 'identity.sessions.revoke'),
                    ('Faculty', 'identity.users.read'),
                    ('AcademicAffairs', 'identity.users.read'),
                    ('AcademicAffairs', 'identity.users.import')
                ) AS defaults(role_name, permission_code)
                JOIN identity.role r ON r.role_name = defaults.role_name
                JOIN identity.permission p ON p.permission_code = defaults.permission_code
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration. Permission rows and assignments may have existed before
            // this migration or may have been customized later, so rollback must not delete them.
        }
    }
}
