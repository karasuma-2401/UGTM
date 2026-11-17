using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UGTM.Database.Migrations.Migrations;

[DbContext(typeof(UGTMDbContext))]
[Migration("20261108110000_GradingRubricGuard")]
public sealed class GradingRubricGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE councils.grading_rubric_usage (
                batch_id uuid PRIMARY KEY,
                used_at_utc timestamp with time zone NOT NULL DEFAULT now()
            );
            INSERT INTO councils.grading_rubric_usage (batch_id)
            SELECT DISTINCT c.batch_id FROM councils.grading_criteria c
            JOIN councils.evaluation_detail d ON d.criteria_id = c.id;

            CREATE FUNCTION councils.mark_grading_rubric_used() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE rubric_batch uuid;
            BEGIN
                SELECT batch_id INTO rubric_batch FROM councils.grading_criteria WHERE id = NEW.criteria_id;
                IF rubric_batch IS NOT NULL THEN
                    PERFORM pg_advisory_xact_lock(hashtextextended(rubric_batch::text, 8753201));
                    INSERT INTO councils.grading_rubric_usage (batch_id) VALUES (rubric_batch)
                    ON CONFLICT (batch_id) DO NOTHING;
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER mark_grading_rubric_used BEFORE INSERT OR UPDATE OF criteria_id
            ON councils.evaluation_detail FOR EACH ROW EXECUTE FUNCTION councils.mark_grading_rubric_used();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS mark_grading_rubric_used ON councils.evaluation_detail;
            DROP FUNCTION IF EXISTS councils.mark_grading_rubric_used();
            DROP TABLE IF EXISTS councils.grading_rubric_usage;
            """);
    }
}
