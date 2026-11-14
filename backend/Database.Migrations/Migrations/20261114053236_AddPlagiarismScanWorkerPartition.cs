using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPlagiarismScanWorkerPartition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "worker_partition",
                schema: "thesis",
                table: "final_report_plagiarism_scan",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_report_plagiarism_scan_worker_partition",
                schema: "thesis",
                table: "final_report_plagiarism_scan",
                column: "worker_partition");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_final_report_plagiarism_scan_worker_partition",
                schema: "thesis",
                table: "final_report_plagiarism_scan");

            migrationBuilder.DropColumn(
                name: "worker_partition",
                schema: "thesis",
                table: "final_report_plagiarism_scan");
        }
    }
}
