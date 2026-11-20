using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleIsHidden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                schema: "identity",
                table: "role",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_hidden",
                schema: "identity",
                table: "role");
        }
    }
}
