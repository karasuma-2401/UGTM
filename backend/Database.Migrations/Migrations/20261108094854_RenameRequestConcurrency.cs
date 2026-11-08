using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RenameRequestConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL already supplies the xmin system column used by the EF concurrency token.
            migrationBuilder.CreateIndex(
                name: "ux_rename_request_active_group",
                schema: "thesis",
                table: "rename_request",
                column: "group_id",
                unique: true,
                filter: "status IN ('Created', 'AdvisorConfirmed', 'FacultyApproved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_rename_request_active_group",
                schema: "thesis",
                table: "rename_request");

        }
    }
}
