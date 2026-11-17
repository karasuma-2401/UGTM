import logging
import re
import unicodedata

from psycopg.types.json import Jsonb

from app.alignment import align_passage_pair, group_adjacent_findings
from app.celery_app import celery_app
from app.config import settings
from app.db import (
    connect,
    get_job,
    record_job_checkpoint,
    record_job_heartbeat,
    schedule_waiting_for_compute,
    set_status,
    transition_job_status,
)
from app.features import build_features

logger = logging.getLogger(__name__)

_RX_DOT_LEADERS = re.compile(r"(?:\.\s+){5,}\.|\.{6,}")
_RX_TOC_LIST_HEADING = re.compile(
    r"^\s*(?:mục\s*lục|danh\s*sách\s*hình(?:\s*vẽ)?|danh\s*sách\s*bảng(?:\s*biểu)?|"
    r"danh\s*mục\s*hình(?:\s*vẽ|\s*ảnh)?|danh\s*mục\s*bảng(?:\s*biểu)?|"
    r"danh\s*mục\s*(?:các\s*)?từ\s*viết\s*tắt)\b",
    re.IGNORECASE,
)
_RX_BODY_START_HEADING = re.compile(
    r"^\s*(?:chương\s+\d|tóm\s*tắt(?:\s+khóa\s+luận|\s+luận\s+văn)?\b|"
    r"abstract\b|1[\.\s]+(?:giới\s*thiệu|mở\s*đầu|tổng\s*quan))",
    re.IGNORECASE,
)
_FRONTMATTER_COMPACT_PREFIXES = (
    "lờicảmơn",
    "lờicámơn",
    "lờicamđoan",
    "camđoancủatácgiả",
    "nhậnxétcủagiảngviên",
    "nhậnxétcủagiáoviên",
    "nhậnxétcủacánbộhướngdẫn",
    "nhậnxétcủangườihướngdẫn",
    "phiếunhậnxét",
    "phiếuchấmđiểm",
)
_FRONTMATTER_COMPACT_PHRASES = (
    "xingửilờicảmơn",
    "xinchânthànhcảmơn",
    "gửilờicảmơnchânthành",
    "bàytỏlòngbiếtơn",
    "gửilờitriân",
    "lờicảmơnsâusắc",
    "xincamđoan",
    "camđoanđâylàcôngtrình",
    "camđoanrằngđâylà",
    "họvàtêngiảngviênhướngdẫn",
    "họvàtêngiảngviênphảnbiện",
)


def _is_academic_boilerplate(
    text: str,
    chunk_no: int | None = None,
    page_number: int | None = None,
) -> bool:
    """Detect non-substantive academic frontmatter (acknowledgments, declarations, leaked dot-leader TOC/LOF/LOT)."""
    if not text or not text.strip():
        return True
    norm = unicodedata.normalize("NFC", text).strip()
    if _RX_DOT_LEADERS.search(norm) or _RX_TOC_LIST_HEADING.search(norm):
        return True

    compact = re.sub(r"\s+", "", norm.casefold())
    compact_head = compact.lstrip("0123456789ivxlcdm.-•:*")
    if compact_head.startswith(_FRONTMATTER_COMPACT_PREFIXES):
        return True

    in_frontmatter_zone = (
        (page_number is not None and page_number <= 15)
        or (chunk_no is not None and chunk_no <= 20)
    )
    if in_frontmatter_zone and not _RX_BODY_START_HEADING.search(norm):
        if any(p in compact for p in _FRONTMATTER_COMPACT_PHRASES):
            return True
        if ("khótránhkhỏi" in compact or "khócóthểtránhkhỏi" in compact) and "thiếusót" in compact:
            return True
        if any(w in compact for w in ("nhómtácgiả", "chúngemxin", "emxin", "tôixin")) and any(
            w in compact for w in ("cảmơn", "cámơn", "triân", "biếtơn")
        ):
            return True
        if (
            len(norm) <= 180
            and "ngày" in compact
            and "tháng" in compact
            and "năm" in compact
            and any(
                w in compact
                for w in ("nhómtácgiả", "tácgiả", "sinhviênthựchiện", "nhómthựchiện", "ngườicamđoan")
            )
        ):
            return True

    return False


