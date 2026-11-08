using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionUploadCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submission_upload_attempt",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retry_after_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_upload_attempt", x => x.id);
                    table.CheckConstraint("ck_submission_upload_attempt", "state IN ('Uploading','Committed','CleanupPending','Cleaning','Cleaned') AND retry_count >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_submission_upload_attempt_object_key",
                schema: "thesis",
                table: "submission_upload_attempt",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_upload_attempt_state_lease_expires_at_utc",
                schema: "thesis",
                table: "submission_upload_attempt",
                columns: new[] { "state", "lease_expires_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_upload_attempt",
                schema: "thesis");
        }
    }
}
