using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreSummaryWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreflightSql);
            migrationBuilder.DropForeignKey(
                name: "fk_score_summary_component_score_summaries_summary_id",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropIndex(
                name: "ix_score_summary_component_summary_id_component_code_source_key",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                schema: "councils",
                table: "score_summary_component",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,4)",
                oldPrecision: 6,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "component_name",
                schema: "councils",
                table: "score_summary_component",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revision_id",
                schema: "councils",
                table: "score_summary_component",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "councils",
                table: "score_summary_component",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_references",
                schema: "councils",
                table: "score_summary_component",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "active_revision_id",
                schema: "councils",
                table: "score_summary",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "calculated_by",
                schema: "councils",
                table: "score_summary",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "config_id",
                schema: "councils",
                table: "score_summary",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "input_fingerprint",
                schema: "councils",
                table: "score_summary",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legacy",
                schema: "councils",
                table: "score_summary",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "legacy_snapshot",
                schema: "councils",
                table: "score_summary",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_revision_id",
                schema: "councils",
                table: "score_summary",
                type: "uuid",
                nullable: true);





            migrationBuilder.CreateTable(
                name: "score_summary_idempotency",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_summary_idempotency", x => x.id);
                    table.CheckConstraint("ck_score_receipt", "operation IN ('ScoreSummary.Calculate','ScoreSummary.Publish') AND response_status=200 AND fingerprint ~ '^[0-9a-f]{64}$' AND jsonb_typeof(response_payload)='object'");
                });

            migrationBuilder.CreateTable(
                name: "score_summary_revision",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_version_no = table.Column<int>(type: "integer", nullable: false),
                    config_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    source_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    final_score_raw = table.Column<decimal>(type: "numeric", nullable: false),
                    final_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    total_weight = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    calculation_formula = table.Column<string>(type: "text", nullable: false),
                    rounding_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    precision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_summary_revision", x => x.id);
                    table.UniqueConstraint("ak_score_summary_revision_id_summary_id", x => new { x.id, x.summary_id });
                    table.CheckConstraint("ck_score_revision", "revision_no > 0 AND config_version_no > 0 AND final_score_raw BETWEEN 0 AND 10 AND final_score BETWEEN 0 AND 10 AND total_weight > 0 AND rounding_mode='AwayFromZero' AND precision=2 AND input_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_score_summary_revision_score_summary_summary_id",
                        column: x => x.summary_id,
                        principalSchema: "councils",
                        principalTable: "score_summary",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_component_revision_id_component_code_source_k",
                schema: "councils",
                table: "score_summary_component",
                columns: new[] { "revision_id", "component_code", "source_key" },
                unique: true,
                filter: "revision_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_component_revision_id_summary_id",
                schema: "councils",
                table: "score_summary_component",
                columns: new[] { "revision_id", "summary_id" });

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_component_summary_id_component_code_source_key",
                schema: "councils",
                table: "score_summary_component",
                columns: new[] { "summary_id", "component_code", "source_key" },
                unique: true,
                filter: "revision_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_idempotency_actor_id_operation_idempotency_key",
                schema: "councils",
                table: "score_summary_idempotency",
                columns: new[] { "actor_id", "operation", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_revision_summary_id_revision_no",
                schema: "councils",
                table: "score_summary_revision",
                columns: new[] { "summary_id", "revision_no" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_score_summary_component_score_summaries_summary_id",
                schema: "councils",
                table: "score_summary_component",
                column: "summary_id",
                principalSchema: "councils",
                principalTable: "score_summary",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_score_summary_component_score_summary_revision_revision_id_",
                schema: "councils",
                table: "score_summary_component",
                columns: new[] { "revision_id", "summary_id" },
                principalSchema: "councils",
                principalTable: "score_summary_revision",
                principalColumns: new[] { "id", "summary_id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(WorkflowSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DowngradeSql);
            migrationBuilder.DropForeignKey(
                name: "fk_score_summary_component_score_summaries_summary_id",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropForeignKey(
                name: "fk_score_summary_component_score_summary_revision_revision_id_",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropTable(
                name: "score_summary_idempotency",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "score_summary_revision",
                schema: "councils");

            migrationBuilder.DropIndex(
                name: "ix_score_summary_component_revision_id_component_code_source_k",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropIndex(
                name: "ix_score_summary_component_revision_id_summary_id",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropIndex(
                name: "ix_score_summary_component_summary_id_component_code_source_key",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropColumn(
                name: "component_name",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropColumn(
                name: "revision_id",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropColumn(
                name: "source_references",
                schema: "councils",
                table: "score_summary_component");

            migrationBuilder.DropColumn(
                name: "active_revision_id",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "calculated_by",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "config_id",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "input_fingerprint",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "is_legacy",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "legacy_snapshot",
                schema: "councils",
                table: "score_summary");

            migrationBuilder.DropColumn(
                name: "published_revision_id",
                schema: "councils",
                table: "score_summary");





            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                schema: "councils",
                table: "score_summary_component",
                type: "numeric(6,4)",
                precision: 6,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_component_summary_id_component_code_source_key",
                schema: "councils",
                table: "score_summary_component",
                columns: new[] { "summary_id", "component_code", "source_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_score_summary_component_score_summaries_summary_id",
                schema: "councils",
                table: "score_summary_component",
                column: "summary_id",
                principalSchema: "councils",
                principalTable: "score_summary",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
