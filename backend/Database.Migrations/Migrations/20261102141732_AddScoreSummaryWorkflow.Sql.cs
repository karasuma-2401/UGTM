namespace UGTM.Database.Migrations.Migrations;

public partial class AddScoreSummaryWorkflow
{
    private const string PreflightSql = """
        DO $$ DECLARE bad uuid; BEGIN
            SELECT id INTO bad FROM councils.score_summary WHERE status NOT IN ('Pending','Calculated','PublishedToStudent')
                OR (final_score IS NOT NULL AND final_score NOT BETWEEN 0 AND 10) LIMIT 1;
            IF bad IS NOT NULL THEN RAISE EXCEPTION 'Invalid legacy score summary %; resolve before migration',bad; END IF;
        END $$;
        """;
    private const string WorkflowSql = """
        ALTER TABLE councils.score_summary ADD CONSTRAINT fk_score_active_revision
            FOREIGN KEY(active_revision_id,id) REFERENCES councils.score_summary_revision(id,summary_id) DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE councils.score_summary ADD CONSTRAINT fk_score_published_revision
            FOREIGN KEY(published_revision_id,id) REFERENCES councils.score_summary_revision(id,summary_id) DEFERRABLE INITIALLY DEFERRED;
        CREATE FUNCTION councils.score_revision_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
            RAISE EXCEPTION 'Score calculation history is immutable' USING ERRCODE='23514';
        END $$;
        CREATE TRIGGER score_revision_immutable BEFORE UPDATE OR DELETE ON councils.score_summary_revision
            FOR EACH ROW EXECUTE FUNCTION councils.score_revision_immutable();
        CREATE TRIGGER score_component_immutable BEFORE UPDATE OR DELETE ON councils.score_summary_component
            FOR EACH ROW WHEN (OLD.revision_id IS NOT NULL) EXECUTE FUNCTION councils.score_revision_immutable();
        CREATE FUNCTION councils.score_summary_frozen() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
            IF OLD.status='PublishedToStudent' THEN RAISE EXCEPTION 'Published scores are immutable' USING ERRCODE='23514'; END IF;
            RETURN CASE WHEN TG_OP='DELETE' THEN OLD ELSE NEW END;
        END $$;
        CREATE TRIGGER score_summary_frozen BEFORE UPDATE OR DELETE ON councils.score_summary
            FOR EACH ROW EXECUTE FUNCTION councils.score_summary_frozen();
        CREATE FUNCTION councils.validate_score_revision() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE sid uuid; s councils.score_summary%ROWTYPE; r councils.score_summary_revision%ROWTYPE; BEGIN
            IF TG_TABLE_NAME='score_summary' THEN sid=NEW.id; ELSE sid=NEW.summary_id; END IF;
            SELECT * INTO s FROM councils.score_summary WHERE id=sid;
            IF NOT FOUND OR s.is_legacy THEN RETURN NULL; END IF;
            SELECT * INTO r FROM councils.score_summary_revision WHERE id=s.active_revision_id AND summary_id=s.id;
            IF NOT FOUND OR s.status NOT IN ('Calculated','PublishedToStudent') OR r.group_id<>s.group_id
                OR s.config_id IS DISTINCT FROM r.config_id OR s.config_version_no IS DISTINCT FROM r.config_version_no
                OR s.calculated_by IS DISTINCT FROM r.calculated_by OR s.calculated_at IS DISTINCT FROM r.calculated_at_utc
                OR s.input_fingerprint IS DISTINCT FROM r.input_fingerprint OR s.final_score IS DISTINCT FROM r.final_score
                OR s.total_weight IS DISTINCT FROM r.total_weight OR s.calculation_formula IS DISTINCT FROM r.calculation_formula
                OR s.calculation_config_snapshot IS DISTINCT FROM r.config_snapshot
                OR r.final_score<>round(r.final_score_raw,2) OR jsonb_typeof(r.source_snapshot)<>'array' OR jsonb_typeof(r.config_snapshot)<>'array'
                OR NOT EXISTS(SELECT 1 FROM councils.score_summary_component c WHERE c.revision_id=r.id)
                OR r.total_weight IS DISTINCT FROM (SELECT sum(c.weight) FROM councils.score_summary_component c WHERE c.revision_id=r.id AND NOT c.is_ignored_in_calculation)
                OR EXISTS(SELECT 1 FROM councils.score_summary_component c WHERE c.revision_id=r.id AND (c.source IS NULL OR c.component_name IS NULL OR c.source_references IS NULL)) THEN
                RAISE EXCEPTION 'Incomplete or inconsistent score revision for %',sid USING ERRCODE='23514';
            END IF;
            IF s.status='PublishedToStudent' AND (s.published_revision_id IS DISTINCT FROM s.active_revision_id
                OR s.published_by IS NULL OR s.published_at IS NULL OR s.published_at<r.calculated_at_utc) THEN
                RAISE EXCEPTION 'Incomplete score publication for %',sid USING ERRCODE='23514';
            END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER score_revision_complete AFTER INSERT OR UPDATE ON councils.score_summary
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION councils.validate_score_revision();
        CREATE CONSTRAINT TRIGGER score_revision_complete AFTER INSERT ON councils.score_summary_revision
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION councils.validate_score_revision();
        CREATE CONSTRAINT TRIGGER score_revision_complete AFTER INSERT ON councils.score_summary_component
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION councils.validate_score_revision();

        -- Backstop for retained/future owner writers. API writers acquire root guards before grade rows.
        -- The same parent serializes a source write with calculate/publish, including absent summaries.
        CREATE FUNCTION councils.guard_published_grade_source() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE v jsonb; versions jsonb[]; gids uuid[]; all_gids uuid[]=ARRAY[]::uuid[]; gid uuid; cid uuid; BEGIN
            versions=CASE WHEN TG_OP='DELETE' THEN ARRAY[to_jsonb(OLD)] WHEN TG_OP='INSERT' THEN ARRAY[to_jsonb(NEW)] ELSE ARRAY[to_jsonb(OLD),to_jsonb(NEW)] END;
            FOREACH v IN ARRAY versions LOOP
            gids=ARRAY[]::uuid[];
            IF TG_TABLE_NAME IN ('advisor_evaluation','reviewer_assignment','council_assignment') THEN gids=ARRAY[(v->>'group_id')::uuid];
            ELSIF TG_TABLE_NAME='reviewer_evaluation' THEN
                SELECT ARRAY[group_id] INTO gids FROM councils.reviewer_assignment WHERE id=(v->>'assignment_id')::uuid;
            ELSIF TG_TABLE_NAME IN ('council_score','council_minute') THEN
                SELECT ARRAY[group_id] INTO gids FROM councils.council_assignment WHERE id=(v->>'council_assignment_id')::uuid;
            ELSIF TG_TABLE_NAME='evaluation_detail' THEN
                SELECT ARRAY[a.group_id] INTO gids FROM councils.council_score s JOIN councils.council_assignment a ON a.id=s.council_assignment_id WHERE s.id=(v->>'evaluation_id')::uuid;
            ELSIF TG_TABLE_NAME='score_summary_component' THEN
                SELECT ARRAY[group_id] INTO gids FROM councils.score_summary WHERE id=(v->>'summary_id')::uuid;
            ELSIF TG_TABLE_NAME IN ('council','council_member') THEN
                cid=CASE WHEN TG_TABLE_NAME='council' THEN (v->>'id')::uuid ELSE (v->>'council_id')::uuid END;
                SELECT array_agg(group_id ORDER BY group_id) INTO gids FROM councils.council_assignment WHERE council_id=cid;
            END IF;
            all_gids=all_gids || COALESCE(gids,ARRAY[]::uuid[]);
            END LOOP;
            FOR gid IN SELECT DISTINCT unnest(all_gids) ORDER BY 1 LOOP
                PERFORM 1 FROM thesis.student_group WHERE id=gid FOR UPDATE;
                IF EXISTS(SELECT 1 FROM councils.score_summary WHERE group_id=gid AND status='PublishedToStudent') THEN
                    RAISE EXCEPTION 'Published grade source is frozen for %',gid USING ERRCODE='55P03';
                END IF;
            END LOOP;
            RETURN CASE WHEN TG_OP='DELETE' THEN OLD ELSE NEW END;
        END $$;
        DO $$ DECLARE t text; BEGIN
            FOREACH t IN ARRAY ARRAY['advisor_evaluation','reviewer_assignment','reviewer_evaluation','council','council_assignment','council_member','council_minute','council_score','evaluation_detail','score_summary_component'] LOOP
                EXECUTE format('CREATE TRIGGER score_source_frozen BEFORE INSERT OR UPDATE OR DELETE ON councils.%I FOR EACH ROW EXECUTE FUNCTION councils.guard_published_grade_source()',t);
            END LOOP;
        END $$;
        SELECT setval(pg_get_serial_sequence('identity.permission','id'),COALESCE((SELECT MAX(id) FROM identity.permission),1),EXISTS(SELECT 1 FROM identity.permission));
        INSERT INTO identity.permission(module,permission_code,permission_name) VALUES
            ('DefenseCouncil','councils.score-summaries.read','Đọc bảng điểm KLTN'),
            ('DefenseCouncil','councils.score-summaries.manage','Tính điểm KLTN'),
            ('DefenseCouncil','councils.score-summaries.publish','Công bố điểm KLTN') ON CONFLICT(permission_code) DO NOTHING;
        INSERT INTO identity.role_permission(role_id,permission_id,assigned_at)
            SELECT r.id,p.id,TIMESTAMPTZ '2026-11-02 00:00:00Z' FROM identity.role r CROSS JOIN identity.permission p
            WHERE (r.role_name IN ('Faculty','Admin') AND p.permission_code IN ('councils.score-summaries.read','councils.score-summaries.manage','councils.score-summaries.publish'))
                OR (r.role_name='AcademicAffairs' AND p.permission_code='councils.score-summaries.read')
            ON CONFLICT(role_id,permission_id) DO NOTHING;
        """;
    private const string DowngradeSql = """
        LOCK TABLE councils.score_summary,councils.score_summary_revision,councils.score_summary_component,councils.score_summary_idempotency IN ACCESS EXCLUSIVE MODE;
        DO $$ BEGIN
            IF EXISTS(SELECT 1 FROM councils.score_summary WHERE NOT is_legacy OR legacy_snapshot IS NOT NULL)
                OR EXISTS(SELECT 1 FROM councils.score_summary_revision) OR EXISTS(SELECT 1 FROM councils.score_summary_idempotency)
                OR EXISTS(SELECT 1 FROM councils.score_summary_component WHERE score<>round(score,4)) THEN
                RAISE EXCEPTION 'Cannot downgrade score workflow with retained business data';
            END IF;
        END $$;
        DO $$ DECLARE t text; BEGIN
            FOREACH t IN ARRAY ARRAY['advisor_evaluation','reviewer_assignment','reviewer_evaluation','council','council_assignment','council_member','council_minute','council_score','evaluation_detail','score_summary_component'] LOOP
                EXECUTE format('DROP TRIGGER score_source_frozen ON councils.%I',t);
            END LOOP;
        END $$;
        DROP TRIGGER score_revision_complete ON councils.score_summary;
        DROP TRIGGER score_revision_complete ON councils.score_summary_revision;
        DROP TRIGGER score_revision_complete ON councils.score_summary_component;
        DROP TRIGGER score_summary_frozen ON councils.score_summary;
        DROP TRIGGER score_revision_immutable ON councils.score_summary_revision;
        DROP TRIGGER score_component_immutable ON councils.score_summary_component;
        ALTER TABLE councils.score_summary DROP CONSTRAINT fk_score_active_revision;
        ALTER TABLE councils.score_summary DROP CONSTRAINT fk_score_published_revision;
        DROP FUNCTION councils.validate_score_revision(),councils.score_summary_frozen(),councils.score_revision_immutable(),councils.guard_published_grade_source();
        -- Preserve permission rows and customized grants. System xmin is never dropped.
        """;
}
