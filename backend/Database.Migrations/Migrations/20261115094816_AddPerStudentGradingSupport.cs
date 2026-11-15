using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPerStudentGradingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_evaluation_detail_criteria_id_evaluation_id",
                schema: "councils",
                table: "evaluation_detail");

            migrationBuilder.AddColumn<string>(
                name: "elo",
                schema: "councils",
                table: "grading_criteria",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "section_code",
                schema: "councils",
                table: "grading_criteria",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "section_name",
                schema: "councils",
                table: "grading_criteria",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "section_order",
                schema: "councils",
                table: "grading_criteria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "student_id",
                schema: "councils",
                table: "evaluation_detail",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_detail_criteria_id_evaluation_id_student_id",
                schema: "councils",
                table: "evaluation_detail",
                columns: new[] { "criteria_id", "evaluation_id", "student_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_detail_student_id",
                schema: "councils",
                table: "evaluation_detail",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_evaluation_detail_criteria_id_evaluation_id_student_id",
                schema: "councils",
                table: "evaluation_detail");

            migrationBuilder.DropIndex(
                name: "ix_evaluation_detail_student_id",
                schema: "councils",
                table: "evaluation_detail");

            migrationBuilder.DropColumn(
                name: "elo",
                schema: "councils",
                table: "grading_criteria");

            migrationBuilder.DropColumn(
                name: "section_code",
                schema: "councils",
                table: "grading_criteria");

            migrationBuilder.DropColumn(
                name: "section_name",
                schema: "councils",
                table: "grading_criteria");

            migrationBuilder.DropColumn(
                name: "section_order",
                schema: "councils",
                table: "grading_criteria");

            migrationBuilder.DropColumn(
                name: "student_id",
                schema: "councils",
                table: "evaluation_detail");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_detail_criteria_id_evaluation_id",
                schema: "councils",
                table: "evaluation_detail",
                columns: new[] { "criteria_id", "evaluation_id" },
                unique: true);
        }
    }
}
