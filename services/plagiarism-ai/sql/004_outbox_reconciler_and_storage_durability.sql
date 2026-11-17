-- UGTM-Plagiarism-AI Migration 004
-- Checkpoint CP-05.2: DB Commit-Enqueue Gap, Outbox Reconciler & Storage Durability

-- 1. Ensure at most ONE active source version per document in corpus
CREATE UNIQUE INDEX IF NOT EXISTS idx_sources_single_active
    ON integrity.sources (corpus_id, source_key)
    WHERE is_active = TRUE;

-- 2. Add reconciliation telemetry columns to integrity.jobs
ALTER TABLE integrity.jobs
    ADD COLUMN IF NOT EXISTS enqueued_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS reconciled_at TIMESTAMPTZ;

-- 3. Partial indexes to accelerate reconciler queries
CREATE INDEX IF NOT EXISTS idx_jobs_reconcile_queued
    ON integrity.jobs (created_at)
    WHERE status = 'queued';

CREATE INDEX IF NOT EXISTS idx_jobs_reconcile_waiting
    ON integrity.jobs (retry_after)
    WHERE status = 'waiting_for_compute';

