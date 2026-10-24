using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicRegistrationConfirmationAndPenalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "confirmation_expires_at_utc",
                schema: "thesis",
                table: "student_group_member",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confirmation_status",
                schema: "thesis",
                table: "student_group_member",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Confirmed");

            migrationBuilder.AddColumn<string>(
                name: "confirmation_token_hash",
                schema: "thesis",
                table: "student_group_member",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmed_at_utc",
                schema: "thesis",
                table: "student_group_member",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invited_by_student_id",
                schema: "thesis",
                table: "student_group_member",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "student_registration_penalty",
                schema: "thesis",
                columns: table => new
                {
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    violation_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_registration_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_violation_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    blocked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_registration_penalty", x => x.student_id);
                    table.ForeignKey(
                        name: "fk_student_registration_penalty_user_student_id",
                        column: x => x.student_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_group_member_confirmation_token_hash",
                schema: "thesis",
                table: "student_group_member",
                column: "confirmation_token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_student_group_member_invited_by_student_id",
                schema: "thesis",
                table: "student_group_member",
                column: "invited_by_student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_group_member_status_expires",
                schema: "thesis",
                table: "student_group_member",
                columns: new[] { "confirmation_status", "confirmation_expires_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "fk_student_group_member_user_invited_by_student_id",
                schema: "thesis",
                table: "student_group_member",
                column: "invited_by_student_id",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_student_group_member_user_invited_by_student_id",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropTable(
                name: "student_registration_penalty",
                schema: "thesis");

            migrationBuilder.DropIndex(
                name: "ix_student_group_member_confirmation_token_hash",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropIndex(
                name: "ix_student_group_member_invited_by_student_id",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropIndex(
                name: "ix_student_group_member_status_expires",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropColumn(
                name: "confirmation_expires_at_utc",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropColumn(
                name: "confirmation_status",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropColumn(
                name: "confirmation_token_hash",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropColumn(
                name: "confirmed_at_utc",
                schema: "thesis",
                table: "student_group_member");

            migrationBuilder.DropColumn(
                name: "invited_by_student_id",
                schema: "thesis",
                table: "student_group_member");
        }
    }
}
