import json
import re
from pathlib import Path
from typing import Any

from app.db import connect

PROJECT_ROOT = Path(__file__).resolve().parent.parent
RETRIEVAL_CONFIG_PATH = PROJECT_ROOT / "configs" / "retrieval_config.json"

_LEXICAL_STOPWORDS = frozenset(
    {
        "của", "trong", "được", "các", "với", "cho", "một", "những", "này", "khi",
        "theo", "trên", "đến", "như", "hoặc", "việc", "phần", "chương", "hình", "bảng",
        "mục", "kết", "quả", "thực", "hiện", "xây", "dựng", "phát", "triển", "nghiên",
        "cứu", "ứng", "dụng", "thống", "thông", "tin", "liệu", "người", "dùng", "phương",
        "pháp", "đánh", "giá", "quá", "trình", "khả", "năng", "dựa", "vào", "qua",
        "bằng", "cùng", "nhưng", "tuy", "nhiên", "cần", "thể", "phải", "biết", "nhiều",
        "hơn", "nhất", "rất", "đang", "được", "chỉ", "còn", "tại", "bởi", "giữa",
        "sau", "trước", "dưới", "ngoài", "cũng", "đều", "mọi", "khác", "nhau", "giúp",
        "nhằm", "tăng", "giảm", "cao", "thấp", "lớn", "nhỏ", "mới", "tốt", "hệ",
        "the", "and", "for", "with", "from", "that", "this", "are", "was", "were",
        "have", "has", "had", "can", "will", "using", "based", "into", "over", "than",
    }
)
_RX_WORD_TOKEN = re.compile(r"[0-9a-zà-ỹ]{3,}")


def build_selective_tsquery(text: str, max_tokens: int = 10) -> str:
    """Build a safe, selective 2-term conjunction tsquery string for GIN tsvector retrieval.

    Avoids scanning common Vietnamese stopwords while capturing exact/near-exact
    lexical overlap in <3ms via integrity.chunks.tsv GIN index.
    """
    clean = (text or "").replace("\x00", "").casefold()
    raw_tokens = _RX_WORD_TOKEN.findall(clean)
    if not raw_tokens:
        return ""

    seen: set[str] = set()
    distinctive: list[str] = []
    fallback: list[str] = []
    for tok in raw_tokens:
        if tok in seen or not tok.isalnum():
            continue
        seen.add(tok)
        if tok in _LEXICAL_STOPWORDS:
            fallback.append(tok)
            continue
        distinctive.append(tok)

    # Prioritize technical/specific tokens (digits or longer words), keeping relative order
    specific = [t for t in distinctive if any(ch.isdigit() for ch in t) or len(t) >= 5]
    others = [t for t in distinctive if t not in specific]
    selected = (specific + others)[:max_tokens]
    if len(selected) < 2 and fallback:
        selected = (selected + fallback)[:4]

    if not selected:
        return ""
    if len(selected) == 1:
        return selected[0]

    clauses: list[str] = []
    for i in range(0, len(selected) - 1, 2):
        clauses.append(f"({selected[i]} & {selected[i + 1]})")
    if len(selected) % 2 == 1:
        clauses.append(f"({selected[-2]} & {selected[-1]})")
    return " | ".join(clauses)


def load_retrieval_config() -> dict[str, Any]:
    """Load production retrieval configuration from configs/retrieval_config.json."""
    if RETRIEVAL_CONFIG_PATH.is_file():
        try:
            return json.loads(RETRIEVAL_CONFIG_PATH.read_text(encoding="utf-8"))
        except Exception:
            pass
    return {
        "top_k_per_branch": 30,
        "fused_k": 25,
        "rrf_k": 60,
        "prefilter": {"enabled": False, "mode": "none"},
    }


def vector_literal(vector) -> str:
    # Cast ::vector ở SQL; không dựng câu SQL bằng nối chuỗi.
    return json.dumps([float(x) for x in vector], separators=(",", ":"))


def _build_filter_clause(
    exclude_source_keys: list[str] | None,
    exclude_document_family: str | None,
) -> tuple[str, list[object]]:
    extra_filters: list[str] = []
    extra_params: list[object] = []
    if exclude_source_keys:
        extra_filters.append("AND NOT (s.source_key = ANY(%s))")
        extra_params.append(list(exclude_source_keys))
    if exclude_document_family:
        extra_filters.append("AND (s.document_family IS NULL OR s.document_family != %s)")
        extra_params.append(exclude_document_family)
    filter_clause = (" " + " ".join(extra_filters)) if extra_filters else ""
    return filter_clause, extra_params


