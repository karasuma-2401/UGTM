using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossModuleAuditOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "integration_event_id",
                schema: "identity",
                table: "audit_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_outbox_message",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_academic_audit_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_outbox_message",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_councils_audit_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_outbox_message",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_thesis_audit_outbox_message", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_audit_log_integration_event_id",
                schema: "identity",
                table: "audit_log",
                column: "integration_event_id",
                unique: true,
                filter: "integration_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_academic_audit_outbox_pending",
                schema: "academic",
                table: "audit_outbox_message",
                columns: new[] { "processed_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_councils_audit_outbox_pending",
                schema: "councils",
                table: "audit_outbox_message",
                columns: new[] { "processed_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_thesis_audit_outbox_pending",
                schema: "thesis",
                table: "audit_outbox_message",
                columns: new[] { "processed_at", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_outbox_message",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "audit_outbox_message",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "audit_outbox_message",
                schema: "thesis");

            migrationBuilder.DropIndex(
                name: "ux_audit_log_integration_event_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "integration_event_id",
                schema: "identity",
                table: "audit_log");
        }
    }
}
