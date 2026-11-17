using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleScoreConfigurationDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_score_weight_config_one_draft",
                schema: "academic",
                table: "score_weight_config",
                column: "batch_id",
                unique: true,
                filter: "status = 'Draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_score_weight_config_one_draft",
                schema: "academic",
                table: "score_weight_config");
        }
    }
}
