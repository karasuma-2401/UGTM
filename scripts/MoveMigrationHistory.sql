DO $history_transition$
BEGIN
    IF to_regclass('shared.__ef_migrations_history') IS NOT NULL THEN
        IF to_regclass('public.__ef_migrations_history') IS NOT NULL THEN
            RAISE EXCEPTION 'Migration history exists in both shared and public. Resolve the conflict before updating.';
        END IF;
        ALTER TABLE shared.__ef_migrations_history SET SCHEMA public;
    END IF;
END;
$history_transition$;
