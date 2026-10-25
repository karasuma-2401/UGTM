using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRenameRequestWorkflowAndSyllabusResubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_syllabus_resubmission",
                schema: "thesis",
                table: "student_group",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                schema: "thesis",
                table: "rename_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by",
                schema: "thesis",
                table: "rename_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "thesis",
                table: "rename_request",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "requested_by_student_id",
                schema: "thesis",
                table: "rename_request",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allow_syllabus_resubmission",
                schema: "thesis",
                table: "student_group");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                schema: "thesis",
                table: "rename_request");

            migrationBuilder.DropColumn(
                name: "rejected_by",
                schema: "thesis",
                table: "rename_request");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "thesis",
                table: "rename_request");

            migrationBuilder.DropColumn(
                name: "requested_by_student_id",
                schema: "thesis",
                table: "rename_request");
        }
    }
}
