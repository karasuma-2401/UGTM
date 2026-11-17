using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddThesisReviewGateFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve legacy data while the application moves from advisor-led to faculty-led review.
            // The obsolete allowed_to_continue and stop_reason columns are intentionally left unmapped.
            migrationBuilder.RenameColumn(
                name: "file_url",
                schema: "thesis",
                table: "submission_file",
                newName: "object_key");

            migrationBuilder.AlterColumn<string>(
                name: "object_key",
                schema: "thesis",
                table: "submission_file",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.RenameColumn(
                name: "advisor_feedback",
                schema: "thesis",
                table: "midterm_report",
                newName: "faculty_feedback");

            migrationBuilder.AddColumn<string>(
                name: "checksum_sha256",
                schema: "thesis",
                table: "submission_file",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                schema: "thesis",
                table: "submission_file",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "application/octet-stream");

            migrationBuilder.CreateTable(
                name: "review_gate_configuration",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gate_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    initial_submission_deadline_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    faculty_response_deadline_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    final_closure_deadline_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_gate_configuration", x => x.id);
                    table.CheckConstraint("ck_review_gate_configuration_deadline_order", "initial_submission_deadline_utc < faculty_response_deadline_utc AND faculty_response_deadline_utc < final_closure_deadline_utc");
                    table.ForeignKey(
                        name: "fk_review_gate_configuration_kltn_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "academic",
                        principalTable: "kltn_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_gate_configuration_user_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_gate_configuration_user_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_review",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gate_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_review", x => x.id);
                    table.CheckConstraint("ck_submission_review_version_no", "version_no > 0");
                    table.ForeignKey(
                        name: "fk_submission_review_user_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_gate_configuration_batch_gate_type",
                schema: "thesis",
                table: "review_gate_configuration",
                columns: new[] { "batch_id", "gate_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_gate_configuration_created_by",
                schema: "thesis",
                table: "review_gate_configuration",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_review_gate_configuration_updated_by",
                schema: "thesis",
                table: "review_gate_configuration",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_submission_review_gate_submission_version",
                schema: "thesis",
                table: "submission_review",
                columns: new[] { "gate_type", "submission_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_review_reviewed_by",
                schema: "thesis",
                table: "submission_review",
                column: "reviewed_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_gate_configuration",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "submission_review",
                schema: "thesis");

            migrationBuilder.DropColumn(
                name: "checksum_sha256",
                schema: "thesis",
                table: "submission_file");

            migrationBuilder.DropColumn(
                name: "content_type",
                schema: "thesis",
                table: "submission_file");

            migrationBuilder.RenameColumn(
                name: "faculty_feedback",
                schema: "thesis",
                table: "midterm_report",
                newName: "advisor_feedback");

            migrationBuilder.RenameColumn(
                name: "object_key",
                schema: "thesis",
                table: "submission_file",
                newName: "file_url");

            migrationBuilder.AlterColumn<string>(
                name: "file_url",
                schema: "thesis",
                table: "submission_file",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);
        }
    }
}