def retrieve(
    query_text: str,
    query_vector,
    corpus_id: str,
    top_k: int | None = None,
    fused_k: int | None = None,
    rrf_k: int | None = None,
    exclude_source_keys: list[str] | None = None,
    exclude_document_family: str | None = None,
    con: Any | None = None,
) -> list[dict]:
    query_text = (query_text or "").replace("\x00", "")
    cfg = load_retrieval_config()
    if top_k is None:
        top_k = int(cfg.get("top_k_per_branch", 30))
    if fused_k is None:
        fused_k = int(cfg.get("fused_k", 25))
    if rrf_k is None:
        rrf_k = int(cfg.get("rrf_k", 60))

    vector = vector_literal(query_vector)
    filter_clause, extra_params = _build_filter_clause(
        exclude_source_keys, exclude_document_family
    )

    unified_sql = f"""
        WITH dense AS (
            SELECT c.id,
                   ROW_NUMBER() OVER (ORDER BY c.embedding <=> %s::vector) AS r_dense
            FROM integrity.chunks c
            JOIN integrity.sources s ON s.id = c.source_id
            WHERE s.corpus_id = %s AND s.is_active = TRUE{filter_clause}
            ORDER BY c.embedding <=> %s::vector
            LIMIT %s
        ),
        lexical AS (
            SELECT c.id,
                   ROW_NUMBER() OVER (ORDER BY similarity(c.content, %s) DESC) AS r_lex
            FROM integrity.chunks c
            JOIN integrity.sources s ON s.id = c.source_id
            WHERE s.corpus_id = %s AND s.is_active = TRUE{filter_clause}
            ORDER BY similarity(c.content, %s) DESC
            LIMIT %s
        ),
        fused AS (
            SELECT COALESCE(d.id, l.id) AS chunk_id,
                   COALESCE(1.0 / (%s + d.r_dense), 0.0)
                   + COALESCE(1.0 / (%s + l.r_lex), 0.0) AS rrf_score
            FROM dense d
            FULL OUTER JOIN lexical l ON d.id = l.id
            ORDER BY rrf_score DESC
            LIMIT %s
        )
        SELECT c.id AS chunk_id,
               s.source_key,
               s.version AS source_version,
               s.filename AS source_filename,
               s.document_family,
               c.page_number AS source_page,
               c.start_offset AS source_start,
               c.end_offset AS source_end,
               c.content AS source_text,
               c.chunk_no AS source_chunk_no,
               f.rrf_score,
               1 - (c.embedding <=> %s::vector) AS dense_score,
               similarity(c.content, %s) AS lexical_score
        FROM fused f
        JOIN integrity.chunks c ON c.id = f.chunk_id
        JOIN integrity.sources s ON s.id = c.source_id
        ORDER BY f.rrf_score DESC
    """
    params = (
        vector,
        corpus_id,
        *extra_params,
        vector,
        top_k,
        query_text,
        corpus_id,
        *extra_params,
        query_text,
        top_k,
        rrf_k,
        rrf_k,
        fused_k,
        vector,
        query_text,
    )

    if con is not None:
        rows = con.execute(unified_sql, params).fetchall()
    else:
        with connect() as db_con:
            rows = db_con.execute(unified_sql, params).fetchall()

    results: list[dict] = []
    for row in rows:
        results.append(
            {
                "chunk_id": int(row["chunk_id"]),
                "source_key": row["source_key"],
                "source_version": int(row["source_version"]),
                "source_filename": row["source_filename"],
                "document_family": row.get("document_family"),
                "source_page": row["source_page"],
                "source_start": int(row["source_start"]),
                "source_end": int(row["source_end"]),
                "source_text": row["source_text"],
                "source_chunk_no": int(row["source_chunk_no"]),
                "rrf_score": float(row["rrf_score"]),
                "dense_score": float(row["dense_score"]),
                "lexical_score": float(row["lexical_score"]),
            }
        )
    return results


