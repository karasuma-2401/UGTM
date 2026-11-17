using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalSubmissionArchivalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE thesis.final_submission, thesis.submission_file IN ACCESS EXCLUSIVE MODE;
                DO $$ DECLARE invalid_id uuid; BEGIN
                    SELECT id INTO invalid_id FROM thesis.final_submission
                    WHERE status NOT IN ('NotSubmitted','Submitted','NeedRevision','AdvisorConfirmed','ArchiveConfirmed')
                       OR current_version_no < 1 OR id='00000000-0000-0000-0000-000000000000'::uuid
                       OR group_id='00000000-0000-0000-0000-000000000000'::uuid LIMIT 1;
                    IF FOUND THEN RAISE EXCEPTION 'Invalid legacy final submission %; repair explicitly before migration',invalid_id; END IF;
                    SELECT id INTO invalid_id FROM thesis.submission_file
                    WHERE entity_type='FinalSubmission' AND (version_no < 1 OR file_size <= 0) LIMIT 1;
                    IF FOUND THEN RAISE EXCEPTION 'Invalid legacy archival file %; repair explicitly before migration',invalid_id; END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_final_submission_student_groups_group_id",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.AddColumn<Guid>(
                name: "current_package_id",
                schema: "thesis",
                table: "final_submission",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legacy",
                schema: "thesis",
                table: "final_submission",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "legacy_snapshot",
                schema: "thesis",
                table: "final_submission",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at_utc",
                schema: "thesis",
                table: "final_submission",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by",
                schema: "thesis",
                table: "final_submission",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "thesis",
                table: "final_submission",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "councils",
                table: "council_minute",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_submission_file_archival_binding",
                schema: "thesis",
                table: "submission_file",
                columns: new[] { "id", "entity_type", "entity_id", "version_no" });

            migrationBuilder.CreateTable(
                name: "final_submission_idempotency",
                schema: "thesis",
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
                    table.PrimaryKey("pk_final_submission_idempotency", x => x.id);
                    table.CheckConstraint("ck_archival_receipt", "operation IN ('FinalSubmission.Submit','FinalSubmission.Confirm') AND fingerprint ~ '^[0-9a-f]{64}$' AND response_status IN (200,201) AND jsonb_typeof(response_payload) = 'object'");
                });

            migrationBuilder.CreateTable(
                name: "final_submission_package",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    abstract_vi = table.Column<string>(type: "text", nullable: false),
                    abstract_en = table.Column<string>(type: "text", nullable: false),
                    keywords_vi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    keywords_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    manifest_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_submission_package", x => x.id);
                    table.UniqueConstraint("ak_archival_package_binding", x => new { x.id, x.submission_id, x.version_no });
                    table.CheckConstraint("ck_archival_package", "version_no > 0 AND manifest_sha256 ~ '^[0-9a-f]{64}$' AND char_length(btrim(abstract_vi)) BETWEEN 1 AND 10000 AND char_length(btrim(abstract_en)) BETWEEN 1 AND 10000 AND char_length(btrim(keywords_vi)) BETWEEN 1 AND 1000 AND char_length(btrim(keywords_en)) BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "fk_final_submission_package_final_submission_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "thesis",
                        principalTable: "final_submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "final_submission_package_file",
                schema: "thesis",
                columns: table => new
                {
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    submission_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_submission_package_file", x => new { x.package_id, x.file_role });
                    table.CheckConstraint("ck_archival_file_role", "file_role IN ('ThesisPdf','ThesisDocx') AND entity_type = 'FinalSubmission'");
                    table.ForeignKey(
                        name: "fk_final_submission_package_file_final_submission_package_pack",
                        columns: x => new { x.package_id, x.submission_id, x.version_no },
                        principalSchema: "thesis",
                        principalTable: "final_submission_package",
                        principalColumns: new[] { "id", "submission_id", "version_no" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_final_submission_package_file_submission_file_submission_fi",
                        columns: x => new { x.submission_file_id, x.entity_type, x.submission_id, x.version_no },
                        principalSchema: "thesis",
                        principalTable: "submission_file",
                        principalColumns: new[] { "id", "entity_type", "entity_id", "version_no" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "final_submission_review",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_version_no = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    stage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_advisor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    manifest_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    defense_proof_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    result_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_submission_review", x => x.id);
                    table.CheckConstraint("ck_archival_review", "action IN ('AdvisorConfirm','FacultyConfirm','RequestRevision') AND stage IN ('Advisor','Faculty') AND manifest_sha256 ~ '^[0-9a-f]{64}$' AND ((action = 'RequestRevision' AND char_length(btrim(reason)) BETWEEN 1 AND 1000) OR (action <> 'RequestRevision' AND reason IS NULL))");
                    table.ForeignKey(
                        name: "fk_final_submission_review_final_submission_package_package_id",
                        columns: x => new { x.package_id, x.submission_id, x.package_version_no },
                        principalSchema: "thesis",
                        principalTable: "final_submission_package",
                        principalColumns: new[] { "id", "submission_id", "version_no" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "final_submission_upload_attempt",
                schema: "thesis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pdf_object_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    docx_object_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    committed_package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    retry_after_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_submission_upload_attempt", x => x.id);
                    table.CheckConstraint("ck_archival_attempt", "state IN ('Uploading','Committed','CleanupPending','Cleaning','Cleaned') AND retry_count >= 0 AND ((state = 'Committed' AND committed_package_id IS NOT NULL) OR (state <> 'Committed' AND committed_package_id IS NULL))");
                    table.ForeignKey(
                        name: "fk_final_submission_upload_attempt_final_submission_package_co",
                        column: x => x.committed_package_id,
                        principalSchema: "thesis",
                        principalTable: "final_submission_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_final_submission_upload_attempt_student_group_group_id",
                        column: x => x.group_id,
                        principalSchema: "thesis",
                        principalTable: "student_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_idempotency_actor_id_operation_idempotency",
                schema: "thesis",
                table: "final_submission_idempotency",
                columns: new[] { "actor_id", "operation", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_package_submission_id_version_no",
                schema: "thesis",
                table: "final_submission_package",
                columns: new[] { "submission_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_package_file_package_id_submission_id_vers",
                schema: "thesis",
                table: "final_submission_package_file",
                columns: new[] { "package_id", "submission_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_package_file_submission_file_id",
                schema: "thesis",
                table: "final_submission_package_file",
                column: "submission_file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_package_file_submission_file_id_entity_typ",
                schema: "thesis",
                table: "final_submission_package_file",
                columns: new[] { "submission_file_id", "entity_type", "submission_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_review_package_id_submission_id_package_ve",
                schema: "thesis",
                table: "final_submission_review",
                columns: new[] { "package_id", "submission_id", "package_version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_review_submission_id_package_version_no_de",
                schema: "thesis",
                table: "final_submission_review",
                columns: new[] { "submission_id", "package_version_no", "decided_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_upload_attempt_committed_package_id",
                schema: "thesis",
                table: "final_submission_upload_attempt",
                column: "committed_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_upload_attempt_docx_object_key",
                schema: "thesis",
                table: "final_submission_upload_attempt",
                column: "docx_object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_upload_attempt_group_id",
                schema: "thesis",
                table: "final_submission_upload_attempt",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_upload_attempt_pdf_object_key",
                schema: "thesis",
                table: "final_submission_upload_attempt",
                column: "pdf_object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_submission_upload_attempt_state_retry_after_utc_lease",
                schema: "thesis",
                table: "final_submission_upload_attempt",
                columns: new[] { "state", "retry_after_utc", "lease_expires_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "fk_final_submission_student_groups_group_id",
                schema: "thesis",
                table: "final_submission",
                column: "group_id",
                principalSchema: "thesis",
                principalTable: "student_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(WorkflowSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DowngradeGuardSql);
            migrationBuilder.DropForeignKey(
                name: "fk_final_submission_student_groups_group_id",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropTable(
                name: "final_submission_idempotency",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "final_submission_package_file",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "final_submission_review",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "final_submission_upload_attempt",
                schema: "thesis");

            migrationBuilder.DropTable(
                name: "final_submission_package",
                schema: "thesis");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_submission_file_archival_binding",
                schema: "thesis",
                table: "submission_file");

            migrationBuilder.DropColumn(
                name: "current_package_id",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "is_legacy",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "legacy_snapshot",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "submitted_at_utc",
                schema: "thesis",
                table: "final_submission");

            migrationBuilder.DropColumn(
                name: "submitted_by",
                schema: "thesis",
                table: "final_submission");

            // xmin is a PostgreSQL system column; removing its EF mapping must not drop it.

            migrationBuilder.AddForeignKey(
                name: "fk_final_submission_student_groups_group_id",
                schema: "thesis",
                table: "final_submission",
                column: "group_id",
                principalSchema: "thesis",
                principalTable: "student_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
