using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations;

[DbContext(typeof(UGTMDbContext))]
[Migration("20261121120000_AddTransactionalCacheRevision")]
public sealed class AddTransactionalCacheRevision : Migration
{
    // Source tables for council read models, including data owned by other modules.
    private static readonly string[] SourceTables =
    [
        "identity.user", "identity.user_role", "identity.role",
        "academic.kltn_batch", "academic.score_weight_config", "academic.score_component_config",
        "topics.topic",
        "thesis.student_group", "thesis.student_group_member", "thesis.midterm_report",
        "thesis.submission_review", "thesis.final_report", "thesis.submission_file",
        "thesis.final_submission", "thesis.final_submission_package",
        "thesis.final_submission_package_file", "thesis.final_submission_review",
        "councils.council", "councils.council_assignment", "councils.council_member",
        "councils.council_minute", "councils.council_score", "councils.advisor_evaluation",
        "councils.reviewer_assignment", "councils.reviewer_evaluation", "councils.evaluation_detail",
        "councils.grading_criteria", "councils.grading_rubric_usage",
        "councils.score_summary", "councils.score_summary_component", "councils.score_summary_revision"
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE public.cache_revision (
                singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
                revision uuid NOT NULL DEFAULT gen_random_uuid(),
                last_transaction xid8 NOT NULL DEFAULT pg_current_xact_id()
            );
            INSERT INTO public.cache_revision (singleton) VALUES (true);
            CREATE FUNCTION public.advance_cache_revision() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                UPDATE public.cache_revision
                SET revision = gen_random_uuid(), last_transaction = pg_current_xact_id()
                WHERE singleton AND last_transaction IS DISTINCT FROM pg_current_xact_id();
                RETURN NULL;
            END $$;
            """);

        foreach (var source in SourceTables)
        {
            var identifiers = source.Split('.');
            var table = $"\"{identifiers[0]}\".\"{identifiers[1]}\"";
            // Acquire the shared revision row at commit, after business writes have acquired
            // their locks. Repeated events in one transaction update the revision only once.
            migrationBuilder.Sql($"""
                CREATE CONSTRAINT TRIGGER cache_revision_changed
                AFTER INSERT OR UPDATE OR DELETE ON {table}
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                EXECUTE FUNCTION public.advance_cache_revision();
                CREATE TRIGGER cache_revision_truncated AFTER TRUNCATE ON {table}
                FOR EACH STATEMENT EXECUTE FUNCTION public.advance_cache_revision();
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var source in SourceTables)
        {
            var identifiers = source.Split('.');
            var table = $"\"{identifiers[0]}\".\"{identifiers[1]}\"";
            migrationBuilder.Sql($"""
                DROP TRIGGER cache_revision_changed ON {table};
                DROP TRIGGER cache_revision_truncated ON {table};
                """);
        }
        migrationBuilder.Sql("DROP FUNCTION public.advance_cache_revision(); DROP TABLE public.cache_revision;");
    }
}
