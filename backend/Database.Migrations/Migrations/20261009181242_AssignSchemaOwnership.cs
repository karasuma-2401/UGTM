using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AssignSchemaOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_log_user_user_id",
                schema: "shared",
                table: "audit_log");

            migrationBuilder.DropForeignKey(
                name: "fk_syllabus_student_groups_group_id",
                schema: "thesis",
                table: "syllabus");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.RenameTable(
                name: "student_group_member",
                schema: "topics",
                newName: "student_group_member",
                newSchema: "thesis");

            migrationBuilder.RenameTable(
                name: "student_group",
                schema: "topics",
                newName: "student_group",
                newSchema: "thesis");

            migrationBuilder.RenameTable(
                name: "notification",
                schema: "shared",
                newName: "notification",
                newSchema: "notifications");

            migrationBuilder.RenameTable(
                name: "audit_log",
                schema: "shared",
                newName: "audit_log",
                newSchema: "identity");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_log_users_user_id",
                schema: "identity",
                table: "audit_log",
                column: "user_id",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_syllabus_student_group_group_id",
                schema: "thesis",
                table: "syllabus",
                column: "group_id",
                principalSchema: "thesis",
                principalTable: "student_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS shared;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_log_users_user_id",
                schema: "identity",
                table: "audit_log");

            migrationBuilder.DropForeignKey(
                name: "fk_syllabus_student_group_group_id",
                schema: "thesis",
                table: "syllabus");

            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.RenameTable(
                name: "student_group_member",
                schema: "thesis",
                newName: "student_group_member",
                newSchema: "topics");

            migrationBuilder.RenameTable(
                name: "student_group",
                schema: "thesis",
                newName: "student_group",
                newSchema: "topics");

            migrationBuilder.RenameTable(
                name: "notification",
                schema: "notifications",
                newName: "notification",
                newSchema: "shared");

            migrationBuilder.RenameTable(
                name: "audit_log",
                schema: "identity",
                newName: "audit_log",
                newSchema: "shared");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_log_user_user_id",
                schema: "shared",
                table: "audit_log",
                column: "user_id",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_syllabus_student_groups_group_id",
                schema: "thesis",
                table: "syllabus",
                column: "group_id",
                principalSchema: "topics",
                principalTable: "student_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS notifications;");
        }
    }
}
