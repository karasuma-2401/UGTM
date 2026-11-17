using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueAcademicPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_kltn_batch_academic_year_semester",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.CreateIndex(
                name: "ix_kltn_batch_academic_year_semester",
                schema: "academic",
                table: "kltn_batch",
                columns: new[] { "academic_year", "semester" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_kltn_batch_academic_year_semester",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.CreateIndex(
                name: "ix_kltn_batch_academic_year_semester",
                schema: "academic",
                table: "kltn_batch",
                columns: new[] { "academic_year", "semester" });
        }
    }
}
