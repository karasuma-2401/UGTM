using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SeedThesisReviewGateConfigurationPermission : Migration
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
                    ('ThesisManagement', 'thesis.review-gate-config.manage', 'Cấu hình thời hạn các cổng thẩm định khóa luận')
                ON CONFLICT (permission_code) DO UPDATE
                SET module = EXCLUDED.module,
                    permission_name = EXCLUDED.permission_name;

                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                SELECT r.id, p.id, TIMESTAMPTZ '2026-10-22 00:00:00Z'
                FROM (VALUES
                    ('Admin', 'thesis.review-gate-config.manage'),
                    ('AcademicAffairs', 'thesis.review-gate-config.manage')
                ) AS defaults(role_name, permission_code)
                JOIN identity.role r ON r.role_name = defaults.role_name
                JOIN identity.permission p ON p.permission_code = defaults.permission_code
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration. Rollback must preserve permission assignments because
            // administrators may have customized them after this migration was applied.
        }
    }
}
