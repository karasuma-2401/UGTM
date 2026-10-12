using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CompleteIdentityAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_log_users_user_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_user_email",
                schema: "identity",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_user_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.RenameIndex(
                name: "ix_user_user_code",
                schema: "identity",
                table: "user",
                newName: "ux_user_user_code");

            migrationBuilder.RenameColumn(
                name: "entity_type",
                schema: "identity",
                table: "audit_log",
                newName: "target_type");

            migrationBuilder.RenameColumn(
                name: "entity_id",
                schema: "identity",
                table: "audit_log",
                newName: "target_id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                schema: "identity",
                table: "audit_log",
                newName: "actor_user_id");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                schema: "identity",
                table: "user",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "access_failed_count",
                schema: "identity",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "activated_at",
                schema: "identity",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_stamp",
                schema: "identity",
                table: "user",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at",
                schema: "identity",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                schema: "identity",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lockout_end_at",
                schema: "identity",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_email",
                schema: "identity",
                table: "user",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "password_changed_at",
                schema: "identity",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "security_stamp",
                schema: "identity",
                table: "user",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "identity",
                table: "user",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "actor_session_id",
                schema: "identity",
                table: "audit_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_user_id",
                schema: "identity",
                table: "audit_log",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "target_type",
                schema: "identity",
                table: "audit_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "outcome",
                schema: "identity",
                table: "audit_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Succeeded");

            migrationBuilder.AddColumn<string>(
                name: "metadata_json",
                schema: "identity",
                table: "audit_log",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason_code",
                schema: "identity",
                table: "audit_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_id",
                schema: "identity",
                table: "audit_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE identity."user"
                SET normalized_email = upper(trim(email)),
                    status = CASE
                        WHEN is_active = FALSE THEN 'Disabled'
                        WHEN password_hash IS NULL THEN 'PendingActivation'
                        ELSE 'Active'
                    END,
                    activated_at = CASE WHEN is_active IS DISTINCT FROM FALSE THEN created_at ELSE NULL END,
                    email_verified_at = CASE WHEN is_active IS DISTINCT FROM FALSE THEN created_at ELSE NULL END,
                    password_changed_at = CASE WHEN password_hash IS NOT NULL THEN created_at ELSE NULL END;

                UPDATE identity.audit_log
                SET request_id = 'legacy-' || id::text,
                    metadata_json = CASE
                        WHEN old_data IS NULL AND new_data IS NULL THEN NULL
                        ELSE jsonb_build_object('oldData', old_data, 'newData', new_data)
                    END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                schema: "identity",
                table: "user",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "identity",
                table: "user",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "request_id",
                schema: "identity",
                table: "audit_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE identity."user" ALTER COLUMN concurrency_stamp DROP DEFAULT;
                ALTER TABLE identity."user" ALTER COLUMN security_stamp DROP DEFAULT;
                """);

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "new_data",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "old_data",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.CreateTable(
                name: "authentication_session",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    authenticated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idle_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    security_stamp_snapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent_summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ip_address_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authentication_session", x => x.id);
                    table.ForeignKey(
                        name: "fk_authentication_session_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "one_time_challenge",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    selector = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    secret_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    destination_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invalidated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    send_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_one_time_challenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_one_time_challenge_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    protected_payload = table.Column<string>(type: "text", nullable: false),
                    protection_purpose = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selector = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    validator_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_authentication_session_session_id",
                        column: x => x.session_id,
                        principalSchema: "identity",
                        principalTable: "authentication_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_refresh_token_refresh_token_replaced_by_token_id",
                        column: x => x.replaced_by_token_id,
                        principalSchema: "identity",
                        principalTable: "refresh_token",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_user_normalized_email",
                schema: "identity",
                table: "user",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_actor_session_id",
                schema: "identity",
                table: "audit_log",
                column: "actor_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_actor_user_id",
                schema: "identity",
                table: "audit_log",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_request_id",
                schema: "identity",
                table: "audit_log",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_timestamp",
                schema: "identity",
                table: "audit_log",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_authentication_session_user_id_revoked_at_absolute_expires_",
                schema: "identity",
                table: "authentication_session",
                columns: new[] { "user_id", "revoked_at", "absolute_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_one_time_challenge_selector",
                schema: "identity",
                table: "one_time_challenge",
                column: "selector",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_one_time_challenge_user_id_purpose_expires_at",
                schema: "identity",
                table: "one_time_challenge",
                columns: new[] { "user_id", "purpose", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_processed_at_next_attempt_at",
                schema: "identity",
                table: "outbox_message",
                columns: new[] { "processed_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_replaced_by_token_id",
                schema: "identity",
                table: "refresh_token",
                column: "replaced_by_token_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_selector",
                schema: "identity",
                table: "refresh_token",
                column: "selector",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_session_id",
                schema: "identity",
                table: "refresh_token",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "one_time_challenge",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refresh_token",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "authentication_session",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ux_user_normalized_email",
                schema: "identity",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_actor_session_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_actor_user_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_request_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_timestamp",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "identity",
                table: "user",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "new_data",
                schema: "identity",
                table: "audit_log",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_data",
                schema: "identity",
                table: "audit_log",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM identity."user" WHERE password_hash IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade identity authentication while users without passwords exist.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM identity.audit_log WHERE actor_user_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade identity authentication while audit rows without an actor user exist.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM identity.audit_log WHERE length(target_type) > 50) THEN
                        RAISE EXCEPTION 'Cannot downgrade identity authentication while audit target types longer than 50 characters exist.';
                    END IF;
                END $$;

                UPDATE identity."user"
                SET is_active = CASE WHEN status = 'Active' THEN TRUE ELSE FALSE END;

                UPDATE identity.audit_log
                SET old_data = metadata_json ->> 'oldData',
                    new_data = metadata_json ->> 'newData';
                """);

            migrationBuilder.DropColumn(
                name: "access_failed_count",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "activated_at",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "concurrency_stamp",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "email_verified_at",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "lockout_end_at",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "normalized_email",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "password_changed_at",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "identity",
                table: "user");

            migrationBuilder.DropColumn(
                name: "actor_session_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "metadata_json",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "outcome",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "reason_code",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "request_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.RenameIndex(
                name: "ux_user_user_code",
                schema: "identity",
                table: "user",
                newName: "ix_user_user_code");

            migrationBuilder.RenameColumn(
                name: "target_type",
                schema: "identity",
                table: "audit_log",
                newName: "entity_type");

            migrationBuilder.RenameColumn(
                name: "target_id",
                schema: "identity",
                table: "audit_log",
                newName: "entity_id");

            migrationBuilder.RenameColumn(
                name: "actor_user_id",
                schema: "identity",
                table: "audit_log",
                newName: "user_id");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                schema: "identity",
                table: "user",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "identity",
                table: "audit_log",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "entity_type",
                schema: "identity",
                table: "audit_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "ix_user_email",
                schema: "identity",
                table: "user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_user_id",
                schema: "identity",
                table: "audit_log",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_log_users_user_id",
                schema: "identity",
                table: "audit_log",
                column: "user_id",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
