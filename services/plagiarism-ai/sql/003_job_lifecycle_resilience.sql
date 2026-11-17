-- Migration 003: Job Lifecycle Resilience & Waiting-for-Compute States (CP-05.1)
-- Adds waiting_for_compute and cancelled states, attempt tracking, heartbeat, checkpointing, and retry scheduling.

-- 1. Update status CHECK constraint on integrity.jobs
ALTER TABLE integrity.jobs DROP CONSTRAINT IF EXISTS jobs_status_check;

DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT conname
        FROM pg_constraint
        WHERE conrelid = 'integrity.jobs'::regclass
          AND contype = 'c'
          AND pg_get_constraintdef(oid) ILIKE '%status%'
    LOOP
        EXECUTE 'ALTER TABLE integrity.jobs DROP CONSTRAINT IF EXISTS ' || quote_ident(r.conname);
    END LOOP;
END $$;

ALTER TABLE integrity.jobs
    ADD CONSTRAINT jobs_status_check
    CHECK (status IN ('queued', 'processing', 'waiting_for_compute', 'completed', 'failed', 'cancelled'));

-- 2. Add resilience tracking columns to integrity.jobs
ALTER TABLE integrity.jobs
    ADD COLUMN IF NOT EXISTS attempt INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS max_attempts INTEGER NOT NULL DEFAULT 5,
    ADD COLUMN IF NOT EXISTS last_heartbeat TIMESTAMPTZ DEFAULT now(),
    ADD COLUMN IF NOT EXISTS checkpoint JSONB DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS retry_after TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS worker_id TEXT;

-- 3. Indexes for fast polling of retryable jobs and stale heartbeat reclamation
CREATE INDEX IF NOT EXISTS idx_jobs_waiting_retry
    ON integrity.jobs (status, retry_after)
    WHERE status = 'waiting_for_compute';

CREATE INDEX IF NOT EXISTS idx_jobs_stale_heartbeat
    ON integrity.jobs (status, last_heartbeat)
    WHERE status = 'processing';
