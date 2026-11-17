using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisorEvaluationWorkflowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "decision_at",
                schema: "councils",
                table: "advisor_evaluation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decision_by",
                schema: "councils",
                table: "advisor_evaluation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "evaluated_by",
                schema: "councils",
                table: "advisor_evaluation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "final_report_id",
                schema: "councils",
                table: "advisor_evaluation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legacy",
                schema: "councils",
                table: "advisor_evaluation",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "councils",
                table: "advisor_evaluation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "report_version_no",
                schema: "councils",
                table: "advisor_evaluation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submission_checksum_sha256",
                schema: "councils",
                table: "advisor_evaluation",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submission_file_id",
                schema: "councils",
                table: "advisor_evaluation",
                type: "uuid",
                nullable: true);

            // PostgreSQL exposes xmin as a built-in system column; there is no physical column to add.

            migrationBuilder.CreateIndex(
                name: "ix_advisor_evaluation_decision_by",
                schema: "councils",
                table: "advisor_evaluation",
                column: "decision_by");

            migrationBuilder.CreateIndex(
                name: "ix_advisor_evaluation_evaluated_by",
                schema: "councils",
                table: "advisor_evaluation",
                column: "evaluated_by");

            migrationBuilder.CreateIndex(
                name: "ix_advisor_evaluation_final_report_id",
                schema: "councils",
                table: "advisor_evaluation",
                column: "final_report_id");

            migrationBuilder.CreateIndex(
                name: "ix_advisor_evaluation_submission_file_id",
                schema: "councils",
                table: "advisor_evaluation",
                column: "submission_file_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_advisor_evaluation_reviewer_assignment_decision",
                schema: "councils",
                table: "advisor_evaluation",
                sql: "is_legacy OR (\r\n    (allows_reviewer_assignment IS NULL\r\n        AND rejection_reason IS NULL\r\n        AND decision_by IS NULL\r\n        AND decision_at IS NULL)\r\n    OR (allows_reviewer_assignment = FALSE\r\n        AND rejection_reason IS NOT NULL\r\n        AND char_length(btrim(rejection_reason)) BETWEEN 1 AND 1000\r\n        AND decision_by IS NOT NULL\r\n        AND decision_at IS NOT NULL)\r\n    OR (allows_reviewer_assignment = TRUE\r\n        AND rejection_reason IS NULL\r\n        AND decision_by IS NOT NULL\r\n        AND decision_at IS NOT NULL)\r\n)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_advisor_evaluation_workflow_binding",
                schema: "councils",
                table: "advisor_evaluation",
                sql: "is_legacy OR (\r\n    final_report_id IS NOT NULL\r\n    AND report_version_no > 0\r\n    AND submission_file_id IS NOT NULL\r\n    AND submission_checksum_sha256 IS NOT NULL\r\n    AND char_length(submission_checksum_sha256) = 64\r\n    AND evaluated_by IS NOT NULL\r\n    AND comment IS NOT NULL\r\n    AND char_length(btrim(comment)) BETWEEN 1 AND 2000\r\n)");

            migrationBuilder.AddForeignKey(
                name: "fk_advisor_evaluation_final_reports_final_report_id",
                schema: "councils",
                table: "advisor_evaluation",
                column: "final_report_id",
                principalSchema: "thesis",
                principalTable: "final_report",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_advisor_evaluation_submission_files_submission_file_id",
                schema: "councils",
                table: "advisor_evaluation",
                column: "submission_file_id",
                principalSchema: "thesis",
                principalTable: "submission_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_advisor_evaluation_users_decision_by",
                schema: "councils",
                table: "advisor_evaluation",
                column: "decision_by",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_advisor_evaluation_users_evaluated_by",
                schema: "councils",
                table: "advisor_evaluation",
                column: "evaluated_by",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_advisor_evaluation_final_reports_final_report_id",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropForeignKey(
                name: "fk_advisor_evaluation_submission_files_submission_file_id",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropForeignKey(
                name: "fk_advisor_evaluation_users_decision_by",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropForeignKey(
                name: "fk_advisor_evaluation_users_evaluated_by",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_advisor_evaluation_decision_by",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_advisor_evaluation_evaluated_by",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_advisor_evaluation_final_report_id",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_advisor_evaluation_submission_file_id",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_advisor_evaluation_reviewer_assignment_decision",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_advisor_evaluation_workflow_binding",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "decision_at",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "decision_by",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "evaluated_by",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "final_report_id",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "is_legacy",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "report_version_no",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "submission_checksum_sha256",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "submission_file_id",
                schema: "councils",
                table: "advisor_evaluation");

            // PostgreSQL exposes xmin as a built-in system column; there is no physical column to remove.
        }
    }
}
