"""Unified Preprocessing and Section Filtering Engine for UGTM Plagiarism AI (CP-02.2).

Ensures 100% parity between offline annotation candidate creation and online worker index/scan:
- Unified configuration loaded from configs/preprocessing_config.json
- Block-level section filtering with explicit discard reason tracking
- Multi-strategy chunking: sliding_window, sentence_window, paragraph_boundary
- Reversible baseline B0 reproduction support
"""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal

from app.documents import (
    Chunk,
    DocumentBlock,
    TextPage,
    extract_blocks,
    extract_pages,
    make_chunks,
)

logger = logging.getLogger(__name__)

PROJECT_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_CONFIG_PATH = PROJECT_ROOT / "configs" / "preprocessing_config.json"

DEFAULT_EXCLUDED_SECTIONS = {
    "cover",
    "table_of_contents",
    "abbreviations",
    "references",
    "appendix",
    "header",
    "footer",
    "empty",
}

DISCARD_REASON_MAP = {
    "cover": "COVER_BOILERPLATE",
    "table_of_contents": "TOC_BOILERPLATE",
    "abbreviations": "ABBREVIATIONS_BOILERPLATE",
    "references": "REFERENCES_BIBLIOGRAPHY",
    "appendix": "APPENDIX_EXCLUDED",
    "header": "HEADER_REPETITION",
    "footer": "FOOTER_REPETITION",
    "empty": "EMPTY_BLOCK_OR_PAGE",
}


@dataclass
class PreprocessingConfig:
    version: str = "preprocessing-v1"
    profile_name: str = "production_v1"
    strategy: Literal["sliding_window", "sentence_window", "paragraph_boundary"] = "sliding_window"
    exclude_sections: set[str] = field(default_factory=lambda: set(DEFAULT_EXCLUDED_SECTIONS))
    sliding_size: int = 90
    sliding_overlap: int = 20
    min_words: int = 8
    sentence_target_words: int = 90
    sentence_overlap_sentences: int = 1
    sentence_max_words: int = 140
    paragraph_target_words: int = 90
    paragraph_max_words: int = 150
    track_discard_reasons: bool = True

    def to_dict(self) -> dict[str, Any]:
        return {
            "version": self.version,
            "profile_name": self.profile_name,
            "strategy": self.strategy,
            "exclude_sections": sorted(self.exclude_sections),
            "sliding_window": {
                "size": self.sliding_size,
                "overlap": self.sliding_overlap,
                "min_words": self.min_words,
            },
            "sentence_window": {
                "target_words": self.sentence_target_words,
                "overlap_sentences": self.sentence_overlap_sentences,
                "min_words": self.min_words,
                "max_words": self.sentence_max_words,
            },
            "paragraph_boundary": {
                "target_words": self.paragraph_target_words,
                "min_words": self.min_words,
                "max_words": self.paragraph_max_words,
            },
            "track_discard_reasons": self.track_discard_reasons,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any], profile_name: str | None = None) -> PreprocessingConfig:
        version = data.get("version", "preprocessing-v1")
        profiles = data.get("profiles", {})
        target_profile = profile_name or data.get("default_profile", "production_v1")

        profile_data = profiles.get(target_profile, {})
        strategy = profile_data.get("strategy", data.get("chunking", {}).get("strategy", "sliding_window"))
        exclude_list = profile_data.get(
            "exclude_sections",
            data.get("section_filtering", {}).get("exclude_sections", list(DEFAULT_EXCLUDED_SECTIONS)),
        )

        sliding_cfg = data.get("chunking", {}).get("sliding_window", {})
        sentence_cfg = data.get("chunking", {}).get("sentence_window", {})
        para_cfg = data.get("chunking", {}).get("paragraph_boundary", {})

        return cls(
            version=version,
            profile_name=target_profile,
            strategy=strategy,
            exclude_sections=set(exclude_list),
            sliding_size=profile_data.get("size", sliding_cfg.get("size", 90)),
            sliding_overlap=profile_data.get("overlap", sliding_cfg.get("overlap", 20)),
            min_words=profile_data.get("min_words", sliding_cfg.get("min_words", 8)),
            sentence_target_words=profile_data.get("target_words", sentence_cfg.get("target_words", 90)),
            sentence_overlap_sentences=profile_data.get("overlap_sentences", sentence_cfg.get("overlap_sentences", 1)),
            sentence_max_words=profile_data.get("max_words", sentence_cfg.get("max_words", 140)),
            paragraph_target_words=profile_data.get("target_words", para_cfg.get("target_words", 90)),
            paragraph_max_words=profile_data.get("max_words", para_cfg.get("max_words", 150)),
            track_discard_reasons=data.get("section_filtering", {}).get("track_discard_reasons", True),
        )


