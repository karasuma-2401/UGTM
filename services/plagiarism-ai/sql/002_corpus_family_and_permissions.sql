-- Migration 002: Add document_family, normalized_text_sha256, and processing_permission (CP-02.3)

ALTER TABLE integrity.sources
    ADD COLUMN IF NOT EXISTS document_family TEXT,
    ADD COLUMN IF NOT EXISTS normalized_text_sha256 TEXT,
    ADD COLUMN IF NOT EXISTS processing_permission TEXT NOT NULL DEFAULT 'all';

CREATE INDEX IF NOT EXISTS idx_source_family
    ON integrity.sources (corpus_id, document_family);

ALTER TABLE integrity.jobs
    ADD COLUMN IF NOT EXISTS document_family TEXT,
    ADD COLUMN IF NOT EXISTS processing_permission TEXT NOT NULL DEFAULT 'all';

