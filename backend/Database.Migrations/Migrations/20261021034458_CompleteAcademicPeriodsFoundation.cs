using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAcademicPeriodsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_stage_index",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropIndex(
                name: "ix_score_weight_config_batch_id",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.RenameColumn(
                name: "stage_index",
                schema: "academic",
                table: "stage",
                newName: "order_index");

            migrationBuilder.RenameColumn(
                name: "deadline_date",
                schema: "academic",
                table: "stage",
                newName: "deadline_at_utc");

            migrationBuilder.RenameIndex(
                name: "ix_stage_batch_id_stage_index",
                schema: "academic",
                table: "stage",
                newName: "ix_stage_batch_id_order_index");

            migrationBuilder.AddColumn<DateOnly>(
                name: "end_date",
                schema: "academic",
                table: "stage",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_overridden",
                schema: "academic",
                table: "stage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "stage_code",
                schema: "academic",
                table: "stage",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "change_reason",
                schema: "academic",
                table: "score_weight_config",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "academic",
                table: "score_weight_config",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                schema: "academic",
                table: "score_weight_config",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_by",
                schema: "academic",
                table: "score_weight_config",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "retired_at",
                schema: "academic",
                table: "score_weight_config",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "academic",
                table: "score_weight_config",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "academic",
                table: "score_weight_config",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "academic",
                table: "kltn_batch",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "schedule_template_version_id",
                schema: "academic",
                table: "kltn_batch",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                schema: "academic",
                table: "kltn_batch",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Ho_Chi_Minh");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "academic",
                table: "kltn_batch",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "academic",
                table: "kltn_batch",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE academic.stage
                SET end_date = GREATEST(
                        start_date,
                        (deadline_at_utc AT TIME ZONE 'Asia/Ho_Chi_Minh')::date),
                    stage_code = 'STAGE_' || order_index::text;

                UPDATE academic.score_weight_config
                SET status = CASE WHEN is_active THEN 'Active' ELSE 'Retired' END;
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "end_date",
                schema: "academic",
                table: "stage",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "stage_code",
                schema: "academic",
                table: "stage",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.CreateTable(
                name: "schedule_template_version",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_template_version", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stage_template_definition",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stage_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    start_offset_days = table.Column<int>(type: "integer", nullable: false),
                    end_offset_days = table.Column<int>(type: "integer", nullable: false),
                    default_deadline_local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    deadline_adjustment_policy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stage_template_definition", x => x.id);
                    table.CheckConstraint("ck_stage_template_offsets", "start_offset_days >= 0 AND end_offset_days >= start_offset_days");
                    table.CheckConstraint("ck_stage_template_order", "order_index BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "fk_stage_template_definition_version",
                        column: x => x.template_version_id,
                        principalSchema: "academic",
                        principalTable: "schedule_template_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "academic",
                table: "schedule_template_version",
                columns: new[] { "id", "created_at", "created_by", "name", "published_at", "published_by", "retired_at", "status", "template_code", "time_zone_id", "version_no" },
                values: new object[] { new Guid("4e035a70-f7d5-49ef-b977-d818eff26896"), new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), null, "Quy trình KLTN chuẩn 21 tuần", new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Active", "UIT_KLTN_STANDARD", "Asia/Ho_Chi_Minh", 1 });

            migrationBuilder.InsertData(
                schema: "academic",
                table: "stage_template_definition",
                columns: new[] { "id", "deadline_adjustment_policy", "default_deadline_local_time", "end_offset_days", "order_index", "stage_code", "stage_name", "start_offset_days", "template_version_id" },
                values: new object[,]
                {
                    { new Guid("11e9e9ab-789e-462f-8eb1-f2d6be05e3b3"), "None", new TimeOnly(23, 59, 0), 13, 2, "REGISTRATION_AND_SYLLABUS", "Đăng ký và đề cương", 7, new Guid("4e035a70-f7d5-49ef-b977-d818eff26896") },
                    { new Guid("2f7fd572-7fc5-4620-972b-36cfcf8c0091"), "None", new TimeOnly(23, 59, 0), 6, 1, "TOPIC_PREPARATION", "Chuẩn bị và công bố đề tài", 0, new Guid("4e035a70-f7d5-49ef-b977-d818eff26896") },
                    { new Guid("32488d73-1973-4ea6-b6d2-23fc1fd13a99"), "None", new TimeOnly(23, 59, 0), 139, 6, "DEFENSE", "Hội đồng và bảo vệ", 126, new Guid("4e035a70-f7d5-49ef-b977-d818eff26896") },
                    { new Guid("a08f9813-286c-4da4-999b-4334a4cf3a20"), "None", new TimeOnly(23, 59, 0), 69, 4, "MIDTERM_GATE", "Đánh giá giữa kỳ", 63, new Guid("4e035a70-f7d5-49ef-b977-d818eff26896") },
                    { new Guid("abf58e41-725e-421b-b9f4-d6dcfe4dddc2"), "None", new TimeOnly(23, 59, 0), 62, 3, "PROGRESS_EXECUTION", "Thực hiện và theo dõi tiến độ", 14, new Guid("4e035a70-f7d5-49ef-b977-d818eff26896") },
                    { new Guid("b2b70276-7538-451b-86a5-cf61031cfbd1"), "None", new TimeOnly(23, 59, 0), 146, 7, "ARCHIVAL_AND_PUBLICATION", "Lưu chiểu và công bố kết quả", 140, new Guid("4e035a70-f7d5-49ef-b977-d818eff26896") },
                    { new Guid("e938156b-26e7-4256-9465-ac2a458e8c15"), "None", new TimeOnly(23, 59, 0), 125, 5, "FINAL_EVALUATION", "Báo cáo cuối kỳ và phản biện", 70, new Guid("4e035a70-f7d5-49ef-b977-d818eff26896") }
                });

            migrationBuilder.Sql(
                """
                UPDATE academic.kltn_batch
                SET schedule_template_version_id = '4e035a70-f7d5-49ef-b977-d818eff26896'
                WHERE schedule_template_version_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_stage_batch_id_stage_code",
                schema: "academic",
                table: "stage",
                columns: new[] { "batch_id", "stage_code" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_stage_dates",
                schema: "academic",
                table: "stage",
                sql: "end_date >= start_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stage_order_index",
                schema: "academic",
                table: "stage",
                sql: "order_index BETWEEN 1 AND 7");

            migrationBuilder.CreateIndex(
                name: "ix_score_weight_config_batch_version",
                schema: "academic",
                table: "score_weight_config",
                columns: new[] { "batch_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_score_weight_config_one_active",
                schema: "academic",
                table: "score_weight_config",
                column: "batch_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_score_component_config_config_id_order_index",
                schema: "academic",
                table: "score_component_config",
                columns: new[] { "config_id", "order_index" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_score_component_order",
                schema: "academic",
                table: "score_component_config",
                sql: "order_index > 0");

            migrationBuilder.CreateIndex(
                name: "ix_kltn_batch_academic_year_semester",
                schema: "academic",
                table: "kltn_batch",
                columns: new[] { "academic_year", "semester" });

            migrationBuilder.CreateIndex(
                name: "ix_kltn_batch_schedule_template_version_id",
                schema: "academic",
                table: "kltn_batch",
                column: "schedule_template_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_template_code_version",
                schema: "academic",
                table: "schedule_template_version",
                columns: new[] { "template_code", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_template_one_active",
                schema: "academic",
                table: "schedule_template_version",
                column: "template_code",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_stage_template_version_code",
                schema: "academic",
                table: "stage_template_definition",
                columns: new[] { "template_version_id", "stage_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stage_template_version_order",
                schema: "academic",
                table: "stage_template_definition",
                columns: new[] { "template_version_id", "order_index" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_kltn_batch_schedule_template_version",
                schema: "academic",
                table: "kltn_batch",
                column: "schedule_template_version_id",
                principalSchema: "academic",
                principalTable: "schedule_template_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_kltn_batch_schedule_template_version",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropTable(
                name: "stage_template_definition",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "schedule_template_version",
                schema: "academic");

            migrationBuilder.DropIndex(
                name: "ix_stage_batch_id_stage_code",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stage_dates",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stage_order_index",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropIndex(
                name: "ix_score_weight_config_batch_version",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropIndex(
                name: "ix_score_weight_config_one_active",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropIndex(
                name: "ix_score_component_config_config_id_order_index",
                schema: "academic",
                table: "score_component_config");

            migrationBuilder.DropCheckConstraint(
                name: "ck_score_component_order",
                schema: "academic",
                table: "score_component_config");

            migrationBuilder.DropIndex(
                name: "ix_kltn_batch_academic_year_semester",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropIndex(
                name: "ix_kltn_batch_schedule_template_version_id",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropColumn(
                name: "end_date",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropColumn(
                name: "is_overridden",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropColumn(
                name: "stage_code",
                schema: "academic",
                table: "stage");

            migrationBuilder.DropColumn(
                name: "change_reason",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "published_at",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "published_by",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "retired_at",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropColumn(
                name: "schedule_template_version_id",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "academic",
                table: "kltn_batch");

            migrationBuilder.RenameColumn(
                name: "order_index",
                schema: "academic",
                table: "stage",
                newName: "stage_index");

            migrationBuilder.RenameColumn(
                name: "deadline_at_utc",
                schema: "academic",
                table: "stage",
                newName: "deadline_date");

            migrationBuilder.RenameIndex(
                name: "ix_stage_batch_id_order_index",
                schema: "academic",
                table: "stage",
                newName: "ix_stage_batch_id_stage_index");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "academic",
                table: "score_weight_config",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM academic.score_weight_config
                        GROUP BY batch_id
                        HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Cannot downgrade AcademicPeriods while a batch has multiple score configuration versions.';
                    END IF;
                END $$;

                UPDATE academic.score_weight_config
                SET is_active = status = 'Active';
                """);

            migrationBuilder.DropColumn(
                name: "status",
                schema: "academic",
                table: "score_weight_config");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stage_index",
                schema: "academic",
                table: "stage",
                sql: "stage_index BETWEEN 1 AND 7");

            migrationBuilder.CreateIndex(
                name: "ix_score_weight_config_batch_id",
                schema: "academic",
                table: "score_weight_config",
                column: "batch_id",
                unique: true);
        }
    }
}
