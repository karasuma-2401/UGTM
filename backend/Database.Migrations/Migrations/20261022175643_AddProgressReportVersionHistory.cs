using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressReportVersionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL exposes xmin as a built-in system column; there is no physical column to add.
            migrationBuilder.CreateTable(
                name: "progress_report_version",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    git_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    student_note = table.Column<string>(type: "text", nullable: true),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_late = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    advisor_feedback = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_report_version", x => x.id);
                    table.ForeignKey(
                        name: "fk_progress_report_version_progress_report_report_id",
                        column: x => x.progress_report_id,
                        principalSchema: "thesis",
                        principalTable: "progress_report",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_progress_report_version_reviewed_by",
                schema: "thesis",
                table: "progress_report_version",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_progress_report_version_submitted_by",
                schema: "thesis",
                table: "progress_report_version",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "ux_progress_report_version_report_id_version_no",
                schema: "thesis",
                table: "progress_report_version",
                columns: new[] { "progress_report_id", "version_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "progress_report_version",
                schema: "thesis");

            // PostgreSQL exposes xmin as a built-in system column; there is no physical column to remove.
        }
    }
}
