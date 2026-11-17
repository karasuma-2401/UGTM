using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CouncilSchedulingUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_council_member_leadership_role",
                schema: "councils",
                table: "council_member",
                columns: new[] { "council_id", "role_in_council" },
                unique: true,
                filter: "role_in_council IN ('Chairman', 'Secretary')");

            migrationBuilder.CreateIndex(
                name: "ux_council_assignment_council_order",
                schema: "councils",
                table: "council_assignment",
                columns: new[] { "council_id", "order_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_council_member_leadership_role",
                schema: "councils",
                table: "council_member");

            migrationBuilder.DropIndex(
                name: "ux_council_assignment_council_order",
                schema: "councils",
                table: "council_assignment");
        }
    }
}
