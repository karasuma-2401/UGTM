using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewerAssignmentWorkflowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hold the table until commit so the preflight and replacement unique index observe
            // the same data. Never resolve existing multiple-reviewer groups by deleting history.
            migrationBuilder.Sql("""
                LOCK TABLE councils.reviewer_assignment IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT group_id FROM councils.reviewer_assignment
                        WHERE is_active GROUP BY group_id HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'SCRUM-104 preflight: multiple active reviewer assignments in a group. Resolve explicitly before retrying; no assignments were selected or removed.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM councils.reviewer_evaluation e
                        LEFT JOIN councils.reviewer_assignment a ON a.id = e.assignment_id
                        WHERE a.id IS NULL
                    ) THEN
                        RAISE EXCEPTION 'SCRUM-104 preflight: orphan reviewer evaluations. Repair the historical references before retrying.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_reviewer_evaluation_reviewer_assignment_assignment_id",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_reviewer_assignment_group_id_assignment_order",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropIndex(
                name: "ix_reviewer_assignment_group_id_reviewer_id",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.AddColumn<Guid>(
                name: "advisor_evaluation_id",
                schema: "councils",
                table: "reviewer_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "advisor_id_at_assignment",
                schema: "councils",
                table: "reviewer_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                schema: "councils",
                table: "reviewer_assignment",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                schema: "councils",
                table: "reviewer_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                schema: "councils",
                table: "reviewer_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "final_report_id",
                schema: "councils",
                table: "reviewer_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legacy",
                schema: "councils",
                table: "reviewer_assignment",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "report_version_no",
                schema: "councils",
                table: "reviewer_assignment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submission_checksum_sha256",
                schema: "councils",
                table: "reviewer_assignment",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submission_file_id",
                schema: "councils",
                table: "reviewer_assignment",
                type: "uuid",
                nullable: true);

            // xmin is a built-in PostgreSQL system column, not a physical column to add.

            migrationBuilder.CreateTable(
                name: "reviewer_assignment_idempotency",
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
                    table.PrimaryKey("pk_reviewer_assignment_idempotency", x => x.id);
                    table.CheckConstraint("ck_reviewer_assignment_idempotency_fingerprint", "request_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_reviewer_assignment_idempotency_operation", "char_length(btrim(operation)) BETWEEN 1 AND 100 AND operation = btrim(operation)");
                    table.CheckConstraint("ck_reviewer_assignment_idempotency_status", "response_status_code IN (200, 201)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_group_id_assigned_at",
                schema: "councils",
                table: "reviewer_assignment",
                columns: new[] { "group_id", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "ux_reviewer_assignment_active_group",
                schema: "councils",
                table: "reviewer_assignment",
                column: "group_id",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviewer_assignment_cancellation",
                schema: "councils",
                table: "reviewer_assignment",
                sql: "(is_active AND cancelled_by IS NULL AND cancelled_at IS NULL AND cancellation_reason IS NULL)\r\nOR (NOT is_active AND (\r\n    (is_legacy AND cancelled_by IS NULL AND cancelled_at IS NULL AND cancellation_reason IS NULL)\r\n    OR (cancelled_by IS NOT NULL\r\n        AND cancelled_by <> '00000000-0000-0000-0000-000000000000'::uuid\r\n        AND cancelled_at IS NOT NULL AND cancelled_at >= assigned_at\r\n        AND cancellation_reason IS NOT NULL\r\n        AND char_length(btrim(cancellation_reason)) BETWEEN 1 AND 1000\r\n        AND cancellation_reason = btrim(cancellation_reason))\r\n))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviewer_assignment_workflow_binding",
                schema: "councils",
                table: "reviewer_assignment",
                sql: "is_legacy OR (\r\n    id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND group_id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND reviewer_id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND assigned_by <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND advisor_id_at_assignment IS NOT NULL\r\n    AND advisor_id_at_assignment <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND reviewer_id <> advisor_id_at_assignment\r\n    AND advisor_evaluation_id IS NOT NULL\r\n    AND advisor_evaluation_id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND final_report_id IS NOT NULL\r\n    AND final_report_id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND report_version_no IS NOT NULL AND report_version_no > 0\r\n    AND submission_file_id IS NOT NULL\r\n    AND submission_file_id <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND submission_checksum_sha256 IS NOT NULL\r\n    AND submission_checksum_sha256 ~ '^[0-9a-f]{64}$'\r\n    AND assignment_order = 1\r\n)");

            migrationBuilder.CreateIndex(
                name: "ux_reviewer_assignment_idempotency_actor_operation_key",
                schema: "councils",
                table: "reviewer_assignment_idempotency",
                columns: new[] { "actor_id", "operation", "idempotency_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_reviewer_evaluation_reviewer_assignment_assignment_id",
                schema: "councils",
                table: "reviewer_evaluation",
                column: "assignment_id",
                principalSchema: "councils",
                principalTable: "reviewer_assignment",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                SELECT setval(
                    pg_get_serial_sequence('identity.permission', 'id'),
                    COALESCE((SELECT MAX(id) FROM identity.permission), 1),
                    EXISTS (SELECT 1 FROM identity.permission));

                INSERT INTO identity.permission (module, permission_code, permission_name)
                VALUES
                    ('DefenseCouncil', 'thesis.reviewer-assignments.read', 'Xem phân công giảng viên phản biện'),
                    ('DefenseCouncil', 'thesis.reviewer-assignments.manage', 'Quản lý phân công giảng viên phản biện')
                ON CONFLICT (permission_code) DO NOTHING;

                INSERT INTO identity.role_permission (role_id, permission_id, assigned_at)
                SELECT r.id, p.id, TIMESTAMPTZ '2026-10-30 00:00:00Z'
                FROM identity.role r
                CROSS JOIN identity.permission p
                WHERE r.role_name IN ('Faculty', 'AcademicAffairs', 'Admin')
                  AND p.permission_code IN ('thesis.reviewer-assignments.read', 'thesis.reviewer-assignments.manage')
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE councils.reviewer_assignment IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT group_id, reviewer_id FROM councils.reviewer_assignment
                        GROUP BY group_id, reviewer_id HAVING COUNT(*) > 1
                    ) OR EXISTS (
                        SELECT group_id, assignment_order FROM councils.reviewer_assignment
                        GROUP BY group_id, assignment_order HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'SCRUM-104 rollback preflight: assignment history cannot satisfy the old unique indexes. Preserve the history and resolve the downgrade explicitly.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_reviewer_evaluation_reviewer_assignment_assignment_id",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropTable(
                name: "reviewer_assignment_idempotency",
                schema: "councils");

            migrationBuilder.DropIndex(
                name: "ix_reviewer_assignment_group_id_assigned_at",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropIndex(
                name: "ux_reviewer_assignment_active_group",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviewer_assignment_cancellation",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviewer_assignment_workflow_binding",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "advisor_evaluation_id",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "advisor_id_at_assignment",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "final_report_id",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "is_legacy",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "report_version_no",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "submission_checksum_sha256",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "submission_file_id",
                schema: "councils",
                table: "reviewer_assignment");

            // xmin is a built-in PostgreSQL system column, not a physical column to remove.

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_group_id_assignment_order",
                schema: "councils",
                table: "reviewer_assignment",
                columns: new[] { "group_id", "assignment_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_group_id_reviewer_id",
                schema: "councils",
                table: "reviewer_assignment",
                columns: new[] { "group_id", "reviewer_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_reviewer_evaluation_reviewer_assignment_assignment_id",
                schema: "councils",
                table: "reviewer_evaluation",
                column: "assignment_id",
                principalSchema: "councils",
                principalTable: "reviewer_assignment",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Preserve permissions and administrator-customized grants on rollback, consistent
            // with the repository's other permission migrations. Reapply remains idempotent.
        }
    }
}
