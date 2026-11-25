using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSystemFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "notification_type",
                schema: "notifications",
                table: "notification",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<Guid>(
                name: "academic_batch_id",
                schema: "notifications",
                table: "notification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "action_path",
                schema: "notifications",
                table: "notification",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deduplication_key",
                schema: "notifications",
                table: "notification",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                schema: "notifications",
                table: "notification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                schema: "notifications",
                table: "notification",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<DateTime>(
                name: "read_at",
                schema: "notifications",
                table: "notification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resource_id",
                schema: "notifications",
                table: "notification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resource_type",
                schema: "notifications",
                table: "notification",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                schema: "notifications",
                table: "notification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_module",
                schema: "notifications",
                table: "notification",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Notifications");

            migrationBuilder.CreateTable(
                name: "device_registration",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    platform = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "web"),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_device_registration", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_registration_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_error_message = table.Column<string>(type: "text", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_notification_delivery", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_delivery_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "notifications",
                        principalTable: "notification",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_event_outbox",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_academic_notification_event_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_event_outbox",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_councils_notification_event_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_event_outbox",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_thesis_notification_event_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_event_outbox",
                schema: "topics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topics_notification_event_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preference",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_notification_preference", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_preference_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_academic_batch_id",
                schema: "notifications",
                table: "notification",
                column: "academic_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_created_id",
                schema: "notifications",
                table: "notification",
                columns: new[] { "recipient_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_unread",
                schema: "notifications",
                table: "notification",
                columns: new[] { "recipient_id", "created_at" },
                descending: new[] { false, true },
                filter: "is_read = false");

            migrationBuilder.CreateIndex(
                name: "ux_notification_recipient_deduplication_key",
                schema: "notifications",
                table: "notification",
                columns: new[] { "recipient_id", "deduplication_key" },
                unique: true,
                filter: "deduplication_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_active_user",
                schema: "notifications",
                table: "device_registration",
                column: "user_id",
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_device_registration_user_installation",
                schema: "notifications",
                table: "device_registration",
                columns: new[] { "user_id", "installation_id" });

            migrationBuilder.CreateIndex(
                name: "ux_device_registration_token_hash",
                schema: "notifications",
                table: "device_registration",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_pending",
                schema: "notifications",
                table: "notification_delivery",
                column: "next_attempt_at",
                filter: "status IN ('Pending', 'RetryScheduled')");

            migrationBuilder.CreateIndex(
                name: "ux_notification_delivery_unique_target",
                schema: "notifications",
                table: "notification_delivery",
                columns: new[] { "notification_id", "channel", "destination" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_academic_notification_event_outbox_pending",
                schema: "academic",
                table: "notification_event_outbox",
                column: "next_attempt_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_academic_notification_event_outbox_event_id",
                schema: "academic",
                table: "notification_event_outbox",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_councils_notification_event_outbox_pending",
                schema: "councils",
                table: "notification_event_outbox",
                column: "next_attempt_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_councils_notification_event_outbox_event_id",
                schema: "councils",
                table: "notification_event_outbox",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_thesis_notification_event_outbox_pending",
                schema: "thesis",
                table: "notification_event_outbox",
                column: "next_attempt_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_thesis_notification_event_outbox_event_id",
                schema: "thesis",
                table: "notification_event_outbox",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topics_notification_event_outbox_pending",
                schema: "topics",
                table: "notification_event_outbox",
                column: "next_attempt_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_topics_notification_event_outbox_event_id",
                schema: "topics",
                table: "notification_event_outbox",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_notification_preference_user_category_channel",
                schema: "notifications",
                table: "notification_preference",
                columns: new[] { "user_id", "category", "channel" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_kltn_batch_academic_batch_id",
                schema: "notifications",
                table: "notification",
                column: "academic_batch_id",
                principalSchema: "academic",
                principalTable: "kltn_batch",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notification_kltn_batch_academic_batch_id",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropTable(
                name: "device_registration",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_delivery",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_event_outbox",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "notification_event_outbox",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "notification_event_outbox",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "notification_event_outbox",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "notification_preference",
                schema: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notification_academic_batch_id",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropIndex(
                name: "ix_notification_recipient_created_id",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropIndex(
                name: "ix_notification_recipient_unread",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropIndex(
                name: "ux_notification_recipient_deduplication_key",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "academic_batch_id",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "action_path",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "deduplication_key",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "read_at",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "resource_id",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "resource_type",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "source_module",
                schema: "notifications",
                table: "notification");

            migrationBuilder.AlterColumn<string>(
                name: "notification_type",
                schema: "notifications",
                table: "notification",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
