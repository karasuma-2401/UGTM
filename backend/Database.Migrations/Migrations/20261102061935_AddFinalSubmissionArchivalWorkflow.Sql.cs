namespace UGTM.Database.Migrations.Migrations;

public partial class AddFinalSubmissionArchivalWorkflow
{
    private const string WorkflowSql = """
        ALTER TABLE thesis.final_submission ADD CONSTRAINT fk_archival_current_package
            FOREIGN KEY (current_package_id,id,current_version_no)
            REFERENCES thesis.final_submission_package(id,submission_id,version_no)
            DEFERRABLE INITIALLY DEFERRED;
        -- Existing confirmation actor FKs must preserve the historical decision on account deletion.
        ALTER TABLE thesis.final_submission DROP CONSTRAINT fk_final_submission_user_advisor_confirmed_by;
        ALTER TABLE thesis.final_submission ADD CONSTRAINT fk_final_submission_user_advisor_confirmed_by
            FOREIGN KEY (advisor_confirmed_by) REFERENCES identity."user"(id) ON DELETE RESTRICT;
        ALTER TABLE thesis.final_submission DROP CONSTRAINT fk_final_submission_user_archived_confirmed_by;
        ALTER TABLE thesis.final_submission ADD CONSTRAINT fk_final_submission_user_archived_confirmed_by
            FOREIGN KEY (archived_confirmed_by) REFERENCES identity."user"(id) ON DELETE RESTRICT;

        CREATE FUNCTION thesis.archival_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Archival history is immutable' USING ERRCODE='23514';
        END $$;
        CREATE TRIGGER archival_package_immutable BEFORE UPDATE OR DELETE ON thesis.final_submission_package
            FOR EACH ROW EXECUTE FUNCTION thesis.archival_immutable();
        CREATE TRIGGER archival_link_immutable BEFORE UPDATE OR DELETE ON thesis.final_submission_package_file
            FOR EACH ROW EXECUTE FUNCTION thesis.archival_immutable();
        CREATE TRIGGER archival_review_immutable BEFORE UPDATE OR DELETE ON thesis.final_submission_review
            FOR EACH ROW EXECUTE FUNCTION thesis.archival_immutable();
        CREATE FUNCTION thesis.archival_file_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF EXISTS (SELECT 1 FROM thesis.final_submission_package_file WHERE submission_file_id=OLD.id) THEN
                RAISE EXCEPTION 'Bound archival file is immutable' USING ERRCODE='23514';
            END IF;
            RETURN CASE WHEN TG_OP='DELETE' THEN OLD ELSE NEW END;
        END $$;
        CREATE TRIGGER archival_file_immutable BEFORE UPDATE OR DELETE ON thesis.submission_file
            FOR EACH ROW EXECUTE FUNCTION thesis.archival_file_immutable();
        CREATE FUNCTION thesis.archival_freeze() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD.status='ArchiveConfirmed' AND NEW IS DISTINCT FROM OLD THEN
                RAISE EXCEPTION 'ArchiveConfirmed is immutable' USING ERRCODE='23514';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER archival_freeze BEFORE UPDATE ON thesis.final_submission
            FOR EACH ROW EXECUTE FUNCTION thesis.archival_freeze();

        CREATE FUNCTION thesis.validate_archival_package() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE sid uuid; s thesis.final_submission; p thesis.final_submission_package;
        BEGIN
            IF TG_TABLE_NAME='final_submission' THEN sid=NEW.id; ELSE sid=NEW.submission_id; END IF;
            SELECT * INTO s FROM thesis.final_submission WHERE id=sid;
            IF NOT FOUND OR s.is_legacy THEN RETURN NULL; END IF;
            SELECT * INTO p FROM thesis.final_submission_package WHERE id=s.current_package_id;
            IF NOT FOUND OR p.submission_id<>s.id OR p.version_no<>s.current_version_no
                OR s.status NOT IN ('Submitted','NeedRevision','AdvisorConfirmed','ArchiveConfirmed')
                OR (s.abstract_vi,s.abstract_en,s.keywords_vi,s.keywords_en,s.submitted_by,s.submitted_at_utc)
                   IS DISTINCT FROM (p.abstract_vi,p.abstract_en,p.keywords_vi,p.keywords_en,p.submitted_by,p.submitted_at_utc)
                OR p.submitted_by='00000000-0000-0000-0000-000000000000'::uuid THEN
                RAISE EXCEPTION 'Invalid current archival package/projection' USING ERRCODE='23514';
            END IF;
            IF (SELECT count(*) FROM thesis.final_submission_package_file WHERE package_id=p.id)<>2
                OR (SELECT count(*) FROM thesis.final_submission_package_file l JOIN thesis.submission_file f ON f.id=l.submission_file_id
                    WHERE l.package_id=p.id AND f.entity_type='FinalSubmission' AND f.entity_id=s.id AND f.version_no=p.version_no
                      AND f.file_size BETWEEN 1 AND 20971520 AND f.checksum_sha256 ~ '^[0-9a-f]{64}$'
                      AND f.uploaded_by=p.submitted_by AND f.uploaded_at=p.submitted_at_utc
                      AND ((l.file_role='ThesisPdf' AND f.file_extension='.pdf' AND f.content_type='application/pdf')
                        OR (l.file_role='ThesisDocx' AND f.file_extension='.docx' AND f.content_type='application/vnd.openxmlformats-officedocument.wordprocessingml.document')))<>2 THEN
                RAISE EXCEPTION 'Archival package requires a valid PDF and DOCX pair' USING ERRCODE='23514';
            END IF;
            IF s.status IN ('AdvisorConfirmed','ArchiveConfirmed') AND (
                s.advisor_confirmed_by IS NULL OR s.advisor_confirmed_at IS NULL
                OR EXISTS (SELECT 1 FROM thesis.final_submission_review WHERE package_id=p.id AND action='RequestRevision')
                OR NOT EXISTS (SELECT 1 FROM thesis.final_submission_review r WHERE r.package_id=p.id
                    AND r.action='AdvisorConfirm' AND r.stage='Advisor' AND r.actor_id=s.advisor_confirmed_by
                    AND r.decided_at_utc=s.advisor_confirmed_at AND r.decided_at_utc>=p.submitted_at_utc
                    AND r.manifest_sha256=p.manifest_sha256 AND jsonb_typeof(r.defense_proof_snapshot)='object')) THEN
                RAISE EXCEPTION 'Advisor confirmation proof is missing' USING ERRCODE='23514';
            END IF;
            IF s.status='ArchiveConfirmed' AND (s.archived_confirmed_by IS NULL OR s.archived_confirmed_at IS NULL
                OR NOT EXISTS (SELECT 1 FROM thesis.final_submission_review r WHERE r.package_id=p.id
                    AND r.action='FacultyConfirm' AND r.stage='Faculty' AND r.actor_id=s.archived_confirmed_by
                    AND r.decided_at_utc=s.archived_confirmed_at AND r.decided_at_utc>=s.advisor_confirmed_at
                    AND r.manifest_sha256=p.manifest_sha256 AND jsonb_typeof(r.defense_proof_snapshot)='object')) THEN
                RAISE EXCEPTION 'Faculty confirmation proof is missing' USING ERRCODE='23514';
            END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER archival_package_complete AFTER INSERT OR UPDATE ON thesis.final_submission
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION thesis.validate_archival_package();
        CREATE CONSTRAINT TRIGGER archival_package_complete AFTER INSERT ON thesis.final_submission_package
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION thesis.validate_archival_package();
        CREATE CONSTRAINT TRIGGER archival_package_complete AFTER INSERT ON thesis.final_submission_package_file
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION thesis.validate_archival_package();
        CREATE CONSTRAINT TRIGGER archival_package_complete AFTER INSERT ON thesis.final_submission_review
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION thesis.validate_archival_package();

        SELECT setval(pg_get_serial_sequence('identity.permission','id'),
            COALESCE((SELECT MAX(id) FROM identity.permission),1),EXISTS(SELECT 1 FROM identity.permission));
        INSERT INTO identity.permission(module,permission_code,permission_name) VALUES
            ('ThesisManagement','thesis.final-submissions.read','Đọc hồ sơ lưu chiểu'),
            ('ThesisManagement','thesis.final-submissions.manage','Xác nhận hồ sơ lưu chiểu')
            ON CONFLICT(permission_code) DO NOTHING;
        INSERT INTO identity.role_permission(role_id,permission_id,assigned_at)
            SELECT r.id,p.id,TIMESTAMPTZ '2026-11-02 00:00:00Z' FROM identity.role r CROSS JOIN identity.permission p
            WHERE (r.role_name IN ('Faculty','Admin') AND p.permission_code IN ('thesis.final-submissions.read','thesis.final-submissions.manage'))
               OR (r.role_name='AcademicAffairs' AND p.permission_code='thesis.final-submissions.read')
            ON CONFLICT(role_id,permission_id) DO NOTHING;
        """;

