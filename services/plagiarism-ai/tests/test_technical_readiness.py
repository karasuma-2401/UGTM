"""Automated tests for CP-06.4: Technical Readiness Assessment & Production Migration Protocol."""

from pathlib import Path
import pytest

ROOT_DIR = Path(__file__).resolve().parent.parent
READINESS_MD_PATH = ROOT_DIR / "docs" / "technical-readiness.md"


def test_technical_readiness_report_exists_and_covers_7_pillars():
    """Verify technical-readiness.md covers all 7 architectural pillars."""
    assert READINESS_MD_PATH.exists(), "technical-readiness.md missing"
    content = READINESS_MD_PATH.read_text(encoding="utf-8")

    pillars = [
        "Quality & Accuracy",
        "Reliability & Resilience",
        "Security & RBAC",
        "Reproducibility",
        "Portability & Packaging",
        "Data Governance & Licensing",
        "Observability & Telemetry",
    ]
    for p in pillars:
        assert p in content, f"Missing architectural pillar in readiness report: {p}"


def test_three_state_evaluations_present():
    """Verify report explicitly marks tested, architected, and missing production gaps."""
    content = READINESS_MD_PATH.read_text(encoding="utf-8")

    assert "[ĐÃ THỬ NGHIỆM]" in content
    assert "[ĐÃ THIẾT KẾ]" in content
    assert "[CÒN THIẾU]" in content


def test_colab_to_dedicated_gpu_migration_instructions():
    """Verify instructions for migrating from Colab to dedicated GPU worker without code change."""
    content = READINESS_MD_PATH.read_text(encoding="utf-8")

    assert "Dedicated GPU Worker" in content
    assert "RemoteGPUInferenceClient" in content
    assert "/health" in content and "/encode" in content and "/rerank" in content
    assert "REMOTE_GPU_URL" in content


def test_production_deployment_checklist_and_scope_boundary():
    """Verify deployment checklist exists and scope excludes commercial sales/billing."""
    content = READINESS_MD_PATH.read_text(encoding="utf-8")

    assert "Production Deployment Checklist" in content
    assert "TRL 6" in content
    assert "TRL 7" in content
    # Verify strict dev stack boundary
    assert "SLA" in content
    assert "không chứa các yếu tố thương mại hóa" in content or "không có task sale" in content.lower()

