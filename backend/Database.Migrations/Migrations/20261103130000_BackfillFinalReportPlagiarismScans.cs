using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace UGTM.Database.Migrations.Migrations;

[DbContext(typeof(UGTMDbContext))]
[Migration("20261103130000_BackfillFinalReportPlagiarismScans")]
public sealed class BackfillFinalReportPlagiarismScans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO thesis.final_report_plagiarism_scan
            (id, final_report_id, submission_file_id, version_no, status, attempt_count, created_at)
        SELECT gen_random_uuid(), report.id, file.id, file.version_no, 'PendingSubmission', 0, file.uploaded_at
        FROM thesis.final_report report
        JOIN thesis.submission_file file ON file.entity_type = 'FinalReport' AND file.entity_id = report.id
        WHERE file.version_no BETWEEN 1 AND report.current_version_no
        ON CONFLICT (final_report_id, version_no) DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