    private const string DowngradeGuardSql = """
        LOCK TABLE thesis.final_submission,thesis.final_submission_package,thesis.final_submission_review,
            thesis.final_submission_package_file,thesis.final_submission_upload_attempt,thesis.final_submission_idempotency IN ACCESS EXCLUSIVE MODE;
        DO $$ BEGIN
            IF EXISTS(SELECT 1 FROM thesis.final_submission WHERE NOT is_legacy OR legacy_snapshot IS NOT NULL)
               OR EXISTS(SELECT 1 FROM thesis.final_submission_package)
               OR EXISTS(SELECT 1 FROM thesis.final_submission_review)
               OR EXISTS(SELECT 1 FROM thesis.final_submission_upload_attempt)
               OR EXISTS(SELECT 1 FROM thesis.final_submission_idempotency) THEN
                RAISE EXCEPTION 'Cannot downgrade archival workflow with retained business data';
            END IF;
        END $$;
        ALTER TABLE thesis.final_submission DROP CONSTRAINT fk_archival_current_package;
        DROP TRIGGER archival_package_complete ON thesis.final_submission;
        DROP TRIGGER archival_package_complete ON thesis.final_submission_package;
        DROP TRIGGER archival_package_complete ON thesis.final_submission_package_file;
        DROP TRIGGER archival_package_complete ON thesis.final_submission_review;
        DROP TRIGGER archival_package_immutable ON thesis.final_submission_package;
        DROP TRIGGER archival_link_immutable ON thesis.final_submission_package_file;
        DROP TRIGGER archival_review_immutable ON thesis.final_submission_review;
        DROP TRIGGER archival_file_immutable ON thesis.submission_file;
        DROP TRIGGER archival_freeze ON thesis.final_submission;
        DROP FUNCTION thesis.validate_archival_package(),thesis.archival_immutable(),thesis.archival_file_immutable(),thesis.archival_freeze();
        ALTER TABLE thesis.final_submission DROP CONSTRAINT fk_final_submission_user_advisor_confirmed_by;
        ALTER TABLE thesis.final_submission ADD CONSTRAINT fk_final_submission_user_advisor_confirmed_by
            FOREIGN KEY(advisor_confirmed_by) REFERENCES identity."user"(id) ON DELETE SET NULL;
        ALTER TABLE thesis.final_submission DROP CONSTRAINT fk_final_submission_user_archived_confirmed_by;
        ALTER TABLE thesis.final_submission ADD CONSTRAINT fk_final_submission_user_archived_confirmed_by
            FOREIGN KEY(archived_confirmed_by) REFERENCES identity."user"(id) ON DELETE SET NULL;
        -- Retain permissions and customized grants; reapply is idempotent.
        """;
}
