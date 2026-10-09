using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class EncapsulateModulePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_score_weight",
                schema: "academic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_score_weight",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_score_weight", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_score_weight_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_score_weight_score_weight_configs_config_id",
                        column: x => x.config_id,
                        principalSchema: "academic",
                        principalTable: "score_weight_config",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_score_weight_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_role_score_weight_config_id_role_id",
                schema: "academic",
                table: "role_score_weight",
                columns: new[] { "config_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_score_weight_role_id",
                schema: "academic",
                table: "role_score_weight",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_score_weight_updated_by",
                schema: "academic",
                table: "role_score_weight",
                column: "updated_by");
        }
    }
}
