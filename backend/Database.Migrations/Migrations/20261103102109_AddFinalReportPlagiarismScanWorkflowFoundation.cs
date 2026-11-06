using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalReportPlagiarismScanWorkflowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "final_report_plagiarism_scan",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PendingSubmission"),
                    similarity_coverage = table.Column<double>(type: "double precision", nullable: true),
                    max_reranker_score = table.Column<double>(type: "double precision", nullable: true),
                    scanned_chunks = table.Column<int>(type: "integer", nullable: true),
                    scanned_characters = table.Column<int>(type: "integer", nullable: true),
                    matched_characters = table.Column<int>(type: "integer", nullable: true),
                    cited_findings_count = table.Column<int>(type: "integer", nullable: true),
                    uncited_findings_count = table.Column<int>(type: "integer", nullable: true),
                    suspected_findings_count = table.Column<int>(type: "integer", nullable: true),
                    raw_report_json = table.Column<string>(type: "jsonb", nullable: true),
                    error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_polled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_check_after_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_report_plagiarism_scan", x => x.id);
                    table.CheckConstraint("ck_final_report_plagiarism_scan_max_reranker_score", "max_reranker_score IS NULL OR (max_reranker_score >= 0 AND max_reranker_score <= 1)");
                    table.CheckConstraint("ck_final_report_plagiarism_scan_similarity_coverage", "similarity_coverage IS NULL OR (similarity_coverage >= 0 AND similarity_coverage <= 100)");
                    table.CheckConstraint("ck_final_report_plagiarism_scan_version_no", "version_no > 0");
                    table.ForeignKey(
                        name: "fk_final_report_plagiarism_scan_final_report_id",
                        column: x => x.final_report_id,
                        principalSchema: "thesis",
                        principalTable: "final_report",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_final_report_plagiarism_scan_submission_file_id",
                        column: x => x.submission_file_id,
                        principalSchema: "thesis",
                        principalTable: "submission_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_final_report_plagiarism_scan_job_id",
                schema: "thesis",
                table: "final_report_plagiarism_scan",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_report_plagiarism_scan_status",
                schema: "thesis",
                table: "final_report_plagiarism_scan",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_final_report_plagiarism_scan_submission_file_id",
                schema: "thesis",
                table: "final_report_plagiarism_scan",
                column: "submission_file_id");

            migrationBuilder.CreateIndex(
                name: "ux_final_report_plagiarism_scan_report_id_version_no",
                schema: "thesis",
                table: "final_report_plagiarism_scan",
                columns: new[] { "final_report_id", "version_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "final_report_plagiarism_scan",
                schema: "thesis");
        }
    }
}