@dataclass
class PreprocessingStats:
    total_sections: int = 0
    total_blocks: int = 0
    kept_blocks: int = 0
    dropped_blocks: int = 0
    dropped_by_reason: dict[str, int] = field(default_factory=dict)
    total_words: int = 0
    total_chunks: int = 0
    chunks_by_section: dict[str, int] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return {
            "total_sections": self.total_sections,
            "total_blocks": self.total_blocks,
            "kept_blocks": self.kept_blocks,
            "dropped_blocks": self.dropped_blocks,
            "dropped_by_reason": self.dropped_by_reason,
            "total_words": self.total_words,
            "total_chunks": self.total_chunks,
            "chunks_by_section": self.chunks_by_section,
        }


def get_preprocessing_config(
    profile_name: str | None = None,
    config_path: Path | str | None = None,
) -> PreprocessingConfig:
    """Load unified preprocessing config from JSON file or return default profile."""
    path = Path(config_path) if config_path else DEFAULT_CONFIG_PATH
    if path.is_file():
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
            return PreprocessingConfig.from_dict(data, profile_name=profile_name)
        except Exception as exc:
            logger.warning("Failed to load preprocessing config from %s: %s; using defaults", path, exc)

    # Defaults fallback
    if profile_name == "baseline_b0":
        return PreprocessingConfig(
            profile_name="baseline_b0",
            strategy="sliding_window",
            exclude_sections=set(),  # Baseline B0 did not filter sections
        )
    return PreprocessingConfig(profile_name=profile_name or "production_v1")


def prepare_document_chunks(
    document_input: str | Path | list[TextPage],
    config: PreprocessingConfig | None = None,
) -> tuple[list[Chunk], PreprocessingStats]:
    """Unified entry point to extract, filter, and chunk a document.

    Ensures 100% consistency across offline tasks and online Celery workers.
    """
    if config is None:
        config = get_preprocessing_config()

    if isinstance(document_input, (str, Path)):
        pages = extract_pages(document_input)
    else:
        pages = document_input

    stats = PreprocessingStats(total_sections=len(pages))
    kept_pages: list[TextPage] = []

    for page in pages:
        block_count = len(page.blocks) if page.blocks else 1
        stats.total_blocks += block_count
        word_count = len(page.text.split())
        stats.total_words += word_count

        if page.section_type in config.exclude_sections:
            stats.dropped_blocks += block_count
            reason = DISCARD_REASON_MAP.get(page.section_type, f"EXCLUDED_{page.section_type.upper()}")
            stats.dropped_by_reason[reason] = stats.dropped_by_reason.get(reason, 0) + block_count
        else:
            stats.kept_blocks += block_count
            kept_pages.append(page)

    # Generate chunks with the configured strategy
    chunks = make_chunks(
        kept_pages,
        size=config.sliding_size,
        overlap=config.sliding_overlap,
        min_words=config.min_words,
        strategy=config.strategy,
        target_words=(
            config.sentence_target_words
            if config.strategy == "sentence_window"
            else config.paragraph_target_words
        ),
        overlap_sentences=config.sentence_overlap_sentences,
        max_words=(
            config.sentence_max_words
            if config.strategy == "sentence_window"
            else config.paragraph_max_words
        ),
    )

    stats.total_chunks = len(chunks)
    for c in chunks:
        stats.chunks_by_section[c.section_type] = stats.chunks_by_section.get(c.section_type, 0) + 1

    return chunks, stats

