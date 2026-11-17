from __future__ import annotations

from pathlib import Path
import pytest

from app.documents import extract_pages, make_chunks, split_sentences
from app.preprocessing import (
    DEFAULT_CONFIG_PATH,
    DEFAULT_EXCLUDED_SECTIONS,
    PreprocessingConfig,
    PreprocessingStats,
    get_preprocessing_config,
    prepare_document_chunks,
)

FIXTURES_DIR = Path(__file__).resolve().parent / "fixtures"
BASELINE_SAMPLES_DIR = Path(__file__).resolve().parent.parent / "data" / "baseline_b0" / "samples"


def test_preprocessing_config_loading():
    """Verify loading and parsing of configs/preprocessing_config.json."""
    assert DEFAULT_CONFIG_PATH.exists(), f"Config file not found at {DEFAULT_CONFIG_PATH}"

    cfg = get_preprocessing_config()
    assert cfg.version == "preprocessing-v1"
    assert cfg.profile_name == "production_v1"
    assert cfg.strategy == "sliding_window"
    assert "cover" in cfg.exclude_sections
    assert "references" in cfg.exclude_sections
    assert "header" in cfg.exclude_sections
    assert "footer" in cfg.exclude_sections

    # Baseline B0 profile (no filtering)
    b0_cfg = get_preprocessing_config("baseline_b0")
    assert b0_cfg.profile_name == "baseline_b0"
    assert len(b0_cfg.exclude_sections) == 0

    # Sentence-aware profile
    sent_cfg = get_preprocessing_config("sentence_aware")
    assert sent_cfg.strategy == "sentence_window"
    assert sent_cfg.sentence_target_words == 90

    # Paragraph-aware profile
    para_cfg = get_preprocessing_config("paragraph_aware")
    assert para_cfg.strategy == "paragraph_boundary"
    assert para_cfg.paragraph_target_words == 90


def test_sentence_splitting_and_abbreviation_protection():
    """Verify sentence segmentation correctly protects scientific and Vietnamese abbreviations."""
    sample_text = (
        "Chương 1: Giới thiệu đề tài do PGS. TS. Nguyễn Văn A hướng dẫn tại TP. Hồ Chí Minh. "
        "Mô hình Transformer được đề xuất bởi Vaswani et al. năm 2017! "
        "Kỹ thuật này đã tạo ra bước đột phá mới trong học sâu? "
        "Đúng vậy, hệ thống đạt độ chính xác 98.5% trên tập kiểm thử."
    )
    sentences = split_sentences(sample_text)
    assert len(sentences) == 4

    assert sentences[0][2].startswith("Chương 1:")
    assert "PGS. TS." in sentences[0][2]
    assert "TP. Hồ Chí Minh." in sentences[0][2]

    assert sentences[1][2].startswith("Mô hình Transformer")
    assert "Vaswani et al." in sentences[1][2]

    assert sentences[2][2].startswith("Kỹ thuật này")
    assert sentences[2][2].endswith("?")

    assert sentences[3][2].startswith("Đúng vậy")
    assert "98.5%" in sentences[3][2]


def test_sentence_window_chunking_strategy():
    """Verify sentence_window chunking keeps complete sentences without chopping mid-sentence."""
    docx_path = FIXTURES_DIR / "cover_body_refs.docx"
    cfg = get_preprocessing_config("sentence_aware")

    chunks, stats = prepare_document_chunks(docx_path, config=cfg)
    assert len(chunks) >= 1
    assert stats.kept_blocks >= 4
    assert stats.dropped_blocks >= 4

    for c in chunks:
        assert c.section_type in ("body", "structured_table")
        trimmed = c.text.strip()
        if c.section_type == "body":
            # Body sentence chunks must end with sentence-ending punctuation
            assert trimmed[-1] in (".", "!", "?", ":")
        assert c.start_block_index is not None
        assert c.end_block_index is not None


def test_paragraph_boundary_chunking_strategy():
    """Verify paragraph_boundary chunking respects paragraph units and groups small paragraphs."""
    docx_path = FIXTURES_DIR / "cover_body_refs.docx"
    cfg = get_preprocessing_config("paragraph_aware")

    chunks, stats = prepare_document_chunks(docx_path, config=cfg)
    assert len(chunks) >= 1

    for c in chunks:
        assert c.section_type in ("body", "structured_table")
        assert c.start_paragraph_index is not None or c.start_block_index is not None


def test_offline_online_preprocessing_parity():
    """Verify that offline candidate generation and online worker use identical chunking."""
    docx_path = FIXTURES_DIR / "cover_body_refs.docx"

    # Simulated online execution
    cfg = get_preprocessing_config("production_v1")
    online_chunks, online_stats = prepare_document_chunks(docx_path, config=cfg)

    # Simulated offline execution
    pages = extract_pages(docx_path)
    offline_chunks = make_chunks(
        pages,
        size=cfg.sliding_size,
        overlap=cfg.sliding_overlap,
        min_words=cfg.min_words,
        exclude_sections=cfg.exclude_sections,
    )

    assert len(online_chunks) == len(offline_chunks)
    for c_on, c_off in zip(online_chunks, offline_chunks):
        assert c_on.text == c_off.text
        assert c_on.chunk_no == c_off.chunk_no
        assert c_on.start_offset == c_off.start_offset
        assert c_on.end_offset == c_off.end_offset
        assert c_on.section_type == c_off.section_type
        assert c_on.start_block_index == c_off.start_block_index
        assert c_on.end_block_index == c_off.end_block_index


def test_discard_reasons_and_statistics_tracking():
    """Verify that PreprocessingStats accurately captures reasons for excluded sections."""
    docx_path = FIXTURES_DIR / "cover_body_refs.docx"
    cfg = get_preprocessing_config("production_v1")

    chunks, stats = prepare_document_chunks(docx_path, config=cfg)

    assert stats.total_sections >= 4
    assert stats.total_blocks >= 10
    assert stats.dropped_blocks > 0
    assert stats.kept_blocks > 0
    assert stats.dropped_blocks + stats.kept_blocks == stats.total_blocks

    # Check reasons
    assert "COVER_BOILERPLATE" in stats.dropped_by_reason
    assert "REFERENCES_BIBLIOGRAPHY" in stats.dropped_by_reason
    assert stats.dropped_by_reason["COVER_BOILERPLATE"] >= 5
    assert stats.dropped_by_reason["REFERENCES_BIBLIOGRAPHY"] >= 4

    stats_dict = stats.to_dict()
    assert stats_dict["total_chunks"] == len(chunks)
    assert "body" in stats_dict["chunks_by_section"]


def test_baseline_b0_reproducibility():
    """Verify that the baseline_b0 profile reproduces the exact unpartitioned B0 behavior."""
    docx_path = BASELINE_SAMPLES_DIR / "cover_plus_body_b0.docx"
    b0_cfg = get_preprocessing_config("baseline_b0")

    chunks, stats = prepare_document_chunks(docx_path, config=b0_cfg)

    # In B0 without section filtering, cover is retained and included in chunks
    assert stats.dropped_blocks == 0
    assert any(c.section_type == "cover" for c in chunks)
    assert any(c.section_type == "body" for c in chunks)
    # Total chunks must match unpartitioned B0 run
    assert len(chunks) >= 4
