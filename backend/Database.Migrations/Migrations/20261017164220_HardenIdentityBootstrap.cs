using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations;

/// <inheritdoc />
public partial class HardenIdentityBootstrap : Migration
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

            -- Remove only the incorrect assignments created by the previous seed migration.
            -- The timestamp predicate preserves assignments an operator customized later.
            DELETE FROM identity.role_permission rp
            USING identity.role r, identity.permission p
            WHERE rp.role_id = r.id
              AND rp.permission_id = p.id
              AND rp.assigned_at = TIMESTAMPTZ '2026-01-01 00:00:00Z'
              AND (
                  (r.role_name = 'Faculty' AND p.permission_code = 'identity.users.import')
                  OR (r.role_name = 'AcademicAffairs' AND p.permission_code IN (
                      'identity.users.manage',
                      'identity.permissions.read'
                  ))
              );

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
        // Data-only hardening migration. Rollback must not delete permission data or
        // role assignments that may have been customized after deployment.
    }
}