def _filter_scannable_chunks(chunks: list) -> list:
    """Filter frontmatter boilerplate and leaked TOC/LOF/LOT chunks while preserving page continuity."""
    if not chunks:
        return []
    frontmatter_pages: set[int] = set()
    for c in chunks:
        p = getattr(c, "page_number", None)
        if p is not None and p <= 15:
            norm = unicodedata.normalize("NFC", c.text or "").strip()
            compact_head = re.sub(r"\s+", "", norm.casefold()).lstrip("0123456789ivxlcdm.-•:*")
            if compact_head.startswith(_FRONTMATTER_COMPACT_PREFIXES):
                frontmatter_pages.add(int(p))

    kept = []
    for c in chunks:
        p = getattr(c, "page_number", None)
        cno = getattr(c, "chunk_no", None)
        norm = unicodedata.normalize("NFC", c.text or "").strip()
        if _is_academic_boilerplate(norm, cno, p):
            continue
        if p is not None and int(p) in frontmatter_pages and not _RX_BODY_START_HEADING.search(norm):
            continue
        kept.append(c)

    return kept if kept else list(chunks)


class StorageTransientError(Exception):
    """Transient storage download failure (network glitch, timeout) eligible for retry."""
    pass


def parse_job(job):
    import requests
    from pathlib import Path
    from app.preprocessing import prepare_document_chunks
    from app.storage import (
        StorageError,
        StorageFileNotFoundError,
        download_to_temp_file,
    )

    try:
        with download_to_temp_file(
            job["file_path"],
            expected_sha256=job.get("file_sha256"),
            suffix=Path(job.get("filename", "")).suffix,
        ) as temp_path:
            if temp_path.stat().st_size == 0:
                raise ValueError("EMPTY_TEXT")
            try:
                chunks, stats = prepare_document_chunks(str(temp_path))
            except ValueError:
                raise
            except Exception as exc:
                logger.warning(
                    "Document parsing failed for file %s: %s",
                    job.get("filename"),
                    exc,
                )
                raise ValueError("CORRUPTED_DOCUMENT") from exc

            if not chunks:
                raise ValueError("EMPTY_TEXT")
            logger.info(
                "Parsed job %s: %d chunks generated (kept_blocks=%d, dropped_blocks=%d)",
                job.get("id"),
                len(chunks),
                stats.kept_blocks,
                stats.dropped_blocks,
            )
            return chunks
    except StorageFileNotFoundError as exc:
        logger.warning("Job %s file not found in storage: %s", job.get("id"), exc)
        raise ValueError("STORAGE_FILE_NOT_FOUND") from exc
    except StorageError as exc:
        if getattr(exc, "code", None) == "FILE_CHECKSUM_MISMATCH":
            logger.warning("Job %s checksum mismatch: %s", job.get("id"), exc)
            raise ValueError("FILE_CHECKSUM_MISMATCH") from exc
        logger.warning("Job %s storage error: %s", job.get("id"), exc)
        raise ValueError("STORAGE_ERROR") from exc
    except requests.RequestException as exc:
        logger.warning("Job %s transient network error downloading file: %s", job.get("id"), exc)
        raise StorageTransientError(f"Transient download error: {exc}") from exc


