-- 005_fts_lexical_index.sql
-- Adds stored generated tsvector column and GIN index on integrity.chunks
-- to accelerate hybrid lexical candidate retrieval by >100x over pg_trgm full-table scans.

ALTER TABLE integrity.chunks
    ADD COLUMN IF NOT EXISTS tsv TSVECTOR
    GENERATED ALWAYS AS (to_tsvector('simple', content)) STORED;

CREATE INDEX IF NOT EXISTS idx_chunks_tsv
    ON integrity.chunks USING GIN (tsv);

CREATE INDEX IF NOT EXISTS idx_chunks_hnsw
    ON integrity.chunks USING hnsw (embedding vector_cosine_ops);

