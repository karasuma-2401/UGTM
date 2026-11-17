CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE SCHEMA IF NOT EXISTS integrity;

CREATE TABLE IF NOT EXISTS integrity.jobs (
    id UUID PRIMARY KEY,
    job_type TEXT NOT NULL CHECK (job_type IN ('index', 'scan')),
    corpus_id TEXT NOT NULL,
    document_key TEXT NOT NULL,
    version INTEGER NOT NULL CHECK (version > 0),
    filename TEXT NOT NULL,
    file_path TEXT NOT NULL,
    file_sha256 TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'queued'
        CHECK (status IN ('queued', 'processing', 'completed', 'failed')),
    report JSONB,
    error_code TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (job_type, corpus_id, document_key, version)
);

CREATE TABLE IF NOT EXISTS integrity.sources (
    id BIGSERIAL PRIMARY KEY,
    corpus_id TEXT NOT NULL,
    source_key TEXT NOT NULL,
    version INTEGER NOT NULL CHECK (version > 0),
    filename TEXT NOT NULL,
    file_sha256 TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT FALSE,
    indexed_at TIMESTAMPTZ,
    UNIQUE (corpus_id, source_key, version)
);

CREATE TABLE IF NOT EXISTS integrity.chunks (
    id BIGSERIAL PRIMARY KEY,
    source_id BIGINT NOT NULL REFERENCES integrity.sources(id) ON DELETE CASCADE,
    chunk_no INTEGER NOT NULL,
    page_number INTEGER,
    start_offset INTEGER NOT NULL,
    end_offset INTEGER NOT NULL,
    content TEXT NOT NULL,
    embedding VECTOR(1024) NOT NULL,
    UNIQUE (source_id, chunk_no)
);

CREATE INDEX IF NOT EXISTS idx_source_active
    ON integrity.sources (corpus_id, is_active);
CREATE INDEX IF NOT EXISTS idx_chunks_source
    ON integrity.chunks (source_id);
CREATE INDEX IF NOT EXISTS idx_chunks_trgm
    ON integrity.chunks USING GIN (content gin_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_jobs_created
    ON integrity.jobs (created_at DESC);

CREATE INDEX IF NOT EXISTS idx_chunks_hnsw ON integrity.chunks
    USING hnsw (embedding vector_cosine_ops);

