using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalReportSubmissionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_final_report_group_id",
                schema: "thesis",
                table: "final_report");

            migrationBuilder.AddColumn<int>(
                name: "current_version_no",
                schema: "thesis",
                table: "final_report",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // PostgreSQL exposes xmin as a built-in system column; there is no physical column to add.

            migrationBuilder.CreateIndex(
                name: "ux_submission_file_final_report_entity_id_version_no",
                schema: "thesis",
                table: "submission_file",
                columns: new[] { "entity_id", "version_no" },
                unique: true,
                filter: "entity_type = 'FinalReport'");

            migrationBuilder.CreateIndex(
                name: "ux_final_report_group_id_version_type",
                schema: "thesis",
                table: "final_report",
                columns: new[] { "group_id", "version_type" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_final_report_current_version_no",
                schema: "thesis",
                table: "final_report",
                sql: "current_version_no > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_submission_file_final_report_entity_id_version_no",
                schema: "thesis",
                table: "submission_file");

            migrationBuilder.DropIndex(
                name: "ux_final_report_group_id_version_type",
                schema: "thesis",
                table: "final_report");

            migrationBuilder.DropCheckConstraint(
                name: "ck_final_report_current_version_no",
                schema: "thesis",
                table: "final_report");

            migrationBuilder.DropColumn(
                name: "current_version_no",
                schema: "thesis",
                table: "final_report");

            // PostgreSQL exposes xmin as a built-in system column; there is no physical column to remove.

            migrationBuilder.CreateIndex(
                name: "ix_final_report_group_id",
                schema: "thesis",
                table: "final_report",
                column: "group_id");
        }
    }
}
