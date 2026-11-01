using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewerEvaluationWorkflowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE councils.reviewer_assignment IN SHARE MODE;
                LOCK TABLE councils.reviewer_evaluation IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT assignment_id FROM councils.reviewer_evaluation GROUP BY assignment_id HAVING COUNT(*) > 1)
                       OR EXISTS (SELECT 1 FROM councils.reviewer_evaluation e LEFT JOIN councils.reviewer_assignment a ON a.id = e.assignment_id WHERE a.id IS NULL)
                       OR EXISTS (SELECT 1 FROM councils.reviewer_evaluation WHERE score IS NULL OR score < 0 OR score > 10 OR defense_approval IS NULL OR defense_approval NOT IN ('Approved', 'Rejected')) THEN
                        RAISE EXCEPTION 'SCRUM-105 preflight: duplicate, orphan or invalid historical reviewer evaluations. Repair explicitly; no data was selected, removed or invented.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(4,2)",
                oldPrecision: 4,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "defense_approval",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at_utc",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "evaluated_by",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legacy",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at_utc",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "uuid",
                nullable: true);
            // xmin is PostgreSQL's system concurrency column, not a physical column to add.


            migrationBuilder.CreateTable(
                name: "reviewer_evaluation_idempotency",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    response_payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviewer_evaluation_idempotency", x => x.id);
                    table.CheckConstraint("ck_reviewer_evaluation_idempotency_fingerprint", "request_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_reviewer_evaluation_idempotency_operation", "char_length(btrim(operation)) BETWEEN 1 AND 100 AND operation = btrim(operation)");
                    table.CheckConstraint("ck_reviewer_evaluation_idempotency_status", "response_status_code IN (200, 201)");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviewer_evaluation_defense_approval",
                schema: "councils",
                table: "reviewer_evaluation",
                sql: "defense_approval IS NULL OR defense_approval IN ('Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviewer_evaluation_status",
                schema: "councils",
                table: "reviewer_evaluation",
                sql: "status IS NULL OR status IN ('Draft', 'Submitted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviewer_evaluation_workflow",
                schema: "councils",
                table: "reviewer_evaluation",
                sql: "is_legacy OR (\r\n    id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND status IS NOT NULL\r\n    AND created_by IS NOT NULL AND created_by <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND evaluated_by IS NOT NULL AND evaluated_by <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND created_at_utc IS NOT NULL AND created_at_utc <= evaluated_at\r\n    AND (comment IS NULL OR (char_length(btrim(comment)) BETWEEN 1 AND 2000 AND comment = btrim(comment)))\r\n    AND (\r\n        (status = 'Draft' AND submitted_by IS NULL AND submitted_at_utc IS NULL AND NOT is_visible_to_student)\r\n        OR (status = 'Submitted' AND score IS NOT NULL AND comment IS NOT NULL AND defense_approval IS NOT NULL\r\n            AND submitted_by IS NOT NULL AND submitted_by = evaluated_by\r\n            AND submitted_at_utc IS NOT NULL AND submitted_at_utc = evaluated_at AND is_visible_to_student)\r\n    )\r\n)");

            migrationBuilder.CreateIndex(
                name: "ux_reviewer_evaluation_idempotency_actor_operation_key",
                schema: "councils",
                table: "reviewer_evaluation_idempotency",
                columns: new[] { "actor_id", "operation", "idempotency_key" },
                unique: true);
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('identity.permission', 'id'),
                    COALESCE((SELECT MAX(id) FROM identity.permission), 1), EXISTS (SELECT 1 FROM identity.permission));
                INSERT INTO identity.permission (module, permission_code, permission_name)
                VALUES ('DefenseCouncil', 'thesis.reviewer-evaluations.read', 'Xem phiếu phản biện và điều kiện bảo vệ')
                ON CONFLICT (permission_code) DO NOTHING;
                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                SELECT r.id, p.id, TIMESTAMPTZ '2026-10-30 00:00:00Z'
                FROM identity.role r CROSS JOIN identity.permission p
                WHERE r.role_name IN ('Faculty', 'AcademicAffairs', 'Admin') AND p.permission_code = 'thesis.reviewer-evaluations.read'
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE councils.reviewer_evaluation IN ACCESS EXCLUSIVE MODE;
                LOCK TABLE councils.reviewer_evaluation_idempotency IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM councils.reviewer_evaluation
                               WHERE NOT is_legacy OR score IS NULL OR defense_approval IS NULL OR status IS NOT NULL
                                  OR created_by IS NOT NULL OR created_at_utc IS NOT NULL OR evaluated_by IS NOT NULL
                                  OR submitted_by IS NOT NULL OR submitted_at_utc IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM councils.reviewer_evaluation_idempotency) THEN
                        RAISE EXCEPTION 'SCRUM-105 rollback preflight: workflow evaluations or receipts must be preserved. Downgrade cannot discard draft/submission semantics or invent missing scores.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "reviewer_evaluation_idempotency",
                schema: "councils");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviewer_evaluation_defense_approval",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviewer_evaluation_status",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviewer_evaluation_workflow",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "evaluated_by",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "is_legacy",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "submitted_at_utc",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "submitted_by",
                schema: "councils",
                table: "reviewer_evaluation");
            // Keep the PostgreSQL system column xmin on downgrade.


            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(4,2)",
                oldPrecision: 4,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "defense_approval",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
            // Preserve permission metadata and custom grants on rollback; reapply is idempotent.
        }
    }
}