@celery_app.task(name="app.tasks.index_source")
def index_source(job_id: str):
    import os
    from app.inference import InferenceError
    from app.models import encode_texts
    from app.observability import StageTimer
    from app.search import vector_literal

    timer = StageTimer(job_id=job_id)

    job = get_job(job_id)
    if not job:
        logger.warning("Index job %s not found", job_id)
        return
    if job.get("status") == "cancelled":
        logger.info("Index job %s was cancelled; skipping execution.", job_id)
        return

    job_attempt = job.get("attempt") or 1
    worker_id = f"worker-{os.getpid()}"

    transition_job_status(
        job_id,
        "processing",
        from_status=["queued", "waiting_for_compute", "processing"],
        attempt=job_attempt,
        worker_id=worker_id,
    )
    record_job_heartbeat(job_id, worker_id)

    try:
        try:
            with timer.time_stage("parse"):
                chunks = parse_job(job)
        except StorageTransientError as exc:
            logger.warning(
                "Index job %s transient storage error during download: %s", job_id, exc
            )
            sched = schedule_waiting_for_compute(
                job_id,
                error_code="STORAGE_TRANSIENT_ERROR",
                checkpoint={"phase": "download_failed"},
            )
            if sched.get("action") == "retry":
                index_source.apply_async(
                    args=[job_id], countdown=int(sched.get("backoff_seconds", 10))
                )
            return

        record_job_checkpoint(job_id, "parsed", {"chunks_count": len(chunks)})

        try:
            with timer.time_stage("encode", {"chunks_count": len(chunks)}):
                embeddings = encode_texts([x.text for x in chunks], batch_size=64)
        except InferenceError as exc:
            logger.warning(
                "Index job %s GPU inference failed during encoding (%s): %s",
                job_id,
                getattr(exc, "error_code", "GPU_UNAVAILABLE"),
                exc,
            )
            sched = schedule_waiting_for_compute(
                job_id,
                error_code=getattr(exc, "error_code", "GPU_UNAVAILABLE"),
                checkpoint={"phase": "parsed", "chunks_count": len(chunks)},
            )
            if sched.get("action") == "retry":
                index_source.apply_async(
                    args=[job_id], countdown=int(sched.get("backoff_seconds", 10))
                )
            return

        from app.documents import resolve_source_display_name

        display_filename = resolve_source_display_name(
            job["document_key"],
            job["filename"],
            job.get("file_path"),
        )
        with timer.time_stage("index_db"):
            with connect() as con:
                row = con.execute(
                    """
                    INSERT INTO integrity.sources
                      (corpus_id, source_key, version, filename, file_sha256)
                    VALUES (%s,%s,%s,%s,%s)
                    ON CONFLICT (corpus_id, source_key, version)
                    DO UPDATE SET filename = EXCLUDED.filename,
                                  file_sha256 = EXCLUDED.file_sha256
                    RETURNING id
                    """,
                    (
                        job["corpus_id"],
                        job["document_key"],
                        job["version"],
                        display_filename,
                        job["file_sha256"],
                    ),
                ).fetchone()
                source_id = row["id"]
                con.execute(
                    "DELETE FROM integrity.chunks WHERE source_id = %s",
                    (source_id,),
                )
                if chunks:
                    chunk_nos = [int(c.chunk_no) for c in chunks]
                    page_nums = [
                        int(c.page_number) if c.page_number is not None else None
                        for c in chunks
                    ]
                    start_offsets = [int(c.start_offset) for c in chunks]
                    end_offsets = [int(c.end_offset) for c in chunks]
                    contents = [(c.text or "").replace("\x00", "") for c in chunks]
                    vec_literals = [vector_literal(v) for v in embeddings]
                    con.execute(
                        """
                        INSERT INTO integrity.chunks
                          (source_id, chunk_no, page_number, start_offset,
                           end_offset, content, embedding)
                        SELECT %s,
                               unnest(%s::int[]),
                               unnest(%s::int[]),
                               unnest(%s::int[]),
                               unnest(%s::int[]),
                               unnest(%s::text[]),
                               unnest(%s::vector[])
                        """,
                        (
                            source_id,
                            chunk_nos,
                            page_nums,
                            start_offsets,
                            end_offsets,
                            contents,
                            vec_literals,
                        ),
                    )
                # Active versioning: kiểm tra phiên bản active hiện tại trong corpus
                active_row = con.execute(
                    """
                    SELECT id, version FROM integrity.sources
                    WHERE corpus_id = %s AND source_key = %s AND is_active = TRUE
                    """,
                    (job["corpus_id"], job["document_key"]),
                ).fetchone()

                current_active_version = int(active_row["version"]) if active_row else 0
                job_version = int(job["version"])

                if job_version >= current_active_version:
                    # Phiên bản này mới hơn hoặc bằng bản đang active: kích hoạt nó
                    con.execute(
                        """
                        UPDATE integrity.sources SET is_active = FALSE
                        WHERE corpus_id = %s AND source_key = %s AND id <> %s
                        """,
                        (job["corpus_id"], job["document_key"], source_id),
                    )
                    con.execute(
                        """
                        UPDATE integrity.sources
                        SET is_active = TRUE, indexed_at = now()
                        WHERE id = %s
                        """,
                        (source_id,),
                    )
                    logger.info(
                        "Source %s v%d activated (previous active: v%d)",
                        job["document_key"],
                        job_version,
                        current_active_version,
                    )
                else:
                    # Phiên bản cũ hơn: index thành công nhưng không kích hoạt đè lên bản mới hơn
                    con.execute(
                        """
                        UPDATE integrity.sources
                        SET is_active = FALSE, indexed_at = now()
                        WHERE id = %s
                        """,
                        (source_id,),
                    )
                    logger.info(
                        "Source %s v%d indexed as inactive (newer v%d already active)",
                        job["document_key"],
                        job_version,
                        current_active_version,
                    )

                res = con.execute(
                    """
                    UPDATE integrity.jobs
                    SET status = 'completed', updated_at = now(),
                        last_heartbeat = now(), report = %s
                    WHERE id = %s::uuid AND status = 'processing' AND attempt = %s
                    """,
                    (
                        Jsonb(
                            {
                                "indexed_chunks": len(chunks),
                                "source_id": job["document_key"],
                                "is_active": job_version >= current_active_version,
                                "execution_telemetry": timer.to_telemetry_dict(),
                            }
                        ),
                        job_id,
                        job_attempt,
                    ),
                )
                if res.rowcount == 0:
                    logger.warning(
                        "Index job %s: completion update skipped (attempt %s superseded or job cancelled).",
                        job_id,
                        job_attempt,
                    )
    except ValueError as exc:
        code = str(exc)
        known_codes = {
            "CORPUS_EMPTY",
            "EMPTY_TEXT",
            "TOO_MANY_PAGES",
            "DOCUMENT_TOO_LONG",
            "CORRUPTED_DOCUMENT",
            "UNSUPPORTED_FILE_FORMAT",
            "STORAGE_FILE_NOT_FOUND",
            "FILE_CHECKSUM_MISMATCH",
            "STORAGE_ERROR",
        }
        err_code = code if code in known_codes else "INDEX_ERROR"
        logger.warning("Index job %s validation failed (%s): %s", job_id, err_code, exc)
        set_status(job_id, "failed", err_code)
    except Exception:
        logger.exception("Index job failed: %s", job_id)
        set_status(job_id, "failed", "INDEX_ERROR")


