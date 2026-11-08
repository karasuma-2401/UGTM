using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TopicRegistrationEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "topic_registration_email",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    protection_purpose = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    protected_payload = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_registration_email", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_topic_registration_email_dispatch",
                schema: "thesis",
                table: "topic_registration_email",
                columns: new[] { "protection_purpose", "processed_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_topic_registration_email_member",
                schema: "thesis",
                table: "topic_registration_email",
                columns: new[] { "member_id", "type", "processed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topic_registration_email",
                schema: "thesis");
        }
    }
}