def retrieve_batch(
    query_texts: list[str],
    query_vectors: list[Any],
    corpus_id: str,
    top_k: int | None = None,
    fused_k: int | None = None,
    rrf_k: int | None = None,
    exclude_source_keys: list[str] | None = None,
    exclude_document_family: str | None = None,
    batch_size: int = 64,
) -> list[list[dict]]:
    """Retrieve candidates for multiple chunks using HNSW + GIN FTS LATERAL SQL queries."""
    if not query_texts:
        return []

    cfg = load_retrieval_config()
    if top_k is None:
        top_k = int(cfg.get("batch_top_k_per_branch", 12))
    if fused_k is None:
        fused_k = int(cfg.get("batch_fused_k", 5))
    if rrf_k is None:
        rrf_k = int(cfg.get("rrf_k", 60))

    filter_clause, extra_params = _build_filter_clause(
        exclude_source_keys, exclude_document_family
    )

    batch_sql = f"""
        WITH queries AS (
            SELECT q_idx,
                   q_text,
                   q_vec,
                   CASE
                       WHEN q_tsq_str <> '' THEN to_tsquery('simple', q_tsq_str)
                       ELSE NULL::tsquery
                   END AS q_tsq
            FROM unnest(%s::int[], %s::text[], %s::vector[], %s::text[])
                 AS u(q_idx, q_text, q_vec, q_tsq_str)
        ),
        dense AS (
            SELECT q.q_idx, d.id, d.r_dense
            FROM queries q
            CROSS JOIN LATERAL (
                SELECT c.id,
                       ROW_NUMBER() OVER (ORDER BY c.embedding <=> q.q_vec) AS r_dense
                FROM integrity.chunks c
                JOIN integrity.sources s ON s.id = c.source_id
                WHERE s.corpus_id = %s AND s.is_active = TRUE{filter_clause}
                ORDER BY c.embedding <=> q.q_vec
                LIMIT %s
            ) d
        ),
        lexical AS (
            SELECT q.q_idx, l.id, l.r_lex
            FROM queries q
            CROSS JOIN LATERAL (
                SELECT c.id,
                       ROW_NUMBER() OVER (ORDER BY ts_rank_cd(c.tsv, q.q_tsq) DESC) AS r_lex
                FROM integrity.chunks c
                JOIN integrity.sources s ON s.id = c.source_id
                WHERE q.q_tsq IS NOT NULL
                  AND c.tsv @@ q.q_tsq
                  AND s.corpus_id = %s AND s.is_active = TRUE{filter_clause}
                ORDER BY ts_rank_cd(c.tsv, q.q_tsq) DESC
                LIMIT %s
            ) l
        ),
        fused AS (
            SELECT COALESCE(d.q_idx, l.q_idx) AS q_idx,
                   COALESCE(d.id, l.id) AS chunk_id,
                   COALESCE(1.0 / (%s + d.r_dense), 0.0)
                   + COALESCE(1.0 / (%s + l.r_lex), 0.0) AS rrf_score,
                   ROW_NUMBER() OVER (
                       PARTITION BY COALESCE(d.q_idx, l.q_idx)
                       ORDER BY COALESCE(1.0 / (%s + d.r_dense), 0.0)
                              + COALESCE(1.0 / (%s + l.r_lex), 0.0) DESC
                   ) AS rrf_rank
            FROM dense d
            FULL OUTER JOIN lexical l ON d.q_idx = l.q_idx AND d.id = l.id
        )
        SELECT f.q_idx,
               c.id AS chunk_id,
               s.source_key,
               s.version AS source_version,
               s.filename AS source_filename,
               s.document_family,
               c.page_number AS source_page,
               c.start_offset AS source_start,
               c.end_offset AS source_end,
               c.content AS source_text,
               c.chunk_no AS source_chunk_no,
               f.rrf_score,
               1 - (c.embedding <=> q.q_vec) AS dense_score,
               similarity(c.content, q.q_text) AS lexical_score
        FROM fused f
        JOIN queries q ON q.q_idx = f.q_idx
        JOIN integrity.chunks c ON c.id = f.chunk_id
        JOIN integrity.sources s ON s.id = c.source_id
        WHERE f.rrf_rank <= %s
        ORDER BY f.q_idx, f.rrf_score DESC
    """

    all_results: list[list[dict]] = [[] for _ in range(len(query_texts))]
    with connect() as con:
        for start in range(0, len(query_texts), batch_size):
            end = min(start + batch_size, len(query_texts))
            q_indices = list(range(start, end))
            q_texts = [(query_texts[i] or "").replace("\x00", "") for i in q_indices]
            q_vecs = [vector_literal(query_vectors[i]) for i in q_indices]
            q_tsqs = [build_selective_tsquery(t) for t in q_texts]

            params = (
                q_indices,
                q_texts,
                q_vecs,
                q_tsqs,
                corpus_id,
                *extra_params,
                top_k,
                corpus_id,
                *extra_params,
                top_k,
                rrf_k,
                rrf_k,
                rrf_k,
                rrf_k,
                fused_k,
            )
            rows = con.execute(batch_sql, params).fetchall()
            for row in rows:
                idx = int(row["q_idx"])
                all_results[idx].append(
                    {
                        "chunk_id": int(row["chunk_id"]),
                        "source_key": row["source_key"],
                        "source_version": int(row["source_version"]),
                        "source_filename": row["source_filename"],
                        "document_family": row.get("document_family"),
                        "source_page": row["source_page"],
                        "source_start": int(row["source_start"]),
                        "source_end": int(row["source_end"]),
                        "source_text": row["source_text"],
                        "source_chunk_no": int(row["source_chunk_no"]),
                        "rrf_score": float(row["rrf_score"]),
                        "dense_score": float(row["dense_score"]),
                        "lexical_score": float(row["lexical_score"]),
                    }
                )
    return all_results
