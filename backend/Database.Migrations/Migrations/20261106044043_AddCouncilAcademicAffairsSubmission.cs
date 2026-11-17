using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCouncilAcademicAffairsSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_submitted_to_academic_affairs",
                schema: "councils",
                table: "council",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "submission_notes",
                schema: "councils",
                table: "council",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at",
                schema: "councils",
                table: "council",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by",
                schema: "councils",
                table: "council",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_submitted_to_academic_affairs",
                schema: "councils",
                table: "council");

            migrationBuilder.DropColumn(
                name: "submission_notes",
                schema: "councils",
                table: "council");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                schema: "councils",
                table: "council");

            migrationBuilder.DropColumn(
                name: "submitted_by",
                schema: "councils",
                table: "council");
        }
    }
}
