using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UGTM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "councils");

            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.EnsureSchema(
                name: "thesis");

            migrationBuilder.EnsureSchema(
                name: "academic");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "topics");

            migrationBuilder.CreateTable(
                name: "kltn_batch",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    semester = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kltn_batch", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    permission_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permission_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    user_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grading_criteria",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criteria_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criteria_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_criteria", x => x.id);
                    table.ForeignKey(
                        name: "fk_grading_criteria_kltn_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "academic",
                        principalTable: "kltn_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "score_weight_config",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_weight_config", x => x.id);
                    table.ForeignKey(
                        name: "fk_score_weight_config_kltn_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "academic",
                        principalTable: "kltn_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stage",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_index = table.Column<int>(type: "integer", nullable: false),
                    stage_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    deadline_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stage", x => x.id);
                    table.ForeignKey(
                        name: "fk_stage_kltn_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "academic",
                        principalTable: "kltn_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permission",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permission", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permission_permission_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity",
                        principalTable: "permission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permission_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "shared",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_data = table.Column<string>(type: "text", nullable: true),
                    new_data = table.Column<string>(type: "text", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_log_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "council",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    council_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    defense_date = table.Column<DateOnly>(type: "date", nullable: false),
                    time_slot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    room = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    decision_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    signed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decision_file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council", x => x.id);
                    table.ForeignKey(
                        name: "fk_council_kltn_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "academic",
                        principalTable: "kltn_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_council_users_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                schema: "shared",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_user_recipient_id",
                        column: x => x.recipient_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_file",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    file_extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_file", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_file_user_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "topic",
                schema: "topics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    advisor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(350)", maxLength: 350, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    requirements = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    student_number = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic", x => x.id);
                    table.ForeignKey(
                        name: "fk_topic_kltn_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "academic",
                        principalTable: "kltn_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_topic_user_advisor_id",
                        column: x => x.advisor_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_role_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_role_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_detail",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    criteria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_detail", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_detail_grading_criterias_criteria_id",
                        column: x => x.criteria_id,
                        principalSchema: "councils",
                        principalTable: "grading_criteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_score_weight",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_score_weight", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_score_weight_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_score_weight_score_weight_configs_config_id",
                        column: x => x.config_id,
                        principalSchema: "academic",
                        principalTable: "score_weight_config",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_score_weight_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "council_member",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    council_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_in_council = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_member", x => x.id);
                    table.ForeignKey(
                        name: "fk_council_member_council_council_id",
                        column: x => x.council_id,
                        principalSchema: "councils",
                        principalTable: "council",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_council_member_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_group",
                schema: "topics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Registered"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_group", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_group_topics_topic_id",
                        column: x => x.topic_id,
                        principalSchema: "topics",
                        principalTable: "topic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "advisor_evaluation",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_advisor_evaluation", x => x.id);
                    table.ForeignKey(
                        name: "fk_advisor_evaluation_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "council_assignment",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    council_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_council_assignment_councils_council_id",
                        column: x => x.council_id,
                        principalSchema: "councils",
                        principalTable: "council",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_council_assignment_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "final_report",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Submitted"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_report", x => x.id);
                    table.ForeignKey(
                        name: "fk_final_report_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "final_submission",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "NotSubmitted"),
                    archived_confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    archived_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_submission", x => x.id);
                    table.ForeignKey(
                        name: "fk_final_submission_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_final_submission_user_archived_confirmed_by",
                        column: x => x.archived_confirmed_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "midterm_report",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Submitted"),
                    faculty_feedback = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_midterm_report", x => x.id);
                    table.ForeignKey(
                        name: "fk_midterm_report_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_midterm_report_user_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "progress_milestone",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_milestone", x => x.id);
                    table.ForeignKey(
                        name: "fk_progress_milestone_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_progress_milestone_user_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rename_request",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    new_title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    old_title_en = table.Column<string>(type: "character varying(350)", maxLength: 350, nullable: true),
                    new_title_en = table.Column<string>(type: "character varying(350)", maxLength: 350, nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Created"),
                    advisor_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    faculty_approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aa_approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rename_request", x => x.id);
                    table.ForeignKey(
                        name: "fk_rename_request_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviewer_assignment",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviewer_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_reviewer_assignment_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviewer_assignment_users_assigned_by",
                        column: x => x.assigned_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviewer_assignment_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "score_summary",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    advisor_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    reviewer_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    council_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    calculation_formula = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_summary", x => x.id);
                    table.ForeignKey(
                        name: "fk_score_summary_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_score_summary_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_group_member",
                schema: "topics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_group_member", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_group_member_kltn_batch_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "academic",
                        principalTable: "kltn_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_group_member_student_group_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_student_group_member_user_student_id",
                        column: x => x.student_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "syllabus",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Submitted"),
                    faculty_feedback = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_syllabus", x => x.id);
                    table.ForeignKey(
                        name: "fk_syllabus_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_syllabus_user_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "council_minute",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    council_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    general_feedback = table.Column<string>(type: "text", nullable: true),
                    required_revisions = table.Column<string>(type: "text", nullable: true),
                    lock_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Open"),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    override_reason = table.Column<string>(type: "text", nullable: true),
                    unlocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_minute", x => x.id);
                    table.ForeignKey(
                        name: "fk_council_minute_council_assignment_council_assignment_id",
                        column: x => x.council_assignment_id,
                        principalSchema: "councils",
                        principalTable: "council_assignment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_council_minute_users_locked_by",
                        column: x => x.locked_by,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "council_score",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    council_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    council_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_score", x => x.id);
                    table.ForeignKey(
                        name: "fk_council_score_council_assignment_council_assignment_id",
                        column: x => x.council_assignment_id,
                        principalSchema: "councils",
                        principalTable: "council_assignment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_council_score_council_member_council_member_id",
                        column: x => x.council_member_id,
                        principalSchema: "councils",
                        principalTable: "council_member",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "progress_report",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Submitted"),
                    student_note = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    advisor_feedback = table.Column<string>(type: "text", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_report", x => x.id);
                    table.ForeignKey(
                        name: "fk_progress_report_progress_milestone_milestone_id",
                        column: x => x.milestone_id,
                        principalSchema: "thesis",
                        principalTable: "progress_milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_progress_report_student_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "topics",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviewer_evaluation",
                schema: "councils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    defense_approval = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_visible_to_student = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviewer_evaluation", x => x.id);
                    table.ForeignKey(
                        name: "fk_reviewer_evaluation_reviewer_assignment_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "councils",
                        principalTable: "reviewer_assignment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_advisor_evaluation_group_id",
                schema: "councils",
                table: "advisor_evaluation",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_user_id",
                schema: "shared",
                table: "audit_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_council_approved_by",
                schema: "councils",
                table: "council",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_council_batch_id",
                schema: "councils",
                table: "council",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_council_assignment_council_id",
                schema: "councils",
                table: "council_assignment",
                column: "council_id");

            migrationBuilder.CreateIndex(
                name: "ix_council_assignment_group_id",
                schema: "councils",
                table: "council_assignment",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_member_council_id_user_id",
                schema: "councils",
                table: "council_member",
                columns: new[] { "council_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_member_user_id",
                schema: "councils",
                table: "council_member",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_council_minute_council_assignment_id",
                schema: "councils",
                table: "council_minute",
                column: "council_assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_minute_locked_by",
                schema: "councils",
                table: "council_minute",
                column: "locked_by");

            migrationBuilder.CreateIndex(
                name: "ix_council_score_council_assignment_id_council_member_id",
                schema: "councils",
                table: "council_score",
                columns: new[] { "council_assignment_id", "council_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_score_council_member_id",
                schema: "councils",
                table: "council_score",
                column: "council_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_detail_criteria_id_evaluation_id",
                schema: "councils",
                table: "evaluation_detail",
                columns: new[] { "criteria_id", "evaluation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_report_group_id",
                schema: "thesis",
                table: "final_report",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_archived_confirmed_by",
                schema: "thesis",
                table: "final_submission",
                column: "archived_confirmed_by");

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_group_id",
                schema: "thesis",
                table: "final_submission",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grading_criteria_batch_id_criteria_code",
                schema: "councils",
                table: "grading_criteria",
                columns: new[] { "batch_id", "criteria_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_midterm_report_group_id",
                schema: "thesis",
                table: "midterm_report",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_midterm_report_reviewed_by",
                schema: "thesis",
                table: "midterm_report",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_id",
                schema: "shared",
                table: "notification",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_permission_permission_code",
                schema: "identity",
                table: "permission",
                column: "permission_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_progress_milestone_created_by",
                schema: "thesis",
                table: "progress_milestone",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_progress_milestone_group_id",
                schema: "thesis",
                table: "progress_milestone",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_progress_report_group_id",
                schema: "thesis",
                table: "progress_report",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_progress_report_milestone_id_group_id",
                schema: "thesis",
                table: "progress_report",
                columns: new[] { "milestone_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rename_request_group_id",
                schema: "thesis",
                table: "rename_request",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_assigned_by",
                schema: "councils",
                table: "reviewer_assignment",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_group_id",
                schema: "councils",
                table: "reviewer_assignment",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_assignment_reviewer_id",
                schema: "councils",
                table: "reviewer_assignment",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_evaluation_assignment_id",
                schema: "councils",
                table: "reviewer_evaluation",
                column: "assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_role_name",
                schema: "identity",
                table: "role",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permission_permission_id",
                schema: "identity",
                table: "role_permission",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_score_weight_config_id_role_id",
                schema: "academic",
                table: "role_score_weight",
                columns: new[] { "config_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_score_weight_role_id",
                schema: "academic",
                table: "role_score_weight",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_score_weight_updated_by",
                schema: "academic",
                table: "role_score_weight",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_group_id",
                schema: "councils",
                table: "score_summary",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_score_summary_published_by",
                schema: "councils",
                table: "score_summary",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "ix_score_weight_config_batch_id",
                schema: "academic",
                table: "score_weight_config",
                column: "batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stage_batch_id",
                schema: "academic",
                table: "stage",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_group_topic_id",
                schema: "topics",
                table: "student_group",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_group_member_batch_id",
                schema: "topics",
                table: "student_group_member",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_group_member_group_id_student_id",
                schema: "topics",
                table: "student_group_member",
                columns: new[] { "group_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_group_member_student_id_batch_id",
                schema: "topics",
                table: "student_group_member",
                columns: new[] { "student_id", "batch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_file_entity_type_entity_id_version_no",
                schema: "thesis",
                table: "submission_file",
                columns: new[] { "entity_type", "entity_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_file_uploaded_by",
                schema: "thesis",
                table: "submission_file",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_syllabus_group_id",
                schema: "thesis",
                table: "syllabus",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_syllabus_reviewed_by",
                schema: "thesis",
                table: "syllabus",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_topic_advisor_id",
                schema: "topics",
                table: "topic",
                column: "advisor_id");

            migrationBuilder.CreateIndex(
                name: "ix_topic_batch_id",
                schema: "topics",
                table: "topic",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_email",
                schema: "identity",
                table: "user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_user_code",
                schema: "identity",
                table: "user",
                column: "user_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_role_role_id",
                schema: "identity",
                table: "user_role",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advisor_evaluation",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "council_minute",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "council_score",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "evaluation_detail",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "final_report",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "final_submission",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "midterm_report",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "notification",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "progress_report",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "rename_request",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "reviewer_evaluation",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "role_permission",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_score_weight",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "score_summary",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "stage",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "student_group_member",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "submission_file",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "syllabus",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "user_role",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "council_assignment",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "council_member",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "grading_criteria",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "progress_milestone",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "reviewer_assignment",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "permission",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "score_weight_config",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "role",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "council",
                schema: "councils");

            migrationBuilder.DropTable(
                name: "student_group",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "topic",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "kltn_batch",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "user",
                schema: "identity");
        }
    }
}
