using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionLinksAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submission_attachment",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    object_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    file_extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_attachment", x => x.id);
                    table.CheckConstraint("ck_submission_attachment_version_no", "version_no > 0");
                });

            migrationBuilder.CreateTable(
                name: "submission_product_link",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_product_link", x => x.id);
                    table.CheckConstraint("ck_submission_product_link_version_no", "version_no > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_submission_attachment_entity_version",
                schema: "thesis",
                table: "submission_attachment",
                columns: new[] { "entity_type", "entity_id", "version_no", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_attachment_uploaded_by",
                schema: "thesis",
                table: "submission_attachment",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_submission_product_link_entity_version",
                schema: "thesis",
                table: "submission_product_link",
                columns: new[] { "entity_type", "entity_id", "version_no", "display_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_attachment",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "submission_product_link",
                schema: "thesis");
        }
    }
}
