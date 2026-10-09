using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AlignFinalReportFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stage_batch_id",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropIndex(
                name: "ix_reviewer_assignment_group_id",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.RenameColumn(
                name: "faculty_feedback",
                schema: "thesis",
                table: "midterm_report",
                newName: "advisor_feedback");

            migrationBuilder.AddColumn<string>(
                name: "calculation_method",
                schema: "academic",
                table: "score_weight_config",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "IntegerCoefficient");

            migrationBuilder.AddColumn<int>(
                name: "version_no",
                schema: "academic",
                table: "score_weight_config",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "calculated_at",
                schema: "councils",
                table: "score_summary",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calculation_config_snapshot",
                schema: "councils",
                table: "score_summary",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "config_version_no",
                schema: "councils",
                table: "score_summary",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_weight",
                schema: "councils",
                table: "score_summary",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exclusion_reason",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ignored_in_calculation",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "revision_feedback",
                schema: "councils",
                table: "reviewer_evaluation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "assignment_order",
                schema: "councils",
                table: "reviewer_assignment",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "councils",
                table: "reviewer_assignment",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "stop_reason",
                schema: "thesis",
                table: "midterm_report",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allowed_to_continue",
                schema: "thesis",
                table: "midterm_report",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "abstract_en",
                schema: "thesis",
                table: "final_submission",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "abstract_vi",
                schema: "thesis",
                table: "final_submission",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "advisor_confirmed_at",
                schema: "thesis",
                table: "final_submission",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "advisor_confirmed_by",
                schema: "thesis",
                table: "final_submission",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_version_no",
                schema: "thesis",
                table: "final_submission",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "faculty_feedback",
                schema: "thesis",
                table: "final_submission",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "keywords_en",
                schema: "thesis",
                table: "final_submission",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "keywords_vi",
                schema: "thesis",
                table: "final_submission",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exclusion_reason",
                schema: "councils",
                table: "council_score",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ignored_in_calculation",
                schema: "councils",
                table: "council_score",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "unlocked_by",
                schema: "councils",
                table: "council_minute",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allows_reviewer_assignment",
                schema: "councils",
                table: "advisor_evaluation",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exclusion_reason",
                schema: "councils",
                table: "advisor_evaluation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ignored_in_calculation",
                schema: "councils",
                table: "advisor_evaluation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "score_component_config",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    component_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    applicability = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    minimum_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_component_config", x => x.id);
                    table.CheckConstraint("ck_score_component_minimum", "minimum_score IS NULL OR minimum_score BETWEEN 0 AND 10");
                    table.CheckConstraint("ck_score_component_weight", "weight >= 0");
                    table.ForeignKey(
                        name: "fk_score_component_config_score_weight_configs_config_id",
                        column: x => x.config_id,
                        principalSchema: "academic",
                        principalTable: "score_weight_config",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "score_summary_component",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    is_ignored_in_calculation = table.Column<bool>(type: "boolean", nullable: false),
                    exclusion_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_summary_component", x => x.id);
                    table.CheckConstraint("ck_summary_component_score", "score BETWEEN 0 AND 10");
                    table.CheckConstraint("ck_summary_component_weight", "weight >= 0");
                    table.ForeignKey(
                        name: "fk_score_summary_component_score_summaries_summary_id",
                        column: x => x.summary_id,
                        principalSchema: "councils",
                        principalTable: "score_summary",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role",
                columns: new[] { "id", "description", "role_name" },
                values: new object[,]
                {
                    { 1, "Admin", "Admin" },
                    { 2, "Faculty", "Faculty" },
                    { 3, "AcademicAffairs", "AcademicAffairs" },
                    { 4, "Lecturer", "Lecturer" },
                    { 5, "Student", "Student" },
                    { 6, "CouncilMember", "CouncilMember" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_topic_student_number",
                schema: "topics",
                table: "topic",
                sql: "student_number IS NULL OR student_number BETWEEN 1 AND 2");

            migrationBuilder.CreateIndex(
                name: "ix_stage_batch_id_stage_index",
                schema: "academic",
                table: "stage",
                columns: new[] { "batch_id", "stage_index" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_stage_index",
                schema: "academic",
                table: "stage",
                sql: "stage_index BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviewer_evaluation_score",
                schema: "councils",
                table: "reviewer_evaluation",
                sql: "score BETWEEN 0 AND 10");

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_group_id_assignment_order",
                schema: "councils",
                table: "reviewer_assignment",
                columns: new[] { "group_id", "assignment_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_group_id_reviewer_id",
                schema: "councils",
                table: "reviewer_assignment",
                columns: new[] { "group_id", "reviewer_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_batch_dates",
                schema: "academic",
                table: "kltn_batch",
                sql: "end_date > start_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_batch_semester",
                schema: "academic",
                table: "kltn_batch",
                sql: "semester > 0");

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_advisor_confirmed_by",
                schema: "thesis",
                table: "final_submission",
                column: "advisor_confirmed_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_council_score_score",
                schema: "councils",
                table: "council_score",
                sql: "score BETWEEN 0 AND 10");

            migrationBuilder.CreateIndex(
                name: "ix_council_minute_unlocked_by",
                schema: "councils",
                table: "council_minute",
                column: "unlocked_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_advisor_evaluation_score",
                schema: "councils",
                table: "advisor_evaluation",
                sql: "score BETWEEN 0 AND 10");

            migrationBuilder.CreateIndex(
                name: "ix_score_component_config_config_id_component_code",
                schema: "academic",
                table: "score_component_config",
                columns: new[] { "config_id", "component_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_component_summary_id_component_code_source_key",
                schema: "councils",
                table: "score_summary_component",
                columns: new[] { "summary_id", "component_code", "source_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_council_minute_users_unlocked_by",
                schema: "councils",
                table: "council_minute",
                column: "unlocked_by",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_final_submission_user_advisor_confirmed_by",
                schema: "thesis",
                table: "final_submission",
                column: "advisor_confirmed_by",
                principalSchema: "identity",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_council_minute_users_unlocked_by",
                schema: "councils",
                table: "council_minute");

            migrationBuilder.DropForeignKey(
                name: "fk_final_submission_user_advisor_confirmed_by",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropTable(
                name: "score_component_config",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "score_summary_component",
                schema: "councils");

            migrationBuilder.DropCheckConstraint(
                name: "ck_topic_student_number",
                schema: "topics",
                table: "topic");

            migrationBuilder.DropIndex(
                name: "ix_stage_batch_id_stage_index",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stage_index",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviewer_evaluation_score",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_reviewer_assignment_group_id_assignment_order",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropIndex(
                name: "ix_reviewer_assignment_group_id_reviewer_id",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_batch_dates",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropCheckConstraint(
                name: "ck_batch_semester",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropIndex(
                name: "ix_final_submission_advisor_confirmed_by",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropCheckConstraint(
                name: "ck_council_score_score",
                schema: "councils",
                table: "council_score");

            migrationBuilder.DropIndex(
                name: "ix_council_minute_unlocked_by",
                schema: "councils",
                table: "council_minute");

            migrationBuilder.DropCheckConstraint(
                name: "ck_advisor_evaluation_score",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DropColumn(
                name: "calculation_method",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "version_no",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "calculated_at",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "calculation_config_snapshot",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "config_version_no",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "total_weight",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "exclusion_reason",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "is_ignored_in_calculation",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "revision_feedback",
                schema: "councils",
                table: "reviewer_evaluation");

            migrationBuilder.DropColumn(
                name: "assignment_order",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "councils",
                table: "reviewer_assignment");

            migrationBuilder.DropColumn(
                name: "stop_reason",
                schema: "thesis",
                table: "midterm_report");

            migrationBuilder.DropColumn(
                name: "allowed_to_continue",
                schema: "thesis",
                table: "midterm_report");

            migrationBuilder.DropColumn(
                name: "abstract_en",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "abstract_vi",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "advisor_confirmed_at",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "advisor_confirmed_by",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "current_version_no",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "faculty_feedback",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "keywords_en",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "keywords_vi",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "exclusion_reason",
                schema: "councils",
                table: "council_score");

            migrationBuilder.DropColumn(
                name: "is_ignored_in_calculation",
                schema: "councils",
                table: "council_score");

            migrationBuilder.DropColumn(
                name: "unlocked_by",
                schema: "councils",
                table: "council_minute");

            migrationBuilder.DropColumn(
                name: "allows_reviewer_assignment",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "exclusion_reason",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.DropColumn(
                name: "is_ignored_in_calculation",
                schema: "councils",
                table: "advisor_evaluation");

            migrationBuilder.RenameColumn(
                name: "advisor_feedback",
                schema: "thesis",
                table: "midterm_report",
                newName: "faculty_feedback");

            migrationBuilder.CreateIndex(
                name: "ix_stage_batch_id",
                schema: "academic",
                table: "stage",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_group_id",
                schema: "councils",
                table: "reviewer_assignment",
                column: "group_id",
                unique: true);
        }
    }
}