@celery_app.task(name="app.tasks.scan_document")
def scan_document(job_id: str):
    import os
    from app.classifier import score_candidates_batch
    from app.inference import InferenceError
    from app.models import encode_texts, rerank_pairs
    from app.observability import StageTimer
    from app.search import load_retrieval_config, retrieve, retrieve_batch

    timer = StageTimer(job_id=job_id)

    job = get_job(job_id)
    if not job:
        logger.warning("Scan job %s not found", job_id)
        return
    if job.get("status") == "cancelled":
        logger.info("Scan job %s was cancelled; skipping execution.", job_id)
        return

    job_attempt = job.get("attempt") or 1
    worker_id = f"worker-{os.getpid()}"

    transition_job_status(
        job_id,
        "processing",
        from_status=["queued", "waiting_for_compute", "processing"],
        attempt=job_attempt,
        worker_id=worker_id,
    )
    record_job_heartbeat(job_id, worker_id)

    try:
        with connect() as con:
            count = con.execute(
                """
                SELECT count(*) AS n FROM integrity.sources
                WHERE corpus_id = %s AND is_active = TRUE
                """,
                (job["corpus_id"],),
            ).fetchone()["n"]
        if count == 0:
            raise ValueError("CORPUS_EMPTY")

        try:
            with timer.time_stage("parse"):
                query_chunks = parse_job(job)
        except StorageTransientError as exc:
            logger.warning(
                "Scan job %s transient storage error during download: %s", job_id, exc
            )
            sched = schedule_waiting_for_compute(
                job_id,
                error_code="STORAGE_TRANSIENT_ERROR",
                checkpoint={"phase": "download_failed"},
            )
            if sched.get("action") == "retry":
                scan_document.apply_async(
                    args=[job_id], countdown=int(sched.get("backoff_seconds", 10))
                )
            return

        scannable_chunks = _filter_scannable_chunks(query_chunks)
        record_job_checkpoint(
            job_id,
            "parsed",
            {
                "chunks_count": len(query_chunks),
                "scannable_chunks_count": len(scannable_chunks),
            },
        )

        try:
            with timer.time_stage("encode", {"chunks_count": len(scannable_chunks)}):
                vectors = encode_texts([x.text for x in scannable_chunks], batch_size=64)
        except InferenceError as exc:
            logger.warning(
                "Scan job %s GPU inference failed during encoding (%s): %s",
                job_id,
                getattr(exc, "error_code", "GPU_UNAVAILABLE"),
                exc,
            )
            sched = schedule_waiting_for_compute(
                job_id,
                error_code=getattr(exc, "error_code", "GPU_UNAVAILABLE"),
                checkpoint={"phase": "parsed", "chunks_count": len(query_chunks)},
            )
            if sched.get("action") == "retry":
                scan_document.apply_async(
                    args=[job_id], countdown=int(sched.get("backoff_seconds", 10))
                )
            return

        all_findings: list[dict] = []
        exclude_source_keys = [job["document_key"]] if job.get("document_key") else None
        exclude_document_family = job.get("document_family")

        # Lựa chọn ứng viên theo configs/retrieval_config.json (CP-04.1)
        retrieval_cfg = load_retrieval_config()
        prefilter_cfg = retrieval_cfg.get("prefilter", {})
        prefilter_enabled = prefilter_cfg.get("enabled", False)
        min_dense = float(prefilter_cfg.get("min_dense_score", 0.48))
        min_lex = float(prefilter_cfg.get("min_lexical_score", 0.25))

        candidates_to_rerank: list[tuple] = []
        with timer.time_stage("retrieve"):
            if hasattr(retrieve, "_mock_name"):
                batch_results = [
                    retrieve(
                        query_chunk.text,
                        vector,
                        job["corpus_id"],
                        exclude_source_keys=exclude_source_keys,
                        exclude_document_family=exclude_document_family,
                    )
                    for query_chunk, vector in zip(scannable_chunks, vectors)
                ]
            else:
                batch_results = retrieve_batch(
                    [c.text for c in scannable_chunks],
                    vectors,
                    job["corpus_id"],
                    exclude_source_keys=exclude_source_keys,
                    exclude_document_family=exclude_document_family,
                )

            for query_chunk, candidates in zip(scannable_chunks, batch_results):
                if not candidates:
                    continue
                for item in candidates:
                    if _is_academic_boilerplate(
                        item.get("source_text", ""),
                        item.get("source_chunk_no"),
                        item.get("source_page"),
                    ):
                        continue
                    dense = float(item["dense_score"])
                    lexical = float(item["lexical_score"])
                    # Cổng tiền lọc thông minh trước GPU Reranker: giữ 100% cặp trùng thực sự
                    # (kể cả diễn giải sâu / song ngữ) nhưng loại bỏ nhiễu công thức và đoạn lệch chủ đề.
                    if prefilter_enabled:
                        keep = dense >= min_dense or lexical >= min_lex
                    else:
                        keep = (
                            dense >= 0.65
                            or lexical >= 0.21
                            or (dense >= 0.53 and lexical >= 0.16)
                        )
                    if keep:
                        candidates_to_rerank.append((query_chunk, item))

        record_job_checkpoint(
            job_id, "retrieved", {"candidates_count": len(candidates_to_rerank)}
        )
        record_job_heartbeat(job_id, worker_id)

        if candidates_to_rerank:
            pairs = [
                [chunk.text, item["source_text"]]
                for chunk, item in candidates_to_rerank
            ]
            try:
                with timer.time_stage("rerank", {"pairs_count": len(candidates_to_rerank)}):
                    scores = rerank_pairs(pairs, batch_size=64)
            except InferenceError as exc:
                logger.warning(
                    "Scan job %s GPU inference failed during reranking (%s): %s",
                    job_id,
                    getattr(exc, "error_code", "GPU_UNAVAILABLE"),
                    exc,
                )
                sched = schedule_waiting_for_compute(
                    job_id,
                    error_code=getattr(exc, "error_code", "GPU_UNAVAILABLE"),
                    checkpoint={"phase": "retrieved", "candidates_count": len(candidates_to_rerank)},
                )
                if sched.get("action") == "retry":
                    scan_document.apply_async(
                        args=[job_id], countdown=int(sched.get("backoff_seconds", 10))
                    )
                return

            with timer.time_stage("align_and_citation"):
                from app.citation import detect_citation_context

                features_list = [
                    build_features(
                        query_chunk.text,
                        item["source_text"],
                        float(item["dense_score"]),
                        float(score),
                    )
                    for (query_chunk, item), score in zip(candidates_to_rerank, scores)
                ]
                batch_clf_results = score_candidates_batch(features_list)

                max_sources_per_chunk = 3
                best_per_chunk_source: dict[int, dict[str, tuple]] = {}

                for idx, ((query_chunk, item), raw_score, features) in enumerate(
                    zip(candidates_to_rerank, scores, features_list)
                ):
                    score = float(raw_score)
                    lexical = float(item["lexical_score"])
                    dense = float(item["dense_score"])
                    result = (
                        batch_clf_results[idx]
                        if batch_clf_results is not None
                        else None
                    )
                    if result is None:
                        # Heuristic demo
                        review = lexical >= 0.65 or (dense >= 0.68 and score >= 0.67)
                        decision_source = "heuristic"
                    else:
                        # Multi-Signal Coherence Guard:
                        # 1. Yêu cầu điểm phân loại đạt ngưỡng sàn vùng xám (>= 0.30) hoặc near-exact.
                        # 2. Yêu cầu tín hiệu đồng thuận từ từ vựng hoặc ngữ nghĩa + tỷ lệ độ dài hợp lý
                        #    để loại bỏ hoàn toàn báo động giả trên công thức toán / mảnh câu cụt.
                        effective_threshold = max(float(result["threshold"]), 0.30)
                        signal_coherent = (score >= 0.33 or lexical >= 0.35) and (
                            lexical >= 0.20
                            or (
                                dense >= 0.54
                                and (features["length_ratio"] >= 0.25 or lexical >= 0.12)
                            )
                        )
                        review = (
                            lexical >= 0.92
                            or (
                                signal_coherent
                                and (
                                    result["score"] >= effective_threshold
                                    or result.get("needs_human_review", False)
                                )
                            )
                        )
                        decision_source = (
                            "near_exact_override"
                            if lexical >= 0.92
                            else result.get("decision_source", "custom_classifier")
                        )
                    if not review:
                        continue

                    cno = int(query_chunk.chunk_no)
                    skey = str(item.get("source_key", ""))
                    rank_key = (
                        float(result["score"]) if result else score,
                        score,
                        dense,
                        lexical,
                    )
                    chunk_bucket = best_per_chunk_source.setdefault(cno, {})
                    prev = chunk_bucket.get(skey)
                    if prev is None or rank_key > prev[0]:
                        chunk_bucket[skey] = (
                            rank_key,
                            query_chunk,
                            item,
                            score,
                            features,
                            result,
                            decision_source,
                        )

                for cno in sorted(best_per_chunk_source.keys()):
                    ranked_candidates = sorted(
                        best_per_chunk_source[cno].values(),
                        key=lambda entry: entry[0],
                        reverse=True,
                    )
                    kept_for_chunk = 0
                    for (
                        _,
                        query_chunk,
                        item,
                        score,
                        features,
                        result,
                        decision_source,
                    ) in ranked_candidates:
                        if kept_for_chunk >= max_sources_per_chunk:
                            break

                        spans = align_passage_pair(
                            query_text=query_chunk.text,
                            source_text=item.get("source_text", ""),
                            query_base_offset=query_chunk.start_offset,
                            source_base_offset=int(item.get("source_start", 0)),
                            source_key=str(item.get("source_key", "")),
                            source_version=int(item.get("source_version", 1)),
                            source_filename=str(item.get("source_filename", "")),
                        )
                        dense = float(item["dense_score"])
                        lexical = float(item["lexical_score"])
                        # Span-Verified Evidence Gate:
                        # Yêu cầu có ít nhất 1 span câu gióng hàng thực tế, HOẶC đồng thuận
                        # cực cao cho trường hợp diễn giải sâu / dịch thuật song ngữ.
                        span_verified = (
                            bool(spans)
                            or (dense >= 0.75 and score >= 0.90 and lexical >= 0.25)
                            or (dense >= 0.78 and score >= 0.94)
                        )
                        if not span_verified:
                            continue

                        cit_ctx = detect_citation_context(
                            query_text=query_chunk.text,
                            source_metadata={
                                "source_key": str(item.get("source_key", "")),
                                "source_version": int(item.get("source_version", 1)),
                                "source_filename": str(item.get("source_filename", "")),
                                "document_family": str(item.get("document_family", "")),
                            },
                        )
                        all_findings.append(
                            {
                                "submission_chunk_no": query_chunk.chunk_no,
                                "submission_page": query_chunk.page_number,
                                "submission_start": query_chunk.start_offset,
                                "submission_end": query_chunk.end_offset,
                                "submission_text": query_chunk.text,
                                **item,
                                "reranker_score": round(float(score), 4),
                                "classifier_score": (
                                    round(result["score"], 4) if result else None
                                ),
                                "decision_source": decision_source,
                                "citation_status": cit_ctx.status,
                                "citation_context": cit_ctx.to_dict(),
                                "feature_values": features,
                                "review_required": True,
                                "aligned_spans": [s.to_dict() for s in spans] if spans else None,
                            }
                        )
                        kept_for_chunk += 1

        if settings.classifier_enabled:
            from app.classifier import load_classifier

            _, classifier_manifest = load_classifier()
            classifier_version = classifier_manifest["model_version"]
            detector_mode = "custom_classifier"
        else:
            classifier_version = None
            detector_mode = "heuristic"

        from app.reports import build_report_v3

        with timer.time_stage("report_generation"):
            report = build_report_v3(
                job_id=job_id,
                job=job,
                query_chunks=query_chunks,
                all_findings=all_findings,
                preprocessing_profile="production_v1",
                corpus_snapshot_id="corpus-v2-73fams",
                classifier_version=classifier_version,
                detector_mode=detector_mode,
                execution_telemetry=timer.to_telemetry_dict(),
            )
        with connect() as con:
            res = con.execute(
                """
                UPDATE integrity.jobs
                SET status = 'completed', report = %s, updated_at = now(), last_heartbeat = now()
                WHERE id = %s::uuid AND status = 'processing' AND attempt = %s
                """,
                (Jsonb(report), job_id, job_attempt),
            )
            if res.rowcount == 0:
                logger.warning(
                    "Scan job %s: completion update skipped (attempt %s superseded or job cancelled).",
                    job_id,
                    job_attempt,
                )
    except ValueError as exc:
        code = str(exc)
        known_codes = {
            "CORPUS_EMPTY",
            "EMPTY_TEXT",
            "TOO_MANY_PAGES",
            "DOCUMENT_TOO_LONG",
            "CORRUPTED_DOCUMENT",
            "UNSUPPORTED_FILE_FORMAT",
            "STORAGE_FILE_NOT_FOUND",
            "FILE_CHECKSUM_MISMATCH",
            "STORAGE_ERROR",
        }
        err_code = code if code in known_codes else "SCAN_ERROR"
        logger.warning("Scan job %s validation failed (%s): %s", job_id, err_code, exc)
        set_status(job_id, "failed", err_code)
    except Exception:
        logger.exception("Scan job failed: %s", job_id)
        set_status(job_id, "failed", "SCAN_ERROR")
